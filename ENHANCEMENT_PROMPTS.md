# Guitar Alchemist Chatbot - Enhancement Exploration Prompts

A series of prompts to explore and reflect on making the chatbot more powerful.

---

## 🎯 **Prompt Series 1: Audio Integration**

### **Prompt 1.1: Basic Audio Playback**
```
Design and implement a chord audio playback system for the Guitar Alchemist Chatbot.

Requirements:
- Play chord sounds when displayed in chat
- Support multiple instruments (guitar, piano, synth)
- Use Web Audio API for browser-based playback
- Integrate with existing VexTab visualization
- Add play/pause controls to chord displays
- Consider using Tone.js or Howler.js libraries

Deliverables:
1. Audio service for chord synthesis
2. UI controls for playback
3. Integration with existing chat messages
4. Volume and instrument selection
5. Caching for performance
```

### **Prompt 1.2: Interactive Metronome**
```
Create an interactive metronome feature for practice sessions.

Requirements:
- Adjustable BPM (40-240)
- Visual and audio feedback
- Time signature selection (4/4, 3/4, 6/8, etc.)
- Accent on beat 1
- Start/stop controls
- Tap tempo feature
- Integration with practice mode

Deliverables:
1. Metronome component
2. Audio synthesis for click sounds
3. Visual beat indicator
4. Tempo presets (Largo, Andante, Allegro, etc.)
5. Persistence of user settings
```

### **Prompt 1.3: Audio Input Recognition**
```
Implement real-time audio input for chord recognition.

Requirements:
- Microphone access via Web Audio API
- Pitch detection algorithm
- Chord recognition from audio
- Real-time feedback
- Tuner functionality
- Display detected notes and chords

Deliverables:
1. Audio input service
2. Pitch detection algorithm (autocorrelation or FFT)
3. Chord recognition logic
4. Visual feedback component
5. Tuner display with cent deviation
```

---

## 🎨 **Prompt Series 2: Advanced Visualization**

### **Prompt 2.1: Interactive Fretboard**
```
Create an interactive guitar fretboard visualization component.

Requirements:
- SVG-based fretboard rendering
- Display chord shapes with finger positions
- Show multiple positions for same chord
- Clickable frets to build custom chords
- Scale visualization across fretboard
- Left-handed mode
- Different tunings (standard, drop D, etc.)

Deliverables:
1. Fretboard component (Blazor or React)
2. Chord shape data structure
3. Position calculator
4. Interactive click handlers
5. Integration with chat messages
6. Export as image
```

### **Prompt 2.2: Circle of Fifths Visualization**
```
Implement an interactive Circle of Fifths component.

Requirements:
- SVG circle with all 12 keys
- Highlight current key
- Show related keys (relative minor, parallel minor)
- Click to change key
- Display common chords in selected key
- Show modulation paths
- Color-coded by key signature

Deliverables:
1. Circle of Fifths component
2. Key relationship calculator
3. Chord suggestions for selected key
4. Modulation path finder
5. Integration with chord search
```

### **Prompt 2.3: Chord Diagram Generator**
```
Create a professional chord diagram generator.

Requirements:
- Generate chord diagrams from chord data
- Multiple notation styles (boxes, dots, numbers)
- Finger position indicators (1, 2, 3, 4)
- Barre chord notation
- Muted/open string indicators
- Export as SVG/PNG
- Printable format

Deliverables:
1. Chord diagram component
2. Rendering engine
3. Style customization
4. Export functionality
5. Print-optimized layout
```

---

## 📚 **Prompt Series 3: Interactive Learning**

### **Prompt 3.1: Lesson System Architecture**
```
Design a comprehensive lesson system for structured learning.

Requirements:
- Curriculum structure (modules, lessons, exercises)
- Progress tracking per user
- Skill level assessment
- Prerequisite management
- Completion tracking
- Quiz/assessment integration
- Personalized learning paths

Deliverables:
1. Database schema for lessons and progress
2. Lesson content management system
3. Progress tracking service
4. Assessment engine
5. Recommendation algorithm
6. UI for lesson navigation
```

