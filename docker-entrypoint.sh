#!/usr/bin/env sh
set -e

export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:${PORT:-10000}}"
export ASPNETCORE_HTTP_PORTS="${PORT:-10000}"

echo "Starting OrderService on ${ASPNETCORE_URLS}"

run_migrations() {
  if [ -z "${ConnectionStrings__DefaultConnection}" ]; then
    echo "ConnectionStrings__DefaultConnection is not set. Set it in Render before enabling RUN_MIGRATIONS."
    return 1
  fi

  echo "Applying database migrations..."
  attempt=1
  max_attempts="${MIGRATION_MAX_ATTEMPTS:-5}"

  until dotnet ef database update \
      --project /src/OrderService.csproj \
      --configuration Release; do
    if [ "${attempt}" -ge "${max_attempts}" ]; then
      echo "Database migrations failed after ${attempt} attempt(s). Check the Render database connection string and migration logs above."
      return 1
    fi

    attempt=$((attempt + 1))
    echo "Migration failed. Retrying in 10 seconds (${attempt}/${max_attempts})..."
    sleep 10
  done

  echo "Database migrations applied."
}

if [ "${RUN_MIGRATIONS:-true}" = "true" ]; then
  run_migrations &
  migration_pid=$!
else
  echo "Skipping database migrations because RUN_MIGRATIONS=${RUN_MIGRATIONS}."
  migration_pid=""
fi

dotnet /app/OrderService.dll &
app_pid=$!

if [ -n "${migration_pid}" ]; then
  if ! wait "${migration_pid}"; then
    echo "Migration process failed; stopping OrderService."
    kill "${app_pid}"
    wait "${app_pid}" || true
    exit 1
  fi
fi

wait "${app_pid}"
