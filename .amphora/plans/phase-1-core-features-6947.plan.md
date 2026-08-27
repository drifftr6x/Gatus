---
schema_version: 1
name: phase-1-core-features
overview: 'Implement Phase 1: Core Features including database schema, authentication, and CRUD
  APIs for devices, content, and users.'
todos:
  - id: t1
    content: Create EF Core entities (Device, Content, User, Schedule) in Domain layer
    status: completed
  - id: t2
    content: Create ApplicationDbContext and entity configurations in Infrastructure
    status: completed
  - id: t3
    content: Add EF Core packages and generate initial migration
    status: completed
  - id: t4
    content: Implement JWT authentication service in Platform.Security
    status: completed
  - id: t5
    content: Create AuthController with login/refresh endpoints
    status: completed
  - id: t6
    content: Create DevicesController with CRUD operations
    status: completed
  - id: t7
    content: Create ContentController with CRUD + file upload
    status: completed
  - id: t8
    content: Create UsersController with CRUD + role management
    status: completed
  - id: t9
    content: Create DTOs and validators in Platform.Contracts
    status: completed
  - id: t10
    content: Update Program.cs with auth, Swagger, and middleware
    status: completed
  - id: t11
    content: Create API client and auth context in React frontend
    status: completed
  - id: t12
    content: Update React pages with real CRUD operations
    status: completed
  - id: t13
    content: Add integration tests for API endpoints
    status: completed
  - id: t14
    content: 'Final verification: build, test, migration, web build'
    status: in_progress
isProject: false
created_at: '2026-08-27T06:46:28'
session_id: sess_60616e1870d76f6b
tool_use_id: create_plan_55
model: FW-Kimi-K3
mode_at_creation: auto
content_hash: fedab52a3305a41b
title: phase-1-core-features
---

# phase-1-core-features

_Implement Phase 1: Core Features including database schema, authentication, and CRUD APIs for devices, content, and users._

## Phase 1: Core Features

### Goals
1. **Database Schema** - EF Core entities, DbContext, migrations for all modules
2. **Authentication** - JWT-based auth with refresh tokens
3. **Device Management API** - CRUD + status updates
4. **Content Management API** - CRUD + file upload
5. **User Management API** - CRUD + role assignment

### Technical Approach

#### 1. Database Schema (EF Core)
- Create entities in `Platform.Domain`:
  - `Device` - id, name, serial, status, lastSeen, location
  - `Content` - id, name, type, url, size, duration
  - `User` - id, email, name, role, passwordHash
  - `Schedule` - id, deviceId, contentId, startTime, endTime
- Create `ApplicationDbContext` in `Platform.Infrastructure`
- Configure PostgreSQL connection
- Add initial migration

#### 2. Authentication
- JWT token generation/validation in `Platform.Security`
- Login/refresh endpoints in `Platform.Api`
- Password hashing with BCrypt
- Role-based authorization policies

#### 3. API Controllers
- RESTful controllers in `Platform.Api`
- DTOs in `Platform.Contracts`
- FluentValidation validators
- Error handling middleware

#### 4. Frontend Integration
- API client with React Query
- Auth context with token storage
- Protected routes
- CRUD pages for devices, content, users

### Files to Create/Modify

**Domain Layer:**
- `src/Platform.Domain/Entities/Device.cs`
- `src/Platform.Domain/Entities/Content.cs`
- `src/Platform.Domain/Entities/User.cs`
- `src/Platform.Domain/Entities/Schedule.cs`

**Infrastructure:**
- `src/Platform.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/Platform.Infrastructure/Persistence/Configurations/*.cs`
- `src/Platform.Infrastructure/Migrations/*.cs`

**API:**
- `src/Platform.Api/Controllers/AuthController.cs`
- `src/Platform.Api/Controllers/DevicesController.cs`
- `src/Platform.Api/Controllers/ContentController.cs`
- `src/Platform.Api/Controllers/UsersController.cs`

**Contracts:**
- `src/Platform.Contracts/Requests/*.cs`
- `src/Platform.Contracts/Responses/*.cs`

**Frontend:**
- `apps/admin-web/src/lib/api.ts`
- `apps/admin-web/src/hooks/useAuth.ts`
- `apps/admin-web/src/pages/devices.tsx` (update)
- `apps/admin-web/src/pages/content.tsx` (update)

### Verification
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] `dotnet ef database update` works
- [ ] `npm run build` succeeds
- [ ] API endpoints respond correctly
