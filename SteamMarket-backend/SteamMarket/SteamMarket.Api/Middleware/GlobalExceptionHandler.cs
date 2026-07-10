using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SteamMarket.Api.Middleware;

/// <summary>
/// Atrapa cualquier excepcion no controlada que llegue hasta el pipeline HTTP y devuelve
/// un JSON consistente (formato ProblemDetails, RFC 7807) en vez de la pagina de error
/// generica de ASP.NET. Los casos ya controlados (inventario privado, 401, etc.) NO pasan
/// por aca porque los controllers/servicios ya los manejan con su propio status code.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Excepcion no controlada en {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ocurrio un error inesperado.",
            Detail = _env.IsDevelopment()
                ? exception.ToString()
                : "Intenta de nuevo mas tarde. Si el problema persiste, contacta soporte.",
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = problem.Status.Value;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problem, ct);

        return true; // ya quedo manejada, no seguir propagando
    }
}
