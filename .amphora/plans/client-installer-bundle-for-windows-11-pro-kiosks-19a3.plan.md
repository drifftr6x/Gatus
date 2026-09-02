---
schema_version: 1
name: Client installer bundle for Windows 11 Pro kiosks
overview: 'A self-contained, domain-join-aware installer bundle that deploys agent + kiosk runtime
  to Windows 11 Pro machines: one setup script, both self-contained binaries, enrollment,
  lockdown (Pro-appropriate path), and verification.'
todos:
  - id: cb1
    content: 'build-client-bundle.ps1: publish agent+runtime, assemble zip'
    status: completed
  - id: cb2
    content: 'setup.ps1: prereqs, files, config, service, enroll, kiosk user, Pro shell replacement
  + restrictions, -WhatIf'
    status: completed
  - id: cb3
    content: 'uninstall.ps1: shell restore, service removal, cleanup switches'
    status: completed
  - id: cb4
    content: Bundle README.txt + docs/CLIENT-DEPLOYMENT.md (domain-join notes, recovery)
    status: completed
  - id: cb5
    content: Build bundle, verify zip contents + -WhatIf dry run, commit
    status: in_progress
isProject: false
created_at: '2026-09-02T07:58:20'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_394
model: FW-Kimi-K3
mode_at_creation: auto
content_hash: 4f1ef4197e43173d
title: Client installer bundle for Windows 11 Pro kiosks
---

# Client installer bundle for Windows 11 Pro kiosks

_A self-contained, domain-join-aware installer bundle that deploys agent + kiosk runtime to Windows 11 Pro machines: one setup script, both self-contained binaries, enrollment, lockdown (Pro-appropriate path), and verification._

## Target environment
- Windows 11 Pro, domain-joined — **Shell Launcher unavailable** (Enterprise/Education/IoT only)
- Lockdown path on Pro: Winlogon shell replacement for the kiosk user + registry UI restrictions + in-process hotkey filter; agent LockdownEngine provides Assigned Access/registry/keyboard providers as policy dictates

## Bundle structure (output: `dist\GatusKiosk-Bundle-<version>.zip`)
```
bundle/
  agent/           SentinelKiosk.Agent.exe + appsettings.json (self-contained win-x64)
  runtime/         SentinelKiosk.Runtime.exe + deps (self-contained win-x64)
  setup.ps1        # one-command installer (admin)
  uninstall.ps1    # full removal incl. shell restore
  README.txt       # operator instructions
```

## setup.ps1 flow (run as Administrator)
1. `-ServerUrl` + `-EnrollmentToken` params (token generated in admin UI first)
2. Prechecks: Win11 Pro detection (warn not fail on other SKUs), .NET not required (self-contained), pending reboot check
3. Copy agent → `%ProgramFiles%\SentinelKiosk\Agent`, runtime → `%ProgramFiles%\SentinelKiosk\Runtime`
4. Write agent config (ServerUrl, intervals, UpdateCheckIntervalSeconds=3600)
5. Create kiosk local user if missing (`-KioskUser`, default `KioskUser`, random password, password never expires) — domain-joined machines: local user keeps kiosk off domain creds; document alternative `-UseDomainUser`
6. Register + start agent service (recovery: restart on failure, delayed auto-start)
7. Enroll agent with token (agent reads `Config\enrollment-token.txt` on first service start — verify install.ps1's existing behavior and reuse it)
8. Shell replacement for kiosk user via Winlogon registry (HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon per-user shell, or default-user shell if kiosk-only machine): `SentinelKiosk.Runtime.exe` instead of explorer.exe — **back up original value to ProgramData\SentinelKiosk\backup** for uninstall/restore
9. Apply base registry restrictions for kiosk user hive (no task manager, no ctrl-alt-del options subset, hide power options optional flag `-AllowPowerButton`)
10. Verification: service running, enrollment pending/online note, shell value set, print admin console next-steps URL

## uninstall.ps1
- Restore original Winlogon shell, remove restrictions, stop+delete service, remove files, optionally remove ProgramData (`-KeepData` switch), remove kiosk user optionally (`-RemoveUser`)

## Build script: `infrastructure/scripts/build-client-bundle.ps1`
- Publishes agent + runtime Release/win-x64 self-contained single-file with version stamping
- Assembles bundle dir + zip with `-Version` and `-OutDir`
- Does NOT bake in ServerUrl/token (per-deployment params)

## Docs
- `docs/CLIENT-DEPLOYMENT.md`: generate token → copy bundle → run setup → verify Online → assign policy → push content; rollback/uninstall; domain-join notes (GPO conflicts with Winlogon shell, fast user switching, kiosk user profile creation on first login); recovery instructions (Safe Mode / maintenance mode)

## Verification
- Run build script locally, inspect zip contents
- Syntax-check setup.ps1 (powershell -NoProfile -Command parse) — full install test requires admin + would alter this machine, so verify by dry-run `-WhatIf` mode (add to setup.ps1)
- Commit
