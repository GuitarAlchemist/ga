"""
Guitar Alchemist Graphiti Service
FastAPI application for temporal knowledge graph operations
"""
import os
import logging
from contextlib import asynccontextmanager
from typing import Dict, Any

from fastapi import FastAPI, HTTPException, Depends
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from dotenv import load_dotenv

from services.graphiti_service import GraphitiMusicService
from models.music_theory import EpisodeRequest, SearchRequest, RecommendationRequest

# Load environment variables
load_dotenv()

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Global service instance
graphiti_service: GraphitiMusicService = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager"""
    global graphiti_service
    
    # Startup
    logger.info("Starting Guitar Alchemist Graphiti Service...")
    try:
        graphiti_service = GraphitiMusicService()
        await graphiti_service.initialize_schema()
        logger.info("Graphiti service initialized successfully")
    except Exception as e:
        logger.error(f"Failed to initialize service: {e}")
        raise
    
    yield
    
    # Shutdown
    logger.info("Shutting down Graphiti service...")


# Create FastAPI app
app = FastAPI(
    title="Guitar Alchemist Graphiti Service",
    description="Temporal knowledge graph service for music learning",
    version="1.0.0",
    lifespan=lifespan
)

# Configure CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure appropriately for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def get_graphiti_service() -> GraphitiMusicService:
    """Dependency to get the Graphiti service instance"""
    if graphiti_service is None:
        raise HTTPException(status_code=503, detail="Service not initialized")
    return graphiti_service


@app.get("/")
async def root():
    """Root endpoint"""
    return {
        "message": "Guitar Alchemist Graphiti Service",
        "version": "1.0.0",
        "status": "running"
    }


@app.get("/health")
async def health_check():
    """Health check endpoint"""
    try:
        # Basic health check - could be enhanced with actual service checks
        return {
            "status": "healthy",
            "service": "graphiti",
            "timestamp": "2025-01-01T00:00:00Z"
        }
    except Exception as e:
        raise HTTPException(status_code=503, detail=f"Service unhealthy: {str(e)}")


@app.post("/episodes")
async def add_episode(
    episode: EpisodeRequest,
    service: GraphitiMusicService = Depends(get_graphiti_service)
) -> Dict[str, Any]:
    """Add a learning episode to the knowledge graph"""
    try:
        result = await service.add_episode(episode)
        if result["status"] == "error":
            raise HTTPException(status_code=400, detail=result["message"])
        return result
    except Exception as e:
        logger.error(f"Error adding episode: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/search")
async def search_knowledge(
    search: SearchRequest,
    service: GraphitiMusicService = Depends(get_graphiti_service)
) -> Dict[str, Any]:
    """Search the knowledge graph"""
    try:
        result = await service.search_knowledge(search)
        if result["status"] == "error":
            raise HTTPException(status_code=400, detail=result["message"])
        return result
    except Exception as e:
        logger.error(f"Error searching knowledge: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/recommendations")
async def get_recommendations(
    recommendation: RecommendationRequest,
    service: GraphitiMusicService = Depends(get_graphiti_service)
) -> Dict[str, Any]:
    """Get personalized recommendations"""
    try:
        result = await service.get_recommendations(recommendation)
        if result["status"] == "error":
            raise HTTPException(status_code=400, detail=result["message"])
        return result
    except Exception as e:
        logger.error(f"Error getting recommendations: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/users/{user_id}/progress")
async def get_user_progress(
    user_id: str,
    service: GraphitiMusicService = Depends(get_graphiti_service)
) -> Dict[str, Any]:
    """Get user's learning progress"""
    try:
        result = await service.get_user_progress(user_id)
        if result["status"] == "error":
            raise HTTPException(status_code=400, detail=result["message"])
        return result
    except Exception as e:
        logger.error(f"Error getting user progress: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/graph/stats")
async def get_graph_stats(
    service: GraphitiMusicService = Depends(get_graphiti_service)
) -> Dict[str, Any]:
    """Get knowledge graph statistics"""
    try:
        # This would query the graph for statistics
        # Simplified implementation for now
        return {
            "status": "success",
            "stats": {
                "total_nodes": 0,  # Would query actual graph
                "total_edges": 0,
                "user_count": 0,
                "chord_count": 0,
                "scale_count": 0
            }
        }
    except Exception as e:
        logger.error(f"Error getting graph stats: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/graph/sync")
async def sync_from_mongodb(
    service: GraphitiMusicService = Depends(get_graphiti_service)
) -> Dict[str, Any]:
    """Sync data from MongoDB to knowledge graph"""
    try:
        # This would implement data synchronization from GA's MongoDB
        # to the Graphiti knowledge graph
        return {
            "status": "success",
            "message": "Sync completed",
            "synced_records": 0  # Would return actual count
        }
    except Exception as e:
        logger.error(f"Error syncing data: {e}")
        raise HTTPException(status_code=500, detail=str(e))


# Error handlers
@app.exception_handler(Exception)
async def global_exception_handler(request, exc):
    """Global exception handler"""
    logger.error(f"Unhandled exception: {exc}")
    return JSONResponse(
        status_code=500,
        content={"detail": "Internal server error"}
    )


if __name__ == "__main__":
    import uvicorn
    
    host = os.getenv("API_HOST", "0.0.0.0")
    port = int(os.getenv("API_PORT", "8000"))
    debug = os.getenv("DEBUG", "false").lower() == "true"
    
    uvicorn.run(
        "main:app",
        host=host,
        port=port,
        reload=debug,
        log_level="info"
    )
