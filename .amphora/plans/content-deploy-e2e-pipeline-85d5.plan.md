---
schema_version: 1
name: Content deploy E2E pipeline
overview: 'Wire up the full content deployment flow: upload → version + package → deploy to
  device/group → agent downloads/verifies/activates → kiosk displays it. Also prep
  for enrolling real kiosk agents.'
todos:
  - id: c1
    content: 'API: ContentStorageService + upload creates ContentVersion zip package with manifest
  + SHA-256'
    status: completed
  - id: c2
    content: 'API: content download endpoint streaming the version zip (device-secret + admin auth)'
    status: completed
  - id: c3
    content: 'API: DeploymentsController — create/poll/status-report/list endpoints'
    status: completed
  - id: c4
    content: 'API: verify device-secret auth works for deployment endpoints'
    status: completed
  - id: c5
    content: 'UI: content page versions list + Deploy modal (devices/group picker)'
    status: completed
  - id: c6
    content: 'UI: deployment status view with per-device results + SignalR live updates'
    status: completed
  - id: c7
    content: 'Agent: verify contract match + send content-activated pipe message to kiosk'
    status: completed
  - id: c8
    content: 'Kiosk runtime: navigate WebView2 on content-activated message'
    status: completed
  - id: c9
    content: Build + test + commit
    status: completed
  - id: c10
    content: 'Live E2E: upload content, deploy to laptop agent, verify Succeeded'
    status: completed
isProject: false
created_at: '2026-08-29T18:08:16'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_365
model: FW-Kimi-K3
mode_at_creation: auto
approved_mode: auto
dismissed: true
content_hash: 22ce82e6e6c3dee1
approved_hash: 22ce82e6e6c3dee1
files_referenced:
  - src/Platform.Api/Controllers/ContentController.cs
  - src/Platform.Api/Controllers/DeploymentsController.cs
  - src/Platform.Api/Services/ContentStorageService.cs
  - apps/api-server/Program.cs
  - apps/admin-web/src/pages/content.tsx
  - agents/windows-agent/Services/DeploymentService.cs
  - agents/windows-kiosk-runtime/Services/PolicyReceiver.cs
  - agents/windows-kiosk-runtime/MainWindow.xaml.cs
title: Content deploy E2E pipeline
---

# Content deploy E2E pipeline

_Wire up the full content deployment flow: upload → version + package → deploy to device/group → agent downloads/verifies/activates → kiosk displays it. Also prep for enrolling real kiosk agents._

## Context
Content upload saves files to a placeholder URL; there's no download endpoint, no DeploymentsController, no version packaging, and the kiosk runtime never receives deployed content. The agent's DeploymentService is fully written and waiting for server endpoints to exist.

## API (server)
1. **ContentStorageService** — real file storage under `AppData/content/` with per-version directories; store relative path on ContentVersion
2. **ContentController.upload** — on upload: save file, compute SHA-256, create Content record + ContentVersion (zip package with manifest.json containing per-file hashes)
3. **GET /api/content/{contentId}/versions/{versionId}/download** — streams the zip package (auth: device secret OR admin JWT)
4. **DeploymentsController** (new):
   - `POST /api/deployments` — create deployment (contentVersionId + deviceIds[] or groupId) → creates Deployment + DeploymentResult per device (status Pending)
   - `GET /api/deployments?deviceId=&status=` — agent poll endpoint (device-secret auth)
   - `POST /api/deployments/{id}/status` — agent reports Succeeded/Failed (updates DeploymentResult, rolls up Deployment status)
   - `GET /api/deployments` (admin) — list with results for UI
5. **Device-secret auth** — check how agent bearer tokens are validated today; ensure deployments + download endpoints accept them

## Admin UI
6. **Content page** — show versions per content item, "Deploy" button → modal: pick devices (or group), confirm → POST /api/deployments
7. **Deployment status view** — per-deployment row showing each device result (Pending/Downloading/Succeeded/Failed + error), live-updating via existing SignalR pattern (add DeploymentStatusChanged broadcast)

## Agent (already written — verify contract matches)
8. Verify `DeploymentInfo` JSON shape matches server DTO (id/contentVersionId/status)
9. After successful activation, agent writes new content path to kiosk via the named pipe (PolicyReceiver) so the kiosk WebView2 navigates to the deployed content — add `ContentActivatedPipeMessage`

## Kiosk runtime
10. PolicyReceiver handles content-activated message → navigate WebView2 to `file:///<activeContentPath>/index.html` (or the content's entry URL)

## Enrollment (item 4)
11. Verify enroll flow works end-to-end on a second machine (token → enroll → heartbeat → deployment). Fix any contract mismatches found.

## Verification
12. Build solution + web, run integration tests, commit
13. Live E2E: upload an image/PDF → deploy to LAPITG001116 (has agent) → watch status flip to Succeeded in UI

## Files
- New: src/Platform.Api/Controllers/DeploymentsController.cs, src/Platform.Api/Services/ContentStorageService.cs (exists? verify)
- Edit: ContentController.cs, Program.cs, DeviceHub.cs (broadcast), content.tsx, api.ts, agent DeploymentService.cs (pipe message), kiosk PolicyReceiver.cs / MainWindow.xaml.cs

## Open questions
- Kiosk display on laptop test: kiosk runtime is Windows-only WPF — on your laptop we can verify agent download/activation; kiosk navigation verified when runtime runs
- Deployment scheduling (future-dated) — keep simple: immediate only for now
