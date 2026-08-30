FROM node:26.8.1-alpine3.24 AS frontend-build

WORKDIR /source/src/App.GitHealth.Web

COPY src/App.GitHealth.Web/package.json src/App.GitHealth.Web/package-lock.json ./
RUN npm ci

COPY src/App.GitHealth.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.400-noble AS backend-build

WORKDIR /source

COPY global.json Directory.Build.props ./
COPY src/App.GitHealth.Core/App.GitHealth.Core.csproj src/App.GitHealth.Core/
COPY src/App.GitHealth.Api/App.GitHealth.Api.csproj src/App.GitHealth.Api/
RUN dotnet restore src/App.GitHealth.Api/App.GitHealth.Api.csproj

COPY src/App.GitHealth.Core/ src/App.GitHealth.Core/
COPY src/App.GitHealth.Api/ src/App.GitHealth.Api/
RUN dotnet publish src/App.GitHealth.Api/App.GitHealth.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:BuildFrontend=false

COPY --from=frontend-build \
    /source/src/App.GitHealth.Web/dist/app-git-health-web/browser/ \
    /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-noble AS runtime

USER root

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl git \
    && git config --system --add safe.directory /repositories \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

RUN mkdir --parents /data /repositories \
    && chown --recursive "$APP_UID:$APP_UID" /data /repositories

COPY --from=backend-build /app/publish/ ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    GitHealth__DataDirectory=/data

EXPOSE 8080
VOLUME ["/data"]

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl --fail --silent --show-error --output /dev/null http://127.0.0.1:8080/health

STOPSIGNAL SIGTERM

ENTRYPOINT ["dotnet", "githealth.dll"]
