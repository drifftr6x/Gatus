# Windows agent

Project: `agents/windows-agent` (`SentinelKiosk.Agent`).

Windows Service name: `SentinelKioskAgent`.

## Responsibilities

| Service | Interval (default) | Behavior |
|---------|--------------------|----------|
| Enrollment | once (`--enroll`) | Token → device id + DPAPI secret |
| Heartbeat | 30s | CPU / RAM / disk / uptime; sets device Online |
| Policy sync | 300s | Fetch policy JSON, cache, optional lockdown apply |
| Deployment | 60s | Download, SHA-256, stage, activate, report |
| Commands | 15s | Poll queued allowlisted commands |
| Telemetry | 300s batch | Queue metrics, upload when reachable |

## Enroll (dev)

1. API + web running
2. Admin: **Devices → Enroll a Device → Generate Token** (optional: link to an existing device row)
3. On the Windows PC:

```powershell
C:\Users\001adm_am\dotnet\dotnet.exe run --project agents\windows-agent -- --enroll <token>
```

Or published EXE:

```powershell
SentinelKiosk.Agent.exe --enroll <token>
```

4. Confirm the device is **Online** and `last_seen_at` moves every ~30s

A used or expired token returns 4xx. Tokens are single-use.

## Authentication

After enrollment, the agent sends its unique bearer device secret on device-facing requests:

```http
Authorization: Bearer <device-secret>
```

The server stores only a SHA-256 hash of the secret and binds the credential to the device ID. Heartbeats, telemetry, command polling/results, deployment polling/status, policy retrieval, and content downloads must use the enrolled device credential. Enrollment remains the only anonymous agent operation and requires a valid one-time token.

If the device receives `401` or `403`, the agent records `CredentialRejected`, preserves cached policy/content, and logs a re-enrollment instruction. Stop the service, generate a new token in **Devices → Enroll a Device**, then run:

```powershell
Stop-Service SentinelKioskAgent
SentinelKiosk.Agent.exe --enroll <new-token>
Start-Service SentinelKioskAgent
```

Enrollment replaces the DPAPI-protected credential only after the server accepts the new token. Confirm the device is Online before deploying content.

## Configuration

`agents/windows-agent/appsettings.json`:

```json
{
  "Agent": {
    "ServerUrl": "http://localhost:5163",
    "HeartbeatIntervalSeconds": 30,
    "UpdateCheckIntervalSeconds": 3600
  }
}
```

Installer `-ServerUrl` should be the **public API origin** in production (HTTPS).

## Self-updates

`UpdateService` polls `GET /api/agent-updates/latest?deviceId=&currentVersion=` on the configured interval. When the server offers a newer, eligible update (strictly newer version, optional `minVersion` floor, deterministic rollout bucket):

1. Download package to `Updates\staging\{version}\` and verify the zip SHA-256
2. Verify `manifest.json` RSA signature against the **DPAPI-pinned server public key** (`SignatureVerifier`), then per-file SHA-256
3. Generate `apply-update.ps1` (stop service → back up binaries to `Updates\backup\` → copy new files → start service → **restore backup if the service fails to start**) and launch it detached
4. Agent exits; the script performs the swap. Log: `Logs\apply-update.log`

The running agent's version is the assembly informational version (csproj `<Version>`), reported in every heartbeat as `agentVersion`.

Publish flow (operator): `infrastructure\scripts\publish-agent-update.ps1 -Version 1.1.0` → upload `dist\agent-update-1.1.0.zip` via `POST /api/agent-updates` (admin JWT). The **server signs the manifest at upload time** — build machines never hold the signing private key.

## State on disk

Preferred: `C:\ProgramData\SentinelKiosk\` (`Config`, `Content`, `Logs`, `Cache`, `State`, `Updates`).

Fallback if ProgramData is not writable: `%LocalAppData%\SentinelKiosk\`.

Logs: `Logs\agent-yyyyMMdd.log`.

## Verifying from SQL

```sql
SELECT name, hostname, status, last_seen_at, ip_address
FROM devices
ORDER BY last_seen_at DESC NULLS LAST;
```

This laptop is registered as **LAPITG001116** in the lab database (re-enrolled with a device-secret-linked token; authenticated heartbeats verified). When the API and agent run on the same laptop, keep the agent URL as `http://localhost:5163`.

## Lockdown

`LockdownEngine` applies reversible OS restrictions only when policy asks for a kiosk profile. Prefer Assigned Access / Shell Launcher on supported editions. Recovery: `install`/`uninstall` scripts and `restore.ps1` patterns under agent scripts — test on a VM first.
