# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY core/MLS.Core/MLS.Core.csproj                                   core/MLS.Core/
COPY modules/broker/MLS.Broker/MLS.Broker.csproj                     modules/broker/MLS.Broker/

RUN dotnet restore modules/broker/MLS.Broker/MLS.Broker.csproj \
    --runtime linux-x64 --no-cache

COPY core/                            core/
COPY modules/broker/MLS.Broker/       modules/broker/MLS.Broker/

RUN dotnet publish modules/broker/MLS.Broker/MLS.Broker.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --no-restore \
    --output /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Non-root user for container security
RUN addgroup -S mls && adduser -S mls -G mls
USER mls

COPY --from=build --chown=mls:mls /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://0.0.0.0:5800

EXPOSE 5800 6800

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:5800/health || exit 1

ENTRYPOINT ["dotnet", "MLS.Broker.dll"]
