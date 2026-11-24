#!/bin/bash
# ===========================================================================
# Inicialización de PostgreSQL para el stack local.
#
# Se ejecuta UNA sola vez, la primera vez que el contenedor crea el volumen
# de datos (cuando está vacío). Para re-ejecutarlo: eliminar el volumen con
#   docker compose down -v
#
# Solo crea la base de datos VACÍA de cada servicio. El esquema (tablas,
# índices, constraints) lo aplica cada servicio al arrancar, ejecutando sus
# migraciones de EF Core — la única autoridad del esquema.
#
# Plantilla: una base de datos por servicio. Agregar una línea CREATE DATABASE
# por servicio nuevo.
# ===========================================================================
set -e

# --- events-service ---------------------------------------------------------
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" \
    -c "CREATE DATABASE events;"

# --- convocatorias-service --------------------------------------------------
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" \
    -c "CREATE DATABASE convocatorias;"


# --- notification-service ---------------------------------------------------
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" \
    -c "CREATE DATABASE notifications;"

# --- keycloak (infraestructura de identidad) ---------------------------------
# Keycloak gestiona su propio esquema dentro de esta base al arrancar.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" \
    -c "CREATE DATABASE keycloak;"

# --- Plantilla para agregar un nuevo servicio (copiar y renombrar) ----------
# psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" \
#     -c "CREATE DATABASE otro;"
