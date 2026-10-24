# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restaurar solo con los archivos de proyecto aprovecha la caché de capas
# mientras las dependencias no cambien.
COPY global.json Directory.Build.props .editorconfig ./
COPY src/SupermarketWEB/SupermarketWEB.csproj src/SupermarketWEB/
RUN dotnet restore src/SupermarketWEB/SupermarketWEB.csproj

COPY src/ src/
RUN dotnet publish src/SupermarketWEB/SupermarketWEB.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__SupermarketDB="Data Source=/app/data/supermarket.db" \
    DataProtection__KeysPath=/app/data/keys \
    HttpsRedirection__Enabled=false

# La base SQLite y las claves de sesión viven en un volumen escribible por el usuario sin privilegios.
RUN mkdir -p /app/data && chown "$APP_UID" /app/data
COPY --from=build /app .

USER $APP_UID
VOLUME /app/data
EXPOSE 8080
ENTRYPOINT ["dotnet", "SupermarketWEB.dll"]
