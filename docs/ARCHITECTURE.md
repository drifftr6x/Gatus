# Architecture

**EdgeWatch Lite** is the focused default experience over the existing modular monolith. One ASP.NET Core host, feature controllers, and shared EF Core schema preserve the full advanced platform without forcing Lite users through every module. Windows endpoints run a **service agent** plus an optional **interactive kiosk runtime**.

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
| Deployments | `GET /api/deployments`, `POST …/status` | Download + SHA-256 + activate |
| Telemetry batch | `POST /api/telemetry` | AllowAnonymous today (device secret later) |
| Live UI | SignalR `/hubs/devices` | Device/content/schedule events |

The agent continues with last-known-good policy and content if the API is unreachable. Telemetry and command results are best-effort while offline.

## Policy model

Device `policyJson` (camelCase) drives the kiosk runtime: home URL, allow/deny lists, session/inactivity timeouts, restart limits, lockdown profile. Assignment is per-device today; groups exist for targeting deployments and bulk actions.

## Content deployment

1. Admin uploads a file → `Content` + `ContentVersion` (checksum, storage path)
2. Admin creates a `Deployment` for devices or a group
3. Agent polls, downloads the version zip, verifies SHA-256, stages, activates
4. Agent reports `DeploymentResult` (Succeeded / Failed / rollback)

## Command model

Allowlisted types only (refresh, restart runtime, reload policy, reboot, diagnostics, …). No generic remote shell. States: Queued → (Delivered/Acknowledged/Running) → Succeeded | Failed | TimedOut | Cancelled.

## Why not microservices yet

Clear controller and entity boundaries exist so identity, devices, content, and commands could split later. A single deployable API is the right size for tens to thousands of kiosks.
