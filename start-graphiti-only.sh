#!/bin/bash

# Guitar Alchemist × Graphiti Demo - Core Services Only
# This script starts just the essential Graphiti services for testing

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🎸 Guitar Alchemist × Graphiti Core Services${NC}"
echo "============================================="

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

# Stop any existing services
print_step "Stopping any existing services..."
docker-compose down 2>/dev/null || true

# Start only the core Graphiti services
print_step "Starting core Graphiti services..."
echo "Starting FalkorDB and Graphiti service..."

if docker-compose up -d falkordb graphiti-service; then
    print_success "Core services started successfully!"
else
    print_error "Failed to start core services!"
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

# Display service URLs
print_success "Core Graphiti services are ready! 🎉"
echo ""
echo -e "${GREEN}📊 Available Services:${NC}"
echo "====================="
echo "• FalkorDB Browser:    http://localhost:3000"
echo "• Graphiti API:        http://localhost:8000"
echo "• Graphiti API Docs:   http://localhost:8000/docs"
echo ""
echo -e "${GREEN}🧪 Test the Graphiti API:${NC}"
echo "========================="
echo ""
echo "1. Test health:"
echo "   curl http://localhost:8000/health"
echo ""
echo "2. Add a practice episode:"
echo '   curl -X POST http://localhost:8000/episodes \'
echo '     -H "Content-Type: application/json" \'
echo '     -d '"'"'{"user_id":"demo-user","episode_type":"practice","content":{"chord_practiced":"Cmaj7","duration_minutes":15,"accuracy":0.85}}'"'"
echo ""
echo "3. Search the knowledge graph:"
echo '   curl -X POST http://localhost:8000/search \'
echo '     -H "Content-Type: application/json" \'
echo '     -d '"'"'{"query":"jazz chords","search_type":"hybrid","limit":5}'"'"
echo ""
echo "4. Get recommendations:"
echo '   curl -X POST http://localhost:8000/recommendations \'
echo '     -H "Content-Type: application/json" \'
echo '     -d '"'"'{"user_id":"demo-user","recommendation_type":"next_chord"}'"'"
echo ""
echo -e "${BLUE}🎸 Ready to test temporal knowledge graphs!${NC}"
echo ""
echo "Press Ctrl+C to stop services"

# Keep the script running and show logs
trap 'echo -e "\n${YELLOW}Stopping services...${NC}"; docker-compose stop falkordb graphiti-service; exit 0' INT

# Follow logs from Graphiti services
echo -e "${BLUE}📋 Following Graphiti service logs (Press Ctrl+C to stop):${NC}"
docker-compose logs -f falkordb graphiti-service
