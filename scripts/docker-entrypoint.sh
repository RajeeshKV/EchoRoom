#!/usr/bin/env sh
set -eu

cd /src

if [ "${RUN_MIGRATIONS:-true}" = "true" ]; then
  echo "Restoring solution for EF Core tools..."
  dotnet restore Chatroom.sln

  echo "Applying EF Core migrations..."
  dotnet ef database update \
    --project Chat.Api/Chat.Api.csproj \
    --startup-project Chat.Api/Chat.Api.csproj
fi

echo "Starting Chat.Api..."
exec dotnet /app/Chat.Api.dll
