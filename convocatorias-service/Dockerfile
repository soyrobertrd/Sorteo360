# syntax=docker/dockerfile:1

# ============================================================================
# Etapa de build: compila y publica la API.
#
# El contexto de build es la RAÍZ del repositorio, no la carpeta del servicio:
# convocatorias-service referencia el chasis compartido shared/Sorteo360.Platform,
# y un ProjectReference que apunta fuera del contexto no se puede resolver.
# Por eso compose declara `context: .` con `dockerfile: convocatorias-service/Dockerfile`,
# y acá las rutas se escriben desde la raíz. El .dockerignore de la raíz es el que
# evita que el resto del monorepo viaje al daemon en cada build.
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar primero los archivos de configuración de la solución y los .csproj
# para aprovechar la caché de capas de Docker en el restore.
COPY Directory.Build.props Directory.Packages.props ./
COPY convocatorias-service/Sorteo360.Convocatorias.slnx convocatorias-service/
COPY shared/Sorteo360.Platform/Sorteo360.Platform.csproj                                                        shared/Sorteo360.Platform/
COPY convocatorias-service/Sorteo360.Convocatorias.Core/Sorteo360.Convocatorias.Core.csproj                     convocatorias-service/Sorteo360.Convocatorias.Core/
COPY convocatorias-service/Sorteo360.Convocatorias.Infrastructure/Sorteo360.Convocatorias.Infrastructure.csproj convocatorias-service/Sorteo360.Convocatorias.Infrastructure/
COPY convocatorias-service/Sorteo360.Convocatorias.Web/Sorteo360.Convocatorias.Web.csproj                       convocatorias-service/Sorteo360.Convocatorias.Web/

# Restaurar solo el proyecto Web (arrastra Core, Infrastructure y el chasis por referencia).
RUN dotnet restore convocatorias-service/Sorteo360.Convocatorias.Web/Sorteo360.Convocatorias.Web.csproj

# Copiar el resto del código y publicar en Release. Solo entran el chasis y el
# servicio: los demás servicios del monorepo no forman parte de esta imagen.
COPY shared/ shared/
COPY convocatorias-service/ convocatorias-service/
RUN dotnet publish convocatorias-service/Sorteo360.Convocatorias.Web/Sorteo360.Convocatorias.Web.csproj \
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

ENTRYPOINT ["dotnet", "Sorteo360.Convocatorias.Web.dll"]
