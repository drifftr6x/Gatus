---
schema_version: 1
name: phase-7-content-library
overview: Enhance content management with versioning UI, rollback capability, deployment history,
  and bulk operations.
todos:
  - id: p7t1
    content: 'API: content version detail + rollback endpoints'
    status: completed
  - id: p7t2
    content: 'API: deployment history + bulk deployment endpoints'
    status: completed
  - id: p7t3
    content: 'API: content export (CSV) endpoint'
    status: completed
  - id: p7t4
    content: 'Frontend: version history page with diff view'
    status: completed
  - id: p7t5
    content: 'Frontend: deployment history timeline with filters'
    status: completed
  - id: p7t6
    content: 'Frontend: content preview modal (image/PDF/HTML)'
    status: completed
  - id: p7t7
    content: 'Frontend: bulk deploy modal with device group selection'
    status: completed
  - id: p7t8
    content: Integration tests for versioning + bulk operations
    status: completed
  - id: p7t9
    content: 'Final verification: build, test, web build + commit'
    status: completed
isProject: false
created_at: '2026-08-28T11:04:31'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_160
model: FW-Kimi-K3
mode_at_creation: auto
dismissed: true
content_hash: 3066eb58b26f412f
title: phase-7-content-library
---

# phase-7-content-library

_Enhance content management with versioning UI, rollback capability, deployment history, and bulk operations._

## Phase 7: Content Library Enhancements

### Goal
Build on the content deployment pipeline (Phase 3) with a full content library UI: version history, rollback, deployment tracking, and bulk operations.

### Features

1. **Content Versioning UI**
   - Version history per content item
   - Compare versions (metadata diff)
   - Set active version
   - Rollback to previous version

2. **Deployment History**
   - Timeline view of all deployments
   - Filter by device, content, status, date range
   - Deployment detail view with per-device results

3. **Bulk Operations**
   - Deploy to multiple devices/groups
   - Bulk delete/archive content
   - Export content metadata (CSV)

4. **Content Preview**
   - Image/video thumbnail preview
   - PDF viewer integration
   - HTML content preview

### API Enhancements
- `GET /api/content/{id}/versions/{versionId}` — version detail
- `POST /api/content/{id}/rollback` — rollback to version
- `GET /api/deployments/history` — deployment timeline
- `POST /api/deployments/bulk` — bulk deployment
- `GET /api/content/export` — CSV export

### Frontend
- `pages/content/[id]/versions.tsx` — version history
- `pages/deployments/history.tsx` — deployment timeline
- `components/content-preview.tsx` — preview modal
- `components/bulk-deploy-modal.tsx` — bulk deployment UI

### Acceptance Criteria
- [ ] Version history visible with metadata
- [ ] Rollback restores previous version
- [ ] Deployment history shows timeline with filters
- [ ] Bulk deploy to device groups
- [ ] Content preview for images/PDFs
- [ ] All tests pass
