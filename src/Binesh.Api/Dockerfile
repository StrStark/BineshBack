# syntax=docker/dockerfile:1.7
#
# Multi-stage build for Binesh.Api.
# Build context is src-v2/ (so .dockerignore + Directory.Build.props are visible).
#
#   docker build -f src/Binesh.Api/Dockerfile -t binesh-api:dev .
#
# The same image runs the API and the migrator:
#   docker run binesh-api:dev                  -> starts the web host
#   docker run binesh-api:dev migrate          -> applies pending migrations and exits

# ── Stage 1: restore (cached on csproj-only changes) ────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Copy only the files needed for restore so the layer caches when source changes
# but project files don't.
COPY Directory.Build.props .editorconfig ./
COPY src/Binesh.Domain/Binesh.Domain.csproj           src/Binesh.Domain/
COPY src/Binesh.Application/Binesh.Application.csproj src/Binesh.Application/
COPY src/Binesh.Infrastructure/Binesh.Infrastructure.csproj src/Binesh.Infrastructure/
COPY src/Binesh.Identity/Binesh.Identity.csproj       src/Binesh.Identity/
COPY src/Binesh.Ai/Binesh.Ai.csproj                   src/Binesh.Ai/
COPY src/Binesh.Etl/Binesh.Etl.csproj                 src/Binesh.Etl/
COPY src/Binesh.Api/Binesh.Api.csproj                 src/Binesh.Api/

RUN dotnet restore src/Binesh.Api/Binesh.Api.csproj

# ── Stage 2: build + publish ────────────────────────────────────────────────
FROM restore AS build
COPY src/ src/
WORKDIR /src/src/Binesh.Api

ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:GenerateDocumentationFile=false

# ── Stage 3: runtime (slim, non-root) ───────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl is needed for the HEALTHCHECK in docker-compose.yml.
# Done as root before USER switch. ~3MB added.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# The aspnet image ships with a non-root user 'app' at $APP_UID.
# We do all writes as that user.
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_PRINT_TELEMETRY_MESSAGE=false

ENTRYPOINT ["dotnet", "Binesh.Api.dll"]
