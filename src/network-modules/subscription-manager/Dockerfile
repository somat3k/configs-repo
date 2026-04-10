# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY core/MLS.Core/MLS.Core.csproj                                                                                   core/MLS.Core/
COPY network-modules/subscription-manager/MLS.Network.SubscriptionManager/MLS.Network.SubscriptionManager.csproj    network-modules/subscription-manager/MLS.Network.SubscriptionManager/

RUN dotnet restore network-modules/subscription-manager/MLS.Network.SubscriptionManager/MLS.Network.SubscriptionManager.csproj \
    --runtime linux-x64 --no-cache

COPY core/                                                                   core/
COPY network-modules/subscription-manager/MLS.Network.SubscriptionManager/  network-modules/subscription-manager/MLS.Network.SubscriptionManager/

RUN dotnet publish network-modules/subscription-manager/MLS.Network.SubscriptionManager/MLS.Network.SubscriptionManager.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --no-restore \
    --output /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S mls && adduser -S mls -G mls
USER mls

COPY --from=build --chown=mls:mls /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://0.0.0.0:5012

EXPOSE 5012 6012

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:5012/health || exit 1

ENTRYPOINT ["dotnet", "MLS.Network.SubscriptionManager.dll"]
