# Deployment Guide

## Overview

Guitar Alchemist can be deployed in multiple environments: local development, Docker containers, Kubernetes, and cloud platforms.

## Local Development

### Prerequisites

- .NET 9 SDK
- Node.js 18+
- MongoDB
- Ollama

### Setup

```powershell
# Clone repository
git clone https://github.com/GuitarAlchemist/ga.git
cd ga

# Setup environment
pwsh Scripts/setup-dev-environment.ps1

# Start services
pwsh Scripts/start-all.ps1 -Dashboard
```

## Docker Deployment

### Build Docker Images

```bash
# Build all services
docker-compose build

# Build specific service
docker build -t ga-api:latest -f Apps/ga-server/GaApi/Dockerfile .
```

### Docker Compose

```yaml
version: '3.8'

services:
  mongodb:
    image: mongo:7.0
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db
    environment:
      MONGO_INITDB_ROOT_USERNAME: admin
      MONGO_INITDB_ROOT_PASSWORD: password

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama

  ga-api:
    build:
      context: .
      dockerfile: Apps/ga-server/GaApi/Dockerfile
    ports:
      - "7001:7001"
    depends_on:
      - mongodb
      - ollama
    environment:
      ConnectionStrings__MongoDB: "mongodb://admin:password@mongodb:27017"
      Ollama__BaseUrl: "http://ollama:11434"

  ga-client:
    build:
      context: .
      dockerfile: Apps/ga-client/Dockerfile
    ports:
      - "5173:5173"
    depends_on:
      - ga-api

volumes:
  mongodb_data:
  ollama_data:
```

### Run Docker Compose

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

## Kubernetes Deployment

### Prerequisites

- Kubernetes cluster (1.24+)
- kubectl configured
- Docker images pushed to registry

### Create Namespace

```bash
kubectl create namespace guitar-alchemist
```

### Deploy MongoDB

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mongodb
  namespace: guitar-alchemist
spec:
  replicas: 1
  selector:
    matchLabels:
      app: mongodb
  template:
    metadata:
      labels:
        app: mongodb
    spec:
      containers:
      - name: mongodb
        image: mongo:7.0
        ports:
        - containerPort: 27017
        volumeMounts:
        - name: mongodb-storage
          mountPath: /data/db
      volumes:
      - name: mongodb-storage
        persistentVolumeClaim:
          claimName: mongodb-pvc
```

### Deploy GA API

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ga-api
  namespace: guitar-alchemist
spec:
  replicas: 3
  selector:
    matchLabels:
      app: ga-api
  template:
    metadata:
      labels:
        app: ga-api
    spec:
      containers:
      - name: ga-api
        image: your-registry/ga-api:latest
        ports:
        - containerPort: 7001
        env:
        - name: ConnectionStrings__MongoDB
          valueFrom:
            secretKeyRef:
              name: ga-secrets
              key: mongodb-connection
        - name: Ollama__BaseUrl
          value: "http://ollama:11434"
        livenessProbe:
          httpGet:
            path: /health
            port: 7001
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 7001
          initialDelaySeconds: 10
          periodSeconds: 5
```

### Deploy Service

```yaml
apiVersion: v1
kind: Service
metadata:
  name: ga-api-service
  namespace: guitar-alchemist
spec:
  type: LoadBalancer
  selector:
    app: ga-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 7001
```

### Apply Kubernetes Manifests

```bash
# Create secrets
kubectl create secret generic ga-secrets \
  --from-literal=mongodb-connection="mongodb://..." \
  -n guitar-alchemist

# Apply manifests
kubectl apply -f k8s/mongodb.yaml
kubectl apply -f k8s/ga-api.yaml
kubectl apply -f k8s/ga-client.yaml

# Check deployment status
kubectl get deployments -n guitar-alchemist
kubectl get pods -n guitar-alchemist
```

## Cloud Deployment

### Azure Container Instances

```bash
# Build and push image
docker build -t ga-api:latest .
docker tag ga-api:latest myregistry.azurecr.io/ga-api:latest
docker push myregistry.azurecr.io/ga-api:latest

# Deploy to ACI
az container create \
  --resource-group myResourceGroup \
  --name ga-api \
  --image myregistry.azurecr.io/ga-api:latest \
  --ports 7001 \
  --environment-variables \
    ConnectionStrings__MongoDB="mongodb://..." \
    Ollama__BaseUrl="http://ollama:11434"
```

### AWS ECS

```bash
# Create ECR repository
aws ecr create-repository --repository-name ga-api

# Push image
docker tag ga-api:latest 123456789.dkr.ecr.us-east-1.amazonaws.com/ga-api:latest
docker push 123456789.dkr.ecr.us-east-1.amazonaws.com/ga-api:latest

# Create ECS task definition and service
# (Use AWS Console or CloudFormation)
```

## Environment Configuration

### Production Settings

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning"
    }
  },
  "ConnectionStrings": {
    "MongoDB": "mongodb+srv://user:pass@cluster.mongodb.net/guitar-alchemist"
  },
  "Embeddings": {
    "Endpoint": "http://ollama:11434",
    "ModelName": "nomic-embed-text"
  },
  "Ollama": {
    "BaseUrl": "http://ollama:11434",
    "ChatModel": "llama3.2:3b",
    "EmbeddingModel": "nomic-embed-text"
  },
  "Cors": {
    "AllowedOrigins": ["https://yourdomain.com"],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
    "AllowedHeaders": ["*"]
  }
}
```

## Health Checks

### Liveness Probe

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
```

### Readiness Probe

```csharp
app.MapGet("/ready", async (IHealthCheckService healthCheck) =>
{
    var result = await healthCheck.CheckHealthAsync();
    return result.Status == HealthStatus.Healthy 
        ? Results.Ok() 
        : Results.ServiceUnavailable();
});
```

## Monitoring

### Jaeger Tracing

```bash
# Deploy Jaeger
docker run -d \
  -p 16686:16686 \
  -p 14268:14268 \
  jaegertracing/all-in-one:latest

# Access at http://localhost:16686
```

### Prometheus Metrics

```bash
# Deploy Prometheus
docker run -d \
  -p 9090:9090 \
  -v prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus:latest
```

## Backup and Recovery

### MongoDB Backup

```bash
# Backup
mongodump --uri "mongodb://localhost:27017" --out ./backup

# Restore
mongorestore --uri "mongodb://localhost:27017" ./backup
```

### Database Snapshots

```bash
# Create snapshot
kubectl exec -n guitar-alchemist mongodb-pod -- \
  mongodump --out /backup

# Copy to local
kubectl cp guitar-alchemist/mongodb-pod:/backup ./backup
```

## Scaling

### Horizontal Scaling

```bash
# Scale API replicas
kubectl scale deployment ga-api --replicas=5 -n guitar-alchemist

# Auto-scaling
kubectl autoscale deployment ga-api \
  --min=2 --max=10 \
  --cpu-percent=80 \
  -n guitar-alchemist
```

## Troubleshooting

### Check Service Status

```bash
# Kubernetes
kubectl get pods -n guitar-alchemist
kubectl describe pod <pod-name> -n guitar-alchemist
kubectl logs <pod-name> -n guitar-alchemist

# Docker
docker ps
docker logs <container-id>
```

### Common Issues

- **Connection Timeout**: Check MongoDB and Ollama connectivity
- **Out of Memory**: Increase resource limits
- **High CPU**: Check for infinite loops or inefficient queries

