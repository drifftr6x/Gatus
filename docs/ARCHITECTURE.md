# Architecture

**Gatus** is the default experience over the existing modular monolith. One ASP.NET Core host, feature controllers, and shared EF Core schema preserve the full platform while keeping the default operator experience focused. Windows endpoints run a **service agent** plus an optional **interactive kiosk runtime**.

## System view

```
Administrator
      │
      ▼
 React admin (Vite)  ──proxy /api,/hubs──►  ASP.NET Core API  (:5163 local)
                                               │
                         ┌─────────────────────┼─────────────────────┐
                         ▼                     ▼                     ▼
                    PostgreSQL 16            SignalR            File storage
                    (source of truth)     /hubs/devices      (content packages)
                         ▲
                         │  REST (enroll, heartbeat, commands, deploy)
                         │
                   Windows Agent (Windows Service)
                         │  named pipe (policy / content-activated)
                         ▼
                   Kiosk Runtime (WPF + WebView2)
```

## Server modules (in-process)

| Area | Location | Responsibility |
|------|----------|----------------|
| Auth / RBAC | `AuthController`, `Platform.Security` | Login, refresh, roles |
| Devices | `DevicesController` | CRUD, enroll, heartbeat, policy, inventory |
| Enrollment | `EnrollmentTokensController` | One-time hashed tokens |
| Groups / templates | `DeviceGroupsController`, `DeviceConfigTemplatesController` | Fleet grouping |
| Content / deploy | `ContentController`, `DeploymentsController` | Versions, SHA-256, jobs |
| Schedules | `SchedulesController` | Device/content windows + overlap check |
| Commands | `CommandsController` | Allowlisted remote commands |
| Telemetry / analytics | `TelemetryController`, `AnalyticsController` | Ingest + reports |
| Alerts / notify | `AlertsController`, `NotificationChannelsController` | Rules, channels |
| Logs / settings | `LogsController`, `SettingsController` | Server logs, platform settings |

EF Core maps snake_case tables (`devices`, `enrollment_tokens`, `commands`, …) in `ApplicationDbContext`.

## Agent / server communication

| Path | Transport | Notes |
|------|-----------|--------|
| Enrollment | `POST /api/devices/enroll` | Anonymous; one-time token hash |
| Heartbeat | `POST /api/devices/{id}/heartbeat` | Marks Online, stores telemetry |
| Policy | `GET /api/devices/{id}/policy` | Agent caches locally |
| Commands | `GET /api/commands?deviceId=&status=Queued` | Agent poll |
| Command result | `POST /api/commands/{id}/result` | State machine |
| Deployments | `GET /api/deployments`, `POST …/status` | Signed download + verify + activate; maintenance-window filtered |
| Telemetry batch | `POST /api/telemetry` | Device-secret authenticated |
| Agent updates | `GET /api/agent-updates/latest`, `…/{id}/download` | Signed self-update packages, eligibility gating |
| Live UI | SignalR `/hubs/devices` | Device/content/schedule events |

All agent routes above (except enroll) require the per-device secret: `Authorization: Bearer <deviceSecret>` + device-ID binding via `DeviceAuthenticationService`. On 401/403 the agent enters its re-enrollment flow.

The agent continues with last-known-good policy and content if the API is unreachable. Telemetry and command results are best-effort while offline.

## Policy model

Device `policyJson` (camelCase) drives the kiosk runtime: home URL, allow/deny lists, session/inactivity timeouts, restart limits, lockdown profile. Assignment is per-device today; groups exist for targeting deployments and bulk actions, and carry **maintenance windows** (start/duration/days, overnight-safe) that gate when agents may pick up deployments.

## Content deployment

1. Admin uploads a file → `Content` + `ContentVersion` (checksum, storage path, **RSA-signed manifest**)
2. Admin creates a `Deployment` for devices or a group — optionally with **rings** (chained deployments with soak delays, 80% success gate), rollout %, or a schedule
3. `DeploymentSchedulerService` activates due deployments; ring N+1 fires after ring N completes + soak
4. Agent polls (window-filtered), downloads the version zip, verifies the **signature against the DPAPI-pinned server key** + per-file SHA-256, stages, activates, tells the kiosk runtime via named pipe
5. Agent reports `DeploymentResult` (Succeeded / Failed); `PreviousVersionId` enables one-click rollback

## Agent self-update

`publish-agent-update.ps1` builds a Release package; an admin uploads it via `POST /api/agent-updates` and the **server signs the manifest** (the build machine never holds the private key). Agents poll `/latest` hourly, verify signature + hashes, stage, then a detached `apply-update.ps1` stops the service, backs up binaries, swaps files, restarts — restoring the backup if the new version fails to start. Eligibility: strictly newer version, optional `minVersion` floor, deterministic rollout bucket.

## Command model

Allowlisted types only (refresh, restart runtime, reload policy, reboot, diagnostics, …). No generic remote shell. States: Queued → (Delivered/Acknowledged/Running) → Succeeded | Failed | TimedOut | Cancelled.

## Why not microservices yet

Clear controller and entity boundaries exist so identity, devices, content, and commands could split later. A single deployable API is the right size for tens to thousands of kiosks.
