# SteamMarket — Backend (.NET 8, Clean Architecture)

Marketplace de items de Dota 2. Backend en C# separado por capas, y frontend
en Astro como proyecto **aparte** (`../SteamMarket-frontend`).

Estado actual:

1. ✅ Login con Steam (OpenID 2.0)
2. ✅ Carga del inventario de Dota 2
3. ✅ Cálculo de precios (cache propia en SQLite + `priceoverview` de Steam, con throttle)
4. ✅ Validación de entrada (Data Annotations en `PricingOptions`, FluentValidation en `returnUrl`)
5. ✅ Manejo global de errores (`IExceptionHandler` + ProblemDetails)
6. ✅ Tests unitarios (`SteamMarket.Tests`)
7. ⏳ Migraciones EF Core generadas (falta correr el comando una vez, ver abajo)

---

## Estructura de la solución

```
SteamMarket.sln
├── SteamMarket.Domain            → núcleo. Entidades y reglas. No depende de nadie.
│     Entities/InventoryItem.cs (ApplyPricing), ValueObjects/SteamId.cs
├── SteamMarket.Application        → casos de uso + interfaces (puertos). Depende de Domain.
│     Common/Interfaces/    → puertos: ISteamInventoryClient, IMarketPriceProvider
│     DTOs/                → InventoryItemDto, InventoryResponse
│     Pricing/              → PricingOptions (con [Range], se valida al arrancar)
│     Services/              → InventoryService + Services/Interfaces/IInventoryService
├── SteamMarket.Infrastructure     → implementaciones reales. Depende de Application y Domain.
│     Steam/                 → SteamInventoryClient (HTTP a steamcommunity.com)
│     Pricing/               → SteamMarketPriceProvider (priceoverview + cache + throttle)
│     Persistence/           → SteamMarketDbContext (EF Core, SQLite)
├── SteamMarket.Api                → entrada web: controllers, Program.cs, login, Swagger.
│     Contracts/, Validation/ → LoginRequest + LoginRequestValidator (evita open redirect)
│     Middleware/            → GlobalExceptionHandler
└── SteamMarket.Tests               → xUnit + Moq. Tests de Domain y Application (con mocks de los puertos).
```

**Dirección de las dependencias** (siempre hacia adentro):

```
Api ──► Infrastructure ──► Application ──► Domain
 └────────────────────────►
```

La regla de oro: **Domain no conoce a nadie**. Application define *interfaces*
(ej. `ISteamInventoryClient`, `IMarketPriceProvider`) y Infrastructure las *implementa*.
Así puedes cambiar de dónde salen los datos (Steam, una BD, un mock para tests) sin
tocar la lógica, y es por eso que `SteamMarket.Tests` puede probar `InventoryService`
sin hablar con Steam ni con SQLite de verdad.

---

## Requisitos

- SDK de **.NET 8**
- Una **Steam Web API Key**: https://steamcommunity.com/dev/apikey

## Puesta en marcha

1. Configura tu Steam API Key con **user-secrets** (nunca en `appsettings.json`,
   que se sube a git — ese archivo solo tiene `"ApiKey": ""` como placeholder):

   ```bash
   cd SteamMarket.Api
   dotnet user-secrets set "Steam:ApiKey" "TU_KEY_AQUI"
   ```

   > En producción: variable de entorno `Steam__ApiKey` (doble guion bajo = la
   > sección anidada `Steam:ApiKey`). No hace falta tocar código ni `appsettings.json`.

2. Genera la migración inicial de EF Core (una sola vez; crea la tabla `MarketPrices`
   del cache de precios). Necesita la herramienta `dotnet-ef` — ya está declarada como
   *local tool* en `.config/dotnet-tools.json`:

   ```bash
   dotnet tool restore
   dotnet ef migrations add InitialCreate --project SteamMarket.Infrastructure --startup-project SteamMarket.Api
   ```

   Si te saltas este paso, el backend arranca igual pero loguea un warning y el
   cache de precios no funciona (la tabla no existe todavía).

3. Desde la carpeta de la solución:

   ```bash
   dotnet restore
   dotnet build
   dotnet run --project SteamMarket.Api
   ```

4. Abre **http://localhost:5000/swagger**.

## Tests

```bash
dotnet test
```

`SteamMarket.Tests` prueba `InventoryItem.ApplyPricing()` (dominio puro) e
`InventoryService` (mockeando `ISteamInventoryClient` e `IMarketPriceProvider` con Moq),
además de los `[Range]` de `PricingOptions`. No requieren el backend corriendo ni
conexión a Steam.

## Cambios de esquema (después de la migración inicial)

Si modificás `CachedMarketPrice` o agregás una nueva entidad persistida:

```bash
dotnet ef migrations add NombreDelCambio --project SteamMarket.Infrastructure --startup-project SteamMarket.Api
```

La migración se aplica sola al arrancar (`db.Database.Migrate()` en `Program.cs`).

## Probar el flujo (desde el navegador)

1. `http://localhost:5000/api/auth/login` → login en Steam
2. `http://localhost:5000/api/auth/me` → deberías ver tu `steamId`
3. `http://localhost:5000/api/inventory/dota` → tus items

> Tu inventario tiene que estar **público** en Steam, o la API devuelve 403.

## Endpoints

| Método | Ruta                  | Qué hace                       |
|--------|-----------------------|--------------------------------|
| GET    | `/api/auth/login`     | Inicia login con Steam         |
| GET    | `/api/auth/me`        | Usuario logueado               |
| POST   | `/api/auth/logout`    | Cierra sesión                  |
| GET    | `/api/inventory/dota` | Items de Dota (requiere login) |

---

## ⚠️ Cookies + Astro (el punto que más problemas da)

El backend usa cookie de sesión. Si Astro (otro origen, `localhost:4321`) llama a la
API con `fetch(..., { credentials: "include" })`, la cookie es cross-site. Dos caminos:

- **Fácil (dev): proxy.** Que Astro reenvíe `/api` al backend, así todo es mismo-origen.
  En `astro.config.mjs`:
  ```js
  export default defineConfig({
    server: { port: 4321 },
    vite: { server: { proxy: { "/api": "http://localhost:5000" } } }
  });
  ```
- **Dominios distintos:** en `Program.cs` pon `SameSite=None` + `SecurePolicy=Always` y corre en https.

---

## Pendientes conocidos (no bloquean el MVP)

- El throttle de `SteamMarketPriceProvider` es por instancia de proceso: si escalás
  el backend a más de una instancia, cada una limita las llamadas a Steam por su cuenta.
- Cookie cross-site en producción: el proxy de Astro resuelve dev; en un dominio real
  hace falta `SameSite=None` + HTTPS (o mismo dominio detrás de un reverse proxy).
