# Security

## Implemented

- **Passwords**: BCrypt hashes; never stored plaintext
- **Sessions**: JWT access tokens + rotating refresh tokens (DB-backed)
- **RBAC policies**: `RequireViewer`, `RequireEditor`, `RequireAdmin` (roles Viewer, Editor, Admin, SuperAdmin)
- **Enrollment tokens**: cryptographically random, SHA-256 at rest, expiry, single-use, revoke
- **Device authentication**: per-device bearer secret, SHA-256 at rest, active-device and device-ID binding on agent routes
- **Content**: SHA-256 on packages; agent refuses mismatch
- **Agent secrets**: DPAPI (`ProtectedData`) on disk
- **Lockdown**: intended to be reversible; recovery scripts under ProgramData (do not ship a policy that cannot be undone)
- **Audit-ish logs**: Serilog file logs + optional `user-actions-*.json` viewed in the Logs page
- **CI**: `.github/workflows/security.yml` plus `ci.yml` / `deploy.yml`
- **Teams alerts**: See [TEAMS-ALERTS.md](TEAMS-ALERTS.md); webhook URLs are bearer secrets and must be rotated if exposed

## Not implemented (do not assume)

- MFA / Entra ID / SAML / LDAP
- Multi-tenant isolation
- Mutual TLS or signed agent payloads
- Hard device-secret validation on heartbeat/telemetry (several agent routes are `[AllowAnonymous]`)
- Production TLS termination in this repo’s Compose file (local API is HTTP :5163)
- Digitally signed agent installers

Enrollment is the only anonymous agent operation. Heartbeat, telemetry, commands, policy, deployments, and content downloads validate the issued per-device `deviceSecret` and requested device ID. Client certificates/mTLS remain future hardening.

## Secrets

- Never commit `.env`, production JWT keys, or enrollment plaintext
- Local JWT secret is in `apps/api-server/appsettings.json` — replace before any shared environment
- Compose Postgres password is a **dev default**

## Threat notes (short)

| Threat | Current mitigation | Gap |
|--------|--------------------|-----|
| Stolen enrollment token | Hash, TTL, one-time | Token shown in UI clipboard |
| Agent impersonation | Per-device secret hash and device-ID binding | Client certificates/mTLS remain future hardening |
| Privilege escalation | Server-side role policies | UI hiding is not enough; always enforce on API |
| Malicious content | Checksum + staging + **RSA-4096 manifest signing** (agent verifies before activation) | Agent binary self-updates unsigned (no self-updater yet) |
| Kiosk escape | WebView2 guards + optional OS lockdown | Depends on Windows edition and policy |
| Irreversible lockdown | Restore scripts, maintenance mode | Test recovery **before** production rollout |

## Vulnerability disclosures

Report issues privately to the platform owners. Dependency advisories (`NU1903` on some packages) should be triaged in `Directory.Packages.props`.
