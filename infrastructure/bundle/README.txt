=====================================================================
 GATUS KIOSK -- CLIENT BUNDLE (Windows 11 Pro)
=====================================================================

CONTENTS
  agent\        Management agent (Windows service: SentinelKioskAgent)
  runtime\      Kiosk runtime (fullscreen WebView2 shell)
  setup.ps1     Installer (run as Administrator)
  uninstall.ps1 Removal + shell restore

REQUIREMENTS
  - Windows 10/11 (any edition; lockdown path is the Pro per-user shell)
  - Administrator rights to install
  - Network access to the Gatus server
  - An enrollment token (admin console -> Devices -> Enroll a Device)

INSTALL
  1. Extract this zip on the target PC.
  2. Open an ELEVATED PowerShell prompt in the extracted folder.
  3. Run:
       powershell -ExecutionPolicy Bypass -File .\setup.ps1 `
         -ServerUrl https://<your-gatus-server> -EnrollmentToken <token>
  4. Verify the device appears Online in the admin console (~60s).
  5. Sign in as the kiosk user (default: KioskUser) or reboot a
     kiosk-dedicated machine. First sign-in applies the shell lockdown.

  Dry run first (prints every action, changes nothing):
       .\setup.ps1 -ServerUrl ... -EnrollmentToken ... -WhatIf

OPTIONS
  -KioskUser <name>    Local kiosk account name (created if missing)
  -UseDomainUser <DOM\user>  Lock down an existing domain user instead
  -AllowPowerButton    Leave power options on the lock screen

UNINSTALL
  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
  Switches: -KeepData (keep logs/content)  -RemoveUser (delete kiosk user)

SUPPORT
  Logs:     C:\ProgramData\SentinelKiosk\Logs\
  Recovery: uninstall.ps1 restores the original shell; Safe Mode also works.
=====================================================================
