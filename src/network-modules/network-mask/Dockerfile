# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY core/MLS.Core/MLS.Core.csproj                                                                       core/MLS.Core/
COPY network-modules/network-mask/MLS.Network.NetworkMask/MLS.Network.NetworkMask.csproj                 network-modules/network-mask/MLS.Network.NetworkMask/

RUN dotnet restore network-modules/network-mask/MLS.Network.NetworkMask/MLS.Network.NetworkMask.csproj \
    --runtime linux-x64 --no-cache

COPY core/                                                           core/
COPY network-modules/network-mask/MLS.Network.NetworkMask/           network-modules/network-mask/MLS.Network.NetworkMask/

RUN dotnet publish network-modules/network-mask/MLS.Network.NetworkMask/MLS.Network.NetworkMask.csproj \
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
    ASPNETCORE_URLS=http://0.0.0.0:5016

EXPOSE 5016 6016

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:5016/health || exit 1

ENTRYPOINT ["dotnet", "MLS.Network.NetworkMask.dll"]
