# Development Guide

## Prerequisites

- .NET 10 SDK (preview)
- Node.js 22+
- Docker Desktop
- Visual Studio 2022 or VS Code

## Setup

1. Clone repository
2. Copy `.env.example` to `.env`
3. Start infrastructure: `docker compose -f infrastructure/compose.yaml up -d`
4. Run migrations: `dotnet ef database update`
5. Start API: `dotnet run --project src/Hosts/Kiosk.Api`
6. Start web: `cd apps/admin-web && npm run dev`

## Code Style

- C#: Follow Microsoft conventions, use `dotnet format`
- TypeScript: ESLint + Prettier
- Commit messages: Conventional Commits

## Testing

```bash
# Backend tests
dotnet test

# Frontend tests
cd apps/admin-web && npm test
```

## Debugging

- API: Attach to `Kiosk.Api` process
- Web: Chrome DevTools with source maps
