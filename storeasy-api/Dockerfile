# Production image. Build context = the REPOSITORY ROOT (one level above this folder), so the store rules catalog
# (rules/) is part of the build: it is copied next to the application and embedded as the fallback, and the API starts
# without any mount. Build it from the repository root:
#   docker build -f storeasy-api/Dockerfile -t storeasy-api .
# (deploy/docker-compose.yml does the same: context .., dockerfile storeasy-api/Dockerfile). The files sent to the
# builder are limited by storeasy-api/Dockerfile.dockerignore. Another catalog can still be mounted and named with
# Storeasy__Stores__RulesPath.
# Runtime settings come from the environment, e.g. ConnectionStrings__DefaultConnectionString,
# Milvasoft__Caching__Redis__ConnectionString, Milvasoft__Identity__Token__SymmetricPublicKey, SeqUri,
# OpenTelemetry__CollectorUrl (see README). The signing key and the connection string are not in the appsettings files:
# without Milvasoft__Identity__Token__SymmetricPublicKey the application stops at startup (README "Secrets").
# STOREASY_COMMIT (optional): source commit shown on the system status page, for example
#   docker build -f storeasy-api/Dockerfile --build-arg STOREASY_COMMIT=$(git rev-parse --short HEAD) -t storeasy-api .
# It is stamped into the assembly's informational version and set as Storeasy__Build__Commit, so it survives an empty
# Storeasy__Build__Commit at runtime.
# The build stage runs on the builder's own platform: the publish is framework dependent and portable (IL plus the
# native SkiaSharp/HarfBuzz libraries of every Linux RID), so only the runtime stage is the target platform. An arm64
# image from an x64 machine: docker buildx build --platform linux/arm64 -f storeasy-api/Dockerfile .
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0-resolute AS build
ARG BUILD_CONFIGURATION=Release
ARG STOREASY_COMMIT=
WORKDIR /repo
COPY rules/ rules/
COPY storeasy-api/ storeasy-api/
WORKDIR /repo/storeasy-api
RUN test -f /repo/rules/catalog.json
RUN dotnet restore "src/Storeasy.Api/Storeasy.Api.csproj"
RUN dotnet publish "src/Storeasy.Api/Storeasy.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false "/p:SourceRevisionId=$STOREASY_COMMIT" --no-restore
RUN test -f /app/publish/rules/catalog.json

# chiseled-extra: no shell (small attack surface), with ICU and tzdata (localization and time zones).
FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled-extra AS final
USER app
WORKDIR /app
EXPOSE 8080
ARG STOREASY_COMMIT=
ENV Storeasy__Build__Commit=$STOREASY_COMMIT
# MILVA_ENV=prod closes the development endpoints and selects the production log sinks. It is baked into the image so a
# forgotten variable can never open them.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    MILVA_ENV=prod
COPY --from=build --chown=app:app /app/publish .
ENTRYPOINT ["dotnet", "Storeasy.Api.dll"]
