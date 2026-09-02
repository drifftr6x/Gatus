# Development guide

## Prerequisites

- .NET 10 SDK (this repo pins SDK via `global.json`; local install is often `C:\Users\001adm_am\dotnet\dotnet.exe` and **is not on PATH**)
- Node.js 22+
- Docker Desktop
- Optional: Git, VS 2022 / VS Code / Rider

PowerShell may block `npm.ps1`. Use `npm.cmd`, `Set-ExecutionPolicy Bypass -Scope Process`, or cmd.exe.

## Local topology

| Process | URL | How to start |
|---------|-----|----------------|
| PostgreSQL / Redis / MinIO | 5432 / 6379 / 9000 | `docker compose -f infrastructure/compose.yaml up -d` |
| API | **http://localhost:5163** | `cd src/Platform.Api && dotnet run` (with `ASPNETCORE_ENVIRONMENT=Development`) |
| Admin web | **http://localhost:5173** (or next free port) | `cd apps/admin-web && npm run dev` |
| Agent | talks to 5163 | `dotnet run --project agents/windows-agent -- --enroll <token>` |

Compose defaults (see `infrastructure/compose.yaml`):

- Container: `kiosk-postgres`
- Database / user: `kiosk` / `kiosk`
- Password: `kiosk-dev-password` (local only)

API connection string lives in `src/Platform.Api/appsettings.json` (`Host=localhost;Port=5432;Database=kiosk;Username=kiosk;Password=kiosk-dev-password`).

Vite proxy is in `apps/admin-web/vite.config.ts` and **must** target the API port (5163). A mismatch produces `http proxy error: /api/auth/login` / `ECONNREFUSED`.

## First-time setup

```powershell
docker compose -f infrastructure/compose.yaml up -d

# API (migrations apply on startup)
cd src\Platform.Api
set ASPNETCORE_ENVIRONMENT=Development
C:\Users\001adm_am\dotnet\dotnet.exe run

# Web
cd apps\admin-web
npm install
npm run dev
```

Login: `admin@gatus.local` / password from `Seed:AdminPassword` in `appsettings.Development.json` (default `Admin123!`; randomly generated + logged if unset). The seeded admin is forced to change password on first login.

## Migrations

`dotnet ef` may fail on this workstation (hostfxr / .NET 8 tool vs .NET 10 SDK). The API calls `Database.Migrate()` at startup.

If a new column is missing at runtime:

1. Apply the SQL from `src/Platform.Infrastructure/Migrations/*.cs` by hand
2. Insert the migration id into `__EFMigrationsHistory`
3. Restart the API

Do not edit already-applied migration files except to keep source in sync with a column that was added out-of-band.

## Tests

```powershell
C:\Users\001adm_am\dotnet\dotnet.exe test KioskPlatform.slnx
cd apps\admin-web
npm run build
```

API integration tests use `WebApplicationFactory` + EF InMemory. Stop the running API if you need to rebuild `src/Platform.Api` (DLLs will be locked). Stop a running agent before rebuilding `SentinelKiosk.Agent.exe`.

Find the API PID:

```powershell
Get-NetTCPConnection -LocalPort 5163 -State Listen |
  Select-Object -ExpandProperty OwningProcess -Unique
```

## Agent on a developer PC

1. In the UI: Devices → Enroll a Device → Generate Token
2. Run (console, not necessarily as a service):

```powershell
C:\Users\001adm_am\dotnet\dotnet.exe run --project agents\windows-agent -- --enroll <token>
```

Credentials are DPAPI-protected. If `C:\ProgramData\SentinelKiosk` is not writable, the agent stores state under `%LocalAppData%\SentinelKiosk`.

`appsettings.json` `Agent:ServerUrl` must be `http://localhost:5163` for local HTTP.

## Code conventions

- C#: file-scoped namespaces, nullable enabled, DTO **records** in `Platform.Contracts`
- React: functional components, React Query, Tailwind
- Commits: Conventional Commits (`feat:`, `fix:`, `docs:`)

There is **no MediatR/CQRS pipeline** in this repo; controllers talk to `ApplicationDbContext` directly.
