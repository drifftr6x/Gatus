---
schema_version: 1
name: phase-9-remote-commands
overview: '''Build the server-side command queue the agent''''s CommandExecutor already polls:
  Command'
todos:
  []
isProject: true
created_at: '2026-08-28T22:09:06'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_351
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 78a8b8c97ff4e85d
title: phase-9-remote-commands
---

# phase-9-remote-commands

_'Build the server-side command queue the agent''s CommandExecutor already polls: Command_

## Phase 9: Remote Commands

### Research: the agent contract (already built)

`agents/windows-agent/Services/CommandExecutor.cs` polls every 15s:
- **GET `/api/commands?deviceId={id}&status=Queued`** → expects `List<CommandInfo>` = `{ id, type, payload?, expiresAt?, timeoutSeconds }`
- **POST `/api/commands/{commandId}/result`** → body `{ commandId, status, message?, timestamp }`
- Statuses the agent sends: `Acknowledged`, `Succeeded`, `Failed`, `Rejected`, `Expired`
- Allowlisted types: RefreshKiosk, RestartKioskRuntime, ClearBrowserSession, ReloadPolicy, SynchronizeContent, RebootWindows, ShutdownWindows, LogOffKioskSession, EnterMaintenanceMode, CollectDiagnostics, UploadLogs
- Auth: Bearer deviceSecret (same pattern as heartbeat — AllowAnonymous for MVP)

### Server gaps (all missing today — agent gets 404)

1. `Command` entity + EF config + migration — id, deviceId, type, payload, status, createdById, timestamps, expiresAt, timeoutSeconds, result message
2. `CommandsController`:
   - `GET /api/commands?deviceId&status=Queued` (AllowAnonymous — agent poll)
   - `POST /api/commands/{id}/result` (AllowAnonymous — agent reports)
   - `POST /api/devices/{id}/commands` (RequireEditor — admin issues command) → creates Queued command
   - `GET /api/commands` history w/ filters (RequireViewer)
   - `POST /api/commands/{id}/cancel` (RequireEditor)
3. DTOs: `CommandInfoDto`, `IssueCommandRequest`, `CommandResultReport`, `CommandDto`
4. Status state machine: Queued → Acknowledged → Succeeded/Failed/Rejected/Expired/Cancelled

### Frontend

5. Commands API client + `CommandDto` types
6. Device detail: "Send Command" dropdown (allowlisted types) + per-device command history table w/ status badges
7. Commands appear via SignalR invalidation (reuse existing useSignalR pattern)

### Acceptance criteria
- [ ] Admin issues a command to a device from the UI
- [ ] Agent polls, picks it up, executes (simulated), reports Acknowledged → Succeeded
- [ ] UI shows live status transitions without refresh
- [ ] Command history persisted with timestamps + result message
- [ ] Non-allowlisted / expired commands handled
- [ ] Full build + tests pass + live E2E verified against running agent
