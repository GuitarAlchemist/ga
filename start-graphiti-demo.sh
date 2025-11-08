#!/bin/bash

# Guitar Alchemist × Graphiti Demo Startup Script
# This script starts all necessary services for the Graphiti integration demo

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🎸 Guitar Alchemist × Graphiti Demo Startup${NC}"
echo "=========================================="

# Function to print colored output
print_step() {
    echo -e "${BLUE}🚀 $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Check prerequisites
print_step "Checking prerequisites..."

if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed or not in PATH"
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose is not installed or not in PATH"
    exit 1
fi

print_success "Docker and Docker Compose found!"

# Check if Ollama is running (optional)
print_step "Checking Ollama setup..."
if command -v ollama &> /dev/null; then
    if ollama list 2>/dev/null | grep -q "qwen2.5-coder:1.5b-base" && ollama list 2>/dev/null | grep -q "nomic-embed-text"; then
        print_success "Ollama models are available!"
    else
        print_warning "Required Ollama models not found. Please run:"
        echo "  ollama pull qwen2.5-coder:1.5b-base"
        echo "  ollama pull nomic-embed-text"
    fi
else
    print_warning "Ollama not found. Please install Ollama and pull required models."
fi

# Start services with Docker Compose
print_step "Starting services with Docker Compose..."
echo "This may take a few minutes on first run..."

# Stop any existing services
docker-compose down 2>/dev/null || true

# Start the services
if docker-compose up -d; then
    print_success "Services started successfully!"
else
    print_error "Failed to start services!"
    exit 1
fi

# Wait for services to be ready
print_step "Waiting for services to be ready..."

# Function to wait for a service
wait_for_service() {
    local service_name=$1
    local url=$2
    local timeout=${3:-60}
    
    echo -n "Waiting for $service_name"
    local count=0
    while [ $count -lt $timeout ]; do
        if curl -s -f "$url" > /dev/null 2>&1; then
            echo ""
            print_success "$service_name is ready!"
            return 0
        fi
        echo -n "."
        sleep 2
        count=$((count + 2))
    done
    echo ""
    print_error "$service_name failed to start within $timeout seconds"
    return 1
}

# Wait for FalkorDB (Redis protocol)
echo -n "Waiting for FalkorDB"
count=0
while [ $count -lt 30 ]; do
    if docker-compose exec -T falkordb redis-cli ping 2>/dev/null | grep -q "PONG"; then
        echo ""
        print_success "FalkorDB is ready!"
        break
    fi
    echo -n "."
    sleep 2
    count=$((count + 2))
done

if [ $count -ge 30 ]; then
    print_error "FalkorDB failed to start"
    exit 1
fi

# Wait for Graphiti service
if ! wait_for_service "Graphiti Service" "http://localhost:8000/health" 60; then
    print_error "Graphiti service failed to start"
    docker-compose logs graphiti-service
    exit 1
fi

# Wait for GA API
if ! wait_for_service "GA API" "http://localhost:7001/health" 60; then
    print_warning "GA API not ready, but continuing..."
fi

# Wait for React frontend
if ! wait_for_service "React Frontend" "http://localhost:5173" 60; then
    print_warning "React frontend not ready, but continuing..."
fi

# Display service URLs
print_success "Demo is ready! 🎉"
echo ""
echo -e "${GREEN}📊 Service URLs:${NC}"
echo "================"
echo "• FalkorDB Browser:    http://localhost:3000"
echo "• Graphiti API:        http://localhost:8000"
echo "• Graphiti API Docs:   http://localhost:8000/docs"
echo "• GA API:              http://localhost:7001"
echo "• GA API Swagger:      http://localhost:7001/swagger"
echo "• React Frontend:      http://localhost:5173"
echo "• Graphiti Demo:       http://localhost:5173/test/graphiti-demo"
echo ""
echo -e "${GREEN}🧪 Quick Test Commands:${NC}"
echo "======================="
echo "# Test Graphiti health"
echo "curl http://localhost:8000/health"
echo ""
echo "# Add a practice episode"
echo 'curl -X POST http://localhost:8000/episodes \'
echo '  -H "Content-Type: application/json" \'
echo '  -d '"'"'{"user_id":"demo-user","episode_type":"practice","content":{"chord_practiced":"Cmaj7","duration_minutes":15,"accuracy":0.85}}'"'"
echo ""
echo "# Search the knowledge graph"
echo 'curl -X POST http://localhost:8000/search \'
echo '  -H "Content-Type: application/json" \'
echo '  -d '"'"'{"query":"jazz chords","search_type":"hybrid","limit":5}'"'"
echo ""
echo -e "${BLUE}🎸 Happy learning with temporal knowledge graphs!${NC}"
echo ""
echo "Press Ctrl+C to stop all services"

# Keep the script running and show logs
trap 'echo -e "\n${YELLOW}Stopping services...${NC}"; docker-compose down; exit 0' INT

# Follow logs from all services
echo -e "${BLUE}📋 Following service logs (Press Ctrl+C to stop):${NC}"
docker-compose logs -f
