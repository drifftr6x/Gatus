# Gatus API server — production image
# Build context must be the repo root: docker build -f infrastructure/docker/api.Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central package management + SDK pin
COPY ["global.json", "./"]
COPY ["Directory.Packages.props", "./"]

# Project files first for restore-layer caching
COPY ["apps/api-server/Platform.ApiServer.csproj", "apps/api-server/"]
COPY ["src/Platform.Api/Platform.Api.csproj", "src/Platform.Api/"]
COPY ["src/Platform.Application/Platform.Application.csproj", "src/Platform.Application/"]
COPY ["src/Platform.Contracts/Platform.Contracts.csproj", "src/Platform.Contracts/"]
COPY ["src/Platform.Domain/Platform.Domain.csproj", "src/Platform.Domain/"]
COPY ["src/Platform.Infrastructure/Platform.Infrastructure.csproj", "src/Platform.Infrastructure/"]
COPY ["src/Platform.Security/Platform.Security.csproj", "src/Platform.Security/"]
COPY ["src/Platform.Shared/Platform.Shared.csproj", "src/Platform.Shared/"]

RUN dotnet restore "apps/api-server/Platform.ApiServer.csproj"

COPY . .
RUN dotnet publish "apps/api-server/Platform.ApiServer.csproj" \
      -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Non-root runtime user
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
# Content store — mount a named volume here to persist uploads across container restarts
VOLUME ["/app/AppData"]

ENTRYPOINT ["dotnet", "Platform.ApiServer.dll"]
