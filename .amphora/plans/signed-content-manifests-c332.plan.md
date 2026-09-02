---
schema_version: 1
name: Signed content manifests
overview: 'Add RSA signing to content manifests: server signs at package creation, agent verifies
  with an embedded/downloaded public key before activation. Closes the trust gap where
  any write access to the content store could push arbitrary payloads to kiosks.'
todos:
  - id: sg-t1
    content: Create SigningService (RSA-4096 key gen/load, sign manifest, expose public key)
    status: completed
  - id: sg-t2
    content: Sign manifests in ContentStorageService at package creation
    status: completed
  - id: sg-t3
    content: GET /api/signing/public-key endpoint (device-secret + admin auth)
    status: completed
  - id: sg-t4
    content: 'Agent: SignatureVerifier with key pinning + fetch'
    status: completed
  - id: sg-t5
    content: 'Agent: wire signature verification into DeploymentService before activation'
    status: completed
  - id: sg-t6
    content: 'Tests: sign/verify round-trip, tamper rejection; integration test for signed upload'
    status: completed
  - id: sg-t7
    content: 'Docs: SECURITY.md + DEPLOYMENT.md checklist; build + commit'
    status: completed
isProject: false
created_at: '2026-09-01T22:23:02'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_426
model: FW-Kimi-K3
mode_at_creation: auto
approved_mode: auto
dismissed: true
content_hash: e091fb497c3a6afb
approved_hash: e091fb497c3a6afb
files_referenced:
  - src/Platform.Api/Services/ContentStorageService.cs
  - agents/windows-agent/Services/DeploymentService.cs
  - agents/windows-agent/Services/EnrollmentService.cs
  - apps/api-server/Program.cs
  - apps/api-server/appsettings.json
  - docs/SECURITY.md
title: Signed content manifests
---

# Signed content manifests

_Add RSA signing to content manifests: server signs at package creation, agent verifies with an embedded/downloaded public key before activation. Closes the trust gap where any write access to the content store could push arbitrary payloads to kiosks._

## Context
The agent verifies SHA-256 file integrity (`VerifyChecksumsAsync`) but has no publisher-authenticity check. The manifest (`manifest.json` inside `package.zip`) is unsigned — anyone with write access to the server's content store, or ability to MITM without TLS, can swap payloads and the agent will install them.

## Design
- **RSA-4096 key pair**, generated server-side on first run (or provided via env/volume): private key stays on server (`AppData/keys/signing.key`), public key exposed via `GET /api/signing/public-key` (device-secret auth) and baked into the agent config at install time as fallback.
- **Signature**: sign the canonical JSON of the manifest (sorted keys, no whitespace) with RSA-SHA256 (PSS padding). Store as `signature` field in manifest.json.
- **Agent verification**: after checksum verification, verify the manifest signature before activation. Fail the deployment (`Failed: signature verification failed`) if invalid or missing when enforcement is on.

## Server steps
1. **`SigningService`** (`src/Platform.Api/Services/`): load-or-generate RSA key pair at startup (persist under content root `keys/`); expose `SignManifest(manifestJson) → base64 signature`; expose public key (PEM or base64 DER)
2. **ContentStorageService**: after building manifest.json, sign it and add `"signature": "<base64>"` to the manifest JSON before zipping
3. **Endpoint**: `GET /api/signing/public-key` — returns `{ algorithm: "RSA-SHA256-PSS", key: "<base64 DER public key>", keyId: "<sha256[:16]>" }`; device-secret or admin auth
4. **Key config**: `Signing:KeyPath` (default `AppData/keys/signing.key`), `Signing:Enforcement` (default true)

## Agent steps
5. **`SignatureVerifier`** in agent: fetch+pin public key at enrollment (store alongside credentials, DPAPI-protected), with fallback to server fetch if missing; verify manifest signature using RSA PSS
6. **DeploymentService**: between checksum verification and activation, call signature verification; report `Failed` with "Invalid content signature" on mismatch
7. **Key rotation**: if server keyId changes (detected on 403/verification failure), re-fetch public key once, retry verification; if still failing, fail deployment and log security event

## Compatibility
- `Signing:Enforcement=false` mode: agent warns but activates (for migration of existing content that predates signing)
- Agent treats a missing `signature` field as failure when enforcement on
- Existing unsigned content: re-sign on first download if server still has the raw file, or leave unsigned (will fail enforcement — admin re-uploads)

## Verification
8. Unit tests: sign/verify round-trip; tampered manifest rejected; wrong key rejected
9. Integration test: upload content → manifest has signature; tamper stored manifest → agent-side verification fails (agent-level unit test is enough for CI)
10. Live E2E on LAPITG001116: deploy signed content → Succeeded; then tamper with a staged copy and confirm rejection path works (can simulate locally)
11. Update docs/SECURITY.md + DEPLOYMENT.md checklist items

## Files
- New: `src/Platform.Api/Services/SigningService.cs`, `agents/windows-agent/Services/SignatureVerifier.cs`
- Edit: `ContentStorageService.cs` (sign on package), `Program.cs` (register + endpoint or new controller), `DeploymentService.cs` (verify step), `appsettings.json` (Signing section)
- Edit: `docs/SECURITY.md`, `docs/DEPLOYMENT.md`

## Open questions
- Key storage: filesystem under content root (simple, fits current single-node model) vs. DPAPI-protected. Going with filesystem + restrictive ACLs; DPAPI adds machine-binding complexity for container deployments.
- Agent update packages (the agent EXE itself): out of scope for this pass — the agent has no self-update mechanism yet. Manifest signing covers content; agent binary signing would come with a future self-updater.
