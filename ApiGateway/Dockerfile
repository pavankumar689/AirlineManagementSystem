FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]

RUN dotnet restore "ApiGateway/ApiGateway.csproj"

COPY . .
WORKDIR "/src/ApiGateway"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ["ApiGateway/ocelot.docker.json", "/app/ocelot.json"]
COPY ["ApiGateway/nlog.config", "/app/nlog.config"]
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
