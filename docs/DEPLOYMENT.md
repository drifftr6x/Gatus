# Deployment

## Environments

| Environment | Purpose | URL |
|-------------|---------|-----|
| Development | Local dev | localhost |
| Staging | Pre-production | staging.example.com |
| Production | Live | app.example.com |

## Docker Deployment

```bash
# Build images
docker compose -f infrastructure/compose.yaml build

# Deploy
docker compose -f infrastructure/compose.yaml up -d
```

## Kubernetes (Future)

Helm charts will be provided for K8s deployment.

## CI/CD

GitHub Actions workflows:
- `ci.yml`: Build and test on PR
- `deploy.yml`: Deploy to staging/production
