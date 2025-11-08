# Guitar Alchemist - Docker Deployment

## Overview

This guide covers deploying Guitar Alchemist using Docker Compose for production environments.

## Quick Start

### Start All Services

```bash
# Build and start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

## Services

The Docker Compose configuration includes:

| Service | Port | Description |
|---------|------|-------------|
| **mongodb** | 27017 | MongoDB database |
| **mongo-express** | 8081 | MongoDB management UI |
| **gaapi** | 7001 | Main REST API server |
| **chatbot** | 7002 | Blazor chatbot application |
| **ga-client** | 5173 | React frontend |
| **jaeger** | 16686 | Distributed tracing UI (optional) |

## Prerequisites

- Docker Desktop 4.0+
- Docker Compose 2.0+
- 4GB RAM minimum
- 10GB disk space

## Configuration

### Environment Variables

Create a `.env` file in the repository root:

```env
# MongoDB
MONGO_INITDB_DATABASE=guitar-alchemist

# GaApi
ASPNETCORE_ENVIRONMENT=Production
OPENAI_API_KEY=your-api-key-here

# Chatbot
CHATBOT_ENVIRONMENT=Production

# React Frontend
VITE_API_URL=http://localhost:7001
```

### Secrets Management

For production, use Docker secrets:

```bash
# Create secrets
echo "your-openai-key" | docker secret create openai_api_key -
echo "your-mongo-password" | docker secret create mongo_password -

# Update docker-compose.yml to use secrets
```

## Building Images

### Build All Images

```bash
docker-compose build
```

### Build Individual Services

```bash
# Build GaApi
docker-compose build gaapi

# Build Chatbot
docker-compose build chatbot

# Build React Frontend
docker-compose build ga-client
```

## Running Services

### Start All Services

```bash
# Start in detached mode
docker-compose up -d

# Start with build
docker-compose up -d --build

# Start specific services
docker-compose up -d mongodb gaapi
```

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f gaapi

# Last 100 lines
docker-compose logs --tail=100 gaapi
```

### Stop Services

```bash
# Stop all services
docker-compose stop

# Stop specific service
docker-compose stop gaapi

# Stop and remove containers
docker-compose down

# Stop and remove containers + volumes
docker-compose down -v
```

## Health Checks

### Check Service Status

```bash
# View running containers
docker-compose ps

# Check health status
docker-compose ps --format json | jq '.[].Health'
```

### Manual Health Checks

```bash
# GaApi
curl http://localhost:7001/health

# MongoDB
docker exec ga-mongodb mongosh --eval "db.adminCommand('ping')"

# Chatbot
curl http://localhost:7002

# React Frontend
curl http://localhost:5173
```

## Monitoring

### Resource Usage

```bash
# View resource usage
docker stats

# View specific service
docker stats ga-api
```

### Logs

```bash
# Follow logs
docker-compose logs -f

# Export logs
docker-compose logs > logs.txt
```

### Distributed Tracing

Access Jaeger UI at http://localhost:16686

## Scaling

### Scale Services

```bash
# Scale GaApi to 3 instances
docker-compose up -d --scale gaapi=3

# Scale Chatbot to 2 instances
docker-compose up -d --scale chatbot=2
```

**Note:** You'll need to configure a load balancer (nginx, traefik) for multiple instances.

## Backup and Restore

### Backup MongoDB

```bash
# Create backup
docker exec ga-mongodb mongodump --out /backup

# Copy backup to host
docker cp ga-mongodb:/backup ./mongodb-backup
```

### Restore MongoDB

```bash
# Copy backup to container
docker cp ./mongodb-backup ga-mongodb:/backup

# Restore
docker exec ga-mongodb mongorestore /backup
```

## Troubleshooting

### Container Won't Start

```bash
# View logs
docker-compose logs <service-name>

# Inspect container
docker inspect <container-name>

# Check resource usage
docker stats
```

### Port Conflicts

