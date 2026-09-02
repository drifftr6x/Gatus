---
schema_version: 1
name: Production compose stack
overview: 'Full production docker-compose: API + admin web + nginx TLS reverse proxy, with corrected
  Dockerfiles matching the current project structure, secrets via env, and one-command
  deployment.'
todos:
  - id: pc-t1
    content: Rewrite api.Dockerfile for current project structure (.NET 10 GA)
    status: pending
  - id: pc-t2
    content: Verify frontend API base URL works behind nginx proxy; fix admin-web.Dockerfile if
  needed
    status: pending
  - id: pc-t3
    content: Update nginx.conf proxy target port + TLS
    status: pending
  - id: pc-t4
    content: Create compose.production.yaml (api + web + postgres, internal networking, volumes)
    status: pending
  - id: pc-t5
    content: Add .env.production.example + self-signed cert script + DEPLOYMENT.md update
    status: pending
  - id: pc-t6
    content: 'Verify: build images, bring stack up, login + SignalR over TLS, commit'
    status: pending
isProject: false
created_at: '2026-09-01T21:28:12'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_309
model: FW-Kimi-K3
mode_at_creation: auto
content_hash: ea707a79c3a51968
files_referenced:
  - infrastructure/compose.yaml
  - infrastructure/docker/api.Dockerfile
  - infrastructure/docker/admin-web.Dockerfile
  - infrastructure/docker/nginx.conf
  - apps/api-server/appsettings.json
  - docs/DEPLOYMENT.md
title: Production compose stack
---

# Production compose stack

_Full production docker-compose: API + admin web + nginx TLS reverse proxy, with corrected Dockerfiles matching the current project structure, secrets via env, and one-command deployment._

## Context
`infrastructure/compose.yaml` only runs data stores (Postgres/Redis/MinIO). The existing `infrastructure/docker/api.Dockerfile` is **stale** — it references a removed project layout (`src/Hosts/Kiosk.Api`, `src/Modules/*`). Real layout: `apps/api-server/Platform.ApiServer.csproj` + `src/Platform.{Domain,Infrastructure,Security,Contracts,Api}`. The admin-web Dockerfile and nginx.conf are close but need fixes.

## Steps

### 1. Rewrite `infrastructure/docker/api.Dockerfile`
- Multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` build → `aspnet:10.0` runtime (drop `-preview` tags — .NET 10 is GA)
- Restore via `KioskPlatform.slnx` or the csproj graph: `apps/api-server` + all `src/Platform.*` + `Directory.Packages.props` + `global.json`
- Publish `Platform.ApiServer.csproj` Release → `/app/publish`
- Runtime: non-root user, `ASPNETCORE_URLS=http://+:8080`, content storage volume at `/app/AppData`

### 2. Fix `infrastructure/docker/admin-web.Dockerfile`
- Add a build-arg for API base URL if needed (frontend uses relative `/api` paths via nginx proxy — verify `api.ts` base URL is relative so no rebuild-per-env)
- Keep nginx serving static build

### 3. Fix `infrastructure/docker/nginx.conf`
- `proxy_pass http://api:8080/` (match new API port 8080)
- Keep TLS, security headers, `/hubs/` WebSocket upgrade for SignalR

### 4. New `infrastructure/compose.production.yaml`
Services:
- **api**: build from api.Dockerfile; env: `ConnectionStrings__DefaultConnection` → `Host=postgres;...`, `Jwt__Secret` from env var, `ASPNETCORE_ENVIRONMENT=Production`; volume `api-content:/app/AppData`; depends_on postgres healthy; no host port (internal only)
- **web**: build from admin-web.Dockerfile; ports 80/443; mounts `./certs` for TLS; depends_on api
- **postgres**: same as dev but no host port published (internal network only)
- Drop redis/minio from production (unused by current code — verify with grep)

### 5. Secrets + config
- `infrastructure/.env.production.example` — POSTGRES_PASSWORD, JWT_SECRET, POSTGRES_DB/USER placeholders
- README/DEPLOYMENT.md update: `docker compose -f infrastructure/compose.yaml -f infrastructure/compose.production.yaml up -d --build` (or single prod file including data stores — decide: single self-contained prod file is simpler)
- Self-signed cert generation script for lab use: `infrastructure/scripts/new-dev-cert.ps1`

### 6. Verify
- `docker compose -f infrastructure/compose.production.yaml config` (validate)
- Build both images: `docker compose ... build`
- Bring up the stack on alternate ports (e.g. 8443) so it doesn't collide with the dev environment, hit `https://localhost:8443/api/product` → expect 401, login flow works, SignalR negotiates over wss
- Tear down, commit

## Files
- Rewrite: `infrastructure/docker/api.Dockerfile`, `infrastructure/docker/nginx.conf`
- Edit: `infrastructure/docker/admin-web.Dockerfile`
- New: `infrastructure/compose.production.yaml`, `infrastructure/.env.production.example`, `infrastructure/scripts/new-dev-cert.ps1`
- Edit: `docs/DEPLOYMENT.md`

## Notes / risks
- Check api.ts base URL handling before assuming relative paths (if it hardcodes localhost:5163, add build-arg)
- .NET 10 GA image tags: confirm `mcr.microsoft.com/dotnet/aspnet:10.0` exists (it should post-GA)
- Migrations: API applies migrations at startup already (saw it in logs), so no separate migrator service needed
