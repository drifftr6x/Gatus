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

## In progress / hardening

- [ ] Authenticate all agent endpoints with `deviceSecret` (heartbeat, telemetry, command poll)
- [ ] Content deploy E2E on a kiosk (upload → deploy → agent Succeeded → runtime navigate)
- [ ] Fix remaining `NU1903` package advisories
- [ ] `dotnet` on PATH / documented SDK path for all operators
- [ ] Production compose: API + web + TLS, not just data stores

## Later

- [ ] MFA, Entra ID / OIDC
- [ ] Signed agent updates and content manifests
- [ ] Deployment rings / maintenance windows
- [ ] Multi-tenancy
- [ ] Android / other OS agents
- [ ] Arbitrary remote desktop (explicitly out of MVP)

## Definition of done (product)

An operator can: install the server, log in, generate a token, enroll a PC, see it online, assign policy, lock it to a site/app, push content, send an allowlisted command, see health/alerts, and recover the PC if the kiosk software fails.