```bash
# Find process using port
netstat -ano | findstr :7001  # Windows
lsof -i :7001                 # Linux/Mac

# Change port in docker-compose.yml
ports:
  - "7002:8080"  # Changed from 7001
```

### MongoDB Connection Issues

```bash
# Check MongoDB logs
docker-compose logs mongodb

# Test connection
docker exec ga-mongodb mongosh --eval "db.adminCommand('ping')"

# Restart MongoDB
docker-compose restart mongodb
```

### Out of Disk Space

```bash
# Remove unused images
docker image prune -a

# Remove unused volumes
docker volume prune

# Remove everything
docker system prune -a --volumes
```

## Production Deployment

### Security Hardening

1. **Use secrets for sensitive data**
   ```yaml
   secrets:
     openai_api_key:
       external: true
   ```

2. **Enable HTTPS**
   - Use reverse proxy (nginx, traefik)
   - Configure SSL certificates

3. **Limit resource usage**
   ```yaml
   deploy:
     resources:
       limits:
         cpus: '2'
         memory: 2G
   ```

4. **Use non-root users**
   ```dockerfile
   USER appuser
   ```

### High Availability

1. **Use Docker Swarm or Kubernetes**
2. **Configure health checks**
3. **Set up load balancing**
4. **Enable auto-restart**
   ```yaml
   restart: unless-stopped
   ```

### Monitoring

1. **Prometheus + Grafana**
   - Add to docker-compose.yml
   - Configure metrics endpoints

2. **ELK Stack**
   - Centralized logging
   - Log aggregation

3. **Jaeger**
   - Already included
   - Distributed tracing

## CI/CD Integration

### GitHub Actions

```yaml
- name: Build and push Docker images
  run: |
    docker-compose build
    docker-compose push
```

### Azure DevOps

```yaml
- task: DockerCompose@0
  inputs:
    action: 'Run services'
    dockerComposeFile: 'docker-compose.yml'
```

## Performance Optimization

### Image Size

```bash
# Use multi-stage builds (already implemented)
# Use alpine base images where possible
# Remove unnecessary files
```

### Build Cache

```bash
# Use BuildKit
DOCKER_BUILDKIT=1 docker-compose build

# Cache from registry
docker-compose build --build-arg BUILDKIT_INLINE_CACHE=1
```

### Network Performance

```bash
# Use host network for better performance (Linux only)
network_mode: "host"
```

## Updating Services

### Rolling Updates

```bash
# Pull latest images
docker-compose pull

# Restart services one by one
docker-compose up -d --no-deps --build gaapi
docker-compose up -d --no-deps --build chatbot
docker-compose up -d --no-deps --build ga-client
```

### Zero-Downtime Deployment

1. Use Docker Swarm or Kubernetes
2. Configure rolling updates
3. Set up health checks
4. Use load balancer

## Cleanup

### Remove All Containers

```bash
# Stop and remove
docker-compose down

# Remove volumes too
docker-compose down -v
```

### Remove Images

```bash
# Remove project images
docker-compose down --rmi all

# Remove all unused images
docker image prune -a
```

## Support

### Common Issues

1. **Port conflicts** - Change ports in docker-compose.yml
2. **Out of memory** - Increase Docker Desktop memory limit
3. **Slow builds** - Use BuildKit and build cache

### Getting Help

1. Check logs: `docker-compose logs`
2. Check health: `docker-compose ps`
3. Inspect containers: `docker inspect <container>`

## Next Steps

1. **Configure SSL/TLS** for production
2. **Set up monitoring** (Prometheus, Grafana)
3. **Configure backups** (automated MongoDB backups)
4. **Set up CI/CD** (automated deployments)
5. **Load testing** (verify performance)

## Related Documentation

- [Start Services](Scripts/START_SERVICES_README.md) - Development with Aspire
- [Testing](Scripts/TEST_SUITE_README.md) - Running tests
- [CI/CD](.github/workflows/ci.yml) - GitHub Actions workflow

