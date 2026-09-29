# Build from the repository root:
#   docker build -f src/Stikling.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the packages stay cached until the project file changes
COPY src/Stikling.Api/Stikling.Api.csproj src/Stikling.Api/
RUN dotnet restore src/Stikling.Api/Stikling.Api.csproj

COPY src/Stikling.Api/ src/Stikling.Api/
RUN dotnet publish src/Stikling.Api/Stikling.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Stikling.Api.dll"]
