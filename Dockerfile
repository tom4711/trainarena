# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# No .git in the image — pass the MinVer version from CI / local build.
ARG APP_VERSION=0.0.0

COPY TrainArena.sln ./
COPY src/TrainArena/TrainArena.csproj src/TrainArena/
RUN dotnet restore src/TrainArena/TrainArena.csproj

COPY src/TrainArena/ src/TrainArena/
RUN dotnet publish src/TrainArena/TrainArena.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:MinVerVersionOverride=${APP_VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TRAINARENA_DB=/data/trainarena.db

RUN mkdir -p /data /app/wwwroot/uploads \
    && chown -R app:app /data /app/wwwroot

USER app

COPY --from=build --chown=app:app /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "TrainArena.dll"]
