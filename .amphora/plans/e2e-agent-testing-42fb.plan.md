---
schema_version: 1
name: e2e-agent-testing
overview: Close the gaps between the Windows agent and the server so a real enrollment → heartbeat
  → dashboard loop works end-to-end on this machine.
todos:
  - id: e2e-t1
    content: 'Server: EnrollmentToken entity + EF configuration + migration'
    status: completed
  - id: e2e-t2
    content: 'Server: POST /api/devices/enroll endpoint (token validate + device create + secret
  issue)'
    status: completed
  - id: e2e-t3
    content: 'Server: EnrollmentTokensController (generate/list/revoke) with RequireEditor'
    status: completed
  - id: e2e-t4
    content: 'Frontend: enrollment token generate/copy UI on Devices page'
    status: completed
  - id: e2e-t5
    content: 'Agent: fix ServerUrl to dev API + add agent/runtime to solution'
    status: completed
  - id: e2e-t6
    content: 'Live E2E test: enroll agent, verify Online status + heartbeats in dashboard'
    status: completed
  - id: e2e-t7
    content: Build/test verification + commit
    status: completed
isProject: false
created_at: '2026-08-28T18:00:06'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_247
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 4bffb839db321f2d
title: e2e-agent-testing
---

# e2e-agent-testing

_Close the gaps between the Windows agent and the server so a real enrollment → heartbeat → dashboard loop works end-to-end on this machine._

## End-to-End Agent Testing

### Research findings

**Agent (agents/windows-agent) is built and functional:**
- `EnrollmentService` → POSTs to `/api/devices/enroll` with token, hostname, hardwareId, public key
- `HeartbeatService` → POSTs to `/api/devices/{id}/heartbeat` every 30s with CPU/RAM/disk metrics
- `LocalStateManager` → DPAPI-protected credentials in `C:\ProgramData\SentinelKiosk\`

**Server gaps that block E2E:**
1. No `POST /api/devices/enroll` endpoint — agent enrollment will 404
2. No `EnrollmentToken` entity/table — no way to generate tokens for agents
3. No admin UI to create enrollment tokens
4. Agent `ServerUrl` = `https://localhost:7001` but dev API is `http://localhost:5163`
5. Agent/runtime projects not in `KioskPlatform.sln`
6. Heartbeat auth mismatch (agent sends Bearer DeviceSecret, server endpoint is AllowAnonymous — acceptable for MVP but must not 401)

### Steps

1. **Server: EnrollmentToken entity + migration** — table with token hash, expiry, single-use flag, created-by
2. **Server: POST /api/devices/enroll** — validate token, create device (or match by hardwareId), return deviceId + deviceSecret; mark token used
3. **Server: EnrollmentTokensController** — admin CRUD to generate/revoke tokens (RequireEditor policy)
4. **Frontend: enrollment token UI** — section on Devices page to generate/copy one-time token
5. **Agent config: point to dev server** — `ServerUrl: http://localhost:5163`, allow HTTP in dev
6. **Solution: add agent + runtime to KioskPlatform.sln**
7. **Live E2E test** — run API, generate token via UI/API, run agent with token, verify device appears Online in dashboard with heartbeats

### Acceptance criteria
- [ ] Admin generates enrollment token (UI + API)
- [ ] Agent enrolls with token → gets deviceId + secret, stores via DPAPI
- [ ] Agent sends heartbeat every 30s → device shows Online in admin UI
- [ ] Token is single-use (second use rejected)
- [ ] Full build + tests pass
