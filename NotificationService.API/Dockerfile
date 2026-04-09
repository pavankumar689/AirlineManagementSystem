FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NotificationService.API/NotificationService.API.csproj",                  "NotificationService.API/"]
COPY ["NotificationService.Application/NotificationService.Application.csproj",  "NotificationService.Application/"]
COPY ["NotificationService.Domain/NotificationService.Domain.csproj",            "NotificationService.Domain/"]
COPY ["NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "NotificationService.Infrastructure/"]
COPY ["Shared.Events/Shared.Events.csproj",                                      "Shared.Events/"]

RUN dotnet restore "NotificationService.API/NotificationService.API.csproj"

COPY . .
WORKDIR "/src/NotificationService.API"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ["NotificationService.API/nlog.config", "/app/nlog.config"]
ENTRYPOINT ["dotnet", "NotificationService.API.dll"]
