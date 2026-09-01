---
schema_version: 1
name: alerting-maturity
overview: 'Add alert lifecycle management: cooldown windows to prevent notification storms,
  escalation policies (ack → notify → page), and a test-notification button on notification
  channels.'
todos:
  - id: am-t1
    content: Add CooldownMinutes to AlertRule + LastNotifiedAt to Alert + migration
    status: completed
  - id: am-t2
    content: Update AlertEvaluatorService with cooldown logic
    status: completed
  - id: am-t3
    content: 'API: POST /api/notification-channels/{id}/test endpoint'
    status: completed
  - id: am-t4
    content: 'Frontend: test button on notification channels'
    status: completed
  - id: am-t5
    content: Create EscalationPolicy + EscalationStep entities + migration
    status: completed
  - id: am-t6
    content: Create AlertEscalationService background worker
    status: completed
  - id: am-t7
    content: 'Frontend: escalation policy selector on rules + status badges'
    status: in_progress
  - id: am-t8
    content: Integration tests + live verification + commit
    status: pending
isProject: false
created_at: '2026-09-01T13:46:39'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_572
model: FW-Kimi-K3
mode_at_creation: auto
content_hash: 3fd79f5a1096af3e
files_referenced:
  - src/Platform.Domain/Entities/Alert.cs
  - src/Platform.Api/Services/AlertEvaluatorService.cs
  - src/Platform.Api/Controllers/NotificationChannelsController.cs
  - src/Platform.Api/Services/NotificationService.cs
  - apps/admin-web/src/pages/alerts.tsx
  - apps/admin-web/src/pages/settings.tsx
title: alerting-maturity
---

# alerting-maturity

_Add alert lifecycle management: cooldown windows to prevent notification storms, escalation policies (ack → notify → page), and a test-notification button on notification channels._

## Current state

- Alert rules evaluate every 30s (CPU/memory/disk/offline/domain)
- Alerts raise, dedupe, auto-resolve when conditions clear
- NotificationService dispatches to channels (email/webhook/Teams) — fire-and-forget
- No cooldown: a flapping device generates notifications every 30s
- No escalation: Active → Acknowledged → Resolved is the only lifecycle
- No test button on notification channels

## 1. Cooldown windows

**Problem:** A device flapping between online/offline or hovering near a threshold generates a notification every 30 seconds.

**Changes:**
- Add `CooldownMinutes` (int, default 15) to `AlertRule`
- Track `LastNotifiedAt` per alert (not per rule) — notifications fire once per alert, then respect the cooldown
- In `AlertEvaluatorService`: when condition is met and an active alert exists, check if the cooldown has elapsed since `LastNotifiedAt` before re-notifying
- Auto-resolved alerts reset the cooldown immediately (new alert for the same condition notifies again)
- Frontend: show cooldown setting on alert rule editor

**Migration:** `ALTER TABLE alert_rules ADD COLUMN cooldown_minutes integer NOT NULL DEFAULT 15`

## 2. Escalation policies

**Problem:** Critical alerts sit unnoticed. No one is forced to acknowledge them.

**Changes:**
- Add `EscalationPolicy` entity: ordered steps with delay + action
  - Step 1: Notify channel X immediately (already exists)
  - Step 2: If not acknowledged after N minutes → notify channel Y (escalation)
  - Step 3: If not acknowledged after N minutes → raise severity to Critical + notify channel Z
- Add `EscalationPolicyId` (nullable FK) to `AlertRule`
- Add `EscalationStep` entity: `PolicyId, Order, DelayMinutes, ChannelId, EscalateSeverity`
- New `AlertEscalationService` (BackgroundService): every 60s, check unacknowledged alerts against their escalation policy steps
- Frontend: alert rule editor gets escalation policy selector; policies page CRUD

**Migration:** `CREATE TABLE escalation_policies`, `CREATE TABLE escalation_steps`, `ALTER TABLE alert_rules ADD COLUMN escalation_policy_id uuid`

## 3. Test notification button

**Problem:** No way to verify a notification channel works without waiting for a real alert.

**Changes:**
- API: `POST /api/notification-channels/{id}/test` — sends a test message through the channel and returns success/failure with error detail
- NotificationService: add `SendTestAsync(channel)` that dispatches a "Gatus Test Notification" message
- Frontend: "Test" button on each notification channel row in Settings → shows toast with result

## 4. Alert rule UI improvements

- Alert rule editor: add cooldown slider, escalation policy dropdown
- Alert list: show cooldown indicator, escalation status badge
- Alert detail: show escalation timeline (step 1 sent at X, step 2 pending in Y minutes)

## Verification

- Integration tests: cooldown prevents duplicate notifications, escalation fires after delay, test endpoint returns success/failure
- Live test: create a rule with 1-minute cooldown, trigger it, verify only one notification fires; test escalation with a short delay
- Frontend build + manual UI test

## Execution order

| # | Item | Files |
|---|------|-------|
| 1 | Cooldown on rules + evaluator | `Alert.cs`, `AlertEvaluatorService.cs`, migration, alerts page |
| 2 | Test notification endpoint + UI | `NotificationChannelsController.cs`, `NotificationService.cs`, settings page |
| 3 | Escalation entities + migration | `EscalationPolicy.cs`, `EscalationStep.cs`, migration |
| 4 | AlertEscalationService background worker | new `AlertEscalationService.cs` |
| 5 | Frontend: rule editor + alert list improvements | `alerts.tsx`, settings page |
| 6 | Integration tests + live verification | test files |
