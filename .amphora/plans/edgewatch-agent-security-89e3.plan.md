---
schema_version: 1
name: edgewatch-agent-security
overview: Replace development-oriented anonymous agent routes with device-secret authentication,
  bind every request to the target device, and add regression tests without breaking
  enrollment or heartbeat workflows.
todos:
  - id: sec-t1
    content: Implement shared device-secret authentication service and credential validation
    status: completed
  - id: sec-t2
    content: Protect agent endpoints and enforce device/resource binding
    status: completed
  - id: sec-t3
    content: Update agent handling for credential rejection and re-enrollment
    status: completed
  - id: sec-t4
    content: Add integration tests for device authentication and cross-device access
    status: completed
  - id: sec-t5
    content: Run live heartbeat regression, full builds/tests, and update security docs
    status: completed
isProject: false
created_at: '2026-08-31T20:26:26'
session_id: sess_60616e1870d76f6b
tool_use_id: call_4t5tyfglk1w9Y7qlemS0s0Nq
model: gpt-5.6-luna
mode_at_creation: auto
dismissed: true
content_hash: 7528ca5cdd4e45f9
files_referenced:
  - src/Platform.Api/Controllers/DevicesController.cs
  - src/Platform.Api/Controllers/TelemetryController.cs
  - src/Platform.Api/Controllers/CommandsController.cs
  - src/Platform.Api/Controllers/DeploymentsController.cs
  - src/Platform.Api/Controllers/ContentController.cs
  - src/Platform.Domain/Entities
  - src/Platform.Infrastructure/Persistence
  - agents/windows-agent/Services
  - tests/Platform.Api.IntegrationTests
  - docs/SECURITY.md
  - docs/WINDOWS-AGENT.md
title: edgewatch-agent-security
---

# edgewatch-agent-security

_Replace development-oriented anonymous agent routes with device-secret authentication, bind every request to the target device, and add regression tests without breaking enrollment or heartbeat workflows._

## Goal

Secure EdgeWatch Lite’s Windows-agent communication before production use. The agent already sends a per-device bearer secret, but the API currently permits anonymous heartbeat, telemetry, command, deployment, and content download routes.

## Current findings

- Enrollment must remain anonymous because it uses a one-time enrollment token.
- Agent stores credentials using DPAPI and sends `Authorization: Bearer <DeviceSecret>`.
- The following routes still use `[AllowAnonymous]`: heartbeat, telemetry ingest, command poll/result, deployment poll/status, and content download.
- Authentication must validate both the bearer secret and the device ID in the route/body/query to prevent cross-device access.

## Implementation steps

1. Add a device-authentication service/middleware that extracts the bearer token, validates it against the device credential record, verifies the device is active, and exposes the authenticated device ID through a claim or request context. Use constant-time secret comparison and avoid logging secrets.
2. Add a server-side device credential persistence model if the current enrollment implementation only stores a non-recoverable/insufficient secret representation. Add rotation and revocation fields without invalidating existing local enrollment unexpectedly.
3. Replace `[AllowAnonymous]` on heartbeat, telemetry ingest, command poll/result, deployment poll/status, policy retrieval/update as applicable, and content download with a device-auth policy. Keep only enrollment anonymous.
4. Add resource binding checks: route/query/body device IDs must match the authenticated device; deployment and content targets must be authorized for that device; admin JWT access must remain supported where intended.
5. Update agent error handling for 401/403, credential revocation, and re-enrollment. Preserve offline cached policy/content behavior.
6. Add integration tests for valid device credentials, missing credentials, invalid credentials, inactive/revoked device, cross-device ID, and anonymous enrollment.
7. Run agent/server builds, API tests, frontend type/build verification, and a live heartbeat regression test against the enrolled laptop.
8. Update EdgeWatch Lite security and agent documentation with credential lifecycle and endpoint requirements.

## Acceptance criteria

- Anonymous enrollment works only with a valid, unexpired, single-use enrollment token.
- A valid device bearer secret can heartbeat, ingest telemetry, poll/report commands, retrieve policy, poll/report deployments, and download authorized content.
- Missing or invalid device credentials return 401/403.
- A device cannot submit data or retrieve commands/content for another device.
- Revoked/inactive devices are rejected.
- Admin JWT routes continue to work where documented.
- Existing enrolled agent continues heartbeating after configuration/build.
- All tests and builds pass.
