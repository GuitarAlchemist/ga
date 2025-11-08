"""
Tests for the Graphiti Music Service
"""
import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from datetime import datetime

from services.graphiti_service import GraphitiMusicService
from models.music_theory import EpisodeRequest, SearchRequest, RecommendationRequest


@pytest.fixture
def mock_graphiti():
    """Mock Graphiti instance"""
    mock = MagicMock()
    mock.add_episode = AsyncMock()
    mock.search = AsyncMock()
    mock.build_indices_and_constraints = AsyncMock()
    return mock


@pytest.fixture
def graphiti_service(mock_graphiti):
    """GraphitiMusicService instance with mocked Graphiti"""
    service = GraphitiMusicService()
    service.graphiti = mock_graphiti
    return service


class TestGraphitiMusicService:
    """Test cases for GraphitiMusicService"""

    @pytest.mark.asyncio
    async def test_initialize_schema_success(self, graphiti_service, mock_graphiti):
        """Test successful schema initialization"""
        # Act
        await graphiti_service.initialize_schema()
        
        # Assert
        mock_graphiti.build_indices_and_constraints.assert_called_once()

    @pytest.mark.asyncio
    async def test_add_episode_success(self, graphiti_service, mock_graphiti):
        """Test successful episode addition"""
        # Arrange
        episode_request = EpisodeRequest(
            user_id="test-user",
            episode_type="practice",
            content={
                "chord_practiced": "Cmaj7",
                "duration_minutes": 15,
                "accuracy": 0.85,
                "difficulty_level": 4
            },
            timestamp=datetime.now()
        )
        
        # Act
        result = await graphiti_service.add_episode(episode_request)
        
        # Assert
        assert result["status"] == "success"
        assert result["message"] == "Episode added successfully"
        mock_graphiti.add_episode.assert_called_once()

    @pytest.mark.asyncio
    async def test_add_episode_failure(self, graphiti_service, mock_graphiti):
        """Test episode addition failure"""
        # Arrange
        episode_request = EpisodeRequest(
            user_id="test-user",
            episode_type="practice",
            content={},
            timestamp=datetime.now()
        )
        
        mock_graphiti.add_episode.side_effect = Exception("Database error")
        
        # Act
        result = await graphiti_service.add_episode(episode_request)
        
        # Assert
        assert result["status"] == "error"
        assert "Database error" in result["message"]

    @pytest.mark.asyncio
    async def test_search_knowledge_semantic(self, graphiti_service, mock_graphiti):
        """Test semantic search"""
        # Arrange
        search_request = SearchRequest(
            query="jazz chords",
            search_type="semantic",
            limit=10,
            user_id="test-user"
        )
        
        mock_results = [
            MagicMock(content="Cmaj7 is a jazz chord", score=0.95),
            MagicMock(content="Dm7 complements Cmaj7", score=0.87)
        ]
        mock_graphiti.search.return_value = mock_results
        
        # Act
        result = await graphiti_service.search_knowledge(search_request)
        
        # Assert
        assert result["status"] == "success"
        assert result["query"] == "jazz chords"
        assert result["count"] == 2
        mock_graphiti.search.assert_called_once_with(
            query="jazz chords",
            search_type="similarity"
        )

    @pytest.mark.asyncio
    async def test_search_knowledge_keyword(self, graphiti_service, mock_graphiti):
        """Test keyword search"""
        # Arrange
        search_request = SearchRequest(
            query="chord progression",
            search_type="keyword",
            limit=5
        )
        
        mock_results = [MagicMock(content="I-V-vi-IV progression", score=0.92)]
        mock_graphiti.search.return_value = mock_results
        
        # Act
        result = await graphiti_service.search_knowledge(search_request)
        
        # Assert
        assert result["status"] == "success"
        assert result["count"] == 1
        mock_graphiti.search.assert_called_once_with(
            query="chord progression",
            search_type="bm25"
        )

    @pytest.mark.asyncio
    async def test_search_knowledge_hybrid(self, graphiti_service, mock_graphiti):
        """Test hybrid search"""
        # Arrange
        search_request = SearchRequest(
            query="beginner guitar chords",
            search_type="hybrid",
            limit=10
        )
        
        mock_results = [
            MagicMock(content="C major chord", score=0.88),
            MagicMock(content="G major chord", score=0.85),
            MagicMock(content="A minor chord", score=0.82)
        ]
        mock_graphiti.search.return_value = mock_results
        
        # Act
        result = await graphiti_service.search_knowledge(search_request)
        
        # Assert
        assert result["status"] == "success"
        assert result["count"] == 3
        mock_graphiti.search.assert_called_once_with(
            query="beginner guitar chords",
            search_type="hybrid"
        )

    @pytest.mark.asyncio
    async def test_get_recommendations_next_chord(self, graphiti_service, mock_graphiti):
        """Test getting next chord recommendations"""
        # Arrange
        rec_request = RecommendationRequest(
            user_id="test-user",
            recommendation_type="next_chord",
            context={
                "current_skill_level": 3.5,
                "recently_practiced": ["C", "G", "Am"]
            }
        )
        
        mock_results = [
            MagicMock(content="Try learning Dm7", score=0.92),
            MagicMock(content="Practice F major chord", score=0.88)
        ]
        mock_graphiti.search.return_value = mock_results
        
        # Act
        result = await graphiti_service.get_recommendations(rec_request)
        
        # Assert
        assert result["status"] == "success"
        assert result["user_id"] == "test-user"
        assert result["recommendation_type"] == "next_chord"
        assert len(result["recommendations"]) > 0

    @pytest.mark.asyncio
    async def test_get_user_progress(self, graphiti_service, mock_graphiti):
        """Test getting user progress"""
        # Arrange
        user_id = "test-user"
        mock_results = [
            MagicMock(content="User practiced Cmaj7", score=0.95),
            MagicMock(content="User completed 12 sessions", score=0.90)
        ]
        mock_graphiti.search.return_value = mock_results
        
        # Act
        result = await graphiti_service.get_user_progress(user_id)
        
        # Assert
        assert result["status"] == "success"
        assert result["user_id"] == user_id
        assert "progress" in result
        assert result["progress"]["skill_level"] > 0

    def test_format_episode_as_text_practice(self, graphiti_service):
        """Test formatting practice episode as text"""
        # Arrange
        episode_data = {
            "user_id": "test-user",
            "episode_type": "practice",
            "timestamp": "2025-01-01T12:00:00Z",
            "chord_practiced": "Cmaj7",
            "duration_minutes": 15,
            "accuracy": 0.85,
            "difficulty_level": 4
        }
        
        # Act
        result = graphiti_service._format_episode_as_text(episode_data)
        
        # Assert
        assert "test-user" in result
        assert "practice session" in result
        assert "Cmaj7 chord" in result
        assert "15 minutes" in result
        assert "85.0%" in result
        assert "difficulty level 4" in result

    def test_format_episode_as_text_lesson(self, graphiti_service):
        """Test formatting lesson episode as text"""
        # Arrange
        episode_data = {
            "user_id": "test-user",
            "episode_type": "lesson",
            "timestamp": "2025-01-01T12:00:00Z",
            "topic": "Jazz chord progressions",
            "concepts_covered": ["ii-V-I", "chord substitutions"]
        }
        
        # Act
        result = graphiti_service._format_episode_as_text(episode_data)
        
        # Assert
        assert "test-user" in result
        assert "lesson" in result
        assert "Jazz chord progressions" in result
        assert "ii-V-I" in result
        assert "chord substitutions" in result

    def test_build_recommendation_query_next_chord(self, graphiti_service):
        """Test building recommendation query for next chord"""
        # Arrange
        rec_request = RecommendationRequest(
            user_id="test-user",
            recommendation_type="next_chord",
            context={
                "recently_practiced": ["C", "G", "Am"]
            }
        )
        
        # Act
        result = graphiti_service._build_recommendation_query(rec_request)
        
        # Assert
        assert "test-user" in result
        assert "next chord" in result
        assert "C G Am" in result

    def test_build_recommendation_query_progression(self, graphiti_service):
        """Test building recommendation query for progression"""
        # Arrange
        rec_request = RecommendationRequest(
            user_id="test-user",
            recommendation_type="progression",
            context={}
        )
        
        # Act
        result = graphiti_service._build_recommendation_query(rec_request)
        
        # Assert
        assert "test-user" in result
        assert "chord progression" in result
        assert "skill level" in result

    def test_process_recommendations(self, graphiti_service):
        """Test processing search results into recommendations"""
        # Arrange
        mock_results = [
            MagicMock(content="Try Dm7 chord", score=0.92),
            MagicMock(content="Practice ii-V-I progression", score=0.88)
        ]
        
        # Act
        result = graphiti_service._process_recommendations(mock_results, "next_chord")
        
        # Assert
        assert len(result) == 2
        assert result[0]["type"] == "next_chord"
        assert result[0]["confidence"] == 0.92
        assert "Try Dm7 chord" in result[0]["content"]

    def test_extract_progress_data(self, graphiti_service):
        """Test extracting progress data from search results"""
        # Arrange
        mock_results = [
            MagicMock(content="User completed session"),
            MagicMock(content="User practiced chord"),
            MagicMock(content="User improved accuracy")
        ]
        user_id = "test-user"
        
        # Act
        result = graphiti_service._extract_progress_data(mock_results, user_id)
        
        # Assert
        assert "skill_level" in result
        assert "sessions_completed" in result
        assert "improvement_trend" in result
        assert result["sessions_completed"] == len(mock_results)
