# ---- Compilación ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Primero solo el .csproj: la restauración de paquetes queda en caché mientras no cambie.
COPY Directory.Build.props ./
COPY KadreeBank.Api/KadreeBank.Api.csproj KadreeBank.Api/
RUN dotnet restore KadreeBank.Api/KadreeBank.Api.csproj

COPY KadreeBank.Api/ KadreeBank.Api/
RUN dotnet publish KadreeBank.Api/KadreeBank.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Ejecución ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
# Usuario sin privilegios incluido en las imágenes oficiales de .NET.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "KadreeBank.Api.dll"]
