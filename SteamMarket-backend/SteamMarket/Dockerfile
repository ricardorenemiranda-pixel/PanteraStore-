# --- Etapa 1: build ---
# Imagen del SDK completo (compilador incluido) solo para compilar; no viaja a produccion.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos primero solo los .csproj para que Docker cachee "dotnet restore" mientras no
# cambien las dependencias (evita re-descargar paquetes en cada build por un cambio de codigo).
COPY SteamMarket.Api/SteamMarket.Api.csproj SteamMarket.Api/
COPY SteamMarket.Application/SteamMarket.Application.csproj SteamMarket.Application/
COPY SteamMarket.Domain/SteamMarket.Domain.csproj SteamMarket.Domain/
COPY SteamMarket.Infrastructure/SteamMarket.Infrastructure.csproj SteamMarket.Infrastructure/

RUN dotnet restore SteamMarket.Api/SteamMarket.Api.csproj

# Recien aca copiamos el resto del codigo fuente (SteamMarket.Tests no hace falta en la imagen).
COPY SteamMarket.Api/ SteamMarket.Api/
COPY SteamMarket.Application/ SteamMarket.Application/
COPY SteamMarket.Domain/ SteamMarket.Domain/
COPY SteamMarket.Infrastructure/ SteamMarket.Infrastructure/

WORKDIR /src/SteamMarket.Api
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish SteamMarket.Api.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# --- Etapa 2: runtime ---
# Imagen liviana, solo el runtime de ASP.NET (sin el SDK/compilador): la que corre en Render.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# .NET 8+ en contenedores ya escucha en 8080 por default (no en 80/5000). Render detecta este
# puerto solo si coincide con lo que configures como "Port" en el servicio (ver guia de deploy).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SteamMarket.Api.dll"]
