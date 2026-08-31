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

## Configuration

`agents/windows-agent/appsettings.json`:

```json
{
  "Agent": {
    "ServerUrl": "http://localhost:5163",
    "HeartbeatIntervalSeconds": 30
  }
}
```

Installer `-ServerUrl` should be the **public API origin** in production (HTTPS).

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

This laptop has already enrolled as **LAPITG001116** in the lab database.

## Lockdown

`LockdownEngine` applies reversible OS restrictions only when policy asks for a kiosk profile. Prefer Assigned Access / Shell Launcher on supported editions. Recovery: `install`/`uninstall` scripts and `restore.ps1` patterns under agent scripts — test on a VM first.
