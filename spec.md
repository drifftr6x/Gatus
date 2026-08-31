# GatUs Kiosk Platform — Technical Specification

**Version:** 0.9.0-dev
**Last Updated:** 2026-08-31
**Repository:** `GatUs` — Living Spaces Enterprise Kiosk Management

---

## 1. Overview

GatUs is an enterprise kiosk management platform for Living Spaces retail stores. It provides centralized management of Windows-based kiosk devices across multiple store locations, including device monitoring, content deployment, scheduling, alerting, and lockdown management.

### Architecture

```
┌─────────────────────┐     ┌──────────────────────┐
│   Admin Web Console  │────▶│   API Server (5163)  │
│   React 19 + Vite    │     │   ASP.NET Core 10    │
│   Port 5175          │     │                      │
└─────────────────────┘     └──────────┬───────────┘
                                       │
                            ┌──────────▼───────────┐
                            │   PostgreSQL 16       │
                            │   Docker container    │
                            └──────────────────────┘
                                       │
                    ┌──────────────────┼──────────────────┐
                    │                  │                  │
          ┌─────────▼──────┐  ┌───────▼───────┐  ┌──────▼────────┐
          │ Windows Agent  │  │ Kiosk Runtime │  │ Ping Monitor  │
          │ (Service)      │  │ (WPF/WebView2)│  │ (Background)  │
          │ .NET 10        │  │ .NET 10       │  │               │
          └────────────────┘  └───────────────┘  └───────────────┘
```

### Projects

| Project | Path | Type | Description |
|---------|------|------|-------------|
| **Platform.ApiServer** | `apps/api-server/` | ASP.NET Core 10 | Main API host |
| **Platform.Api** | `src/Platform.Api/` | Class library | Controllers, services, SignalR hubs |
| **Platform.Domain** | `src/Platform.Domain/` | Class library | Entity models, enums |
| **Platform.Infrastructure** | `src/Platform.Infrastructure/` | Class library | EF Core, DbContext, configurations |
| **Platform.Contracts** | `src/Platform.Contracts/` | Class library | DTOs, request/response records |
| **Platform.Security** | `src/Platform.Security/` | Class library | JWT auth, token service, policies |
| **Platform.Application** | `src/Platform.Application/` | Class library | (Reserved for application services) |
| **Platform.Shared** | `src/Platform.Shared/` | Class library | (Shared utilities) |
| **admin-web** | `apps/admin-web/` | React 19 + Vite | Admin console SPA |
| **SentinelKiosk.Agent** | `agents/windows-agent/` | .NET 10 Windows Service | Device agent |
| **SentinelKiosk.Runtime** | `agents/windows-kiosk-runtime/` | WPF + WebView2 | Kiosk display runtime |

---

## 2. Domain Model

### 2.1 Core Entities

#### Device
Represents a physical kiosk machine.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Name | string (200) | Device name (e.g., PCS3DR007001) |
| SerialNumber | string? (100) | Hardware serial number |
| Description | string? (1000) | Free-text description |
| Location | string? (500) | Physical location |
| Status | DeviceStatus | Offline, Online, Maintenance, Error |
| LastSeenAt | DateTime? | Last heartbeat or ping |
| IpAddress | string? (50) | IPv4 address |
| Hostname | string? (200) | FQDN hostname |
| MacAddress | string? (20) | MAC address |
| FirmwareVersion | string? (100) | Firmware version |
| GroupId | Guid? (FK) | Device group assignment |
| Tags | string? | JSON array of tag strings |
| Latitude | double? | Geo coordinate (auto-geocoded) |
| Longitude | double? | Geo coordinate (auto-geocoded) |
| CreatedAt | DateTime | Creation timestamp |
| UpdatedAt | DateTime? | Last update timestamp |
| IsActive | bool | Soft delete flag |
| DeviceSecretHash | string? (200) | SHA-256 of device secret |
| EnrolledAt | DateTime? | Agent enrollment timestamp |

