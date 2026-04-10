# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY core/MLS.Core/MLS.Core.csproj                           core/MLS.Core/
COPY modules/defi/MLS.DeFi/MLS.DeFi.csproj                  modules/defi/MLS.DeFi/

RUN dotnet restore modules/defi/MLS.DeFi/MLS.DeFi.csproj \
    --runtime linux-x64 --no-cache

COPY core/                          core/
COPY modules/defi/MLS.DeFi/         modules/defi/MLS.DeFi/

RUN dotnet publish modules/defi/MLS.DeFi/MLS.DeFi.csproj \
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
    ASPNETCORE_URLS=http://0.0.0.0:5500

EXPOSE 5500 6500

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:5500/health || exit 1

ENTRYPOINT ["dotnet", "MLS.DeFi.dll"]
