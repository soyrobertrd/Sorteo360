#!/usr/bin/env bash
#
# Paridad local con .github/workflows/ci.yml. Corre los mismos pasos de
# compose, backend y frontend que corre CI en cada PR, para tener la misma senal
# antes de pushear en vez de esperar a Actions. A proposito no esta
# enganchado a .githooks/pre-push — ver CONTRIBUTING.md.
#
# Uso:
#   scripts/check.sh              # compose + backend + frontend (default de ci.yml)
#   scripts/check.sh --compose    # solo los chequeos de compose, de roles y de rutas
#   scripts/check.sh --backend    # solo backend
#   scripts/check.sh --frontend   # solo frontend

set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() {
  cat <<'EOF'
Uso: scripts/check.sh [--compose] [--backend] [--frontend]

Sin flags: corre los tres, igual que el default de ci.yml.
  --compose   paridad de roles entre el realm, docs/roles.md y las
              politicas, y los tests de este script.
  --backend   dotnet restore/build de la solucion raiz, despues dotnet
              test de los cuatro Tests.Unit (espeja el job backend de ci.yml).
  --frontend  npm ci, despues build, lint, format:check y test en frontend/
              (espeja el job frontend de ci.yml). Se niega si
              frontend/node_modules es un enlace a otro checkout.
EOF
}

run_compose=false
run_backend=false
run_frontend=false

if [ "$#" -eq 0 ]; then
  run_compose=true
  run_backend=true
  run_frontend=true
fi

for arg in "$@"; do
  case "$arg" in
    --compose) run_compose=true ;;
    --backend) run_backend=true ;;
    --frontend) run_frontend=true ;;
    -h|--help) usage; exit 0 ;;
    *)
      echo "scripts/check.sh: argumento desconocido '$arg'" >&2
      usage >&2
      exit 1
      ;;
  esac
done

# Corre un paso en el directorio dado, mostrando el comando antes de correrlo.
run_step() {
  local dir="$1"
  shift
  echo "--> $*"
  ( cd "$dir" && "$@" )
}

# El chequeo y sus propios tests: el segundo es lo que prueba que el primero
# enrojece, en vez de darlo por hecho.
compose_check() {
  run_step "$repo_root" scripts/roles-parity.sh || return 1
  run_step "$repo_root" scripts/roles-parity.test.sh || return 1
  run_step "$repo_root" scripts/check.test.sh || return 1
}

backend_check() {
  run_step "$repo_root" dotnet restore Sorteo360.Platform.slnx || return 1
  run_step "$repo_root" dotnet build Sorteo360.Platform.slnx -c Release --no-restore || return 1

  # Los mismos cinco proyectos Tests.Unit que ci.yml, nunca la solucion
  # entera: un proyecto de test que referencia un Microsoft.NET.Sdk.Web
  # corre 0 tests y sale limpio bajo --no-build, asi que --no-build tampoco
  # se usa aqui.
  local projects=(
    shared/Sorteo360.Platform.Tests.Unit/Sorteo360.Platform.Tests.Unit.csproj
    events-service/Sorteo360.Events.Tests.Unit/Sorteo360.Events.Tests.Unit.csproj
    convocatorias-service/Sorteo360.Convocatorias.Tests.Unit/Sorteo360.Convocatorias.Tests.Unit.csproj
    notification-service/Sorteo360.Notifications.Tests.Unit/Sorteo360.Notifications.Tests.Unit.csproj
  )
  local project
  for project in "${projects[@]}"; do
    run_step "$repo_root" dotnet test "$project" -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage || return 1
  done
}

frontend_check() {
  # npm ci borra node_modules antes de instalar. Si es un enlace (un worktree
  # que reusa los paquetes de otro checkout), vaciaria el destino ajeno.
  local modules="$repo_root/frontend/node_modules"
  if [ -L "$modules" ]; then
    echo "frontend/node_modules es un enlace a $(readlink "$modules")." >&2
    echo "npm ci vaciaria ese destino, que pertenece a otro checkout. No se corre." >&2
    echo "Arreglo: rm frontend/node_modules en este worktree (borra solo el enlace) y vuelve a correr." >&2
    return 1
  fi
  run_step "$repo_root/frontend" npm ci || return 1
  run_step "$repo_root/frontend" npm run build || return 1
  run_step "$repo_root/frontend" npm run lint || return 1
  run_step "$repo_root/frontend" npm run format:check || return 1
  run_step "$repo_root/frontend" npm test || return 1
}

overall=0

if [ "$run_compose" = true ]; then
  echo "=== compose ==="
  if compose_check; then
    echo "=== compose: OK ==="
  else
    echo "=== compose: FALLO ===" >&2
    overall=1
  fi
fi

if [ "$run_backend" = true ]; then
  echo "=== backend ==="
  if backend_check; then
    echo "=== backend: OK ==="
  else
    echo "=== backend: FALLO ===" >&2
    overall=1
  fi
fi

if [ "$run_frontend" = true ]; then
  echo "=== frontend ==="
  if frontend_check; then
    echo "=== frontend: OK ==="
  else
    echo "=== frontend: FALLO ===" >&2
    overall=1
  fi
fi

exit "$overall"
