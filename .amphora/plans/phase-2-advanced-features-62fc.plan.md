---
schema_version: 1
name: phase-2-advanced-features
overview: 'Implement Phase 2: real-time sync via SignalR, telemetry ingestion/dashboard, scheduling
  API + UI, and a live dashboard — plus a database seeder for development.'
todos:
  - id: p2t1
    content: Create Schedule DTOs + SchedulesController with conflict detection
    status: completed
  - id: p2t2
    content: Create SignalR DeviceHub + register in Program.cs
    status: completed
  - id: p2t3
    content: Create TelemetryController (ingest + query + summary)
    status: completed
  - id: p2t4
    content: Create DbSeeder for development data
    status: completed
  - id: p2t5
    content: 'Frontend: signalr client + useSignalR hook wiring into React Query'
    status: completed
  - id: p2t6
    content: 'Frontend: schedules page (replace Policies in nav)'
    status: completed
  - id: p2t7
    content: 'Frontend: live dashboard with real stats + telemetry sparklines'
    status: completed
  - id: p2t8
    content: Integration tests for schedules + telemetry + hub negotiation
    status: completed
  - id: p2t9
    content: 'Final verification: build/test/web build + commit'
    status: completed
isProject: false
created_at: '2026-08-27T07:29:50'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_227
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: ba9347d4e0052d7b
title: phase-2-advanced-features
---

# phase-2-advanced-features

_Implement Phase 2: real-time sync via SignalR, telemetry ingestion/dashboard, scheduling API + UI, and a live dashboard — plus a database seeder for development._

## Phase 2: Advanced Features

### Goals
1. **Scheduling** — SchedulesController (CRUD API) + React scheduling page (replace Policies stub)
2. **Real-time sync** — SignalR hub broadcasting device status/content changes; React hook for live updates
3. **Telemetry** — ingestion endpoint + query endpoints + dashboard charts
4. **Live Dashboard** — real stat cards (online devices, content items, active schedules, alerts)
5. **DB Seeder** — dev seed data (admin user, sample devices/content/schedules) so the app is usable immediately

### Backend

**Scheduling API** (`SchedulesController`):
- GET list (filter by device/content/date range), GET by id, POST, PUT, DELETE
- Conflict detection: overlapping schedules on same device
- DTOs: `CreateScheduleRequest`, `UpdateScheduleRequest`, `ScheduleDto`

**SignalR** (`DeviceHub`):
- Hub method `SendHeartbeat(deviceId)` → broadcasts status change to admins
- Server→client events: `DeviceStatusChanged`, `ContentUpdated`, `ScheduleChanged`
- Registered at `/hubs/devices`

**Telemetry** (`TelemetryController`):
- POST `/api/telemetry` (device ingestion, batched metrics)
- GET `/api/telemetry/device/{id}` (time-series query with range params)
- GET `/api/telemetry/summary` (aggregate stats for dashboard)

**Seeder** (`DbSeeder`):
- Development-only: default admin user, 3 sample devices, 4 content items, 2 schedules, telemetry points

### Frontend

- `lib/signalr.ts` — hub connection with auto-reconnect + token auth
- `useSignalR` hook → invalidates React Query caches on events (live UI updates)
- `pages/schedules.tsx` — full CRUD page (replaces Policies in nav)
- `pages/dashboard.tsx` — live stat cards + device status list fed by telemetry summary
- Charts: lightweight SVG sparklines (no new deps) for telemetry trends

### Files
- `src/Platform.Api/Controllers/SchedulesController.cs`, `TelemetryController.cs`
- `src/Platform.Api/Hubs/DeviceHub.cs`
- `src/Platform.Infrastructure/Persistence/DbSeeder.cs`
- `src/Platform.Contracts/Requests|Responses/Schedule*.cs`, `Telemetry*.cs`
- `apps/admin-web/src/lib/signalr.ts`, `hooks/useSignalR.ts`
- `apps/admin-web/src/pages/schedules.tsx`, updated `dashboard.tsx`, `app-shell.tsx`, `App.tsx`

### Verification
- [ ] dotnet build + all tests pass (add Schedule conflict tests)
- [ ] npm run build passes
- [ ] SignalR negotiates (integration test)
- [ ] Seeder populates dev DB
