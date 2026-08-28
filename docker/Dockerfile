FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ShipmentTelemetry.sln ./
COPY src/ShipmentTelemetry.Api/ShipmentTelemetry.Api.csproj src/ShipmentTelemetry.Api/
COPY src/ShipmentTelemetry.Application/ShipmentTelemetry.Application.csproj src/ShipmentTelemetry.Application/
COPY src/ShipmentTelemetry.Domain/ShipmentTelemetry.Domain.csproj src/ShipmentTelemetry.Domain/
COPY src/ShipmentTelemetry.Infrastructure/ShipmentTelemetry.Infrastructure.csproj src/ShipmentTelemetry.Infrastructure/
COPY src/ShipmentTelemetry.Contracts/ShipmentTelemetry.Contracts.csproj src/ShipmentTelemetry.Contracts/
COPY tests/ShipmentTelemetry.UnitTests/ShipmentTelemetry.UnitTests.csproj tests/ShipmentTelemetry.UnitTests/
COPY tests/ShipmentTelemetry.IntegrationTests/ShipmentTelemetry.IntegrationTests.csproj tests/ShipmentTelemetry.IntegrationTests/
COPY tests/ShipmentTelemetry.ArchitectureTests/ShipmentTelemetry.ArchitectureTests.csproj tests/ShipmentTelemetry.ArchitectureTests/

RUN dotnet restore src/ShipmentTelemetry.Api/ShipmentTelemetry.Api.csproj

COPY . .
RUN dotnet publish src/ShipmentTelemetry.Api/ShipmentTelemetry.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShipmentTelemetry.Api.dll"]
