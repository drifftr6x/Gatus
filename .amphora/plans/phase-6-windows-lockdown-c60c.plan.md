---
schema_version: 1
name: phase-6-windows-lockdown
overview: Implement safe, reversible Windows lockdown policies using supported mechanisms (Assigned
  Access, Shell Launcher, CSP/GPO) with administrative recovery mode.
todos:
  - id: p6t1
    content: Create LockdownPolicy model + LockdownEngine core
    status: completed
  - id: p6t2
    content: Create ShellLauncherProvider for Enterprise/Education
    status: completed
  - id: p6t3
    content: Create AssignedAccessProvider for single-app kiosk
    status: completed
  - id: p6t4
    content: Create RegistryPolicyProvider for UI restrictions
    status: completed
  - id: p6t5
    content: Create KeyboardFilterProvider for hotkey blocking
    status: completed
  - id: p6t6
    content: Create MaintenanceModeService with timeout + recovery
    status: completed
  - id: p6t7
    content: 'Create PowerShell scripts: enable/disable/restore lockdown'
    status: completed
  - id: p6t8
    content: Integrate LockdownEngine into Windows Agent service
    status: completed
  - id: p6t9
    content: Add lockdown status reporting to heartbeat
    status: completed
  - id: p6t10
    content: Build verification + commit
    status: completed
isProject: false
created_at: '2026-08-28T10:50:21'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_146
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 843aca80b5714971
title: phase-6-windows-lockdown
---

# phase-6-windows-lockdown

_Implement safe, reversible Windows lockdown policies using supported mechanisms (Assigned Access, Shell Launcher, CSP/GPO) with administrative recovery mode._

## Phase 6: Windows Lockdown Engine

### Goal
Implement configurable, reversible Windows lockdown policies using **supported Windows mechanisms only** — no destructive or irreversible modifications. Every change must have a documented recovery path.

### Architecture

```text
┌─────────────────────────────────────────┐
│      Windows Agent Service              │
│  ┌─────────────────────────────────┐    │
│  │  LockdownEngine                 │    │
│  │  • Policy evaluation            │    │
│  │  • Reversible change tracking   │    │
│  │  • Rollback on failure          │    │
│  └─────────────────────────────────┘    │
│              ↓                          │
│  ┌─────────────────────────────────┐    │
│  │  LockdownProvider (interface)   │    │
│  │  • ShellLauncherProvider        │    │
│  │  • AssignedAccessProvider       │    │
│  │  • RegistryPolicyProvider       │    │
│  │  • KeyboardFilterProvider       │    │
│  └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
              ↓
    C:\ProgramData\SentinelKiosk\Lockdown\
      ├── backup\           (pre-change backups)
      ├── state\            (current lockdown state)
      └── recovery\         (recovery scripts)
```

### Supported Lockdown Mechanisms

| Mechanism | Windows Edition | Use Case |
|-----------|----------------|----------|
| **Shell Launcher** | Enterprise/Education/IoT | Replace shell with kiosk runtime |
| **Assigned Access** | All | Single-app kiosk mode (UWP or desktop) |
| **Registry Policies** | All | Disable Task Manager, Settings, Control Panel, etc. |
| **Keyboard Filter** | Enterprise/Education/IoT | Block Alt+Tab, Ctrl+Esc, etc. |
| **AppLocker** | Enterprise/Education | Restrict executable launching |

### Lockdown Policy Model

```json
{
  "shell": {
    "mode": "shell-launcher|assigned-access|registry",
    "kioskRuntimePath": "C:\\Kiosk\\SentinelKiosk.Runtime.exe",
    "autoLaunch": true,
    "restartOnCrash": true,
    "maxRestartAttempts": 3
  },
  "windowsUi": {
    "disableTaskbar": true,
    "disableStartMenu": true,
    "disableDesktop": true,
    "disableSettings": true,
    "disableControlPanel": true,
    "disableFileExplorer": true,
    "disableRunDialog": true,
    "disableCommandPrompt": true,
    "disablePowerShell": true,
    "disableTaskManager": true,
    "disableNotificationCenter": true
  },
  "keyboard": {
    "blockAltTab": true,
    "blockAltF4": true,
    "blockWindowsKey": true,
    "blockCtrlEsc": true,
    "blockWinR": true,
    "blockWinX": true,
    "blockContextMenus": true
  },
  "maintenance": {
    "enabled": true,
    "durationMinutes": 30,
    "requireReason": true,
    "autoReturnToKiosk": true
  }
}
```

### Recovery Mode
- **Safe administrative recovery**: Hold Shift+Ctrl+Win+Alt+R for 10 seconds during boot
- **Maintenance mode**: Temporary disable with timeout (30 min default), requires reason
- **Emergency recovery**: Boot to Safe Mode, run `C:\ProgramData\SentinelKiosk\Lockdown\recovery\restore.ps1`

### Files to Create
- `agents/windows-agent/Services/LockdownEngine.cs`
- `agents/windows-agent/Services/Lockdown/ILockdownProvider.cs`
- `agents/windows-agent/Services/Lockdown/ShellLauncherProvider.cs`
- `agents/windows-agent/Services/Lockdown/AssignedAccessProvider.cs`
- `agents/windows-agent/Services/Lockdown/RegistryPolicyProvider.cs`
- `agents/windows-agent/Services/Lockdown/KeyboardFilterProvider.cs`
- `agents/windows-agent/Services/MaintenanceModeService.cs`
- `agents/windows-agent/Models/LockdownPolicy.cs`
- `agents/windows-agent/Scripts/enable-lockdown.ps1`
- `agents/windows-agent/Scripts/disable-lockdown.ps1`
- `agents/windows-agent/Scripts/restore.ps1`

### Acceptance Criteria
- [ ] Lockdown policies applied via supported Windows mechanisms
- [ ] All changes tracked and reversible
- [ ] Rollback on application failure
- [ ] Maintenance mode with timeout and reason
- [ ] Emergency recovery script generated
- [ ] No permanent system modifications
- [ ] Agent reports lockdown status to server
