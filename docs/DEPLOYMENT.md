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
#    Generate a strong JWT secret:  openssl rand -base64 48
#    The API refuses to start in non-Development with a missing, short (<32), or placeholder secret.

# 2. TLS (lab self-signed; replace with real certs in production)
.\scripts\new-dev-cert.ps1            # creates certs\server.crt + server.key

# 3. Build + run
docker compose --env-file .env.production -f compose.production.yaml up -d --build
```

Then browse to `https://localhost` (self-signed warning expected in lab). The API applies EF migrations at startup; no separate migration step.

## First admin account

In **Development** the seeder creates `admin@gatus.local` using the password from `Seed:AdminPassword` in `appsettings.Development.json` (or a randomly generated one, printed to the logs as a warning). The seeded admin is flagged `MustChangePassword` — the web UI forces a password change on first login. In **Production** no users are seeded; create the first admin via the register endpoint or the database, and set `must_change_password = true` on the row to force a change on first login.

Layout:

| Service | Container | Exposure |
|---------|-----------|----------|
| `web` (nginx + static SPA) | gatus-web | Host ports 80/443 (`HTTP_PORT`/`HTTPS_PORT`) |
| `api` (.NET 10) | gatus-api | Internal only — proxied at `/api/` and `/hubs/` |
| `postgres` | gatus-postgres | Internal only |

Data persists in named volumes: `postgres-data`, `api-content` (uploaded content packages).

The frontend calls relative `/api` + `/hubs` paths, so no per-environment frontend build is needed. nginx terminates TLS, proxies, and forwards `X-Forwarded-*`; the API honors them via `UseForwardedHeaders` (added for this stack). Redis/MinIO are dev-only and intentionally omitted.

## Agent self-updates

Agents poll `GET /api/agent-updates/latest` hourly (`Agent:UpdateCheckIntervalSeconds`) with their device secret. When an active update with a newer version is offered, the agent downloads the package, **verifies the RSA-signed manifest** (same pinned server key as content) plus per-file SHA-256, stages to `%ProgramData%\SentinelKiosk\Updates\staging\`, then runs a self-apply script: stop service → back up current binaries to `Updates\backup\` → copy new files → start service → **restore backup if the new version fails to start**.

Publishing an update (admin action, server signs at upload — the build machine never holds the private key):

```powershell
cd infrastructure\scripts
.\publish-agent-update.ps1 -Version 1.1.0     # builds Release win-x64 + zips to dist\
# Upload dist\agent-update-1.1.0.zip via POST /api/agent-updates (multipart:
#   file, version, rolloutPercent, optional minVersion/notes) — admin UI TBD
```

Eligibility gates on the server: strictly newer version, optional `minVersion` floor, and deterministic `rolloutPercent` bucketing (same device always lands in the same bucket per update). Uploading a new update deactivates older ones; re-activate an older version to roll the fleet back. Update application is logged on the device at `Logs\apply-update.log` and visible fleet-wide via `agentVersion` in heartbeats.

## Docker Compose notes

`infrastructure/compose.yaml` starts **Postgres, Redis, and MinIO only**. It does not containerize the API or the admin UI. Do not expect `docker compose up` to serve `https://app.example.com`.

## Database

- Provider: PostgreSQL 16
- Migrations: applied by the API at startup
- Backup/restore: see **[BACKUP-RESTORE.md](BACKUP-RESTORE.md)** — scripted dumps with retention (`infrastructure/scripts/backup-postgres.ps1`), verified restores with pre-restore snapshots, and the AppData/signing-key backup that must accompany every DB backup.

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
- [x] JWT secret never committed; startup validation rejects missing/short/placeholder secrets (still `.env.production` — a real secret manager is a nice-to-have)
- [x] Authenticate every agent endpoint with device credentials
- [x] Postgres backups + tested restore — see [BACKUP-RESTORE.md](BACKUP-RESTORE.md) (daily scheduled dump + AppData incl. signing keys; verified restore drill)
- [x] Signed content manifests (RSA-4096; agent verifies before activation; key persisted under content root `keys/`, served via `GET /api/signing/public-key`)
- [x] Signed agent self-update packages (same signing path; see "Agent self-updates" above; Authenticode code-signing still open)
- [ ] Restrict CORS to the real admin origin (same-origin behind nginx mitigates)
- [x] No hardcoded seed credentials; seeded admin forced to change password on first login (`MustChangePassword`)
