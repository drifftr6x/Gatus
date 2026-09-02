---
schema_version: 1
name: 'Security hardening: JWT secret + seed users'
overview: 'Close the two remaining security gaps: JWT secret sourced from environment/secret
  store with startup validation, and seed users disabled outside development with
  forced password change.'
todos:
  - id: sec1
    content: JWT startup validation + move dev secret to appsettings.Development.json
    status: completed
  - id: sec2
    content: 'DbSeeder: config-driven admin password, no hardcoded Admin123!'
    status: completed
  - id: sec3
    content: User.MustChangePassword + migration
    status: completed
  - id: sec4
    content: 'AuthController: mustChangePassword in login + change-password endpoint'
    status: completed
  - id: sec5
    content: 'Frontend: forced change-password flow on login'
    status: completed
  - id: sec6
    content: Integration tests + docs + build/test/web verify + commit
    status: in_progress
isProject: false
created_at: '2026-09-02T00:15:23'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_62
model: FW-Kimi-K3
mode_at_creation: auto
approved_mode: auto
content_hash: 2c57ecdc473de58f
approved_hash: 2c57ecdc473de58f
title: 'Security hardening: JWT secret + seed users'
---

# Security hardening: JWT secret + seed users

_Close the two remaining security gaps: JWT secret sourced from environment/secret store with startup validation, and seed users disabled outside development with forced password change._

## Current state (verified)
- `appsettings.json` has placeholder JWT secret; prod compose already injects `JWT_SECRET` env var
- DbSeeder runs only in Development (Program.cs:285 gate) but seeds `admin@gatus.local / Admin123!` — fine for dev, but no protection if someone runs with `ASPNETCORE_ENVIRONMENT=Development` in prod, and no forced password change
- No startup validation of JWT secret strength

## Changes

### 1. JWT secret handling
- **Program.cs startup guard**: on boot, validate `Jwt:Secret` — fail fast if missing, < 32 chars, or equals the known placeholder, UNLESS in Development (dev keeps the placeholder for zero-friction onboarding; log a warning instead)
- **appsettings.Development.json**: move dev JWT secret here so appsettings.json ships with NO secret at all
- **DEPLOYMENT.md**: document secret generation (`openssl rand -base64 48`) + `.env.production` setup (partially exists — verify)

### 2. Seed user hardening
- **DbSeeder**: read seed admin password from config `Seed:AdminPassword` (dev appsettings only); generate a random one and log it if not configured — no more hardcoded `Admin123!` in source
- **User entity**: add `MustChangePassword` flag (migration); seed admin gets `true`
- **AuthController**: login response includes `mustChangePassword`; add `POST /api/auth/change-password` endpoint (authenticated, validates current password, BCrypt-hashes new)
- **Frontend**: if login returns `mustChangePassword`, force a change-password screen before entering the app
- Optionally seed a Viewer user in dev for role testing

### 3. Tests
- Startup guard unit-ish test (or validate via integration test factory config)
- change-password endpoint integration tests (success, wrong current password, weak new password)
- Verify 22 existing tests still pass

## Files
- apps/api-server/Program.cs, appsettings.json, appsettings.Development.json
- src/Platform.Infrastructure/Persistence/DbSeeder.cs
- src/Platform.Domain/Entities/User.cs + UserConfiguration + new migration
- src/Platform.Api/Controllers/AuthController.cs
- apps/admin-web: login flow + new change-password page
- docs/DEPLOYMENT.md

## Verification
- Build, 22+ tests, web build, commit
- Quick manual: dev seed still works; login with seeded admin forces password change
