# Deployment

## Local / lab

1. `docker compose -f infrastructure/compose.yaml up -d`
2. Run `apps/api-server` (HTTP 5163) and `apps/admin-web` (Vite 5173)
3. Enroll agents with tokens from the Devices page

This is the only fully exercised path today.

## Production stack (Docker Compose)

`infrastructure/compose.production.yaml` runs the full stack: **Postgres + API + admin web** behind nginx with TLS. One command:

```powershell
cd infrastructure

# 1. Secrets
cp .env.production.example .env.production
#    edit .env.production — set POSTGRES_PASSWORD and JWT_SECRET (min 32 chars)

# 2. TLS (lab self-signed; replace with real certs in production)
.\scripts\new-dev-cert.ps1            # creates certs\server.crt + server.key

# 3. Build + run
docker compose --env-file .env.production -f compose.production.yaml up -d --build
```

Then browse to `https://localhost` (self-signed warning expected in lab). The API applies EF migrations at startup; no separate migration step.

Layout:

| Service | Container | Exposure |
|---------|-----------|----------|
| `web` (nginx + static SPA) | gatus-web | Host ports 80/443 (`HTTP_PORT`/`HTTPS_PORT`) |
| `api` (.NET 10) | gatus-api | Internal only — proxied at `/api/` and `/hubs/` |
| `postgres` | gatus-postgres | Internal only |

Data persists in named volumes: `postgres-data`, `api-content` (uploaded content packages).

The frontend calls relative `/api` + `/hubs` paths, so no per-environment frontend build is needed. nginx terminates TLS, proxies, and forwards `X-Forwarded-*`; the API honors them via `UseForwardedHeaders` (added for this stack). Redis/MinIO are dev-only and intentionally omitted.

## Docker Compose notes

`infrastructure/compose.yaml` starts **Postgres, Redis, and MinIO only**. It does not containerize the API or the admin UI. Do not expect `docker compose up` to serve `https://app.example.com`.

## Database

- Provider: PostgreSQL 16
- Migrations: applied by the API at startup
- Backup (lab): `pg_dump` from the `kiosk-postgres` container

```powershell
docker exec kiosk-postgres pg_dump -U kiosk kiosk > kiosk-backup.sql
```

Restore only onto a matching schema version.

## Windows agent (target PC)

Requires Administrator for service install:

```powershell
.\agents\windows-agent\install.ps1 -ServerUrl "https://your-api.example" -EnrollmentToken "<token>"
```

Uninstall: `uninstall.ps1`.

Self-contained publish (`win-x64`) is configured on the agent csproj so the kiosk does not need a machine-wide .NET runtime. Building on a developer box while the agent EXE is running will fail with file-lock errors.

## Kiosk runtime

`agents/windows-kiosk-runtime/install-runtime.ps1` can use Shell Launcher (Enterprise/Education/IoT) or a Winlogon `Shell` replacement (Pro). **Always keep a recovery path** (Safe Mode + restore script). Do not replace the shell on your daily driver without a plan.

## CI/CD

| Workflow | Role |
|----------|------|
| `.github/workflows/ci.yml` | Restore, build, test (.NET 10 + Node 22) on `main`/`develop` |
| `.github/workflows/security.yml` | Dependency / security scan |
| `.github/workflows/deploy.yml` | Deploy job stub — configure environments before use |

## Production checklist (not all done)

- [x] HTTPS reverse proxy and HSTS (nginx in `compose.production.yaml`)
- [ ] Rotate JWT signing key; store in a secret manager (currently `.env.production`)
- [x] Authenticate every agent endpoint with device credentials
- [ ] Postgres backups + tested restore
- [ ] Signed agent/runtime binaries
- [ ] Restrict CORS to the real admin origin (same-origin behind nginx mitigates)
- [ ] Replace seed users; disable `Admin123!`
