---
schema_version: 1
name: Deployment rings + maintenance windows
overview: Add named deployment rings (pilot → broad) as device targeting with staged auto-advance,
  plus maintenance windows that gate when deployments activate on devices.
todos:
  - id: rg-t1
    content: 'Entities + EF configs + migration: ring fields on Deployment, window fields on DeviceGroup'
    status: completed
  - id: rg-t2
    content: CreateDeploymentRequest rings support → chained deployments with parent links
    status: completed
  - id: rg-t3
    content: 'Scheduler: ring soak activation + 80% success gate'
    status: completed
  - id: rg-t4
    content: 'Agent poll endpoint: maintenance window filtering + blockedByWindow in list DTO'
    status: completed
  - id: rg-t5
    content: 'Groups API: maintenance window CRUD fields'
    status: completed
  - id: rg-t6
    content: 'Frontend: rings tab in deploy modal + window editor on groups page + ring badges'
    status: completed
  - id: rg-t7
    content: Integration tests + build/test/web verify + commit
    status: in_progress
isProject: false
created_at: '2026-09-01T23:36:01'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_505
model: FW-Kimi-K3
mode_at_creation: auto
approved_mode: auto
content_hash: bf000abdbd311fa5
approved_hash: bf000abdbd311fa5
files_referenced:
  - src/Platform.Domain/Entities/ContentVersion.cs
  - src/Platform.Domain/Entities/DeviceGroup.cs
  - src/Platform.Api/Controllers/DeploymentsController.cs
  - src/Platform.Api/Services/DeploymentSchedulerService.cs
  - apps/admin-web/src/pages/content.tsx
  - apps/admin-web/src/pages/groups.tsx
title: Deployment rings + maintenance windows
---

# Deployment rings + maintenance windows

_Add named deployment rings (pilot → broad) as device targeting with staged auto-advance, plus maintenance windows that gate when deployments activate on devices._

## Context
Deployments already support: group/device targeting, `ScheduledAt` (activation time), `RolloutPercent` (doubling waves with 80% success gate). What's missing:
- **Rings**: no way to say "deploy to Pilot group first, then Broad after soak" as one operation — you create separate deployments manually.
- **Maintenance windows**: deployments activate the moment the agent polls, regardless of time of day. Production kiosks need updates constrained to off-hours windows.

## Design

### Rings
- `RingId` (nullable FK to `device_groups`) on `Deployment` — but simpler and more powerful: a **ring chain**. Add `Deployment.NextDeploymentId`? Too coupled. Better:
- **`DeploymentRing` entity**: Name, Order, SoakMinutes (time after previous ring completes before next starts), GroupId (which device group is in this ring)
- Deployment gains `RingStrategy`: when creating with `rings: [{groupId, soakMinutes}, ...]`, the server creates one `Deployment` per ring, chained: ring N+1 stays `Scheduled` until ring N completes + soak elapses.
- Implementation: store `ParentDeploymentId` + `RingOrder` on Deployment; `DeploymentSchedulerService` checks parent completion + soak before activating child.

### Maintenance windows
- `MaintenanceWindow` fields on `DeviceGroup` (simpler than per-device): `WindowStartTime` (TimeOnly, e.g. 02:00), `WindowDurationMinutes`, `WindowDays` (bitmask or CSV: "Mon,Tue,..."). Nullable = no restriction.
- Enforcement point: **agent poll endpoint** (`GET /api/deployments?deviceId=&status=Queued`) — server only returns deployments whose target group's window is currently open. Deployments stay Queued until window opens.
- Dashboard/API: deployments blocked by window show a `WaitingForWindow` indicator (computed, not stored — keep the status model clean; expose `blockedByWindow: true` in the list DTO).

## Server steps
1. Entities: `RingOrder`/`ParentDeploymentId`/`SoakMinutes` on Deployment; window fields on DeviceGroup; EF configs + migration
2. `CreateDeploymentRequest`: add `rings: [{groupId, soakMinutes}]` alternative to deviceIds/groupId — server creates chained deployments (first active/scheduled, rest Scheduled with parent link)
3. `DeploymentSchedulerService`: when a ring deployment completes, schedule child at `completedAt + soakMinutes`
4. Agent poll endpoint: filter by maintenance window of the device's group
5. Deployment list DTO: `ringOrder`, `parentDeploymentId`, `blockedByWindow` (computed per result)
6. Groups API: CRUD for window fields

## Frontend steps
7. Deploy modal: "Rings" tab — pick ordered groups + soak time per ring; shows chain preview
8. Groups page: maintenance window editor (start time, duration, days-of-week)
9. Deployment list: ring badges (Ring 1/2/3), "waiting for window" indicator on blocked results

## Verification
10. Integration tests: ring chain creation, soak-based activation, window-open/closed filtering of agent poll
11. Live: create a 2-ring deploy (LAPITG001116 in pilot ring), watch pilot complete → soak → broad activates; test window blocking with a closed window
12. Build, test, web build, commit

## Files
- Edit: `ContentVersion.cs` (Deployment fields), `DeviceGroup.cs` (window fields), EF configs, migration
- Edit: `DeploymentsController.cs`, `DeploymentSchedulerService.cs`, `DeviceGroupsController.cs`
- Contracts: `CreateDeploymentRequest`, deployment DTOs
- Frontend: deploy modal (content.tsx), groups.tsx, deployments list
- Tests: new integration tests

## Simplifications
- Window is per-group (not per-device, not global) — matches how operators think about store locations
- Window is local server time; per-timezone windows are future work
- Ring failure policy: child rings stay Scheduled (paused) if parent success rate < 80% (same threshold as waves) — admin must manually cancel or force
