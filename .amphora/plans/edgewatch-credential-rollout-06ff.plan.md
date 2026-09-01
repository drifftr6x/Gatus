---
schema_version: 1
name: edgewatch-credential-rollout
overview: Apply and validate the device-secret schema migration, make the agent handle revoked
todos:
  []
isProject: true
created_at: '2026-08-31T21:03:50'
session_id: sess_60616e1870d76f6b
tool_use_id: call_nrigA7ykieZG37TqkHM9EOWq
model: gpt-5.6-luna
mode_at_creation: auto
dismissed: true
content_hash: 7d50f086441d98d6
title: edgewatch-credential-rollout
---

# edgewatch-credential-rollout

_Apply and validate the device-secret schema migration, make the agent handle revoked_

## Critical finding

The security code is compiled, but the active PostgreSQL database does **not** yet contain `device_secret_hash`, `device_secret_issued_at`, or `device_secret_revoked_at`. The migration file exists in source, but the local database migration history has not applied it. Existing enrolled agents also have no explicit 401/403 recovery path.

## Steps

1. Add the device-secret migration to the active schema safely (`IF NOT EXISTS`) and verify `__EFMigrationsHistory` plus column presence. Keep the migration source tracked.
2. Add agent authentication failure handling: on heartbeat/policy/command/deployment 401/403, log a clear credential-revoked state, stop retry storms, preserve cached policy/content, and expose re-enrollment instructions. Do not automatically wipe credentials or repeatedly generate enrollment requests.
3. Add an explicit secure re-enrollment procedure to the installer/agent documentation and ensure a newly issued token replaces the old DPAPI credential only after successful enrollment.
4. Add integration tests for valid secret, missing secret, invalid secret, revoked/inactive device, and cross-device mismatch across heartbeat/telemetry/command/deployment paths.
5. Re-enroll the real laptop agent with a fresh token, verify heartbeat 200 and online status, then verify telemetry, policy, command poll, and deployment polling.
6. Run full solution build/tests and admin web build; update EdgeWatch Lite security documentation and commit.

## Acceptance criteria

- Database contains all device credential columns and records migration application.
- New enrollment returns a secret whose hash is stored, never plaintext.
- Invalid/missing/revoked credentials receive 401/403.
- Agent enters an explicit `CredentialRejected`/offline state without destructive local reset.
- Freshly re-enrolled real agent heartbeats successfully.
- Full .NET tests/build and frontend build pass.
