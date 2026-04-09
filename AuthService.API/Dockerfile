FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all project files first (layer caching)
COPY ["AuthService.API/AuthService.API.csproj",            "AuthService.API/"]
COPY ["AuthService.Application/AuthService.Application.csproj", "AuthService.Application/"]
COPY ["AuthService.Domain/AuthService.Domain.csproj",      "AuthService.Domain/"]
COPY ["AuthService.Infrastructure/AuthService.Infrastructure.csproj", "AuthService.Infrastructure/"]
COPY ["Shared.Events/Shared.Events.csproj",                "Shared.Events/"]

RUN dotnet restore "AuthService.API/AuthService.API.csproj"

COPY . .
WORKDIR "/src/AuthService.API"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ["AuthService.API/nlog.config", "/app/nlog.config"]
ENTRYPOINT ["dotnet", "AuthService.API.dll"]
