# Changelog

## [Unreleased]

### Added

- Enrollment tokens (hashed, single-use, expiry, revoke) and `POST /api/devices/enroll`
- Admin UI to generate/copy enrollment tokens
- Windows agent enrollment CLI (`--enroll`), heartbeat, policy sync, deployments, commands, telemetry
- WebView2 kiosk runtime and lockdown engine
- Device groups, alerts, analytics, notification channels, log viewer
- Content versions, deployments, SHA-256 packages
- SignalR live dashboard, schedules, telemetry ingest

### Fixed

- Seed and login identity: `admin@gatus.local` / `editor@gatus.local`
- Vite proxy target aligned with API port **5163** (was 5000)
- Agent JSON enrollment deserialize (case-insensitive)
- Agent state directory fallback when ProgramData is not writable
- `enrollment_tokens.device_id` on fresh databases

### Security

- Anonymous agent routes remain for lab use; production must bind device secrets
- Known NuGet advisories (`Microsoft.OpenApi`, `System.Security.Cryptography.Xml`) still need package bumps
