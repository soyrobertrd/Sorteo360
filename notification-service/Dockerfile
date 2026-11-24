# syntax=docker/dockerfile:1

# ============================================================================
# Etapa de build: compila y publica la API.
#
# El contexto de build es la RAÍZ del repositorio, no la carpeta del servicio:
# notification-service referencia el chasis compartido shared/Sorteo360.Platform,
# y un ProjectReference que apunta fuera del contexto no se puede resolver.
# Por eso compose declara `context: .` con `dockerfile: notification-service/Dockerfile`,
# y acá las rutas se escriben desde la raíz. El .dockerignore de la raíz es el que
# evita que el resto del monorepo viaje al daemon en cada build.
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar primero los archivos de configuración de la solución y los .csproj
# para aprovechar la caché de capas de Docker en el restore.
COPY Directory.Build.props Directory.Packages.props ./
COPY notification-service/Sorteo360.Notifications.slnx notification-service/
COPY shared/Sorteo360.Platform/Sorteo360.Platform.csproj                                                     shared/Sorteo360.Platform/
COPY notification-service/Sorteo360.Notifications.Core/Sorteo360.Notifications.Core.csproj                   notification-service/Sorteo360.Notifications.Core/
COPY notification-service/Sorteo360.Notifications.Infrastructure/Sorteo360.Notifications.Infrastructure.csproj notification-service/Sorteo360.Notifications.Infrastructure/
COPY notification-service/Sorteo360.Notifications.Web/Sorteo360.Notifications.Web.csproj                     notification-service/Sorteo360.Notifications.Web/

# Restaurar solo el proyecto Web (arrastra Core, Infrastructure y el chasis por referencia).
RUN dotnet restore notification-service/Sorteo360.Notifications.Web/Sorteo360.Notifications.Web.csproj

# Copiar el resto del código y publicar en Release. Solo entran el chasis y el
# servicio: los demás servicios del monorepo no forman parte de esta imagen.
COPY shared/ shared/
COPY notification-service/ notification-service/
RUN dotnet publish notification-service/Sorteo360.Notifications.Web/Sorteo360.Notifications.Web.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ============================================================================
# Etapa de runtime: imagen mínima con solo lo necesario para ejecutar la API.
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl se usa para el HEALTHCHECK contra los endpoints de salud.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Kestrel escucha en 8080 (puerto no privilegiado, compatible con usuario no-root).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Ejecutar como usuario no-root provisto por la imagen base de .NET.
USER $APP_UID

COPY --from=build /app/publish .

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
    CMD curl -fsS http://localhost:8080/_alive || exit 1

ENTRYPOINT ["dotnet", "Sorteo360.Notifications.Web.dll"]
