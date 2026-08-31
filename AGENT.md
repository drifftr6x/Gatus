# AGENT.md — AI assistant guidelines

## Product

**Gatus Kiosk**: .NET 10 modular monolith API + React admin + Windows agent + WebView2 runtime.

Do not invent a `src/Hosts/Kiosk.Api` or `src/Modules/` tree — those paths are obsolete. Host is `apps/api-server`. Libraries are `src/Platform.*`.

## Environment (this workstation)

- SDK: `C:\Users\001adm_am\dotnet\dotnet.exe` (often **not** on PATH)
- API: **http://localhost:5163** (`apps/api-server/Properties/launchSettings.json`)
- Vite proxy: `apps/admin-web/vite.config.ts` → 5163
- Postgres: Docker `kiosk-postgres`, db/user `kiosk`, password `kiosk-dev-password`
- Seed: `admin@gatus.local` / `Admin123!`
- Node: system Node or `C:\Users\001adm_am\node\node-v22.14.0-win-x64`
- PowerShell may block `npm.ps1` — use `npm.cmd`

Before starting a second API, stop the listener on 5163. Before rebuilding the agent, stop `SentinelKiosk.Agent.exe`.

`dotnet ef` is unreliable here; apply SQL + `__EFMigrationsHistory` if migrate-on-startup is not enough.

## Conventions

- C#: file-scoped namespaces, nullable, DTO records in `Platform.Contracts`
- No MediatR — controllers use `ApplicationDbContext`
- React: hooks, React Query, Tailwind, pages under `apps/admin-web/src/pages`
- Commits: Conventional Commits; never commit secrets

## Tests

```powershell
C:\Users\001adm_am\dotnet\dotnet.exe test KioskPlatform.slnx
```

Integration tests: `WebApplicationFactory` + InMemory. Domain tests do not need the API process.

## Docs

Keep `README.md` and `docs/*` in sync when ports, seed emails, or enroll flows change.
