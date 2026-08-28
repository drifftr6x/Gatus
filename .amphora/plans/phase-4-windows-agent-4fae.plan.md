---
schema_version: 1
name: phase-4-windows-agent
overview: 'Build the Windows agent service that runs on kiosk machines: enrollment, heartbeat,
  policy sync, content deployment download/verify/activate, command execution, and
  local state management.'
todos:
  - id: p4t1
    content: Create windows-agent project structure + Program.cs Windows Service host
    status: completed
  - id: p4t2
    content: Create EnrollmentService with token exchange + DPAPI credential storage
    status: completed
  - id: p4t3
    content: Create HeartbeatService with system metrics collection
    status: completed
  - id: p4t4
    content: Create PolicySyncService with local cache + drift detection
    status: completed
  - id: p4t5
    content: Create DeploymentService with download/verify/stage/activate/rollback
    status: completed
  - id: p4t6
    content: Create CommandExecutor with allowlisted commands
    status: completed
  - id: p4t7
    content: Create TelemetryCollector with local queue + batch upload
    status: completed
  - id: p4t8
    content: Create LocalStateManager for config/content/log management
    status: completed
  - id: p4t9
    content: Create install.ps1 + agent configuration
    status: completed
  - id: p4t10
    content: Agent tests + final verification + commit
    status: completed
isProject: false
created_at: '2026-08-28T10:19:52'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_86
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 32a7c077a8d30795
title: phase-4-windows-agent
---

# phase-4-windows-agent

_Build the Windows agent service that runs on kiosk machines: enrollment, heartbeat, policy sync, content deployment download/verify/activate, command execution, and local state management._

## Phase 4: Windows Agent

### Goal
Create a Windows Service that runs on kiosk machines, enrolls with the server, sends heartbeats, syncs policies, downloads/deploys content, executes remote commands, and reports status.

### Architecture

```text
┌─────────────────────────────────────────┐
│         Windows Agent Service           │
│  ┌─────────┐ ┌─────────┐ ┌───────────┐ │
│  │Enrollment│ │Heartbeat│ │PolicySync │ │
│  │ Service  │ │ Service │ │ Service   │ │
│  └─────────┘ └─────────┘ └───────────┘ │
│  ┌─────────┐ ┌─────────┐ ┌───────────┐ │
│  │Deployment│ │ Command │ │Telemetry  │ │
│  │ Service  │ │Executor │ │Collector  │ │
│  └─────────┘ └─────────┘ └───────────┘ │
│         ↓ Local State ↓                 │
│  C:\ProgramData\SentinelKiosk\          │
│    ├── Config/    ├── Content/          │
│    ├── Logs/      ├── Cache/            │
│    └── State/     └── Updates/          │
└─────────────────────────────────────────┘
```

### Components

1. **EnrollmentService** — one-time token exchange → device credentials (DPAPI-protected)
2. **HeartbeatService** — configurable interval, system metrics (CPU, RAM, disk, uptime)
3. **PolicySyncService** — poll/fetch policy versions, cache locally, detect drift
4. **DeploymentService** — download content, verify SHA-256, stage, atomic activate, rollback
5. **CommandExecutor** — allowlisted commands: refresh, restart, reboot, diagnostics
6. **TelemetryCollector** — queue metrics locally, batch upload when online
7. **LocalStateManager** — encrypted config, content cache, log rotation

### Security
- TLS certificate validation
- Device credential storage via DPAPI (CurrentUser scope)
- SHA-256 verification before content activation
- No arbitrary shell commands — strict allowlist

### Files to Create
- `agents/windows-agent/Program.cs` — Windows Service host
- `agents/windows-agent/Services/EnrollmentService.cs`
- `agents/windows-agent/Services/HeartbeatService.cs`
- `agents/windows-agent/Services/PolicySyncService.cs`
- `agents/windows-agent/Services/DeploymentService.cs`
- `agents/windows-agent/Services/CommandExecutor.cs`
- `agents/windows-agent/Services/TelemetryCollector.cs`
- `agents/windows-agent/Services/LocalStateManager.cs`
- `agents/windows-agent/Models/AgentConfig.cs`
- `agents/windows-agent/appsettings.json`
- `agents/windows-agent/install.ps1` — service installer script

### Acceptance Criteria
- [ ] Agent enrolls with server using token
- [ ] Heartbeat sent every 30s with system metrics
- [ ] Policy sync detects and applies changes
- [ ] Content deployment: download → verify → stage → activate → report
- [ ] Command execution: refresh, restart, reboot (simulated in dev)
- [ ] Local state persisted to C:\ProgramData\SentinelKiosk\
- [ ] Service installs via install.ps1
- [ ] All tests pass
