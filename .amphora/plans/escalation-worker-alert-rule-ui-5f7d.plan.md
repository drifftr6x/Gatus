---
schema_version: 1
name: Escalation worker + alert rule UI
overview: Implement the AlertEscalationService background worker that executes escalation steps
  for unacknowledged alerts, plus frontend alert rule editor with cooldown and escalation
  policy selectors.
todos:
  - id: es-t1
    content: Create AlertEscalationService background worker with step execution
    status: completed
  - id: es-t2
    content: Register AlertEscalationService in Program.cs
    status: completed
  - id: es-t3
    content: 'Frontend: cooldown input + escalation policy selector on alert rule editor'
    status: completed
  - id: es-t4
    content: 'Frontend: escalation status badges on alerts list'
    status: completed
  - id: es-t5
    content: Build, test, commit
    status: completed
isProject: false
created_at: '2026-09-01T20:31:17'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_115
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: be8f6d54785b6d15
files_referenced:
  - src/Platform.Api/Services/AlertEvaluatorService.cs
  - src/Platform.Api/Controllers/AlertsController.cs
  - apps/admin-web/src/pages/alerts.tsx
  - apps/admin-web/src/lib/api.ts
  - src/Platform.Api/Program.cs
title: Escalation worker + alert rule UI
---

# Escalation worker + alert rule UI

_Implement the AlertEscalationService background worker that executes escalation steps for unacknowledged alerts, plus frontend alert rule editor with cooldown and escalation policy selectors._

## Context
Escalation policies and steps exist in the DB (from previous work), and the evaluator sets `EscalationPolicyId` on new alerts. But nothing executes the escalation steps — unacknowledged alerts never escalate to step 2, 3, etc. Also, the alert rule editor doesn't expose cooldown or escalation policy selection.

## Steps

### 1. AlertEscalationService (background worker)
- New `src/Platform.Api/Services/AlertEscalationService.cs` (BackgroundService, 60s interval)
- Query: alerts where `Status = Active` (not acknowledged/resolved), `EscalationPolicyId IS NOT NULL`, `EscalationStep < maxStep`
- For each alert: find the next step where `DelayMinutes <= (now - RaisedAt)` and `Step.Order > alert.EscalationStep`
- Execute: notify the step's channel, optionally escalate severity, set `alert.EscalationStep = step.Order`, `alert.LastNotifiedAt = now`
- Log escalation events
- Register in `Program.cs`

### 2. Alert rule editor UI improvements
- In `alerts.tsx` (or settings), when creating/editing a rule: add cooldown minutes input + escalation policy dropdown
- Show escalation status badge on alert rows (e.g. "Step 2/3")

### 3. Frontend: escalation status in alert list
- Show `EscalationStep` on alert badges (if > 0)
- Maybe show escalation policy name on rule rows

### 4. Verification
- Build + test
- Live test: create a rule with a 1-minute escalation policy, trigger it, don't acknowledge → verify step 2 fires

## Files
- New: `src/Platform.Api/Services/AlertEscalationService.cs`
- Edit: `src/Platform.Api/Program.cs` (register), `apps/admin-web/src/pages/alerts.tsx`, `apps/admin-web/src/lib/api.ts`
- Already exists: entities, migration, EscalationPoliciesController
