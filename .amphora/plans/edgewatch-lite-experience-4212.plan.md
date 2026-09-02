---
schema_version: 1
name: EdgeWatch Lite experience
overview: Introduce EdgeWatch Lite as the default product experience while preserving all current
  advanced capabilities behind an Advanced Features area and server-controlled feature
  configuration.
todos:
  - id: lite-t1
    content: Add server product configuration DTO and protected /api/product endpoint
    status: completed
  - id: lite-t2
    content: Add typed product API client and React Query product configuration hook
    status: completed
  - id: lite-t3
    content: Update AppShell to EdgeWatch Lite branding and Lite/Advanced navigation
    status: completed
  - id: lite-t4
    content: Add Lite navigation routes using safe existing policy and command flows
    status: completed
  - id: lite-t5
    content: Update documentation and product naming without removing advanced features
    status: completed
  - id: lite-t6
    content: Add endpoint tests and run full build/type verification
    status: completed
isProject: false
created_at: '2026-08-31T19:57:05'
last_edited_at: '2026-08-31T20:01:31'
session_id: sess_60616e1870d76f6b
tool_use_id: call_LguE3veQhq88LvLgOvduZpJn
model: gpt-5.6-luna
mode_at_creation: plan
dismissed: true
content_hash: 0aa847478ce7a2eb
files_referenced:
  - src/Platform.Api/Controllers/ProductController.cs
  - src/Platform.Contracts/Responses/ProductResponses.cs
  - apps/admin-web/src/lib/api.ts
  - apps/admin-web/src/hooks/useProductConfig.ts
  - apps/admin-web/src/components/app-shell.tsx
  - apps/admin-web/src/App.tsx
  - README.md
  - docs/ARCHITECTURE.md
  - docs/ROADMAP.md
title: EdgeWatch Lite experience
---

# EdgeWatch Lite experience

_Introduce EdgeWatch Lite as the default product experience while preserving all current advanced capabilities behind an Advanced Features area and server-controlled feature configuration._

## Context

The current platform already has the capability set required for EdgeWatch Lite: device enrollment, heartbeats/telemetry, groups, policy JSON, WebView2 runtime, lockdown engine, commands, content, schedules, alerts, analytics, notifications, and logs. The visible product is still branded `Gatus Kiosk`, and the navigation exposes every advanced module equally.

The requested direction is **EdgeWatch Lite**, not a separate codebase and not deletion of existing features. Lite becomes the focused default experience; current advanced features remain available through an Advanced Features section.

## Updated implementation scope

The first implementation slice will focus on product identity and navigation safely, before adding new profile/action screens. Existing pages and APIs remain unchanged while the new server-backed feature configuration is introduced.

### Lite navigation

- Dashboard
- Devices
- Kiosk Profiles (route will initially point to the existing policy-management surface until the dedicated profile editor is implemented)
- Remote Actions (route will initially point to the existing command/device action surface until the dedicated action center is implemented)
- Settings

### Advanced navigation

- Groups
- Schedules
- Content
- Alerts
- Analytics
- Notifications
- Logs

## Implementation steps

1. Add a server-backed product configuration contract with `productName = EdgeWatch Lite`, `edition = Lite`, and feature flags for advanced modules. Use safe development defaults and keep the configuration extensible for a future Enterprise edition.
2. Add a protected `/api/product` endpoint returning product identity, edition, enabled features, and Lite/Advanced navigation policy. Do not expose secrets or environment configuration.
3. Add a typed `productApi` client and `useProductConfig` hook with React Query caching. Use the returned configuration to render the EdgeWatch Lite brand and navigation groups.
4. Update `AppShell` to show Lite navigation first, add an Advanced Features expandable section, preserve direct routes for existing advanced pages, and add a clear EdgeWatch Lite edition label.
5. Add Lite navigation routes using safe existing pages/flows first; do not invent a duplicate policy or command data model in this slice. A dedicated Kiosk Profiles editor and Remote Actions center will follow as the next implementation slice.
6. Update login/dashboard/page branding and documentation from Gatus Kiosk to EdgeWatch Lite while documenting that advanced capabilities remain enabled and available.
7. Add API endpoint tests and frontend build/type verification. Verify existing routes and current feature functionality remain reachable.

## Files/areas to touch

- `src/Platform.Api/Controllers/ProductController.cs`
- `src/Platform.Contracts/Responses/ProductResponses.cs`
- `apps/admin-web/src/lib/api.ts`
- `apps/admin-web/src/hooks/useProductConfig.ts`
- `apps/admin-web/src/components/app-shell.tsx`
- `apps/admin-web/src/App.tsx`
- `README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/DEVELOPMENT.md`
- API integration tests and frontend build checks

## Acceptance criteria

- The console is visibly branded **EdgeWatch Lite**.
- Lite users see a concise navigation focused on Dashboard, Devices, Kiosk Profiles, Remote Actions, and Settings.
- Advanced modules remain accessible through an Advanced Features section and existing URLs.
- The server, not only the browser, determines which features are enabled.
- Existing devices, groups, analytics, content, alerts, logs, notifications, schedules, commands, and agent workflows continue to function.
- `dotnet build`, API tests, TypeScript validation, and the admin web build pass.
