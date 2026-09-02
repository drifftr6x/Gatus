# Kiosk Pilot Checklist — Windows 11 Pro (domain-joined)

Hands-on validation of the client bundle on one spare PC before any fleet rollout.
Target: ~45 minutes. You need: a spare Win 11 Pro domain-joined PC, local admin on it,
and access to the admin console.

## 0. Prep (5 min, at your desk)

- [ ] API is running and reachable from the pilot PC: `http://<server>:5163` (or the prod HTTPS origin)
- [ ] In the admin console: **Devices → Enroll a Device → Generate Token** → copy it (single-use, 24h expiry)
- [ ] Copy `dist\GatusKiosk-Bundle-1.0.0.zip` to the pilot PC (USB / network share)

## 1. Baseline (5 min)

- [ ] Note the PC name: `hostname`
- [ ] Confirm you can sign in with your domain admin account and reach the server:
      `curl http://<server>:5163/api/product` → expect 401 (auth required = alive)
- [ ] Create a restore point: `Checkpoint-Computer -Description "Pre-Gatus-Kiosk"` (elevated)

## 2. Dry run (3 min)

- [ ] Extract the zip, open **elevated** PowerShell in that folder
- [ ] `.\setup.ps1 -ServerUrl http://<server>:5163 -EnrollmentToken <token> -WhatIf`
- [ ] Read the output: confirms admin, OS edition, domain join, and every action it *would* take

## 3. Install (5 min)

- [ ] Real run: `.\setup.ps1 -ServerUrl http://<server>:5163 -EnrollmentToken <token>`
- [ ] Expect: files copied, `SentinelKioskAgent` service installed + started, token staged,
      first-logon task registered (kiosk profile hasn't loaded yet)
- [ ] Admin console → Devices: the PC should appear **Online** within ~60s

## 4. Management plane verification (10 min)

- [ ] Device detail page shows heartbeats (last seen ticking), CPU/mem/disk metrics
- [ ] Send a command: device detail → **Refresh** (or restart runtime) → watch it go Queued → Succeeded
- [ ] Deploy content: Content → upload a simple HTML page → Deploy → target this device →
      verify Succeeded (agent downloads, signature-verifies, stages, activates)
- [ ] Assign a kiosk policy: set home URL, lockdown profile → agent syncs within ~5 min

## 5. Kiosk mode (10 min)

- [ ] Sign out of your admin session; sign in as **KioskUser**
      (first sign-in: brief normal Explorer, the `Gatus-KioskShell` task applies the shell)
- [ ] Sign out, sign in again as KioskUser → fullscreen WebView2 shell with your content
- [ ] Try escapes: Ctrl+Alt+Del (Task Manager blocked), Alt+F4, right-click, downloads — all blocked
- [ ] Walk away: session timeout / inactivity reset per policy

## 6. Recovery validation (5 min) — do NOT skip

- [ ] From your admin account: `.\uninstall.ps1` → shell restored, service removed
- [ ] Re-run `setup.ps1` (needs a **new** enrollment token) → kiosk mode again
- [ ] This proves fleet recovery works before you depend on it

## 7. Sign-off

- [ ] Agent logs clean: `C:\ProgramData\SentinelKiosk\Logs\agent-*.log`
- [ ] No unexpected GPO conflicts on your domain (restriction policies applied only to KioskUser)
- [ ] Document anything surprising in the pilot notes below

## Pilot notes

| Date | Machine | Result | Issues found |
|------|---------|--------|--------------|
|      |         |        |              |

## If something goes wrong

| Symptom | Action |
|---|---|
| Device never shows Online | Check `Logs\agent-*.log`; verify ServerUrl reachable from the PC; token may be consumed/expired — generate a new one |
| KioskUser gets normal desktop | First sign-in loads the profile; sign out/in again. If still Explorer, check `HKU\<SID>\...\Winlogon\Shell` exists and scheduled task ran |
| Need desktop back on kiosk account | `uninstall.ps1` as admin, or Safe Mode |
| Service won't start | Event Viewer → Application log; run `SentinelKiosk.Agent.exe` manually in a console to see the error |
