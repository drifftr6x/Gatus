# Roadmap

## Done (lab-usable)

- [x] Repo foundation (.NET 10, React admin, Compose Postgres/Redis/MinIO, CI)
- [x] Auth, RBAC, EF Core, device/content/user CRUD
- [x] SignalR, telemetry, schedules, live dashboard
- [x] Content versions, deployments, SHA-256 storage
- [x] Windows agent (enroll, heartbeat, policy, deploy, commands, telemetry)
- [x] WebView2 kiosk runtime
- [x] Lockdown engine (reversible providers + maintenance mode)
- [x] Groups, alerts, analytics, notification channels, log viewer
- [x] Enrollment tokens + live enroll of a real Windows PC (heartbeat Online)
- [x] **Device-secret authentication on all agent endpoints** (heartbeat, telemetry, commands, deployments, policy, content downloads) with device-ID binding + re-enrollment on 401/403
- [x] **Content deploy E2E** on a real device (upload → deploy → agent Succeeded → runtime navigate via named pipe)
- [x] **RSA-4096 signed content manifests** (agent verifies against pinned server key before activation)
- [x] **Deployment rings + maintenance windows** (chained deployments with soak + 80% success gate; per-group deploy windows, overnight-safe)
- [x] **Alert lifecycle maturity**: per-alert notification cooldown, escalation policies (delay → notify/escalate), test-notification button
- [x] **Production compose stack**: API + web + Postgres + nginx TLS, internal networking
- [x] **JWT secret startup validation** + no hardcoded seed passwords; forced password change (`MustChangePassword`) for seeded/provisioned users
- [x] **Agent self-updater**: server-signed binary packages, eligibility gating (version/minVersion/rollout %), verified self-swap with automatic rollback
- [x] **Backup/restore procedures**: scripted `pg_dump` + AppData (incl. signing keys) with retention, verified restore drill, Task Scheduler registration, runbook

## In progress / hardening

- [x] Package advisories clean (`dotnet list package --vulnerable` = 0 across all 12 projects)
- [ ] `dotnet` on PATH / documented SDK path for all operators
- [x] Admin UI for agent updates (Settings → Agent Updates: upload/sign, activate, rollout %, delete)

## Later

- [ ] MFA, Entra ID / OIDC
- [ ] Off-site backup sync (S3/B2) — scripts already take `-OutDir` for synced folders
- [ ] Kiosk runtime (WPF) self-update channel
- [ ] Delta updates for agent/content packages
- [ ] Multi-tenancy
- [ ] Android / other OS agents
- [ ] Arbitrary remote desktop (explicitly out of MVP)
- [ ] WAL archiving / PITR for sub-daily RPO

## Definition of done (product)

An operator can: install the server, log in, generate a token, enroll a PC, see it online, assign policy, lock it to a site/app, push content, send an allowlisted command, see health/alerts, update the agent remotely, restore the server from backup, and recover the PC if the kiosk software fails.
