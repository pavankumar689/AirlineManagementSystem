FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BookingService.API/BookingService.API.csproj",                  "BookingService.API/"]
COPY ["BookingService.Application/BookingService.Application.csproj",  "BookingService.Application/"]
COPY ["BookingService.Domain/BookingService.Domain.csproj",            "BookingService.Domain/"]
COPY ["BookingService.Infrastructure/BookingService.Infrastructure.csproj", "BookingService.Infrastructure/"]
COPY ["Shared.Events/Shared.Events.csproj",                            "Shared.Events/"]

RUN dotnet restore "BookingService.API/BookingService.API.csproj"

COPY . .
WORKDIR "/src/BookingService.API"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ["BookingService.API/nlog.config", "/app/nlog.config"]
ENTRYPOINT ["dotnet", "BookingService.API.dll"]
