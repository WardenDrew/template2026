FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY api/api.csproj api/
COPY opaque-dotnet/opaque-dotnet.csproj opaque-dotnet/
RUN dotnet restore api/api.csproj

COPY api/ api/
COPY opaque-dotnet/ opaque-dotnet/
RUN dotnet publish api/api.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "api.dll"]