#### DeviceGroup
Logical grouping of devices (typically per store location).

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Name | string (200) | Group name (e.g., "Store 42 - Buford, GA") |
| Description | string? (1000) | Optional description |
| CreatedAt | DateTime | Creation timestamp |
| UpdatedAt | DateTime? | Last update |
| IsActive | bool | Soft delete flag |

#### DeviceTelemetry
Time-series metric data points from agent heartbeats.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| DeviceId | Guid (FK) | Owning device |
| Timestamp | DateTime | Metric timestamp |
| MetricName | string? (100) | Metric name (cpu_usage, memory_usage, etc.) |
| MetricValue | string? (500) | Metric value (string-typed) |
| Unit | string? (20) | Unit of measure |

**Metric names:** `cpu_usage`, `memory_usage`, `disk_free_percent`, `disk_free_mb`, `disk_total_mb`, `memory_available_mb`, `uptime_seconds`, `uptime`, `cpu_count`, `os_version`, `agent_version`

#### DeviceConnectivity
Connectivity snapshot recorded each ping cycle or agent heartbeat.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| DeviceId | Guid (FK) | Owning device |
| Timestamp | DateTime | Snapshot timestamp |
| IsOnline | bool | Whether device was reachable |
| ResponseTimeMs | int? | Ping response time |
| Source | string (20) | "ping" or "agent" |

#### DeviceCommand
Commands sent to devices from the admin console.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| DeviceId | Guid (FK) | Target device |
| Type | CommandType | RestartKiosk, RebootDevice, SyncPolicy, RefreshContent, CollectLogs, RunScript |
| Payload | string? (2000) | Command payload (JSON) |
| Status | CommandStatus | Pending, Sent, Acknowledged, Completed, Failed, Expired, Cancelled |
| CreatedById | Guid (FK) | User who issued the command |
| CreatedAt | DateTime | Creation time |
| ExpiresAt | DateTime | Expiration (default 15 min) |
| AcknowledgedAt | DateTime? | Agent acknowledged |
| CompletedAt | DateTime? | Agent completed |
| ResultMessage | string? (2000) | Result output |

#### Alert
Alert instances triggered by rules.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| DeviceId | Guid (FK) | Affected device |
| Severity | AlertSeverity | Info, Warning, Critical |
| Title | string (200) | Alert title |
| Message | string? (2000) | Alert detail |
| Status | AlertStatus | Active, Acknowledged, Resolved, Suppressed |
| RaisedAt | DateTime | When triggered |
| AcknowledgedAt/ById | DateTime?/Guid? | Acknowledgment |
| ResolvedAt | DateTime? | Resolution time |
| AutoResolved | bool | Auto-resolved flag |

#### AlertRule
Configurable alert rules evaluated against telemetry.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Name | string (200) | Rule name |
| MetricName | string (100) | Metric to monitor |
| Condition | AlertCondition | GreaterThan, LessThan, Equals, NotEquals, Offline |
| Threshold | double | Threshold value |
| DurationMinutes | int | Duration before triggering |
| Severity | AlertSeverity | Alert severity |
| GroupId | Guid? (FK) | Scope to group (null = all) |
| DeviceId | Guid? (FK) | Scope to device (null = all) |
| CooldownMinutes | int | Cooldown before re-triggering |
| IsActive | bool | Enable/disable |

#### AlertNotification
Tracks which alerts were sent to which channels.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| AlertId | Guid (FK) | Related alert |
| ChannelId | Guid (FK) | Notification channel |
| SentAt | DateTime | Send timestamp |
| Success | bool | Delivery success |
| ErrorMessage | string? (1000) | Failure reason |
| RetryCount | int | Retry attempts |

#### NotificationChannel
Configured notification destinations (webhooks, Slack, Teams).

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Name | string (200) | Channel name |
| Type | ChannelType | Webhook, Slack, Teams, Email |
| EndpointUrl | string (500) | Destination URL |
| IsEnabled | bool | Enable/disable |
| MinSeverity | AlertSeverity | Minimum severity to notify |
| CreatedAt | DateTime | Creation time |
| UpdatedAt | DateTime? | Last update |

