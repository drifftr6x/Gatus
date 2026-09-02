# Backup & Restore Runbook

## What must be backed up

Two artifacts, **always together in the same backup window**:

| Artifact | Where | Contains | Losing it means |
|---|---|---|---|
| Postgres dump | docker volume `postgres-data` / `gatus_postgres-data` | devices, users, alerts, deployments, telemetry | full state loss |
| API AppData | `apps/api-server/AppData` (dev, via `ContentStorage:Root` in appsettings.json) or `api-content` volume (prod) | content packages, agent update packages, **`content/keys/signing.key`** | deployments point at missing files; **without the signing key no agent can ever verify a new package again** — you'd have to re-enroll every device |

> Note: `ContentStorage:Root` pins the storage location. Without it the signing service falls back to the *binary* directory (`bin/.../AppData`), which the backup script does not cover — don't remove that setting.

Logs (`logs/`, agent `Logs\`) are **not** backed up — they're diagnostic, not state.

## Scripts

All in `infrastructure/scripts/`:

```powershell
# Database (pg_dump custom format, compressed, integrity-checked)
.\backup-postgres.ps1                                        # dev container
.\backup-postgres.ps1 -ContainerName gatus-postgres          # prod stack

# AppData (content + agent updates + signing key)
.\backup-appdata.ps1                                         # dev folder
.\backup-appdata.ps1 -Mode volume -VolumeName gatus_api-content   # prod volume

# Restore (takes a pre-restore snapshot automatically)
.\restore-postgres.ps1 -DumpFile ..\..\backups\kiosk-20260908-023000.dump -Force
```

Backups land in `<repo>\backups\` with `YYYYMMDD-HHMMSS` timestamps; `-RetentionDays 30` prunes older files (default; `0` = keep forever). `-OutDir` lets you target a synced folder (OneDrive, NAS) for off-box copies.

## Scheduling

**Windows (dev/lab):** as Administrator —
```powershell
.\register-backup-task.ps1 -At 02:30
```
Creates a daily `Gatus-Backup` task, logs to `backups\backup-YYYYMMDD.log`. Test with `Start-ScheduledTask -TaskName Gatus-Backup`.

**Linux (prod host):** cron —
```
30 2 * * *  cd /opt/gatus && pg_dump ... # or run the scripts via pwsh
```

## Recovery: full drill onto a fresh machine

1. Install Docker, clone the repo, copy `backups\` onto the machine.
2. `docker compose -f infrastructure/compose.yaml up -d` (or `compose.production.yaml`) — let the API run migrations once to create the schema, **or** restore onto the empty DB; both work since `pg_restore --clean --if-exists` handles either.
3. Restore data:
   ```powershell
   .\infrastructure\scripts\restore-postgres.ps1 -DumpFile .\backups\kiosk-<latest>.dump -Force
   ```
4. Restore AppData:
   - dev layout: extract `appdata-<latest>.zip` over `apps\api-server\AppData\`
   - prod volume: `docker run --rm -v gatus_api-content:/data -v ${PWD}\backups:/in alpine sh -c "cd /data && tar xf /in/appdata.tar"` (after extracting the zip)
5. Restart the API, log in, check Devices page loads and a device detail shows content/deployments.
6. Spot-check signing: `GET /api/signing/public-key` should return the **same key id** agents have pinned. If it changed, agents will reject packages — restore `signing.key` from the AppData backup.

**Expected RPO/RTO:** daily backups → up to 24h data loss (RPO). Restore on a prepared host ≈ 10 minutes (RTO), dominated by dump size.

## Failure scenarios

- **Bad restore:** every restore first writes `pre-restore-*.dump`; re-run `restore-postgres.ps1` with that file to go back.
- **Signing key lost but DB intact:** existing deployed content keeps working (already on devices), but all new packages fail verification fleet-wide. Recovery: generate a new key (delete `content/keys/signing.key`, restart API), then re-pin every agent (re-enrollment flow). Avoid this — back up AppData.
- **Container/volume deleted:** named volumes survive `compose down`; they die with `down -v`. Backups are outside Docker, so restore per the drill above.

## Future options (not implemented)

- Off-site upload (S3/B2) — point `-OutDir` at a rclone/robocopy-synced folder today
- WAL archiving + PITR for sub-daily RPO
- Automated restore verification (nightly restore into scratch DB + smoke query)
