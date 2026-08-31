# Kiosk runtime

Project: `agents/windows-kiosk-runtime` (`SentinelKiosk.Runtime`).

WPF app hosting **WebView2** in a borderless fullscreen window. It is a **separate process** from the Windows Service because WebView2 must run in an interactive user session.

## Features

- Home URL from `KioskConfiguration` (`%ProgramData%\SentinelKiosk\Config\kiosk-config.json`)
- URL allowlist / denylist (`NavigationGuard`, wildcards)
- Session timeout and inactivity reset (`SessionManager`); optional cache clear
- Crash monitor with capped restarts
- Policy updates over named pipe `SentinelKioskPolicyPipe`
- Blocks typical chrome (context menu, DevTools, downloads, popups) unless policy allows them
- Best-effort hotkey filter in-process (not a replacement for OS kiosk / Keyboard Filter)

## Install

`install-runtime.ps1` (Administrator):

- Enterprise/Education/IoT: Shell Launcher when available
- Pro: Winlogon `Shell` replacement (backs up original `Shell` value)

Do not run shell replacement on a workstation you need to recover without Safe Mode.

## Agent interaction

After a successful content activate, the agent can notify the runtime over the named pipe so WebView2 navigates to the new package (typically `file:///` + `index.html` or the policy home URL).

## Logs

`C:\ProgramData\SentinelKiosk\Logs\runtime-*.log`