#### Schedule
Content display schedules for devices.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Name | string (200) | Schedule name |
| ContentId | Guid (FK) | Content to display |
| StartTime | TimeSpan | Daily start time |
| EndTime | TimeSpan | Daily end time |
| DaysOfWeek | DayOfWeekFlags | Active days (bitflags) |
| Priority | int | Priority (higher wins conflicts) |
| IsActive | bool | Enable/disable |

#### User
Admin console users.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Email | string (256) | Login email (unique) |
| DisplayName | string (200) | Display name |
| PasswordHash | string (500) | BCrypt password hash |
| Role | UserRole | Viewer, Editor, Admin, SuperAdmin |
| IsActive | bool | Account enabled |
| LastLoginAt | DateTime? | Last login |
| CreatedAt | DateTime | Creation time |

#### RefreshToken
JWT refresh tokens for session management.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid (FK) | Owning user |
| TokenHash | string (500) | SHA-256 of token |
| ExpiresAt | DateTime | Expiration |
| CreatedAt | DateTime | Issue time |
| RevokedAt | DateTime? | Revocation time |
| ReplacedByTokenId | Guid? | Token rotation chain |

### 2.2 Content Pipeline Entities

#### Content
Media/content items for kiosk display.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Name | string (200) | Content name |
| Description | string? (1000) | Description |
| Type | ContentType | Image, Video, Html, Pdf, Url |
| Url | string? (2000) | External URL (for Url type) |
| ThumbnailUrl | string? (2000) | Thumbnail URL |
| FileSizeBytes | long | File size |
| DurationSeconds | int? | Display duration |
| MimeType | string? (100) | MIME type |
| IsActive | bool | Enable/disable |

#### ContentVersion
Immutable versioned content packages.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| ContentId | Guid (FK) | Parent content |
| Version | int | Version number (auto-increment) |
| PackagePath | string (500) | Storage path for zip package |
| Sha256Checksum | string (64) | Package checksum |
| FileSizeBytes | long | Package size |
| MimeType | string? (100) | Content MIME type |
| ReleaseNotes | string? (2000) | Version notes |
| IsActive | bool | Active version flag |

#### Deployment
Content deployment to devices/groups.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Name | string (200) | Deployment name |
| Description | string? (1000) | Description |
| ContentVersionId | Guid (FK) | Version to deploy |
| Status | DeploymentStatus | Pending, InProgress, Completed, PartiallyCompleted, Failed, Cancelled |
| ScheduledAt | DateTime? | Scheduled time |
| StartedAt | DateTime? | Start time |
| CompletedAt | DateTime? | Completion time |
| CreatedById | Guid? (FK) | Creator user |

#### DeploymentResult
Per-device result within a deployment.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| DeploymentId | Guid (FK) | Parent deployment |
| DeviceId | Guid (FK) | Target device |
| Status | DeploymentResultStatus | Pending, Downloading, Verifying, Staging, Activating, Completed, Failed, RolledBack |
| StartedAt | DateTime? | Start time |
| CompletedAt | DateTime? | Completion time |
| ErrorMessage | string? (2000) | Failure reason |
| RetryCount | int | Retry attempts |
| RollbackPerformed | bool | Rollback flag |

### 2.3 Enrollment Entity

#### EnrollmentToken
One-time tokens for device enrollment.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| Token | string (100) | Enrollment token string |
| DeviceId | Guid? (FK) | Pre-assigned device (optional) |
| DeviceName | string? (200) | Device name hint |
| ExpiresAt | DateTime | Token expiration |
| IsUsed | bool | Whether token was used |
| UsedAt | DateTime? | Usage timestamp |

---

## 3. API Endpoints

All routes are prefixed with `/api`. Auth policies: `RequireAdmin` (Admin+SuperAdmin), `RequireEditor` (Editor+), `RequireViewer` (Viewer+), `AllowAnonymous` (no JWT).

### 3.1 Authentication (`/api/auth`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/login` | Anonymous | Login with email/password, returns JWT + refresh token |
| POST | `/refresh` | Anonymous | Refresh JWT using refresh token |
| POST | `/logout` | Authenticated | Revoke refresh token |
| GET | `/me` | Authenticated | Get current user profile |

