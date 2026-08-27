# Sentinel Kiosk Platform

Enterprise-grade, modular kiosk management platform built with .NET 10 and React.

## Overview

Sentinel Kiosk Platform provides centralized management for distributed kiosk fleets, including device provisioning, content scheduling, user access control, and real-time telemetry.

## Architecture

- **Backend**: .NET 10 (C# 14) with Modular Monolith architecture
- **Frontend**: React 19 + TypeScript + Vite + Tailwind CSS v4
- **Database**: PostgreSQL 16 with Entity Framework Core
- **Cache**: Redis 7
- **Storage**: MinIO (S3-compatible)
- **API**: RESTful + SignalR for real-time updates

## Quick Start

### Prerequisites

- .NET 10 SDK (preview)
- Node.js 22+
- Docker & Docker Compose
- PostgreSQL 16 (or use Docker)

### Development

```bash
# Start infrastructure
docker compose -f infrastructure/compose.yaml up -d

# Run API
dotnet run --project src/Hosts/Kiosk.Api

# Run Admin Web (in another terminal)
cd apps/admin-web
npm install
npm run dev
```

### Building

```bash
# Backend
dotnet build

# Frontend
cd apps/admin-web && npm run build
```

## Project Structure

```
├── src/
│   ├── Hosts/           # API hosts
│   └── Modules/         # Feature modules
├── tests/               # Test projects
├── apps/
│   └── admin-web/       # React admin dashboard
├── infrastructure/      # Docker, compose, configs
└── docs/                # Documentation
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Security](docs/SECURITY.md)
- [Development Guide](docs/DEVELOPMENT.md)
- [Deployment](docs/DEPLOYMENT.md)
- [API Reference](docs/API.md)

## License

Proprietary - All rights reserved
