#!/usr/bin/env sh
set -eu

if [ "${RUN_MIGRATIONS:-true}" = "true" ]; then
  echo "Restoring project for EF tools..."
  dotnet restore /src/Chat.Api/Chat.Api.csproj

  echo "Applying EF Core migrations..."
  dotnet ef database update \
    --project /src/Chat.Api/Chat.Api.csproj \
    --startup-project /src/Chat.Api/Chat.Api.csproj
fi

echo "Starting Chat.Api..."
exec dotnet /app/Chat.Api.dll
