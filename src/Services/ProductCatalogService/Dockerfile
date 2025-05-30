FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/ProductCatalogService/ProductCatalogService.csproj", "src/Services/ProductCatalogService/"]
RUN dotnet restore "src/Services/ProductCatalogService/ProductCatalogService.csproj"
COPY . .
WORKDIR "/src/src/Services/ProductCatalogService"
RUN dotnet build "ProductCatalogService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProductCatalogService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProductCatalogService.dll"]
