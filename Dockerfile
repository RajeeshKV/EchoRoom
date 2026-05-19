# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Chatroom.sln ./
COPY Chat.Api/Chat.Api.csproj Chat.Api/
RUN dotnet restore Chatroom.sln

COPY . .
RUN dotnet publish Chat.Api/Chat.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${PATH}:/root/.dotnet/tools"
ENV PORT=10000
ENV ASPNETCORE_URLS=http://+:10000
ENV RUN_MIGRATIONS=true

EXPOSE 10000

RUN dotnet tool install --global dotnet-ef --version "8.*"

COPY --from=build /src /src
COPY --from=build /app/publish /app
COPY scripts/docker-entrypoint.sh /app/docker-entrypoint.sh

RUN chmod +x /app/docker-entrypoint.sh

ENTRYPOINT ["sh", "/app/docker-entrypoint.sh"]
