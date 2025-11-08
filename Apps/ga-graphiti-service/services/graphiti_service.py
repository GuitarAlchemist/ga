"""
Graphiti Knowledge Graph Service for Guitar Alchemist
"""
import os
import asyncio
from typing import List, Dict, Any, Optional
from datetime import datetime
import logging

from graphiti_core import Graphiti
from graphiti_core.llm_client.openai_generic_client import OpenAIGenericClient
from graphiti_core.llm_client.config import LLMConfig
from graphiti_core.embedder.openai import OpenAIEmbedder, OpenAIEmbedderConfig
from graphiti_core.cross_encoder.openai_reranker_client import OpenAIRerankerClient

from models.music_theory import (
    ChordEntity, ScaleEntity, UserEntity, ProgressionEntity,
    LearningProgressEdge, ChordRelationshipEdge, ScaleChordEdge,
    EpisodeRequest, SearchRequest, RecommendationRequest
)

logger = logging.getLogger(__name__)


class GraphitiMusicService:
    """Service class for managing music theory knowledge graphs with Graphiti"""
    
    def __init__(self):
        self.graphiti: Optional[Graphiti] = None
        self._initialize_graphiti()
    
    def _initialize_graphiti(self):
        """Initialize Graphiti with Ollama configuration"""
        try:
            # Use Ollama for local LLM and embeddings
            ollama_base_url = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434/v1")
            chat_model = os.getenv("OLLAMA_CHAT_MODEL", "qwen2.5-coder:1.5b-base")
            embedding_model = os.getenv("OLLAMA_EMBEDDING_MODEL", "nomic-embed-text")
            
            # Configure LLM client for Ollama
            llm_config = LLMConfig(
                api_key="ollama",  # Placeholder - Ollama doesn't need real API key
                model=chat_model,
                small_model=chat_model,
                base_url=ollama_base_url,
            )
            
            llm_client = OpenAIGenericClient(config=llm_config)
            
            # Configure embedder for Ollama
            embedder = OpenAIEmbedder(
                config=OpenAIEmbedderConfig(
                    api_key="ollama",
                    embedding_model=embedding_model,
                    embedding_dim=768,  # nomic-embed-text dimension
                    base_url=ollama_base_url,
                )
            )
            
            # Configure cross-encoder/reranker
            cross_encoder = OpenAIRerankerClient(
                client=llm_client, 
                config=llm_config
            )
            
            # Initialize Graphiti with FalkorDB (or Neo4j)
            db_type = os.getenv("GRAPH_DB_TYPE", "falkordb")
            
            if db_type == "falkordb":
                from graphiti_core.driver.falkordb_driver import FalkorDriver
                driver = FalkorDriver(
                    host=os.getenv("FALKORDB_HOST", "localhost"),
                    port=int(os.getenv("FALKORDB_PORT", "6379")),
                    database=os.getenv("FALKORDB_DATABASE", "graphiti")
                )
            else:
                # Default to Neo4j
                from graphiti_core.driver.neo4j_driver import Neo4jDriver
                driver = Neo4jDriver(
                    uri=os.getenv("NEO4J_URI", "bolt://localhost:7687"),
                    user=os.getenv("NEO4J_USER", "neo4j"),
                    password=os.getenv("NEO4J_PASSWORD", "password"),
                    database=os.getenv("NEO4J_DATABASE", "neo4j")
                )
            
            self.graphiti = Graphiti(
                graph_driver=driver,
                llm_client=llm_client,
                embedder=embedder,
                cross_encoder=cross_encoder
            )
            
            logger.info(f"Graphiti initialized successfully with {db_type}")
            
        except Exception as e:
            logger.error(f"Failed to initialize Graphiti: {e}")
            raise
    
    async def initialize_schema(self):
        """Initialize the knowledge graph schema"""
        try:
            await self.graphiti.build_indices_and_constraints()
            logger.info("Graphiti schema initialized successfully")
        except Exception as e:
            logger.error(f"Failed to initialize schema: {e}")
            raise
    
    async def add_episode(self, episode_request: EpisodeRequest) -> Dict[str, Any]:
        """Add a learning episode to the knowledge graph"""
        try:
            episode_data = {
                "user_id": episode_request.user_id,
                "episode_type": episode_request.episode_type,
                "timestamp": episode_request.timestamp.isoformat(),
                **episode_request.content
            }
            
            # Convert to text for Graphiti processing
            episode_text = self._format_episode_as_text(episode_data)
            
            # Add episode to Graphiti
            await self.graphiti.add_episode(
                name=f"episode_{episode_request.user_id}_{int(episode_request.timestamp.timestamp())}",
                episode_body=episode_text,
                source_description=f"Learning episode for user {episode_request.user_id}"
            )
            
            logger.info(f"Added episode for user {episode_request.user_id}")
            return {"status": "success", "message": "Episode added successfully"}
            
        except Exception as e:
            logger.error(f"Failed to add episode: {e}")
            return {"status": "error", "message": str(e)}
    
    async def search_knowledge(self, search_request: SearchRequest) -> Dict[str, Any]:
        """Search the knowledge graph"""
        try:
            # Perform search based on type
            if search_request.search_type == "semantic":
                results = await self.graphiti.search(
                    query=search_request.query,
                    search_type="similarity"
                )
            elif search_request.search_type == "keyword":
                results = await self.graphiti.search(
                    query=search_request.query,
                    search_type="bm25"
                )
            else:  # hybrid or graph
                results = await self.graphiti.search(
                    query=search_request.query,
                    search_type="hybrid"
                )
            
            # Limit results
            limited_results = results[:search_request.limit] if results else []
            
            return {
                "status": "success",
                "query": search_request.query,
                "results": limited_results,
                "count": len(limited_results)
            }
            
        except Exception as e:
            logger.error(f"Search failed: {e}")
            return {"status": "error", "message": str(e)}
    
    async def get_recommendations(self, rec_request: RecommendationRequest) -> Dict[str, Any]:
        """Get personalized recommendations for a user"""
        try:
            # Build context-aware query
            context_query = self._build_recommendation_query(rec_request)
            
            # Search for relevant information
            search_results = await self.graphiti.search(
                query=context_query,
                search_type="hybrid"
            )
            
            # Process results into recommendations
            recommendations = self._process_recommendations(
                search_results, 
                rec_request.recommendation_type
            )
            
            return {
                "status": "success",
                "user_id": rec_request.user_id,
                "recommendation_type": rec_request.recommendation_type,
                "recommendations": recommendations
            }
            
        except Exception as e:
            logger.error(f"Recommendation generation failed: {e}")
            return {"status": "error", "message": str(e)}
    
    async def get_user_progress(self, user_id: str) -> Dict[str, Any]:
        """Get user's learning progress over time"""
        try:
            # Query for user's learning history
            progress_query = f"user {user_id} learning progress practice sessions skill development"
            
            results = await self.graphiti.search(
                query=progress_query,
                search_type="hybrid"
            )
            
            # Process results to extract progress information
            progress_data = self._extract_progress_data(results, user_id)
            
            return {
                "status": "success",
                "user_id": user_id,
                "progress": progress_data
            }
            
        except Exception as e:
            logger.error(f"Failed to get user progress: {e}")
            return {"status": "error", "message": str(e)}
    
    def _format_episode_as_text(self, episode_data: Dict[str, Any]) -> str:
        """Convert episode data to natural language text for Graphiti"""
        user_id = episode_data.get("user_id")
        episode_type = episode_data.get("episode_type")
        timestamp = episode_data.get("timestamp")
        
        text_parts = [f"User {user_id} had a {episode_type} session on {timestamp}."]
        
        # Add specific content based on episode type
        if episode_type == "practice":
            chord = episode_data.get("chord_practiced")
            duration = episode_data.get("duration_minutes")
            accuracy = episode_data.get("accuracy", 0)
            difficulty = episode_data.get("difficulty_level", 1)
            
            text_parts.append(f"They practiced the {chord} chord for {duration} minutes.")
            text_parts.append(f"Their accuracy was {accuracy:.1%} at difficulty level {difficulty}.")
            
        elif episode_type == "lesson":
            topic = episode_data.get("topic")
            concepts = episode_data.get("concepts_covered", [])
            
            text_parts.append(f"The lesson covered {topic}.")
            if concepts:
                text_parts.append(f"Concepts included: {', '.join(concepts)}.")
        
        return " ".join(text_parts)
    
    def _build_recommendation_query(self, rec_request: RecommendationRequest) -> str:
        """Build a context-aware query for recommendations"""
        user_id = rec_request.user_id
        rec_type = rec_request.recommendation_type
        context = rec_request.context or {}
        
        query_parts = [f"user {user_id}"]
        
        if rec_type == "next_chord":
            query_parts.append("next chord to learn progression difficulty level")
            if "recently_practiced" in context:
                chords = context["recently_practiced"]
                query_parts.append(f"after practicing {' '.join(chords)}")
                
        elif rec_type == "progression":
            query_parts.append("chord progression suitable for skill level")
            
        elif rec_type == "exercise":
            query_parts.append("practice exercise technique improvement")
        
        return " ".join(query_parts)
    
    def _process_recommendations(self, search_results: List[Any], rec_type: str) -> List[Dict[str, Any]]:
        """Process search results into structured recommendations"""
        recommendations = []
        
        for result in search_results[:5]:  # Limit to top 5
            # Extract relevant information from search result
            # This would need to be customized based on Graphiti's result format
            rec = {
                "type": rec_type,
                "content": str(result),  # Simplified - would need proper parsing
                "confidence": getattr(result, 'score', 0.5),
                "reasoning": "Based on your learning history and current skill level"
            }
            recommendations.append(rec)
        
        return recommendations
    
    def _extract_progress_data(self, results: List[Any], user_id: str) -> Dict[str, Any]:
        """Extract progress information from search results"""
        # This would analyze the temporal aspects of the user's learning
        # Simplified implementation - would need proper temporal analysis
        return {
            "skill_level": 3.5,  # Would be calculated from actual data
            "sessions_completed": len(results),
            "recent_activity": "Practiced jazz chords",
            "improvement_trend": "positive",
            "next_milestone": "Master 7th chords"
        }