### **Prompt 3.2: Interactive Exercises**
```
Create interactive music theory exercises with immediate feedback.

Exercise Types:
- Chord identification (audio or visual)
- Interval recognition
- Scale degree identification
- Chord progression analysis
- Rhythm reading
- Ear training

Deliverables:
1. Exercise framework
2. Question generation system
3. Answer validation
4. Feedback mechanism
5. Scoring and analytics
6. Difficulty progression
```

### **Prompt 3.3: Gamification System**
```
Implement gamification to increase engagement and motivation.

Requirements:
- Points system for activities
- Achievement badges
- Daily streaks
- Leaderboards (optional)
- Progress milestones
- Challenges and quests
- Reward unlocks

Deliverables:
1. Points and rewards system
2. Badge definitions and artwork
3. Streak tracking
4. Achievement notification UI
5. Progress visualization
6. Motivation messages
```

---

## 🎼 **Prompt Series 4: Music Analysis**

### **Prompt 4.1: Chord Progression Analyzer**
```
Build an intelligent chord progression analysis system.

Requirements:
- Parse chord progression input (text or structured)
- Identify key and mode
- Detect common patterns (I-V-vi-IV, ii-V-I, etc.)
- Analyze harmonic function (tonic, dominant, subdominant)
- Identify borrowed chords and modulations
- Suggest improvements or variations
- Generate analysis report

Deliverables:
1. Progression parser
2. Key detection algorithm
3. Pattern recognition engine
4. Harmonic analysis service
5. Suggestion generator
6. Analysis report component
```

### **Prompt 4.2: Scale Recommendation Engine**
```
Create a system to recommend scales for improvisation over chord progressions.

Requirements:
- Analyze chord progression
- Identify compatible scales/modes
- Rank by compatibility
- Show scale degrees over each chord
- Highlight chord tones vs. tensions
- Suggest approach notes
- Visual scale display on fretboard

Deliverables:
1. Scale compatibility analyzer
2. Mode identification
3. Ranking algorithm
4. Scale-to-chord mapper
5. Improvisation suggestions
6. Integration with fretboard visualization
```

### **Prompt 4.3: Voice Leading Optimizer**
```
Implement voice leading analysis and optimization.

Requirements:
- Analyze voice movement between chords
- Identify smooth vs. disjunct motion
- Detect parallel fifths/octaves
- Suggest optimal voicings
- Minimize voice movement
- Consider register and range
- Generate voice leading diagram

Deliverables:
1. Voice leading analyzer
2. Voicing optimizer
3. Rule checker (parallel motion, etc.)
4. Alternative voicing generator
5. Visual voice leading diagram
6. Integration with chord display
```

---

## 👤 **Prompt Series 5: Personalization**

### **Prompt 5.1: User Profile System**
```
Design and implement a comprehensive user profile system.

Requirements:
- User authentication (email, OAuth)
- Profile information (skill level, instrument, goals)
- Preferences (theme, notation style, tuning)
- Learning history
- Favorite chords/progressions
- Custom tags and notes
- Privacy controls

Deliverables:
1. Authentication system
2. User database schema
3. Profile management UI
4. Preference storage
5. Data export/import
6. Privacy settings
```

### **Prompt 5.2: Adaptive AI Personality**
```
Create an adaptive AI that learns from user interactions.

Requirements:
- Track user's skill level over time
- Adjust explanation complexity
- Remember user preferences
- Personalize recommendations
- Adapt teaching style
- Provide contextual help
- Celebrate progress

Deliverables:
1. User modeling system
2. Skill level assessment
3. Adaptive response generator
4. Personalization engine
5. Progress celebration system
6. Context-aware help
```

### **Prompt 5.3: Custom Collections**
```
Implement a system for users to save and organize musical content.

Requirements:
- Save chord progressions
- Create setlists
- Organize by project/song
- Tag and categorize
- Search and filter
- Share collections
- Export in various formats

Deliverables:
1. Collection data model
2. CRUD operations
3. Organization UI
4. Search and filter
5. Sharing mechanism
6. Export functionality
```

---

## 🎵 **Prompt Series 6: AI Composition**