### 3.2 Devices (`/api/devices`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Viewer | List devices (paginated, filter by status/group/search) |
| GET | `/{id}` | Viewer | Get device detail with latest telemetry |
| POST | `/` | Editor | Create device (auto-geocodes from group) |
| PUT | `/{id}` | Editor | Update device (auto-geocodes on group change) |
| DELETE | `/{id}` | Admin | Soft-delete device |
| POST | `/{id}/heartbeat` | Device auth | Agent heartbeat with metrics |
| POST | `/bulk/group` | Editor | Bulk assign devices to group (auto-geocodes) |
| POST | `/import` | Editor | Excel/CSV bulk import (auto-geocodes, auto-creates groups) |
| POST | `/enroll` | Anonymous | Enroll device with token, returns device secret |
| GET | `/enrollment-tokens` | Editor | List enrollment tokens |
| POST | `/enrollment-tokens` | Editor | Generate new enrollment token |
| DELETE | `/enrollment-tokens/{id}` | Editor | Revoke enrollment token |

### 3.3 Device Groups (`/api/devicegroups`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Viewer | List all groups with device counts |
| GET | `/{id}` | Viewer | Get group detail |
| POST | `/` | Editor | Create group |
| PUT | `/{id}` | Editor | Update group |
| DELETE | `/{id}` | Editor | Delete group (devices unassigned) |

### 3.4 Content (`/api/content`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Viewer | List content items (paginated) |
| GET | `/{id}` | Viewer | Get content detail |
| POST | `/` | Editor | Create content (URL-based) |
| PUT | `/{id}` | Editor | Update content metadata |
| DELETE | `/{id}` | Admin | Delete content |
| POST | `/upload` | Editor | Upload file (creates versioned zip package) |
| GET | `/{id}/versions` | Viewer | List content versions |
| GET | `/{id}/versions/{version}/download` | Anonymous | Download version package (device secret auth) |

### 3.5 Deployments (`/api/deployments`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Viewer (admin) / Anonymous (agent poll with `?deviceId=&status=Pending`) | List deployments |
| POST | `/` | Editor | Create deployment (by deviceIds or groupId) |
| POST | `/{id}/status` | Anonymous | Agent reports deployment status |
| POST | `/{id}/cancel` | Editor | Cancel pending deployment |

### 3.6 Telemetry (`/api/telemetry`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/` | Anonymous | Ingest telemetry batch from agent |
| GET | `/device/{deviceId}` | Viewer | Time-series telemetry for device |
| GET | `/summary` | Viewer | Fleet-wide summary (totals, online, errors) |

### 3.7 Alerts (`/api/alerts`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Viewer | List alerts (filter by severity/status/device) |
| GET | `/count` | Viewer | Alert counts (active, critical) |
| POST | `/{id}/acknowledge` | Editor | Acknowledge alert |
| POST | `/{id}/resolve` | Editor | Resolve alert |
| GET | `/rules` | Viewer | List alert rules |
| POST | `/rules` | Editor | Create alert rule |
| PUT | `/rules/{id}` | Editor | Update alert rule |

### 3.8 Schedules (`/api/schedules`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Viewer | List schedules |
| POST | `/` | Editor | Create schedule (with conflict detection) |
| PUT | `/{id}` | Editor | Update schedule |
| DELETE | `/{id}` | Editor | Delete schedule |
| GET | `/device/{deviceId}` | Viewer | Schedules for a device |

### 3.9 Commands (`/api/commands`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Viewer | List commands (filter by device/status) |
| POST | `/` | Editor | Send command to device(s) |
| POST | `/{id}/acknowledge` | Anonymous | Agent acknowledges command |
| POST | `/{id}/complete` | Anonymous | Agent reports command result |

### 3.10 Analytics (`/api/analytics`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/uptime` | Viewer | Per-device uptime percentage |
| GET | `/alert-trends` | Viewer | Alert volume over time |
| GET | `/telemetry` | Viewer | Aggregated telemetry metrics |
| GET | `/device-health` | Viewer | Device health summaries |
| GET | `/connectivity` | Viewer | Time-bucketed online/offline per device |

