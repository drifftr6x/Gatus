# API reference

Local base URL: `http://localhost:5163`

OpenAPI is served by the ASP.NET Core host (no Swashbuckle). Most routes require:

```http
Authorization: Bearer <accessToken>
```

Exceptions: `POST /api/auth/login`, `POST /api/auth/refresh`, and the **device-facing** routes — enroll, heartbeat, telemetry ingest, command poll/result, deployment poll/status, policy GET, content + agent-update downloads, and `GET /api/signing/public-key`. These are `AllowAnonymous` but authenticated by `DeviceAuthenticationService`: `Authorization: Bearer <deviceSecret>` + device-ID binding; revoked/mismatched credentials get 401/403 and the agent re-enrolls. `POST /api/devices/enroll` is the only fully anonymous agent operation.

## Auth

| Method | Path | Notes |
|--------|------|--------|
| POST | `/api/auth/login` | `{ email, password }` → access + refresh; response `user.mustChangePassword` |
| POST | `/api/auth/refresh` | Rotate refresh token |
| POST | `/api/auth/logout` | |
| POST | `/api/auth/register` | Admin-gated in practice |
| GET | `/api/auth/me` | Current user |
| POST | `/api/auth/change-password` | `{ currentPassword, newPassword }` (min 8) — clears `MustChangePassword`, invalidates all sessions |

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
| POST | `/api/deployments` | Optional `rings[]` (groupId + soakMinutes), `rolloutPercent`, `scheduledAt` |
| POST | `/api/deployments/{id}/cancel` |
| POST | `/api/deployments/{id}/rollback` | Redeploy previous content version |
| POST | `/api/deployments/{deploymentId}/status` | Agent result report (device-auth) |

Agent poll `GET /api/deployments?deviceId=&status=Pending` filters out deployments outside the target group's **maintenance window** (overnight-safe) and returns `blockedByWindow` info. Ring chains activate only after the parent completes + soak minutes with ≥80% success.

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
| CRUD | `/api/alerts/rules` | Includes `cooldownMinutes` + `escalationPolicyId` |

## Agent updates

| Method | Path | Who | Notes |
|--------|------|-----|-------|
| GET | `/api/agent-updates` | Editor | List |
| POST | `/api/agent-updates` | Editor | Multipart zip (`file`, `version`, `rolloutPercent`, `minVersion?`, `notes?`) → server hashes files, signs manifest, repackages; deactivates older |
| POST | `/api/agent-updates/{id}/activate` | Editor | Make this the offered update |
| POST | `/api/agent-updates/{id}/deactivate` | Editor | |
| DELETE | `/api/agent-updates/{id}` | Admin | Removes files too |
| GET | `/api/agent-updates/latest?deviceId=&currentVersion=` | Device | 204 if up to date; gated by newer version, `minVersion` floor, deterministic rollout bucket |
| GET | `/api/agent-updates/{id}/download?deviceId=` | Device | Signed package zip |

## Other

| Method | Path |
|--------|------|
| CRUD | `/api/schedules` |
| CRUD | `/api/users` |
| GET | `/api/logs` |
| CRUD | `/api/notification-channels` |
| POST | `/api/notification-channels/{id}/test` | Send a test message (Editor) |
| CRUD | `/api/escalation-policies` | Alert escalation policies + ordered steps |
| GET | `/api/signing/public-key` | Device/admin — RSA public key for manifest verification |
| GET/PUT | `/api/settings` |

## SignalR

Hub: `/hubs/devices` (JWT on the connection). UI invalidates React Query caches on device/content/schedule/deployment events.

## Errors

Many endpoints return `{ "error": "message" }` with 4xx. Auth failures are 401. Duplicate serial on create is 409.