### **Prompt 6.1: Chord Progression Generator**
```
Build an AI-powered chord progression generator.

Requirements:
- Generate progressions by genre (jazz, pop, rock, etc.)
- Mood/emotion-based generation (happy, sad, dark, bright)
- Complexity control (simple to advanced)
- Length specification (4, 8, 16 bars)
- Key selection
- Variation generator
- Export and save

Deliverables:
1. Progression generation algorithm
2. Genre templates
3. Mood mapping system
4. Variation generator
5. Quality scoring
6. Integration with chat interface
```

### **Prompt 6.2: Song Structure Assistant**
```
Create an AI assistant for song structure and arrangement.

Requirements:
- Suggest verse/chorus/bridge structures
- Recommend section lengths
- Propose key changes
- Suggest dynamics and intensity
- Provide transition ideas
- Analyze existing song structures
- Generate arrangement templates

Deliverables:
1. Structure templates by genre
2. Section recommendation engine
3. Transition generator
4. Arrangement analyzer
5. Visual structure diagram
6. Export to DAW format
```

### **Prompt 6.3: Harmonic Variation Generator**
```
Implement a system to generate variations of chord progressions.

Requirements:
- Substitute chords (diatonic and chromatic)
- Add extensions (7ths, 9ths, 11ths, 13ths)
- Reharmonization suggestions
- Modal interchange
- Secondary dominants
- Tritone substitutions
- Maintain harmonic function

Deliverables:
1. Substitution rules engine
2. Extension generator
3. Reharmonization algorithm
4. Variation ranker
5. Side-by-side comparison
6. Explanation of changes
```

---

## 🔌 **Prompt Series 7: Integration & Export**

### **Prompt 7.1: Multi-Format Export**
```
Implement comprehensive export functionality.

Requirements:
- PDF export (chord charts, tabs, lessons)
- MIDI file generation
- MusicXML export
- Guitar Pro format
- Plain text
- JSON/CSV for data
- Image export (PNG, SVG)

Deliverables:
1. Export service architecture
2. Format converters
3. Template system for PDF
4. MIDI generator
5. MusicXML writer
6. Export UI and options
```

### **Prompt 7.2: Sharing & Collaboration**
```
Create sharing and collaboration features.

Requirements:
- Share chord progressions via link
- Embed in websites
- Social media integration
- Collaborative editing
- Comments and feedback
- Version history
- Access control

Deliverables:
1. Sharing service
2. Link generation
3. Embed code generator
4. Real-time collaboration (SignalR)
5. Comment system
6. Version control
```

---

## 🎮 **Prompt Series 8: Practice & Performance**

### **Prompt 8.1: Practice Mode**
```
Design a dedicated practice mode with tools and tracking.

Requirements:
- Practice session timer
- Loop sections
- Slow down tempo
- Track practice time
- Set goals and reminders
- Practice log
- Progress analytics

Deliverables:
1. Practice mode UI
2. Session timer
3. Loop functionality
4. Tempo control
5. Practice tracking database
6. Analytics dashboard
```

### **Prompt 8.2: Backing Track Generator**
```
Create a system to generate simple backing tracks.

Requirements:
- Generate tracks from chord progressions
- Multiple styles (rock, jazz, blues, etc.)
- Adjustable tempo
- Instrument selection
- Loop functionality
- Export as audio file
- Sync with metronome

Deliverables:
1. Backing track generator
2. Style templates
3. Audio synthesis
4. Tempo and key control
5. Export functionality
6. Integration with practice mode
```

---

## 🤔 **Reflection Prompts**

### **Reflection 1: User Value**
```
For each proposed enhancement, reflect on:
1. Who is the target user? (beginner, intermediate, advanced)
2. What problem does it solve?
3. How does it improve the learning experience?
4. What is the expected usage frequency?
5. Does it align with our core mission?
```

### **Reflection 2: Technical Feasibility**
```
For each enhancement, consider:
1. What are the technical dependencies?
2. What is the estimated development time?
3. What are the performance implications?
4. What are the maintenance requirements?
5. Are there existing libraries we can leverage?
```

### **Reflection 3: Prioritization**
```
Rank enhancements by:
1. User value (1-10)
2. Development effort (1-10)
3. Strategic importance (1-10)
4. Innovation factor (1-10)
5. Calculate priority score: (Value × Strategic) / Effort
```

---

**Use these prompts to systematically explore and implement powerful enhancements! 🚀**