### 3.11 Notifications (`/api/notificationchannels`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Admin | List notification channels |
| POST | `/` | Admin | Create channel |
| PUT | `/{id}` | Admin | Update channel |
| DELETE | `/{id}` | Admin | Delete channel |
| POST | `/{id}/test` | Admin | Test channel delivery |

### 3.12 Logs (`/api/logs`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Viewer | Query log entries (level, search, time range, source) |
| GET | `/levels` | Viewer | Available log levels |

### 3.13 Users (`/api/users`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Admin | List users |
| GET | `/{id}` | Admin | Get user detail |
| POST | `/` | Admin | Create user |
| PUT | `/{id}` | Admin | Update user |
| PUT | `/{id}/role` | Admin | Change user role |
| DELETE | `/{id}` | Admin | Deactivate user |

---

## 4. Real-Time (SignalR)

### Hub: `/hubs/devices`

**Server → Client broadcasts:**
- `DeviceStatusChanged` — device online/offline/status change
- `AlertTriggered` — new alert fired
- `TelemetryReceived` — new telemetry data point

**Authentication:** JWT token via query string or header.

---

## 5. Services

### 5.1 Server-Side Services

| Service | Type | Description |
|---------|------|-------------|
| **PingMonitorService** | Background (60s cycle) | Pings all non-agent devices, updates status, records connectivity snapshots, requires 2 consecutive failures before marking offline |
| **AlertEvaluatorService** | Background (30s cycle) | Evaluates alert rules against telemetry, creates/resolves alerts, broadcasts via SignalR |
| **NotificationService** | Singleton | Sends alert notifications to configured channels (webhook, Slack, Teams) |
| **CommandService** | Scoped | Issues commands to devices, tracks status |
| **ContentStorageService** | Singleton | Zip packaging, SHA-256 checksums, manifest.json generation, path-traversal guard |
| **GeocodingService** | Singleton | Nominatim geocoding with in-memory cache, auto-assigns lat/lng from group names |
| **DeviceEventBroadcaster** | Scoped | SignalR broadcast wrapper for device events |

### 5.2 Agent Services (Windows Service)

| Service | Interval | Description |
|---------|----------|-------------|
| **EnrollmentService** | On-demand | Token exchange, DPAPI credential storage |
| **HeartbeatService** | 30s | Collects system metrics (CPU, memory, disk, uptime), sends to server |
| **PolicySyncService** | 300s | Syncs policies from server, local cache, drift detection |
| **DeploymentService** | 60s | Polls for pending deployments, downloads/verifies/stages/activates content, reports status |
| **CommandExecutor** | 15s | Polls for pending commands, executes allowlisted commands |
| **TelemetryCollector** | 300s | Batches telemetry locally, uploads in bulk |
| **LocalStateManager** | — | Manages config, content, logs on local filesystem |

### 5.3 Kiosk Runtime Services (WPF)

| Service | Description |
|---------|-------------|
| **NavigationGuard** | URL allowlist/denylist enforcement for WebView2 |
| **SessionManager** | Session timeout + inactivity reset |
| **CrashMonitor** | Auto-restart with exponential backoff |
| **PolicyReceiver** | Receives policy updates from agent |

---

## 6. Admin Console Pages

| Page | Route | Description |
|------|-------|-------------|
| **Dashboard** | `/dashboard` | Fleet summary cards, device map (Leaflet), connectivity charts, recent deployments, device health grid, recent alerts |
| **Devices** | `/devices` | Device table with filters (group, status, search), CRUD, bulk actions, enrollment token modal, import modal |
| **Device Detail** | `/devices/:id` | Device info, telemetry charts, command history, alert history |
| **Groups** | `/groups` | Group cards with sort/filter, expandable device lists, device picker modal |
| **Content** | `/content` | Content cards, file upload, deploy modal (by group or device) |
| **Schedules** | `/schedules` | Schedule CRUD with time/day pickers |
| **Alerts** | `/alerts` | Alert list with acknowledge/resolve, alert rules management |
| **Analytics** | `/analytics` | Uptime reports, alert trends, telemetry aggregation charts |
| **Notifications** | `/notifications` | Notification channel management |
| **Logs** | `/logs` | Server logs + user action audit trail, filterable |
| **Settings** | `/settings` | Application settings |

