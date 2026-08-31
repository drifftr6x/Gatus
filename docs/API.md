# API reference

Local base URL: `http://localhost:5163`

OpenAPI is served by the ASP.NET Core host (no Swashbuckle). Most routes require:

```http
Authorization: Bearer <accessToken>
```

Exceptions: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/devices/enroll`, heartbeat, telemetry ingest, agent command poll/result, and some deployment poll/status routes (device-facing; tighten before internet exposure).

## Auth

| Method | Path | Notes |
|--------|------|--------|
| POST | `/api/auth/login` | `{ email, password }` → access + refresh |
| POST | `/api/auth/refresh` | Rotate refresh token |
| POST | `/api/auth/logout` | |
| POST | `/api/auth/register` | Admin-gated in practice |
| GET | `/api/auth/me` | Current user |

## Devices

| Method | Path |
|--------|------|
| GET | `/api/devices` |
| GET | `/api/devices/{id}` |
| POST | `/api/devices` |
| PUT | `/api/devices/{id}` |
| DELETE | `/api/devices/{id}` |
| POST | `/api/devices/enroll` |
| POST | `/api/devices/{id}/heartbeat` |
| GET/PUT | `/api/devices/{id}/policy` |
| POST | `/api/devices/{id}/commands` |
| POST | `/api/devices/import` |

Enrollment body (agent): `{ enrollmentToken, hostname, hardwareId, osInfo, publicKey }`.

Response: `{ deviceId, deviceSecret, serverUrl?, policyAssignment? }`.

## Enrollment tokens (admin)

| Method | Path |
|--------|------|
| GET | `/api/enrollmenttokens` |
| POST | `/api/enrollmenttokens` |
| POST | `/api/enrollmenttokens/{id}/revoke` |
| DELETE | `/api/enrollmenttokens/{id}` |

Create returns the **plaintext token once**. The database stores SHA-256 only. Tokens are single-use and expire (default 24h).

## Groups and templates

| Method | Path |
|--------|------|
| CRUD | `/api/devicegroups` |
| CRUD | `/api/deviceconfigtemplates` |

(Exact controller names follow `api/[controller]` unless noted.)

## Content and deployments

| Method | Path |
|--------|------|
| GET/POST | `/api/content` |
| GET/PUT/DELETE | `/api/content/{id}` |
| POST | `/api/content/upload` |
| GET | `/api/content/{contentId}/versions` |
| GET | `/api/content/{contentId}/versions/{version}/download` |
| GET | `/api/deployments` |
| POST | `/api/deployments` |
| POST | `/api/deployments/{id}/cancel` |
| POST | `/api/deployments/{deploymentId}/status` |

## Commands

| Method | Path | Who |
|--------|------|-----|
| GET | `/api/commands?deviceId=&status=` | Agent poll |
| POST | `/api/commands/{id}/result` | Agent |
| GET | `/api/commands/history` | Admin |
| POST | `/api/devices/{deviceId}/commands` | Admin |
| POST | `/api/commands/{id}/cancel` | Admin |

## Telemetry, analytics, alerts

| Method | Path |
|--------|------|
| POST | `/api/telemetry` |
| GET | `/api/telemetry/device/{id}` |
| GET | `/api/telemetry/summary` |
| GET | `/api/analytics/uptime` |
| GET | `/api/analytics/alert-trends` |
| GET | `/api/analytics/telemetry` |
| GET | `/api/analytics/device-health` |
| GET | `/api/analytics/connectivity` |
| GET | `/api/alerts` |
| POST | `/api/alerts/{id}/acknowledge` |
| POST | `/api/alerts/{id}/resolve` |
| CRUD | `/api/alerts/rules` |

## Other

| Method | Path |
|--------|------|
| CRUD | `/api/schedules` |
| CRUD | `/api/users` |
| GET | `/api/logs` |
| CRUD | `/api/notification-channels` |
| GET/PUT | `/api/settings` |

## SignalR

Hub: `/hubs/devices` (JWT on the connection). UI invalidates React Query caches on device/content/schedule/deployment events.

## Errors

Many endpoints return `{ "error": "message" }` with 4xx. Auth failures are 401. Duplicate serial on create is 409.
