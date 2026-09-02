# Client Deployment — Windows 11 Pro Kiosks

How to take a domain-joined Windows 11 Pro PC to a locked-down, managed kiosk.

## TL;DR

1. Build the bundle: `infrastructure/scripts/build-client-bundle.ps1 -Version 1.0.0`
2. In the admin console: **Devices → Enroll a Device → Generate Token** (one-time, 24h expiry)
3. Copy `dist/GatusKiosk-Bundle-1.0.0.zip` to the client, extract
4. Elevated PowerShell:
   ```powershell
   .\setup.ps1 -ServerUrl https://<server> -EnrollmentToken <token>
   ```
5. Verify the device shows **Online** in the console, assign its policy, push content
6. Sign in as the kiosk user (or reboot a dedicated machine) — kiosk mode engages

## What setup.ps1 does

| Step | Detail |
|---|---|
| Preflight | admin check, OS edition notice, domain-join info, pending-reboot warning |
| Kiosk user | creates local `KioskUser` (random never-expiring password) or locks down `-UseDomainUser` |
| Agent | copies to `Program Files\SentinelKiosk\Agent`, writes production config, stages enrollment token, installs `SentinelKioskAgent` service (delayed auto-start, restart-on-failure) |
| Runtime | copies WebView2 shell to `Program Files\SentinelKiosk\Runtime` |
| Lockdown | **per-user** Winlogon shell = `SentinelKiosk.Runtime.exe` (replaces explorer.exe for the kiosk user only), registry restrictions (no Task Manager, no password change, no power button unless `-AllowPowerButton`) |
| First logon | if the kiosk profile has never been loaded, a `Gatus-KioskShell` scheduled task applies the shell + restrictions at first sign-in |

Everything is reversible: `uninstall.ps1` restores the shell, removes restrictions, service, and files (`-KeepData` keeps logs/content; `-RemoveUser` deletes the local account).

## Domain-join notes

- **Per-user shell, not machine-wide** — domain users signing into the same PC get normal Explorer. Only the kiosk account is locked down.
- **GPO conflicts**: if domain GPOs set `DisableTaskMgr`/explorer policies, they may fight the local hive settings. Prefer a kiosk OU with restricted GPO application, or manage these via GPO and skip the bundle's restriction step.
- **Local vs domain kiosk account**: the default local `KioskUser` keeps kiosk sessions off domain credentials and survives domain trust issues. Use `-UseDomainUser DOM\user` only if the kiosk content itself needs domain resources.
- **First profile load**: the kiosk profile must load once (sign in interactively, or let the machine auto-logon) before the shell override can be written to `HKEY_USERS\<SID>`. The bundle handles this with the first-logon task — expect one normal Explorer sign-in, then kiosk mode from the second sign-in onward.
- **Auto-logon** (optional, kiosk-dedicated machines): configure via `netplwiz` or Sysinternals Autologon for the kiosk account; combined with the shell replacement the machine boots straight into the kiosk.

## Lockdown path by edition

This bundle targets **Pro**. On Windows 11 **Enterprise/Education/IoT**, Shell Launcher (via the agent's `ShellLauncherProvider`) is the stronger, supported mechanism — the agent applies it from policy when available. The bundle's Winlogon-shell approach still works as a baseline everywhere.

## Recovery

| Situation | Action |
|---|---|
| Kiosk broken, need desktop | Sign in as another (admin) user — only the kiosk account is locked |
| Kiosk account unusable | Safe Mode → `uninstall.ps1`, or delete the per-user `Shell` value under `HKU\<SID>\...\Winlogon` |
| Agent not reporting | `Get-Service SentinelKioskAgent`; logs at `C:\ProgramData\SentinelKiosk\Logs\agent-*.log` |
| Maintenance window | Agent `MaintenanceModeService` can relax lockdown temporarily per policy |

## Updating agents later

Don't redeploy this bundle for routine agent updates — use the **self-update channel**: `publish-agent-update.ps1 -Version x.y.z` → upload via API → agents download, verify the signed manifest, and swap themselves with automatic rollback. The bundle is for initial install only.
