"""
Music Theory Knowledge Graph Models for Graphiti Integration
"""
from datetime import datetime
from typing import Optional, List, Dict, Any
from pydantic import BaseModel, Field
from graphiti_core.nodes import EntityNode
from graphiti_core.edges import EntityEdge


class ChordEntity(EntityNode):
    """Represents a musical chord in the knowledge graph"""
    name: str = Field(description="Chord name (e.g., 'Cmaj7', 'Am')")
    root_note: str = Field(description="Root note of the chord")
    chord_type: str = Field(description="Type of chord (major, minor, dominant7, etc.)")
    complexity_level: int = Field(description="Difficulty level 1-10", ge=1, le=10)
    fret_positions: Optional[List[int]] = Field(description="Fret positions for standard tuning")
    pitch_classes: Optional[List[int]] = Field(description="Pitch class set representation")
    
    class Config:
        json_schema_extra = {
            "example": {
                "name": "Cmaj7",
                "root_note": "C",
                "chord_type": "major7",
                "complexity_level": 4,
                "fret_positions": [0, 3, 2, 0, 0, 0],
                "pitch_classes": [0, 4, 7, 11]
            }
        }


class ScaleEntity(EntityNode):
    """Represents a musical scale in the knowledge graph"""
    name: str = Field(description="Scale name (e.g., 'C Major', 'A Minor Pentatonic')")
    root_note: str = Field(description="Root note of the scale")
    scale_type: str = Field(description="Type of scale (major, minor, pentatonic, etc.)")
    intervals: List[int] = Field(description="Interval pattern in semitones")
    modes: Optional[List[str]] = Field(description="Associated modes")
    
    class Config:
        json_schema_extra = {
            "example": {
                "name": "C Major",
                "root_note": "C",
                "scale_type": "major",
                "intervals": [2, 2, 1, 2, 2, 2, 1],
                "modes": ["Ionian", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Aeolian", "Locrian"]
            }
        }


class UserEntity(EntityNode):
    """Represents a user in the learning system"""
    user_id: str = Field(description="Unique user identifier")
    skill_level: float = Field(description="Overall skill level 0.0-10.0", ge=0.0, le=10.0)
    learning_style: Optional[str] = Field(description="Preferred learning approach")
    goals: Optional[List[str]] = Field(description="Learning goals")
    
    class Config:
        json_schema_extra = {
            "example": {
                "user_id": "user123",
                "skill_level": 3.5,
                "learning_style": "visual",
                "goals": ["learn jazz chords", "improve fingerpicking"]
            }
        }


class ProgressionEntity(EntityNode):
    """Represents a chord progression"""
    name: str = Field(description="Progression name or identifier")
    chords: List[str] = Field(description="Sequence of chord names")
    key: str = Field(description="Musical key")
    genre: Optional[str] = Field(description="Musical genre")
    complexity_level: int = Field(description="Difficulty level 1-10", ge=1, le=10)
    
    class Config:
        json_schema_extra = {
            "example": {
                "name": "I-V-vi-IV",
                "chords": ["C", "G", "Am", "F"],
                "key": "C major",
                "genre": "pop",
                "complexity_level": 2
            }
        }


# Edge Types for Relationships

class LearningProgressEdge(EntityEdge):
    """Tracks user learning progress over time"""
    session_duration: int = Field(description="Practice session duration in minutes")
    accuracy_score: float = Field(description="Accuracy percentage 0.0-1.0", ge=0.0, le=1.0)
    difficulty_attempted: int = Field(description="Difficulty level attempted", ge=1, le=10)
    mistakes_made: Optional[List[str]] = Field(description="Common mistakes during session")
    improvement_notes: Optional[str] = Field(description="Notes on improvement")


class ChordRelationshipEdge(EntityEdge):
    """Represents relationships between chords"""
    relationship_type: str = Field(description="Type of relationship (progression, substitution, etc.)")
    strength: float = Field(description="Strength of relationship 0.0-1.0", ge=0.0, le=1.0)
    context: Optional[str] = Field(description="Musical context where relationship applies")


class ScaleChordEdge(EntityEdge):
    """Connects scales to their constituent chords"""
    degree: int = Field(description="Scale degree (1-7)")
    function: str = Field(description="Harmonic function (tonic, subdominant, dominant)")


# Pydantic models for API requests/responses

class EpisodeRequest(BaseModel):
    """Request model for adding learning episodes"""
    user_id: str
    episode_type: str = Field(description="Type of episode (practice, lesson, assessment)")
    content: Dict[str, Any] = Field(description="Episode content")
    timestamp: Optional[datetime] = Field(default_factory=datetime.now)
    
    class Config:
        json_schema_extra = {
            "example": {
                "user_id": "user123",
                "episode_type": "practice",
                "content": {
                    "chord_practiced": "Cmaj7",
                    "duration_minutes": 15,
                    "accuracy": 0.85,
                    "difficulty_level": 4
                }
            }
        }


class SearchRequest(BaseModel):
    """Request model for knowledge graph searches"""
    query: str = Field(description="Search query")
    search_type: str = Field(default="hybrid", description="Search type (semantic, keyword, hybrid, graph)")
    limit: int = Field(default=10, description="Maximum number of results", ge=1, le=100)
    user_id: Optional[str] = Field(description="User ID for personalized results")
    
    class Config:
        json_schema_extra = {
            "example": {
                "query": "jazz chords for beginners",
                "search_type": "hybrid",
                "limit": 10,
                "user_id": "user123"
            }
        }


class RecommendationRequest(BaseModel):
    """Request model for personalized recommendations"""
    user_id: str
    recommendation_type: str = Field(description="Type of recommendation (next_chord, progression, exercise)")
    context: Optional[Dict[str, Any]] = Field(description="Additional context")
    
    class Config:
        json_schema_extra = {
            "example": {
                "user_id": "user123",
                "recommendation_type": "next_chord",
                "context": {
                    "current_skill_level": 3.5,
                    "recently_practiced": ["C", "G", "Am"]
                }
            }
        }
