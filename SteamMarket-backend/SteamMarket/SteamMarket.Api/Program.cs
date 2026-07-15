using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SteamMarket.Api.Middleware;
using SteamMarket.Application;
using SteamMarket.Infrastructure;
using SteamMarket.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Config ---
var frontendUrl = builder.Configuration["Frontend:Url"] ?? "http://localhost:4321";
var steamApiKey = builder.Configuration["Steam:ApiKey"] ?? string.Empty;

// --- Capas ---
builder.Services.AddApplication(builder.Configuration);      // capa Application
builder.Services.AddInfrastructure(builder.Configuration);   // capa Infrastructure

// --- Web ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Validacion (FluentValidation) ---
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// --- Manejo global de errores (IExceptionHandler + ProblemDetails, RFC 7807) ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Render (y la mayoria de PaaS) terminan HTTPS en su propio proxy y le hablan a nuestro
// contenedor por HTTP simple puertas adentro. Sin esto, la app cree que la conexion es HTTP:
// rompe el login de Steam (el return_to que arma queda en http:// en vez de https://) y la
// cookie con Secure=true (heredado de SameSite/HTTPS) no se manda. ClearAll() porque no
// conocemos de antemano la IP del proxy de Render (no es una red fija como en un datacenter
// propio) -- confiamos en el header porque el contenedor no es alcanzable directo desde afuera.
//
// XForwardedHost tambien hace falta porque el frontend (Vercel) le hace un rewrite a /api/*
// hacia este backend: sin esto, Request.Host queda en "panterastore-api.onrender.com" (el host
// real que ve el contenedor) en vez de "panterastore-pi.vercel.app" (el dominio real por el que
// entro el usuario), y el "return_to" que le mandamos a Steam apunta mal -- Steam redirige
// derecho a Render saltandose Vercel, y la cookie de correlacion (guardada para el dominio de
// Vercel) nunca llega, tirando "cookie not found" / "state parameter was invalid".
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(frontendUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// --- Autenticacion: cookie local + reto contra Steam ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Steam";
})
.AddCookie(options =>
{
    options.Cookie.Name = "SteamMarket.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax; // ver README para el caso cross-site
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
})
.AddSteam(options =>
{
    options.ApplicationKey = steamApiKey; // https://steamcommunity.com/dev/apikey
});

var app = builder.Build();

// La Steam API Key ya NO vive en appsettings.json en texto plano (ver README):
// - Dev: dotnet user-secrets set "Steam:ApiKey" "tu-key" (adentro de SteamMarket.Api/)
// - Prod: variable de entorno Steam__ApiKey (el doble guion bajo es la convencion de
//   .NET para mapear a la seccion anidada "Steam:ApiKey").
// No tiramos la app abajo si falta: el resto del backend (inventario, precios) puede
// probarse igual sin login real. Solo avisamos fuerte para que no se pierda de vista.
if (string.IsNullOrWhiteSpace(steamApiKey))
{
    app.Logger.LogWarning(
        "Steam:ApiKey no esta configurada -> el login con Steam no va a funcionar. " +
        "Dev: 'dotnet user-secrets set \"Steam:ApiKey\" \"tu-key\"' en SteamMarket.Api. " +
        "Prod: variable de entorno Steam__ApiKey.");
}

// Aplica migraciones de EF Core al arrancar (reemplaza EnsureCreated: asi los cambios de
// esquema quedan versionados). La PRIMERA vez hay que generar la migracion (ver README):
//   dotnet ef migrations add InitialCreate --project SteamMarket.Infrastructure --startup-project SteamMarket.Api
// Sin eso, Database.GetMigrations() esta vacio y no hay nada que aplicar todavia.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SteamMarketDbContext>();

    if (!db.Database.GetMigrations().Any())
    {
        app.Logger.LogWarning(
            "No hay migraciones de EF Core generadas todavia -> la tabla MarketPrices no existe. " +
            "Corre una vez, desde la carpeta de la solucion (SteamMarket-backend/SteamMarket): " +
            "dotnet ef migrations add InitialCreate --project SteamMarket.Infrastructure --startup-project SteamMarket.Api");
    }
    else
    {
        db.Database.Migrate();
        await SeedLootBoxesAsync(db);
    }
}

// Tiene que ir ANTES que cualquier otro middleware que dependa de saber si la conexion es
// HTTPS o cual es la IP real del cliente (auth, HTTPS redirection, etc.).
app.UseForwardedHeaders();

// Debe ir lo mas temprano posible en el pipeline: atrapa excepciones de todo lo que viene despues.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Crea las cajas base (nombre/categoria/precio) igual que las secciones de giordota.com/cajas,
// SOLO si la tabla esta vacia (para no pisar nada si el admin ya las edito desde /admin). El
// pool de items posibles de cada caja queda vacio a proposito: eso lo tiene que cargar el admin
// a mano (POST /api/admin/lootboxes/{slug}/items), porque cada item necesita stock real
// verificable en el inventario de la cuenta bot, no es algo que se pueda inventar en un seed.
static async Task SeedLootBoxesAsync(SteamMarketDbContext db)
{
    if (await db.LootBoxes.AnyAsync()) return;

    var boxes = new (string Slug, string Name, string Category, decimal Price, int SortOrder)[]
    {
        ("carry", "Carry", "Rol", 2.90m, 0),
        ("support", "Support", "Rol", 2.90m, 1),
        ("midlaner", "Midlaner", "Rol", 2.90m, 2),
        ("offlaner", "Offlaner", "Rol", 2.90m, 3),
        ("hard-support", "Hard Support", "Rol", 2.90m, 4),

        ("buti", "Buti", "VIP", 14.90m, 0),
        ("macarius", "Macarius", "VIP", 9.90m, 1),
        ("gloglo-king", "GloGloKing", "VIP", 9.90m, 2),
        ("techisor", "Techisor", "VIP", 9.90m, 3),
        ("daad", "DaaD", "VIP", 9.90m, 4),
        ("papicha", "Papicha", "VIP", 9.90m, 5),

        ("heraldo", "Heraldo", "Medalla", 0.75m, 0),
        ("guardian", "Guardian", "Medalla", 1.50m, 1),
        ("cruzado", "Cruzado", "Medalla", 2.50m, 2),
        ("arconte", "Arconte", "Medalla", 3.50m, 3),
        ("leyenda", "Leyenda", "Medalla", 5.50m, 4),
        ("ancient", "Ancient", "Medalla", 7.50m, 5),

        ("la-gratis", "La gratis", "Gratis", 0m, 0),
        ("redes-sociales", "Redes sociales", "Gratis", 0m, 1),
        ("super-fan", "Super fan", "Gratis", 0m, 2),
        ("la-chismosa", "La Chismosa", "Gratis", 0m, 3),
    };

    foreach (var b in boxes)
    {
        db.LootBoxes.Add(new LootBox
        {
            Id = Guid.NewGuid(),
            Slug = b.Slug,
            Name = b.Name,
            Category = b.Category,
            Price = b.Price,
            SortOrder = b.SortOrder,
            IsActive = true,
        });
    }

    await db.SaveChangesAsync();
}
