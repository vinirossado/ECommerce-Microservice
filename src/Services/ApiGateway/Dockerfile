FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/ApiGateway/ApiGateway.csproj", "src/Services/ApiGateway/"]
RUN dotnet restore "src/Services/ApiGateway/ApiGateway.csproj"
COPY . .
WORKDIR "/src/src/Services/ApiGateway"
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Make sure we copy the ocelot.json file to the output directory
#COPY ["src/Services/ApiGateway/ocelot.json", "/app/"]
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
