# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY core/MLS.Core/MLS.Core.csproj                                   core/MLS.Core/
COPY block-controller/MLS.BlockController/MLS.BlockController.csproj block-controller/MLS.BlockController/

RUN dotnet restore block-controller/MLS.BlockController/MLS.BlockController.csproj \
    --runtime linux-x64 --no-cache

COPY core/          core/
COPY block-controller/ block-controller/

RUN dotnet publish block-controller/MLS.BlockController/MLS.BlockController.csproj \
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
    ASPNETCORE_URLS=http://0.0.0.0:5100

EXPOSE 5100 6100

ENTRYPOINT ["dotnet", "MLS.BlockController.dll"]
