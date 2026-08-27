FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["src/Hosts/Kiosk.Api/Kiosk.Api.csproj", "src/Hosts/Kiosk.Api/"]
COPY ["src/Modules/Identity/Kiosk.Modules.Identity/Kiosk.Modules.Identity.csproj", "src/Modules/Identity/Kiosk.Modules.Identity/"]
COPY ["src/Modules/Devices/Kiosk.Modules.Devices/Kiosk.Modules.Devices.csproj", "src/Modules/Devices/Kiosk.Modules.Devices/"]
COPY ["src/Modules/Content/Kiosk.Modules.Content/Kiosk.Modules.Content.csproj", "src/Modules/Content/Kiosk.Modules.Content/"]
COPY ["src/Modules/Sync/Kiosk.Modules.Sync/Kiosk.Modules.Sync.csproj", "src/Modules/Sync/Kiosk.Modules.Sync/"]
COPY ["src/Modules/Scheduling/Kiosk.Modules.Scheduling/Kiosk.Modules.Scheduling.csproj", "src/Modules/Scheduling/Kiosk.Modules.Scheduling/"]
COPY ["src/Modules/Telemetry/Kiosk.Modules.Telemetry/Kiosk.Modules.Telemetry.csproj", "src/Modules/Telemetry/Kiosk.Modules.Telemetry/"]
COPY ["src/Modules/Users/Kiosk.Modules.Users/Kiosk.Modules.Users.csproj", "src/Modules/Users/Kiosk.Modules.Users/"]
RUN dotnet restore "src/Hosts/Kiosk.Api/Kiosk.Api.csproj"
COPY . .
WORKDIR "/src/src/Hosts/Kiosk.Api"
RUN dotnet build "Kiosk.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Kiosk.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kiosk.Api.dll"]
