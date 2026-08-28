---
schema_version: 1
name: phase-5-kiosk-runtime
overview: Build the WebView2 kiosk runtime that displays content in fullscreen, enforces URL
  restrictions, handles session timeouts, and auto-restarts on failure.
todos:
  - id: p5t1
    content: Create WPF project structure + Program.cs entry point
    status: pending
  - id: p5t2
    content: Create MainWindow.xaml with fullscreen WebView2 host
    status: pending
  - id: p5t3
    content: Create KioskConfiguration model with policy settings
    status: pending
  - id: p5t4
    content: Create NavigationGuard with URL allowlist/denylist
    status: pending
  - id: p5t5
    content: Create SessionManager with timeout + inactivity reset
    status: pending
  - id: p5t6
    content: Create CrashMonitor with auto-restart + backoff
    status: pending
  - id: p5t7
    content: Create PolicyReceiver for agent communication
    status: pending
  - id: p5t8
    content: Create app.manifest + install-runtime.ps1
    status: pending
  - id: p5t9
    content: Build verification + commit
    status: pending
isProject: false
created_at: '2026-08-28T10:34:48'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_102
model: FW-Kimi-K3
mode_at_creation: auto
content_hash: e1f126de28e3b214
title: phase-5-kiosk-runtime
---

# phase-5-kiosk-runtime

_Build the WebView2 kiosk runtime that displays content in fullscreen, enforces URL restrictions, handles session timeouts, and auto-restarts on failure._

## Phase 5: Kiosk Runtime (WebView2 Browser)

### Goal
Create a WPF application with WebView2 that runs as the kiosk display: fullscreen, configurable home URL, URL allowlist/denylist, session timeout, inactivity reset, auto-restart on crash.

### Architecture

```text
┌─────────────────────────────────────────┐
│      Windows Kiosk Runtime (WPF)        │
│  ┌─────────────────────────────────┐    │
│  │  WebView2 Browser Control       │    │
│  │  • Fullscreen borderless        │    │
│  │  • Configurable home URL        │    │
│  │  • URL allowlist/denylist       │    │
│  │  • Session timeout              │    │
│  │  • Inactivity reset             │    │
│  │  • Cache/session clearing       │    │
│  └─────────────────────────────────┘    │
│  ┌─────────────────────────────────┐    │
│  │  Runtime Monitor                │    │
│  │  • Crash detection              │    │
│  │  • Auto-restart (max attempts)  │    │
│  │  • Watchdog heartbeat           │    │
│  └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
```

### Components

1. **MainWindow** — fullscreen WPF window hosting WebView2
2. **KioskConfiguration** — policy-driven settings (home URL, restrictions, timeouts)
3. **NavigationGuard** — URL allowlist/denylist enforcement
4. **SessionManager** — timeout tracking, inactivity reset, cache clearing
5. **CrashMonitor** — detect renderer failures, auto-restart with backoff
6. **PolicyReceiver** — sync with agent service via local IPC/named pipe

### Security
- No visible browser chrome (address bar, tabs, etc.)
- URL restrictions enforced before navigation
- Session data cleared on timeout/reset
- No arbitrary file downloads
- Pop-up blocking

### Files to Create
- `agents/windows-kiosk-runtime/SentinelKiosk.Runtime.csproj`
- `agents/windows-kiosk-runtime/Program.cs` — WPF app entry
- `agents/windows-kiosk-runtime/MainWindow.xaml` + `.cs`
- `agents/windows-kiosk-runtime/Models/KioskConfiguration.cs`
- `agents/windows-kiosk-runtime/Services/NavigationGuard.cs`
- `agents/windows-kiosk-runtime/Services/SessionManager.cs`
- `agents/windows-kiosk-runtime/Services/CrashMonitor.cs`
- `agents/windows-kiosk-runtime/Services/PolicyReceiver.cs`
- `agents/windows-kiosk-runtime/app.manifest` — DPI awareness, fullscreen
- `agents/windows-kiosk-runtime/install-runtime.ps1`

### Acceptance Criteria
- [ ] Fullscreen borderless WebView2 window
- [ ] Configurable home URL loaded on start
- [ ] URL allowlist blocks unauthorized navigation
- [ ] Session timeout returns to home URL
- [ ] Inactivity timeout triggers reset
- [ ] Crash detected → auto-restart (max 3 attempts, 5s delay)
- [ ] No browser UI visible to user
- [ ] Cache cleared on session end
- [ ] Builds and runs on Windows
