#!/usr/bin/env bash
# ============================================================================
# Copia de seguridad comprimida de PostgreSQL (todas las bases de Sorteo360).
#
# Uso:
#   ./scripts/backup-postgres.sh [carpeta-destino] [dias-retencion]
# Ejemplo cron diario a las 03:00 AM:
#   0 3 * * * /ruta/al/repo/scripts/backup-postgres.sh /var/backups/sorteo360 14
#
# Requiere el stack de producción arriba (docker-compose.prod.yml) y `.env.prod`
# o variables POSTGRES_USER / COMPOSE_FILE exportadas.
# ============================================================================
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-.env.prod}"
BACKUP_DIR="${1:-./backups/postgres}"
RETENTION_DAYS="${2:-14}"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

PG_USER="${POSTGRES_USER:-sorteo360}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

OUT_FILE="${BACKUP_DIR}/sorteo360-pg-${TIMESTAMP}.sql.gz"

echo "Generando respaldo en ${OUT_FILE}..."
docker compose -f "$COMPOSE_FILE" exec -T postgres \
  pg_dumpall -U "$PG_USER" --clean --if-exists | gzip -9 > "$OUT_FILE"

if [[ ! -s "$OUT_FILE" ]]; then
  echo "Error: el respaldo quedó vacío (${OUT_FILE})." >&2
  rm -f "$OUT_FILE"
  exit 1
fi

echo "Eliminando respaldos con más de ${RETENTION_DAYS} días..."
find "$BACKUP_DIR" -name 'sorteo360-pg-*.sql.gz' -type f -mtime +"$RETENTION_DAYS" -delete

echo "Respaldo completado: ${OUT_FILE}"

