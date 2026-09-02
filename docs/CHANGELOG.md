# Changelog

## [Unreleased]

### Added

- **Agent self-updater**: `agent_updates` table, `AgentUpdatesController` (admin upload/sign/activate, agent `latest`/`download` with version + minVersion + rollout-% gating), agent `UpdateService` (hourly poll, signed-manifest + per-file SHA-256 verification, detached `apply-update.ps1` self-swap with backup/rollback), `publish-agent-update.ps1` packaging script
- **Backup/restore suite**: `backup-postgres.ps1` (binary-safe `pg_dump -Fc` + integrity check + retention), `restore-postgres.ps1` (pre-restore snapshot + verification), `backup-appdata.ps1` (local or named-volume, covers signing keys), `register-backup-task.ps1` (Task Scheduler), `docs/BACKUP-RESTORE.md` runbook
- **Security hardening**: JWT secret startup validation (fail fast on missing/short/placeholder outside Development), JWT secret removed from committed `appsettings.json`, config-driven/random seed admin password, `MustChangePassword` flag + forced change-password flow (invalidates all sessions)
- **Deployment rings**: `RingOrder`/`ParentDeploymentId`/`SoakMinutes` on deployments, ring-chain activation in `DeploymentSchedulerService` with 80% success gate, ring presets in deploy modal, ring badges on dashboard
- **Maintenance windows**: `MaintenanceWindowStart/Duration/Days` on device groups (overnight-safe), agent poll filtering, group editor UI, window badges
- **Alert maturity**: per-alert notification cooldown (`CooldownMinutes`), escalation policies (`EscalationPolicy`/`EscalationStep` + `AlertEscalationService`), test-notification endpoint + UI button
- Enrollment tokens (hashed, single-use, expiry, revoke) and `POST /api/devices/enroll`
- Admin UI to generate/copy enrollment tokens
- Windows agent enrollment CLI (`--enroll`), heartbeat, policy sync, deployments, commands, telemetry
- WebView2 kiosk runtime and lockdown engine
- Device groups, alerts, analytics, notification channels, log viewer
- Content versions, deployments, SHA-256 + RSA-signed packages
- SignalR live dashboard, schedules, telemetry ingest

### Fixed

- Seed and login identity: `admin@gatus.local` / `editor@gatus.local`
- Vite proxy target aligned with API port **5163** (was 5000)
- Agent JSON enrollment deserialize (case-insensitive)
- Agent state directory fallback when ProgramData is not writable
- `enrollment_tokens.device_id` on fresh databases
- `ContentStorage:Root` pinned in appsettings so signing keys land under `apps/api-server/AppData` (covered by backups) instead of `bin/.../AppData`
- Backup scripts hardened for PowerShell 5.1 (binary-safe `docker cp` instead of PS redirection; quote-safe SQL via stdin)

### Security

- All agent endpoints now require the per-device `deviceSecret` (Bearer) bound to the device ID; 401/403 triggers the agent re-enrollment flow. Enrollment is the only anonymous agent operation
- Agent binaries and content packages are both RSA-4096 (PSS) signed; agents pin the server public key via DPAPI
- Seeded admin accounts are flagged `MustChangePassword`; the web UI blocks app access until changed
- The API refuses to start outside Development with a missing, short (<32), or placeholder JWT secret
- Known NuGet advisories (`Microsoft.OpenApi`, `System.Security.Cryptography.Xml`) still need package bumps
