# Architecture

## Overview

Gatus Kiosk Platform follows a **Modular Monolith** architecture with clear module boundaries, enabling future extraction into microservices if needed.

## Module Structure

Each module follows Clean Architecture principles:

```
Module/
├── Domain/          # Entities, value objects, domain events
├── Application/     # Use cases, DTOs, interfaces
├── Infrastructure/  # EF Core, external services
└── Api/             # Controllers, endpoints
```

## Core Modules

| Module | Purpose |
|--------|---------|
| Identity | Authentication, authorization, tenant management |
| Devices | Kiosk device registration, provisioning, monitoring |
| Content | Media management, playlists, scheduling |
| Sync | Data synchronization between cloud and edge |
| Scheduling | Content playback schedules, campaigns |
| Telemetry | Metrics, logs, alerts from devices |
| Users | User management, roles, permissions |

## Technology Stack

- **.NET 10**: Latest framework with C# 14 features
- **ASP.NET Core**: Web API with minimal APIs and controllers
- **Entity Framework Core**: ORM with PostgreSQL provider
- **SignalR**: Real-time communication
- **React 19**: Frontend framework
- **Vite**: Build tool and dev server
- **Tailwind CSS v4**: Utility-first CSS

## Data Flow

1. **Command Flow**: API → Application → Domain → Infrastructure
2. **Query Flow**: API → Application → Infrastructure (read-optimized)
3. **Event Flow**: Domain events → MediatR → Handlers

## Security

- JWT-based authentication
- Role-based access control (RBAC)
- Tenant isolation
- API rate limiting
- Input validation

## Scalability

- Horizontal scaling via load balancer
- Redis for distributed caching
- PostgreSQL read replicas
- MinIO for object storage
