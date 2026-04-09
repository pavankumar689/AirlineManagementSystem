FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["FlightService.API/FlightService.API.csproj",                  "FlightService.API/"]
COPY ["FlightService.Application/FlightService.Application.csproj",  "FlightService.Application/"]
COPY ["FlightService.Domain/FlightService.Domain.csproj",            "FlightService.Domain/"]
COPY ["FlightService.Infrastructure/FlightService.Infrastructure.csproj", "FlightService.Infrastructure/"]
COPY ["Shared.Events/Shared.Events.csproj",                          "Shared.Events/"]

RUN dotnet restore "FlightService.API/FlightService.API.csproj"

COPY . .
WORKDIR "/src/FlightService.API"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ["FlightService.API/nlog.config", "/app/nlog.config"]
ENTRYPOINT ["dotnet", "FlightService.API.dll"]
