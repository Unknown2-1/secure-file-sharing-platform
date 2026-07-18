FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/ src/
RUN dotnet restore src/VaultShare.Api/VaultShare.Api.csproj
RUN dotnet publish src/VaultShare.Api/VaultShare.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN addgroup -S vaultshare && adduser -S vaultshare -G vaultshare
WORKDIR /app
COPY --from=build --chown=vaultshare:vaultshare /app .
USER vaultshare
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 CMD wget -qO- http://localhost:8080/health/live
ENTRYPOINT ["dotnet", "VaultShare.Api.dll"]
