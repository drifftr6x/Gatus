---
schema_version: 1
name: phase-3-content-deployment
overview: 'Build the content deployment pipeline: upload content to server storage, create deployments
  targeting devices, verify checksums, stage atomically, and track deployment status
  with rollback capability.'
todos:
  - id: p3t1
    content: Create ContentVersion, Deployment, DeploymentResult entities + configurations
    status: completed
  - id: p3t2
    content: Create EF migration for deployment tables
    status: completed
  - id: p3t3
    content: Create ContentStorageService for file upload/download with SHA-256
    status: completed
  - id: p3t4
    content: Create DeploymentsController with CRUD + status tracking
    status: completed
  - id: p3t5
    content: Update ContentController for file upload + version listing
    status: completed
  - id: p3t6
    content: Create deployment DTOs in Contracts
    status: completed
  - id: p3t7
    content: 'Frontend: deployments page with create/list/cancel UI'
    status: completed
  - id: p3t8
    content: Integration tests for deployment workflow
    status: completed
  - id: p3t9
    content: 'Final verification: build, test, web build + commit'
    status: completed
isProject: false
created_at: '2026-08-28T10:11:39'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_67
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 7fce3e310506bdd0
title: phase-3-content-deployment
---

# phase-3-content-deployment

_Build the content deployment pipeline: upload content to server storage, create deployments targeting devices, verify checksums, stage atomically, and track deployment status with rollback capability._

## Phase 3: Content Deployment Pipeline

### Goal
Enable administrators to upload content to the server, create targeted deployments, and have agents download/verify/stage/activate content atomically — with full status tracking and rollback.

### Architecture

```text
Admin Console
     ↓
POST /api/content (upload file)
     ↓
Content stored in /storage/content/{id}/{version}/
     ↓
POST /api/deployments (target devices, schedule)
     ↓
Deployment records created (status: Pending)
     ↓
SignalR notification → Agent polls /api/deployments/{id}
     ↓
Agent downloads → SHA-256 verify → Stage → Activate → Report
```

### Database Changes
- `ContentVersion` table: immutable versions with SHA-256, file path, size
- `Deployment` table: target devices, content version, status, schedule
- `DeploymentResult` table: per-device status, timestamps, error details

### API Endpoints
- `POST /api/content/upload` — multipart file upload, store to disk, return metadata
- `GET /api/content/{id}/versions` — list content versions
- `POST /api/deployments` — create deployment job
- `GET /api/deployments` — list deployments with status
- `GET /api/deployments/{id}/results` — per-device results
- `POST /api/deployments/{id}/cancel` — cancel pending deployment

### Agent Contract (for Phase 4)
- Agent polls or receives SignalR push for new deployments
- Downloads to staging dir, verifies SHA-256, activates atomically
- Reports status back to server

### Files to Create
- `src/Platform.Domain/Entities/ContentVersion.cs`, `Deployment.cs`, `DeploymentResult.cs`
- `src/Platform.Contracts/Requests/DeploymentRequests.cs`
- `src/Platform.Contracts/Responses/DeploymentResponses.cs`
- `src/Platform.Api/Controllers/DeploymentsController.cs`
- `src/Platform.Api/Services/ContentStorageService.cs`
- `src/Platform.Infrastructure/Persistence/Configurations/ContentVersionConfiguration.cs` (etc.)
- `apps/admin-web/src/pages/deployments.tsx` — deployment management UI

### Acceptance Criteria
- [ ] Upload file → stored with SHA-256 checksum
- [ ] Create deployment targeting specific devices
- [ ] Deployment status tracked (Pending → Running → Succeeded/Failed)
- [ ] Content versions listed with metadata
- [ ] API returns proper errors for invalid requests
- [ ] All tests pass
