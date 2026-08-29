---
schema_version: 1
name: phase-10-monitoring-alerts
overview: 'Add alerting on top of existing heartbeat/telemetry data: Alert + AlertRule entities,
  a server-side evaluator that raises alerts from device metrics and offline state,
  alert APIs, and an Alerts page in the admin console with acknowledge/resolve.'
todos:
  - id: p10t1
    content: Alert + AlertRule entities, EF configs, migration
    status: completed
  - id: p10t2
    content: Alert DTOs in Contracts
    status: completed
  - id: p10t3
    content: 'AlertEvaluatorService (background): evaluate rules, raise/dedupe/auto-resolve'
    status: completed
  - id: p10t4
    content: 'AlertsController: list/acknowledge/resolve/count + rules CRUD with RBAC'
    status: completed
  - id: p10t5
    content: Seed default alert rules; register evaluator in Program.cs; extend dashboard summary
    status: completed
  - id: p10t6
    content: 'Frontend: alertsApi client + AlertsPage (severity badges, filters, ack/resolve) +
  nav'
    status: completed
  - id: p10t7
    content: 'Frontend: dashboard Active Alerts card + recent alerts'
    status: completed
  - id: p10t8
    content: 'Live E2E: trigger alert (offline/low disk) and verify it appears; ack/resolve'
    status: completed
  - id: p10t9
    content: Build/test verification + commit
    status: completed
isProject: false
created_at: '2026-08-28T22:22:18'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_401
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 0dffcf45b7b4d755
title: phase-10-monitoring-alerts
---

# phase-10-monitoring-alerts

_Add alerting on top of existing heartbeat/telemetry data: Alert + AlertRule entities, a server-side evaluator that raises alerts from device metrics and offline state, alert APIs, and an Alerts page in the admin console with acknowledge/resolve._

## Phase 10: Monitoring & Alerts

### Research: data sources that already exist
- **Heartbeat** (every 30s): `cpuUsage`, `memoryUsage`, `diskFreePercent`, `kioskStatus`, uptime → stored in `Device.LastSeenAt` + Status
- **Telemetry** (5min batch): `disk_free_mb`, `disk_total_mb`, `uptime`, `cpu_count` → `device_telemetry` table
- **Device status**: Online/Offline/Maintenance/Error
- **No alerts exist** — no Alert/AlertRule entity, no evaluator, no UI

### What to build

**Backend:**
1. `Alert` entity: deviceId, severity (Info/Warning/Critical), title, message, status (Active/Acknowledged/Resolved), raisedAt, acknowledgedAt/ById, resolvedAt — EF config + migration
2. `AlertRule` entity: metric (cpu/memory/disk/offline), operator (>/</equals), threshold, severity, enabled — seeded defaults (disk<10% Critical, memory>90% Warning, cpu>95% Warning, offline>5min Critical)
3. `AlertEvaluatorService` (BackgroundService): every 30s evaluates latest heartbeat/device state against enabled rules; raises new alert or keeps existing Active alert (dedupe — one active alert per device+rule); auto-resolves when condition clears
4. `AlertsController`: GET list (filter severity/status/device), POST {id}/acknowledge, POST {id}/resolve, GET count for dashboard badge; GET/POST/DELETE rules (RequireEditor for rule management)
5. Wire into telemetry/heartbeat path OR standalone evaluator (standalone = simpler, uses Device.Status + LastSeenAt + latest telemetry)
6. Dashboard: add "Active Alerts" count card (uses existing summary endpoint — extend it)

**Frontend:**
7. alertsApi client + types
8. AlertsPage: table (severity badge, device, title, time, status), filter by severity/status, acknowledge/resolve buttons; add to nav under Dashboard
9. Dashboard: Active Alerts stat card + recent alerts list; SignalR invalidation on alert events

### Acceptance criteria
- [ ] Default rules seeded (disk low, mem high, cpu high, offline)
- [ ] Evaluator raises alert when threshold crossed, dedupes, auto-resolves on recovery
- [ ] Admin acknowledges/resolves alerts in UI
- [ ] Alerts page shows severity badges + filters
- [ ] Dashboard shows active alert count
- [ ] Live test: take a device offline / set low disk → alert appears in UI
- [ ] Build + tests pass + commit
