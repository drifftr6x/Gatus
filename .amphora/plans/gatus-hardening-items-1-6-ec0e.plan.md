---
schema_version: 1
name: gatus-hardening-items-1-6
overview: 'Implement the 6 highest-impact improvements: HTTPS/security headers, refresh token
  hardening, rate limiting, agent service self-healing, deployment scheduling, and
  content rollback from UI.'
todos:
  - id: h1-t1
    content: Add rate limiting middleware with per-endpoint policies
    status: completed
  - id: h1-t2
    content: Add rate limiting integration tests
    status: completed
  - id: h2-t0
    content: Fix refresh token validation (opaque token, not JWT) + expiry rotation
    status: completed
  - id: h2-t1
    content: Convert refresh tokens to httpOnly cookies (server + frontend)
    status: completed
  - id: h2-t2
    content: Add token reuse detection + integration tests
    status: completed
  - id: h3-t1
    content: Add HSTS + security headers middleware
    status: completed
  - id: h3-t2
    content: Update nginx config with TLS termination + HSTS
    status: completed
  - id: h4-t1
    content: Install agent as Windows Service with recovery on LAPITG001116
    status: completed
  - id: h4-t2
    content: Add disk-space guard + old content cleanup to agent
    status: completed
  - id: h5-t1
    content: Add ScheduledAt + RolloutPercent to deployment creation and polling
    status: completed
  - id: h5-t2
    content: Create DeploymentSchedulerService background worker
    status: completed
  - id: h5-t3
    content: 'Frontend: schedule picker + rollout wave progress'
    status: completed
  - id: h6-t1
    content: 'API: POST /api/deployments/{id}/rollback endpoint'
    status: completed
  - id: h6-t2
    content: 'Agent: set PreviousVersionId on successful deployment'
    status: completed
  - id: h6-t3
    content: 'Frontend: rollback button + confirmation dialog'
    status: completed
  - id: h7-t1
    content: 'Final verification: build, test, live E2E, commit'
    status: completed
isProject: false
created_at: '2026-09-01T10:07:43'
last_edited_at: '2026-09-01T10:08:40'
session_id: sess_60616e1870d76f6b
tool_use_id: edit_plan_324
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 0c495eced155a028
files_referenced:
  - apps/api-server/Program.cs
  - src/Platform.Api/Controllers/AuthController.cs
  - src/Platform.Api/Controllers/DeploymentsController.cs
  - apps/admin-web/src/lib/api.ts
  - apps/admin-web/src/hooks/useAuth.tsx
  - agents/windows-agent/Services/DeploymentService.cs
  - agents/windows-agent/Services/LocalStateManager.cs
  - agents/windows-agent/install.ps1
  - src/Platform.Domain/Entities/ContentVersion.cs
  - infrastructure/nginx/
  - infrastructure/docker-compose.yml
title: gatus-hardening-items-1-6
---

# gatus-hardening-items-1-6

_Implement the 6 highest-impact improvements: HTTPS/security headers, refresh token hardening, rate limiting, agent service self-healing, deployment scheduling, and content rollback from UI._

## Order of implementation

Items are ordered by dependency and risk: security foundations first, then operational features that build on them.

---

### 1. Rate limiting on public endpoints (do first — smallest, standalone)

**Current state:** No `AddRateLimiter`/`UseRateLimiter` in Program.cs. Login, refresh, enroll, and device-facing endpoints are unthrottled.

**Changes:**
- Add `RateLimiter` middleware in `apps/api-server/Program.cs` with `AddRateLimiter()` + partitioned fixed-window policies
- Policy groups:
  - `auth` — login, register, refresh: 5 requests/minute per IP
  - `enroll` — POST /api/devices/enroll: 3 requests/minute per IP
  - `device` — heartbeat, telemetry, commands, policy, deployments, downloads: 120 requests/minute per device ID (agents poll frequently)
  - `api` — general authenticated endpoints: 300 requests/minute per user
- Return `429 Too Many Requests` with `Retry-After` header
- Add integration tests: rapid login attempts get 429

**Files:** `apps/api-server/Program.cs`, `src/Platform.Api/` (policy attributes or conventions), tests

---

### 2. Refresh token hardening — fix bugs + httpOnly cookies

**Current state (3 confirmed bugs):**
1. `localStorage` stores both tokens (XSS-vulnerable)
2. **`GetPrincipalFromExpiredToken` treats the opaque refresh token as a JWT — refresh is broken.** The token is a random Base64 string, not a signed JWT, so validation always fails before the database check.
3. **`RefreshTokenExpiresAt` is never updated on rotation** — the new token inherits the original login expiry.

**Changes:**
- **Fix refresh validation:** `RefreshToken` endpoint should look up the user by `refresh_token` field directly (opaque token comparison), not parse it as a JWT. Remove the `GetPrincipalFromExpiredToken` call for refresh tokens.
- **Fix expiry rotation:** Set `RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshDays)` on each rotation.
- **Server (AuthController):**
  - Login + refresh: set refresh token as `HttpOnly; Secure; SameSite=Lax` cookie named `gatus-refresh`
  - Access token still returned in JSON body (short-lived, in-memory on frontend)
  - Refresh endpoint reads from cookie instead of request body
  - Logout clears the cookie
  - Add reuse detection: if the presented refresh token doesn't match the current one, invalidate the entire session (force re-login)
- **Frontend (api.ts):**
  - Remove `refreshToken` from `localStorage`
  - On 401: call `/api/auth/refresh` with empty body (cookie sent automatically via `credentials: 'include'`)
  - Keep `accessToken` in memory only (not localStorage) — page refresh triggers one refresh call
  - Add `credentials: 'include'` to all fetch options
