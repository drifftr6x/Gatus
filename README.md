# Gatus Kiosk Platform

Enterprise Windows kiosk management: enroll devices, push policy and content, monitor health, and send allowlisted remote commands.

The admin UI is branded **Gatus Kiosk**. The Windows agent and kiosk runtime still use the `SentinelKiosk` assembly and service names (`SentinelKioskAgent`, `C:\ProgramData\SentinelKiosk\`).

## What it does today

- JWT login with refresh-token rotation and RBAC (Viewer / Editor / Admin / SuperAdmin)
- Device inventory, groups, tags, enrollment tokens, heartbeats, and live dashboard
- Content library with versioned packages, SHA-256, and deployments
- Schedules, alerts, analytics, notification channels, and server log viewer
- Windows agent: enroll, heartbeat, policy sync, content deploy, command poll, telemetry
- WPF + WebView2 kiosk runtime (fullscreen, URL guards, session timeout)

## Stack

| Layer | Technology |
|-------|------------|
| API | .NET 10 / ASP.NET Core, EF Core, PostgreSQL 16 |
| Real-time | SignalR (`/hubs/devices`) |
| Admin UI | React 19, TypeScript, Vite, Tailwind CSS |
| Cache / object store | Redis 7, MinIO (Compose infra; API uses Postgres + local content storage) |
| Agent | .NET 10 Windows Worker Service (`net10.0-windows`) |
| Kiosk | WPF + WebView2 |

## Quick start (this machine)

Infrastructure (already typical):

```powershell
docker compose -f infrastructure/compose.yaml up -d
```

Containers: `kiosk-postgres` (db `kiosk` / user `kiosk`), `kiosk-redis`, `kiosk-minio`.

**Terminal 1 — API** (listens on **http://localhost:5163**):

```powershell
cd apps\api-server
C:\Users\001adm_am\dotnet\dotnet.exe run
```

If `dotnet` is not on PATH, always use that SDK path.

**Terminal 2 — Admin UI** (Vite, usually **http://localhost:5173**):

```powershell
cd apps\admin-web
npm install
npm run dev
```

Vite proxies `/api` and `/hubs` to `http://localhost:5163`. If login fails with `ECONNREFUSED`, the API is down or the proxy port is wrong.

### Seed logins (local only)

| Email | Password | Role |
|-------|----------|------|
| `admin@gatus.local` | `Admin123!` | SuperAdmin |
| `editor@gatus.local` | `Editor123!` | Editor |

Seeder runs only when the `users` table is empty.

## Repository layout

```
apps/api-server          ASP.NET Core host (Program.cs, launchSettings)
apps/admin-web           React admin console
src/Platform.Api         Controllers, hubs, API services
src/Platform.Domain      Entities
src/Platform.Infrastructure  EF Core, migrations, seeder
src/Platform.Contracts   Request/response DTOs
src/Platform.Security    JWT + RBAC policies
src/Platform.Application / Platform.Shared
agents/windows-agent     Windows Service agent
agents/windows-kiosk-runtime  WebView2 kiosk process
tests/                   Domain + API integration tests
infrastructure/          docker compose
docs/                    Architecture, API, security, ops
```

Solution file: `KioskPlatform.slnx`.

## Documentation

| Doc | Contents |
|-----|----------|
| [Architecture](docs/ARCHITECTURE.md) | Modules, data flow, agent/server model |
| [Development](docs/DEVELOPMENT.md) | Local setup, ports, migrations, troubleshooting |
| [API](docs/API.md) | HTTP + SignalR surface |
| [Windows agent](docs/WINDOWS-AGENT.md) | Enroll, heartbeat, install |
| [Kiosk runtime](docs/KIOSK-RUNTIME.md) | WebView2 process and policy pipe |
| [Security](docs/SECURITY.md) | Auth, secrets, threat notes |
| [Teams alerts](docs/TEAMS-ALERTS.md) | Create Teams Workflow webhooks and test alerts |
| [Deployment](docs/DEPLOYMENT.md) | Compose, CI, production caveats |
| [Roadmap](docs/ROADMAP.md) | Done vs remaining |
| [Changelog](docs/CHANGELOG.md) | Notable changes |
| [Technical spec](spec.md) | Longer product/engineering specification |

AI coding conventions: [AGENT.md](AGENT.md).

## License

Proprietary — all rights reserved.
