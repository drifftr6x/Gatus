# Deployment

## Local / lab

1. `docker compose -f infrastructure/compose.yaml up -d`
2. Run `apps/api-server` (HTTP 5163) and `apps/admin-web` (Vite 5173)
3. Enroll agents with tokens from the Devices page

This is the only fully exercised path today.

## Docker Compose notes

`infrastructure/compose.yaml` starts **Postgres, Redis, and MinIO only**. It does not containerize the API or the admin UI. Do not expect `docker compose up` to serve `https://app.example.com`.

Production-shaped compose / nginx files may exist as drafts; treat them as incomplete until they are run in a staging environment.

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

- [ ] HTTPS reverse proxy and HSTS
- [ ] Rotate JWT signing key; store in a secret manager
- [ ] Authenticate every agent endpoint with device credentials
- [ ] Postgres backups + tested restore
- [ ] Signed agent/runtime binaries
- [ ] Restrict CORS to the real admin origin
- [ ] Replace seed users; disable `Admin123!`
