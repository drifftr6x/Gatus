---
schema_version: 1
name: Phase 0 — Repository Foundation
overview: 'Bootstrap the Project Sentinel Kiosk monorepo: .NET 10 solution skeleton, React/Vite
  admin shell, Docker Compose infrastructure, full Phase 0 documentation (architecture,
  threat model, DB schema, enrollment flow, policy model, command model, roadmap),
  and GitHub Actions CI. Working directory is empty — greenfield.'
todos:
  - id: t1
    content: Git init, branching, root config files (.gitignore, .editorconfig, global.json, Directory
  props, .env.example)
    status: pending
  - id: t2
    content: Create .NET solution + module class libraries + API host + test projects; verify build/test
    status: pending
  - id: t3
    content: Scaffold React/TS/Vite admin-web with Tailwind + app shell; verify build
    status: pending
  - id: t4
    content: Write infrastructure/compose.yaml + Dockerfiles
    status: pending
  - id: t5
    content: Write full Phase 0 docs (README, ARCHITECTURE, SECURITY, DEVELOPMENT, DEPLOYMENT,
  AGENT, API, CHANGELOG, ROADMAP, docs/ stubs)
    status: pending
  - id: t6
    content: Add GitHub Actions CI + security workflows
    status: pending
  - id: t7
    content: Final verification (build/test/web build/compose config) + commit on feature branch
    status: pending
isProject: false
created_at: '2026-08-26T21:18:05'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_5
model: FW-Kimi-K3
mode_at_creation: plan
content_hash: e4aa9f3d37e9a004
title: Phase 0 — Repository Foundation
---

# Phase 0 — Repository Foundation

_Bootstrap the Project Sentinel Kiosk monorepo: .NET 10 solution skeleton, React/Vite admin shell, Docker Compose infrastructure, full Phase 0 documentation (architecture, threat model, DB schema, enrollment flow, policy model, command model, roadmap), and GitHub Actions CI. Working directory is empty — greenfield._

## Context

Working directory `C:\...\GatUs` is empty — this is a greenfield bootstrap of the platform specified in the master build prompt. Architecture is settled (modular monolith, .NET 10 LTS server, PostgreSQL + Redis + MinIO infra, React/Vite admin, Windows agent deferred to Phase 2).

## Decisions carried from design

- Modular monolith; `src/` module class libraries + thin `apps/api-server` host — not premature microservices.
- .NET 10 LTS pinned via `global.json`; React + TypeScript + Vite + Tailwind + shadcn/ui for admin-web.
- Phase 0 scope: docs, repo structure, compilable solution, compose infra, CI. No kiosk UI, lockdown, or DB migrations (Phase 1).

## Steps

1. `git init` + branch setup (`main` → `develop` → `feature/repository-foundation`), `.gitignore`, `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.env.example`.
2. Create solution + projects: `apps/api-server` (ASP.NET Core), `src/Platform.{Domain,Application,Infrastructure,Api,Contracts,Shared,Security}`, test projects `tests/Platform.Domain.Tests` + `tests/Platform.Api.IntegrationTests`. Wire references, verify `dotnet build` and `dotnet test` succeed.
3. Scaffold `apps/admin-web` with Vite + React + TS + Tailwind + shadcn/ui primitives (app shell with nav per §45 placeholder layout); verify `npm run build`.
4. Write `infrastructure/compose.yaml` (postgres:16-alpine, redis:7-alpine, minio) + Dockerfiles for api/web (deferred wiring to Phase 1).
5. Write all documentation: README, ARCHITECTURE.md (diagrams, comms model), SECURITY.md (threat model), DEVELOPMENT.md, DEPLOYMENT.md, AGENT.md, API.md, CHANGELOG.md, ROADMAP.md + docs/ folder stubs.
6. Add `.github/workflows/ci.yml` (dotnet build/test, web build/lint) and `security.yml` (dependency scan).
7. Verify: `dotnet build`, `dotnet test`, `npm run build`; commit on feature branch with conventional commit.

## Acceptance criteria

```text
docker compose -f infrastructure/compose.yaml config   # validates
dotnet build KioskPlatform.sln                          # succeeds
dotnet test  KioskPlatform.sln                          # succeeds
npm --prefix apps/admin-web run build                   # succeeds
```
