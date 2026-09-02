---
schema_version: 1
name: Agent self-updater with signed binaries
overview: 'Agents can self-update: server publishes signed agent update packages (reusing the
  RSA SigningService), agents poll for updates, verify signature, stage, and swap
  binaries via a safe updater script with rollback.'
todos:
  - id: au1
    content: AgentUpdate entity + EF config + migration
    status: completed
  - id: au2
    content: 'AgentUpdatesController: admin upload/sign/list/activate + agent latest/download endpoints'
    status: completed
  - id: au3
    content: 'Agent: real assembly version in csproj + heartbeat'
    status: completed
  - id: au4
    content: 'Agent: UpdateService poll/download/verify/stage + apply-update.ps1 self-swap with
  rollback'
    status: completed
  - id: au5
    content: 'Integration tests: eligibility gating, auth, upload flow'
    status: completed
  - id: au6
    content: publish-agent-update.ps1 + docs + build/test verify + commit
    status: completed
isProject: false
created_at: '2026-09-02T05:42:05'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_165
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 424b71d25c394621
title: Agent self-updater with signed binaries
---

# Agent self-updater with signed binaries

_Agents can self-update: server publishes signed agent update packages (reusing the RSA SigningService), agents poll for updates, verify signature, stage, and swap binaries via a safe updater script with rollback._

## Design (reuses signed-manifest infrastructure)

Update package = zip containing new agent binaries + `manifest.json` (version, per-file SHA-256, signature, keyId) — identical format to content packages, so `SigningService` (server) and `SignatureVerifier` (agent, DPAPI-pinned key) are reused as-is.

**Update model:** server-side `AgentUpdate` entity (version, package path, sha256, rollout %, min current version, createdAt, isActive). Agents report `agentVersion` in heartbeat (already sent, hardcoded "1.0.0" — will read real assembly version). Server decides eligibility; agent polls a `GET /api/agent-updates/latest` (device-secret auth) and self-updates when a newer version is offered.

### 1. Server: AgentUpdate entity + storage + migration
- `AgentUpdate` entity: Id, Version (semver string), PackagePath, Sha256, SizeBytes, RolloutPercent, MinVersion, Notes, IsActive, CreatedAt
- Storage under `AppData/agent-updates/{version}/` via ContentStorageService-style helper (or reuse it)
- Migration `20260908000000_AddAgentUpdates`

### 2. Server: AgentUpdatesController (admin)
- `POST /api/agent-updates` (RequireEditor): multipart upload of a published agent zip → compute SHA-256, sign manifest, store, deactivate older versions
- `GET /api/agent-updates` (admin): list
- `POST /api/agent-updates/{id}/activate|deactivate`
- `DELETE /api/agent-updates/{id}`

### 3. Server: agent-facing endpoints (device-secret auth, like commands/deployments)
- `GET /api/agent-updates/latest?currentVersion=x.y.z` → returns update info if eligible (version comparison, rollout bucket by device id hash, min version gate) else 204
- `GET /api/agent-updates/{id}/download` → streams signed zip

### 4. Agent: real version + UpdateService
- csproj: `<Version>` + heartbeat reports `AssemblyInformationalVersion` instead of hardcoded "1.0.0"
- New `UpdateService` (BackgroundService, poll interval config `UpdateCheckIntervalSeconds` default 3600):
  - Poll latest → if newer: download zip to `updates/staging/`, verify manifest signature (reuse SignatureVerifier), verify per-file SHA-256
  - Write `updates/pending.json` marker; trigger self-update
- **Self-update mechanism:** agent is a running Windows Service — it can't replace its own exe. Approach: agent extracts verified package to `updates/staging/`, writes an `apply-update.ps1` (stop service → backup current dir → copy new files → start service; on failure restore backup), launches it detached (`powershell -WindowStyle Hidden` via Process.Start with `UseShellExecute`), then exits. The script's first action after successful start: report new version in next heartbeat. Rollback = restore `backup/` dir.
  - Keep the script unsigned-by-file but gated: it only runs from files whose hashes were verified against a signed manifest — the trust anchor is the signature check, not the script.

### 5. Server: eligibility logic + tests
- Semver-ish comparison (System.Version parse), deterministic rollout bucket: `(deviceId guid bytes hash) % 100 < RolloutPercent`
- Integration tests: latest endpoint gating (no update, older min-version, rollout %), download auth (device secret required), admin upload flow with in-memory zip
- Update existing heartbeat test if agentVersion assertion exists

### 6. Ops/docs
- `scripts/publish-agent-update.ps1`: builds agent in Release, zips binaries + placeholder manifest, prints SHA — admin uploads via UI/API (server signs at upload time, so the script doesn't need the private key)
- DEPLOYMENT.md / README section: how to publish an update, rollout %, rollback story
- Note: publishing deliberately requires admin action — no auto-update pipeline from CI yet

## Files
- New: `src/Platform.Domain/Entities/AgentUpdate.cs`, config, migration, `AgentUpdatesController.cs`, agent `Services/UpdateService.cs`, `apply-update.ps1` (templated by agent), `infrastructure/scripts/publish-agent-update.ps1`, tests file
- Edit: `HeartbeatService.cs` (real version), agent csproj + appsettings (interval), `ApplicationDbContext.cs`, heartbeat DTO already has agentVersion, DEPLOYMENT.md/README

## Out of scope
- Kiosk runtime (WPF) self-update — separate component, later
- Delta updates — full package swap only (agent is ~small)
- Server-side auto-publish from CI

## Verification
- Build solution (stop agent service first), 26+ tests, web build (no UI this phase — API + PowerShell), commit
- Optional live check: package current agent as "update", point at test device, watch version flip in heartbeat
