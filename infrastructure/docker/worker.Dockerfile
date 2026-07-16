FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY backend/ backend/
RUN dotnet restore backend/src/VaultShare.Worker/VaultShare.Worker.csproj
RUN dotnet publish backend/src/VaultShare.Worker/VaultShare.Worker.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
RUN addgroup -S vaultshare && adduser -S vaultshare -G vaultshare
WORKDIR /app
COPY --from=build --chown=vaultshare:vaultshare /app .
USER vaultshare
ENTRYPOINT ["dotnet", "VaultShare.Worker.dll"]

