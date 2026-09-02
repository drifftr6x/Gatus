---
schema_version: 1
name: Postgres backup + restore procedures
overview: Automated scheduled backups with retention for both dev and production compose stacks,
  a verified restore procedure, and an operations runbook.
todos:
  - id: bk1
    content: backup-postgres.ps1 with retention + integrity check
    status: completed
  - id: bk2
    content: restore-postgres.ps1 with pre-restore snapshot + verification
    status: completed
  - id: bk3
    content: backup-appdata.ps1 (content, agent-updates, signing keys)
    status: completed
  - id: bk4
    content: register-backup-task.ps1 (Task Scheduler daily job)
    status: completed
  - id: bk5
    content: docs/BACKUP-RESTORE.md runbook + DEPLOYMENT.md link
    status: completed
  - id: bk6
    content: 'Live drill: backup dev DB, restore to scratch DB, verify, commit'
    status: in_progress
isProject: false
created_at: '2026-09-02T07:21:35'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_240
model: FW-Kimi-K3
mode_at_creation: auto
content_hash: 0d6c7e0b3b7807d1
title: Postgres backup + restore procedures
---

# Postgres backup + restore procedures

_Automated scheduled backups with retention for both dev and production compose stacks, a verified restore procedure, and an operations runbook._

## Current state (verified)
- Postgres 16 in Docker (`kiosk-postgres` dev, `gatus-postgres` prod), named volume `postgres-data`, no backups at all beyond a manual one-liner in DEPLOYMENT.md
- No backup scripts, no restore docs, no volume snapshot story
- Also un-backed-up: `AppData/` (content packages, agent updates, signing keys!) — the signing private key loss means no agent can ever verify new packages

## Changes

### 1. backup-postgres.ps1 (infrastructure/scripts)
- `pg_dump -Fc` (custom format, compressed) inside the container → stream out to dated file `backups/kiosk-YYYYMMDD-HHMMSS.dump`
- Parameters: `-ContainerName` (default kiosk-postgres), `-Database`, `-User`, `-OutDir`, `-RetentionDays` (default 30) with automatic prune of older dumps
- Verifies dump integrity after write (`pg_restore --list`)
- Exit codes + logging suitable for Task Scheduler

### 2. restore-postgres.ps1
- Takes a dump file → safety: requires `-Force` if target DB non-empty; drops+recreates schema via `pg_restore --clean --if-exists`
- Pre-restore snapshot (dumps current DB to a `pre-restore-*.dump` first) so a bad restore is itself recoverable
- Post-check: table count + `__EFMigrationsHistory` latest migration printed

### 3. backup-appdata.ps1
- Tar/zip the API `AppData` dir (content packages, agent-updates, **signing keys**) alongside DB dumps — a DB restore without matching content files leaves deployments pointing at missing packages
- Same retention policy; note in docs that DB + AppData should be backed up in the same window

### 4. Scheduled task registration
- `register-backup-task.ps1`: creates a Windows Task Scheduler daily job calling backup-postgres.ps1 + backup-appdata.ps1 (dev box convenience; prod uses cron/whatever the host provides — document both)

### 5. Docs: docs/BACKUP-RESTORE.md (runbook)
- Backup schedule recommendation, what each artifact contains, off-host copy guidance
- **Restore drill**: step-by-step full recovery onto a fresh machine (compose up → restore dump → restore AppData → verify login + devices page)
- Recovery time/point expectations, signing-key loss scenario called out explicitly
- Update DEPLOYMENT.md database section to point at the runbook

### 6. Verification (live drill)
- Run backup against dev container, verify dump file + `pg_restore --list`
- Restore into a scratch database (`kiosk-restore-test`), verify table counts + migration history, drop it
- Commit

## Files
- New: infrastructure/scripts/{backup-postgres,restore-postgres,backup-appdata,register-backup-task}.ps1, docs/BACKUP-RESTORE.md
- Edit: docs/DEPLOYMENT.md (link runbook)

## Out of scope
- Off-site/cloud upload (S3 etc.) — documented as a follow-up; scripts take -OutDir so any sync tool can pick up the folder
- Continuous WAL archiving / PITR — overkill for current scale; noted as future option
