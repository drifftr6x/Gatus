# AGENT.md - AI Assistant Guidelines

## Project Context

This is an enterprise kiosk management platform built with:
- **Backend**: .NET 10 (C# 14) - Modular Monolith
- **Frontend**: React 19 + TypeScript + Vite + Tailwind CSS v4
- **Database**: PostgreSQL 16 with EF Core
- **Infrastructure**: Docker, Redis, MinIO

## Code Conventions

### C# / .NET
- Use file-scoped namespaces
- Prefer records for DTOs
- Use nullable reference types
- Follow Clean Architecture within modules
- Use MediatR for CQRS pattern

### TypeScript / React
- Use functional components with hooks
- Prefer named exports
- Use TypeScript strict mode
- Follow ESLint configuration
- Use Tailwind for styling (no CSS modules)

### File Naming
- C#: PascalCase (e.g., `DeviceService.cs`)
- TypeScript: kebab-case (e.g., `device-service.ts`)
- React components: PascalCase (e.g., `DeviceList.tsx`)

## Testing Requirements

- Write unit tests for domain logic
- Write integration tests for API endpoints
- Use xUnit for .NET tests
- Use Vitest for React tests

## Security Notes

- Never commit secrets
- Use user-secrets for local dev
- Validate all inputs
- Use parameterized queries

## Common Commands

```bash
# .NET
dotnet build
dotnet test
dotnet run --project src/Hosts/Kiosk.Api

# React
cd apps/admin-web
npm run dev
npm run build
npm test

# Docker
docker compose -f infrastructure/compose.yaml up -d
```

## Module Structure

Each module in `src/Modules/` follows:
```
ModuleName/
├── Domain/
├── Application/
├── Infrastructure/
└── Api/
```

## Important Files

- `global.json` - .NET SDK version
- `Directory.Build.props` - Shared MSBuild properties
- `Directory.Packages.props` - Central package management