### Components

| Component | Description |
|-----------|-------------|
| `DeviceMap` | Leaflet map with dark CSS filter, OSM tiles, device pins colored by status |
| `ConnectivityChart` | Per-group expandable uptime bar charts (green/red/gray time slots) |
| `AppShell` | Layout with sidebar nav, top bar (SignalR status, user, theme, sign out) |
| `ThemePicker` | Dark/light mode + accent color selection |
| `ProtectedRoute` | Auth guard wrapper |

---

## 7. Security

### Authentication
- **JWT** (HS256) with 60-minute expiry
- **Refresh tokens** with rotation (7-day expiry, stored hashed)
- **Device secrets** — DPAPI-encrypted on agent, SHA-256 hashed on server

### Authorization Policies
| Policy | Roles |
|--------|-------|
| RequireViewer | Viewer, Editor, Admin, SuperAdmin |
| RequireEditor | Editor, Admin, SuperAdmin |
| RequireAdmin | Admin, SuperAdmin |

### Device Authentication
- Agent endpoints accept `X-Device-Id` + `X-Device-Secret` headers (AllowAnonymous with custom validation)
- Device secrets are 32-byte random strings, SHA-256 hashed in DB

### Logging & Audit
- **Structured JSON logs** (Serilog Compact JSON) — `logs/log-YYYYMMDD.json`
- **User action audit** — `logs/user-actions-YYYYMMDD.json` (all mutations + important reads)
- **Correlation IDs** — `X-Correlation-Id` header propagated through all requests
- Log retention: 30 days (server), 90 days (audit)

---

## 8. Database

**Engine:** PostgreSQL 16 (Docker container `kiosk-postgres`)
**ORM:** EF Core 10 with Npgsql
**Naming:** snake_case columns, plural table names

### Tables (17)
`alert_notifications`, `alert_rules`, `alerts`, `commands`, `content`, `content_versions`, `deployment_results`, `deployments`, `device_connectivity`, `device_groups`, `device_telemetry`, `devices`, `enrollment_tokens`, `notification_channels`, `refresh_tokens`, `schedules`, `users`

---

## 9. Auto-Geocoding

Devices automatically receive latitude/longitude coordinates based on their group name:

1. Group name follows pattern: `"Store {NN} - {City}, {ST}"`
2. The part after `" - "` is extracted (e.g., `"Buford, GA"`)
3. Geocoded via Nominatim (OpenStreetMap) API
4. Results cached in-memory (no repeated API calls for same city)
5. Applied on: device create, device update (group change), bulk group assign, Excel import
6. Skipped if device already has explicit coordinates

---

## 10. Content Deployment Flow

```
Admin uploads file → Zip package created → SHA-256 checksum → ContentVersion stored
      ↓
Admin creates Deployment (devices or group) → Status: Pending
      ↓
Agent polls GET /api/deployments?deviceId=X&status=Pending (every 60s)
      ↓
Agent downloads package → verifies SHA-256 → extracts to Content/{versionId}/
      ↓
Agent reports status POST /api/deployments/{id}/status
      ↓
Server rolls up per-device results → Deployment status: Completed/Failed
      ↓
Dashboard shows progress bars + status badges
```

---

## 11. Connectivity Monitoring

```
PingMonitorService (60s cycle)
  ├─ For each active device (non-agent):
  │    ├─ Ping IP (fallback: hostname)
  │    ├─ 2 consecutive failures → mark Offline
  │    └─ Record DeviceConnectivity snapshot (is_online, timestamp)
  │
Agent Heartbeat (30s cycle)
  └─ Record DeviceConnectivity snapshot (source: "agent")
```

The dashboard connectivity chart renders time-bucketed online/offline bars per device, grouped by device group, with configurable time ranges (6h/24h/3d/7d).
