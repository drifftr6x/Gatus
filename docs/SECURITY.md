# Security

## Implemented

- **Passwords**: BCrypt hashes; never stored plaintext
- **Sessions**: JWT access tokens + rotating refresh tokens (DB-backed)
- **RBAC policies**: `RequireViewer`, `RequireEditor`, `RequireAdmin` (roles Viewer, Editor, Admin, SuperAdmin)
- **Enrollment tokens**: cryptographically random, SHA-256 at rest, expiry, single-use, revoke
- **Device authentication**: per-device bearer secret, SHA-256 at rest, active-device and device-ID binding on agent routes
- **Content**: SHA-256 on packages + **RSA-4096 (PSS) signed manifests**; agent verifies signature against a DPAPI-pinned server key before activation
- **Agent self-updates**: same signed-manifest scheme for binaries; staged self-swap with automatic rollback on failed start
- **JWT secret**: startup validation — API refuses to boot outside Development with a missing/short (<32)/placeholder secret; not committed to the repo
- **Forced password change**: `MustChangePassword` on seeded/provisioned users; change endpoint invalidates all sessions; no hardcoded passwords in source
- **Agent secrets**: DPAPI (`ProtectedData`) on disk
- **Lockdown**: intended to be reversible; recovery scripts under ProgramData (do not ship a policy that cannot be undone)
- **Audit-ish logs**: Serilog file logs + optional `user-actions-*.json` viewed in the Logs page
- **CI**: `.github/workflows/security.yml` plus `ci.yml` / `deploy.yml`
- **Teams alerts**: See [TEAMS-ALERTS.md](TEAMS-ALERTS.md); webhook URLs are bearer secrets and must be rotated if exposed

## Not implemented (do not assume)

- MFA / Entra ID / SAML / LDAP
- Multi-tenant isolation
- Mutual TLS
- Code-signed agent binaries (Authenticode; the *payload* signing is already done via signed manifests)


Enrollment is the only anonymous agent operation. Heartbeat, telemetry, commands, policy, deployments, agent-update checks/downloads, and content downloads validate the issued per-device `deviceSecret` and requested device ID. Client certificates/mTLS remain future hardening.

## Secrets

- Never commit `.env`, production JWT keys, or enrollment plaintext
- JWT secret is **not in committed config** — dev uses `appsettings.Development.json`; production sets `Jwt__Secret` (compose requires it). Generate: `openssl rand -base64 48`
- Compose Postgres password is a **dev default**
- Dev seed admin password comes from `Seed:AdminPassword` (dev appsettings) or is randomly generated and logged — never hardcoded in source

## Threat notes (short)

| Threat | Current mitigation | Gap |
|--------|--------------------|-----|
| Stolen enrollment token | Hash, TTL, one-time | Token shown in UI clipboard |
| Agent impersonation | Per-device secret hash and device-ID binding | Client certificates/mTLS remain future hardening |
| Privilege escalation | Server-side role policies | UI hiding is not enough; always enforce on API |
| Malicious content | Checksum + staging + **RSA-4096 manifest signing** (agent verifies before activation) | Content zip format itself is not sandboxed on extract |
| Malicious agent update | Signed manifest + per-file SHA-256 + deterministic rollout gating + automatic rollback | Binaries not Authenticode-signed; staged-swap script runs as SYSTEM |
| Kiosk escape | WebView2 guards + optional OS lockdown | Depends on Windows edition and policy |
| Irreversible lockdown | Restore scripts, maintenance mode | Test recovery **before** production rollout |

## Vulnerability disclosures

Report issues privately to the platform owners. Dependency posture is clean (`dotnet list package --vulnerable` reports zero across all projects); re-run it after any package bump in `Directory.Packages.props`.
