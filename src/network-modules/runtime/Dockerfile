# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY core/MLS.Core/MLS.Core.csproj                                         core/MLS.Core/
COPY network-modules/runtime/MLS.Network.Runtime/MLS.Network.Runtime.csproj  network-modules/runtime/MLS.Network.Runtime/

RUN dotnet restore network-modules/runtime/MLS.Network.Runtime/MLS.Network.Runtime.csproj \
    --runtime linux-x64 --no-cache

COPY core/                                              core/
COPY network-modules/runtime/MLS.Network.Runtime/       network-modules/runtime/MLS.Network.Runtime/

RUN dotnet publish network-modules/runtime/MLS.Network.Runtime/MLS.Network.Runtime.csproj \
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
    ASPNETCORE_URLS=http://0.0.0.0:5013

EXPOSE 5013 6013

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:5013/health || exit 1

ENTRYPOINT ["dotnet", "MLS.Network.Runtime.dll"]
