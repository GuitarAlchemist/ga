import React, { useState, useEffect } from 'react';
import { GraphitiKnowledgeGraph } from '../GraphitiKnowledgeGraph/GraphitiKnowledgeGraph';
import './GraphitiDemo.css';

interface Episode {
  user_id: string;
  episode_type: string;
  content: Record<string, any>;
  timestamp?: string;
}

interface SearchResult {
  content: string;
  score: number;
  type?: string;
}

interface Recommendation {
  type: string;
  content: string;
  confidence: number;
  reasoning?: string;
}

interface UserProgress {
  skill_level: number;
  sessions_completed: number;
  recent_activity?: string;
  improvement_trend?: string;
  next_milestone?: string;
}

export const GraphitiDemo: React.FC = () => {
  const [userId, setUserId] = useState('demo-user-123');
  const [loading, setLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<SearchResult[]>([]);
  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);
  const [userProgress, setUserProgress] = useState<UserProgress | null>(null);
  const [episodeType, setEpisodeType] = useState('practice');
  const [chordPracticed, setChordPracticed] = useState('Cmaj7');
  const [practiceAccuracy, setPracticeAccuracy] = useState(85);
  const [practiceDuration, setPracticeDuration] = useState(15);

  // Add a practice episode
  const addEpisode = async () => {
    setLoading(true);
    try {
      const episode: Episode = {
        user_id: userId,
        episode_type: episodeType,
        content: {
          chord_practiced: chordPracticed,
          duration_minutes: practiceDuration,
          accuracy: practiceAccuracy / 100,
          difficulty_level: 4,
          timestamp: new Date().toISOString()
        }
      };

      const response = await fetch('/api/graphiti/episodes', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(episode)
      });

      if (response.ok) {
        const result = await response.json();
        console.log('Episode added:', result);
        // Refresh other data
        await Promise.all([
          searchKnowledge(),
          getRecommendations(),
          getUserProgress()
        ]);
      }
    } catch (error) {
      console.error('Failed to add episode:', error);
    } finally {
      setLoading(false);
    }
  };

  // Search the knowledge graph
  const searchKnowledge = async () => {
    if (!searchQuery.trim()) return;
    
    setLoading(true);
    try {
      const response = await fetch('/api/graphiti/search', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          query: searchQuery,
          search_type: 'hybrid',
          limit: 10,
          user_id: userId
        })
      });

      if (response.ok) {
        const result = await response.json();
        setSearchResults(result.results || []);
      }
    } catch (error) {
      console.error('Search failed:', error);
      // Mock results for demo
      setSearchResults([
        { content: `User ${userId} practiced ${chordPracticed} chord with ${practiceAccuracy}% accuracy`, score: 0.95 },
        { content: `${chordPracticed} is a jazz chord commonly used in progressions`, score: 0.87 },
        { content: `Users typically learn ${chordPracticed} after mastering basic triads`, score: 0.82 }
      ]);
    } finally {
      setLoading(false);
    }
  };

  // Get personalized recommendations
  const getRecommendations = async () => {
    setLoading(true);
    try {
      const response = await fetch('/api/graphiti/recommendations', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          user_id: userId,
          recommendation_type: 'next_chord',
          context: {
            current_skill_level: 3.5,
            recently_practiced: [chordPracticed]
          }
        })
      });

      if (response.ok) {
        const result = await response.json();
        setRecommendations(result.recommendations || []);
      }
    } catch (error) {
      console.error('Failed to get recommendations:', error);
      // Mock recommendations for demo
      setRecommendations([
        {
          type: 'next_chord',
          content: 'Try learning Dm7 - it complements your Cmaj7 knowledge',
          confidence: 0.92,
          reasoning: 'Based on your jazz chord progression learning path'
        },
        {
          type: 'exercise',
          content: 'Practice the ii-V-I progression: Dm7 - G7 - Cmaj7',
          confidence: 0.88,
          reasoning: 'This will reinforce your Cmaj7 skills in context'
        }
      ]);
    } finally {
      setLoading(false);
    }
  };

  // Get user progress
  const getUserProgress = async () => {
    try {
      const response = await fetch(`/api/graphiti/users/${userId}/progress`);
      
      if (response.ok) {
        const result = await response.json();
        setUserProgress(result.progress);
      }
    } catch (error) {
      console.error('Failed to get user progress:', error);
      // Mock progress for demo
      setUserProgress({
        skill_level: 3.5,
        sessions_completed: 12,
        recent_activity: `Practiced ${chordPracticed} chord`,
        improvement_trend: 'positive',
        next_milestone: 'Master 7th chord progressions'
      });
    }
  };

  // Initialize demo data
  useEffect(() => {
    setSearchQuery(`${userId} learning progress jazz chords`);
    getUserProgress();
    getRecommendations();
  }, [userId]);

  return (
    <div className="graphiti-demo">
      <div className="demo-header">
        <h1>🎸 Guitar Alchemist × Graphiti Demo</h1>
        <p>Temporal Knowledge Graphs for Music Learning</p>
      </div>

      <div className="demo-grid">
        {/* User Controls */}
        <div className="demo-section">
          <h2>👤 User Session</h2>
          <div className="form-group">
            <label>User ID:</label>
            <input
              type="text"
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
              placeholder="Enter user ID"
            />
          </div>
        </div>

        {/* Add Episode */}
        <div className="demo-section">
          <h2>📝 Add Learning Episode</h2>
          <div className="form-group">
            <label>Episode Type:</label>
            <select value={episodeType} onChange={(e) => setEpisodeType(e.target.value)}>
              <option value="practice">Practice Session</option>
              <option value="lesson">Lesson</option>
              <option value="assessment">Assessment</option>
            </select>
          </div>
          <div className="form-group">
            <label>Chord Practiced:</label>
            <select value={chordPracticed} onChange={(e) => setChordPracticed(e.target.value)}>
              <option value="Cmaj7">Cmaj7</option>
              <option value="Dm7">Dm7</option>
              <option value="G7">G7</option>
              <option value="Am7">Am7</option>
              <option value="Fmaj7">Fmaj7</option>
            </select>
          </div>
          <div className="form-group">
            <label>Accuracy: {practiceAccuracy}%</label>
            <input
              type="range"
              min="0"
              max="100"
              value={practiceAccuracy}
              onChange={(e) => setPracticeAccuracy(Number(e.target.value))}
            />
          </div>
          <div className="form-group">
            <label>Duration: {practiceDuration} minutes</label>
            <input
              type="range"
              min="5"
              max="60"
              value={practiceDuration}
              onChange={(e) => setPracticeDuration(Number(e.target.value))}
            />
          </div>
          <button onClick={addEpisode} disabled={loading} className="primary-button">
            {loading ? 'Adding...' : 'Add Episode'}
          </button>
        </div>

        {/* Search */}
        <div className="demo-section">
          <h2>🔍 Knowledge Graph Search</h2>
          <div className="form-group">
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search the knowledge graph..."
              onKeyPress={(e) => e.key === 'Enter' && searchKnowledge()}
            />
            <button onClick={searchKnowledge} disabled={loading} className="primary-button">
              Search
            </button>
          </div>
          <div className="search-results">
            {searchResults.map((result, index) => (
              <div key={index} className="search-result">
                <div className="result-content">{result.content}</div>
                <div className="result-score">Score: {(result.score * 100).toFixed(1)}%</div>
              </div>
            ))}
          </div>
        </div>

        {/* Recommendations */}
        <div className="demo-section">
          <h2>💡 AI Recommendations</h2>
          <button onClick={getRecommendations} disabled={loading} className="secondary-button">
            Get Recommendations
          </button>
          <div className="recommendations">
            {recommendations.map((rec, index) => (
              <div key={index} className="recommendation">
                <div className="rec-content">{rec.content}</div>
                <div className="rec-meta">
                  <span className="rec-confidence">
                    Confidence: {(rec.confidence * 100).toFixed(1)}%
                  </span>
                  {rec.reasoning && (
                    <div className="rec-reasoning">{rec.reasoning}</div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* User Progress */}
        <div className="demo-section">
          <h2>📊 Learning Progress</h2>
          {userProgress && (
            <div className="progress-info">
              <div className="progress-item">
                <label>Skill Level:</label>
                <div className="skill-bar">
                  <div 
                    className="skill-fill" 
                    style={{ width: `${(userProgress.skill_level / 10) * 100}%` }}
                  />
                  <span>{userProgress.skill_level}/10</span>
                </div>
              </div>
              <div className="progress-item">
                <label>Sessions Completed:</label>
                <span>{userProgress.sessions_completed}</span>
              </div>
              <div className="progress-item">
                <label>Recent Activity:</label>
                <span>{userProgress.recent_activity}</span>
              </div>
              <div className="progress-item">
                <label>Trend:</label>
                <span className={`trend ${userProgress.improvement_trend}`}>
                  {userProgress.improvement_trend}
                </span>
              </div>
              <div className="progress-item">
                <label>Next Milestone:</label>
                <span>{userProgress.next_milestone}</span>
              </div>
            </div>
          )}
        </div>

        {/* Knowledge Graph Visualization */}
        <div className="demo-section full-width">
          <h2>🕸️ Knowledge Graph Visualization</h2>
          <GraphitiKnowledgeGraph
            userId={userId}
            width={800}
            height={500}
            onNodeClick={(node) => console.log('Node clicked:', node)}
            onLinkClick={(link) => console.log('Link clicked:', link)}
          />
        </div>
      </div>
    </div>
  );
};