- **CORS:** already has `AllowCredentials()` — verify origins cover the frontend port
- Add `AccessTokenExpiresAt` to the response so the frontend knows when to proactively refresh
- Integration tests: refresh via cookie works, refresh with old/rotated cookie fails (reuse detection)

**Files:** `src/Platform.Api/Controllers/AuthController.cs`, `src/Platform.Security/Services/TokenService.cs`, `apps/admin-web/src/lib/api.ts`, `apps/admin-web/src/hooks/useAuth.tsx`, tests

---

### 3. HTTPS + security headers

**Current state:** `app.UseHttpsRedirection()` is in Program.cs but the app runs on HTTP. No HSTS, no CSP, no security headers. Nginx config exists from `p8t3` but may not have TLS config.

**Changes:**
- **Development:** Add `UseHsts()` (non-dev only) + security headers middleware:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self' ws: wss:`
- **Production (nginx):** Update `infrastructure/nginx/` config with TLS termination, HSTS header, proxy_pass to `http://api:5163`
- **docker-compose:** Add certificate volume mount, expose 443
- **appsettings.json:** Add `Kestrel:Endpoints:Https` section for direct HTTPS in production
- **Agent config:** Document that `ServerUrl` should use `https://` in production
- Integration test: verify security headers present on responses

**Files:** `apps/api-server/Program.cs`, `apps/api-server/appsettings.json`, `infrastructure/nginx/`, `infrastructure/docker-compose.yml`, tests

---

### 4. Agent watchdog + self-healing

**Current state:** Agent runs as foreground process on LAPITG001116. `install.ps1` creates the Windows Service with `sc.exe failure` recovery actions (restart/5000/restart/10000/restart/30000) but the service isn't actually installed on the dev laptop. No disk-space monitoring. No old-content cleanup.

**Changes:**
- **Install the service on LAPITG001116** using the existing `install.ps1`
- **Disk-space guard in DeploymentService:** Before downloading, check free space on the content drive; if < 500MB free, report failure with "Insufficient disk space" and alert
- **Content cleanup in LocalStateManager:** Add `CleanupOldContentAsync()` — keep current + backup, delete content dirs older than 30 days or when total size exceeds configurable limit
- **Kiosk runtime watchdog:** Add a `EnsureKioskRunning` check that starts the kiosk runtime process if it's not running and policy requires it
- **Service recovery verification:** After install, verify `sc.exe qfailure SentinelKioskAgent` shows restart actions
- Add integration test for disk-space check

**Files:** `agents/windows-agent/Services/DeploymentService.cs`, `agents/windows-agent/Services/LocalStateManager.cs`, `agents/windows-agent/install.ps1`, `agents/windows-agent/Models/AgentConfig.cs`

---

### 5. Deployment scheduling + rollout waves

**Current state:** `Deployment.ScheduledAt` exists in the entity but is never set by the controller or checked by the agent. Deployments are always immediate. No rollout wave support.

**Changes:**
- **Entity:** Add `RolloutPercent` (int, nullable) to Deployment — if set, only deploy to that % of target devices initially
- **API:**
  - `CreateDeployment`: accept `scheduledAt` and `rolloutPercent` in request; set `ScheduledAt` on entity
  - `PollDeployments` (agent endpoint): filter `WHERE ScheduledAt IS NULL OR ScheduledAt <= now()`
  - Add `DeploymentSchedulerService` (BackgroundService): every 30s, find scheduled deployments that are due, activate them (log + SignalR broadcast)
  - For rollout waves: after initial wave completes, create follow-up DeploymentResults for the next batch
- **Frontend:**
  - Deploy modal: add "Schedule for later" toggle + datetime picker
  - Deployment list: show scheduled status with countdown
  - Rollout wave progress bar on deployment detail
- Integration tests: create scheduled deployment, verify agent doesn't see it until due, verify rollout wave logic

**Files:** `src/Platform.Domain/Entities/ContentVersion.cs`, `src/Platform.Api/Controllers/DeploymentsController.cs`, new `src/Platform.Api/Services/DeploymentSchedulerService.cs`, `apps/admin-web/src/pages/content.tsx` (deploy modal), deployment list page, migration, tests

---

### 6. Content rollback from UI

**Current state:** Agent has `RollbackAsync` that restores from `.backup` directory on failure. `DeploymentResult.RollbackPerformed` and `PreviousVersionId` exist but are never set. No API endpoint or UI for admin-triggered rollback.

**Changes:**
- **API:**
  - `POST /api/deployments/{id}/rollback` — creates a new Deployment targeting the same devices, pointing at the `PreviousVersionId` (or the version before the deployed one)
  - Require Editor role
  - Broadcast via SignalR
- **Agent:**
  - Already handles any deployment the same way (download → verify → activate) — a rollback deployment is just a deployment of the previous version
  - Ensure `PreviousVersionId` is set on DeploymentResult when a deployment succeeds
- **Frontend:**
  - Deployment history/detail: "Rollback" button on completed deployments
  - Confirmation dialog showing which version will be restored
  - Rollback deployment appears in list with `isRollback: true` badge
- Integration tests: deploy v2, rollback to v1, verify agent activates v1

**Files:** `src/Platform.Api/Controllers/DeploymentsController.cs`, `agents/windows-agent/Services/DeploymentService.cs` (set PreviousVersionId), frontend deployment history page, tests

---

## Verification plan

After all 6 items:
1. Full solution build (all projects)
2. Integration test suite (target: 20+ tests)
3. Frontend build
4. Live verification on LAPITG001116:
   - Service installed and running
   - Rate limiting returns 429 on rapid login
   - Refresh token works via httpOnly cookie (not in localStorage)
   - Security headers present
   - Scheduled deployment activates at the right time
   - Rollback button restores previous content version
