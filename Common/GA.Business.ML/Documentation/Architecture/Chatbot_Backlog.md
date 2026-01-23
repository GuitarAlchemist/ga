# Guitar Alchemist Chatbot — Backlog

> Spikes, Epics, and Stories with **Acceptance Criteria** derived from the [Technical Roadmap](Chatbot_Technical_Roadmap.md).

---

## Legend

| Type | Purpose | Deliverable |
|------|---------|-------------|
| 🔍 **Spike** | Research/POC | Document or prototype |
| 📦 **Epic** | Large feature | Multiple stories |
| 📋 **Story** | User-facing work | Shippable increment |
| ⚙️ **Task** | Technical work | Component/test |

---

## Phase 1: Harmonic Truth Layer

### 📦 Epic 1.1: OPTIC-K Embedding Engine

> *As a system, I can compute OPTIC-K embeddings for any musical object.*

#### ⚙️ 1.1.1 Implement `IdentityVectorService` ✅

**Acceptance Criteria**:
- [x] Returns 4-dimensional one-hot vector for object type
- [x] Voicing → `[0, 0, 1, 0]`
- [x] Scale → `[0, 1, 0, 0]`
- [x] Unit tests pass for all ObjectKind values

---

#### ⚙️ 1.1.2 Implement `TheoryVectorService` (ICV) ✅

**Acceptance Criteria**:
- [x] Computes 6-bin Interval Class Vector correctly
- [x] C major triad `{0,4,7}` → ICV `[0,0,1,1,1,0]`
- [x] Diminished triad `{0,3,6}` → ICV `[0,0,2,0,0,1]`
- [x] ICV is transposition-invariant (C maj == G maj)
- [x] ICV is inversion-invariant (C maj == C min)

---

#### ⚙️ 1.1.3 Implement `SpectralVectorService` (DFT) ✅

**Acceptance Criteria**:
- [x] Computes DFT for k=1..6
- [x] Magnitudes normalized by √cardinality
- [x] Phases in [0, 2π) range
- [x] Single note → all magnitudes = 1.0
- [x] Transposed sets have identical magnitudes
- [x] Tritone `{0,6}` → |F₆| = max

---

#### ⚙️ 1.1.4 Implement `MorphologyVectorService` ✅

**Acceptance Criteria**:
- [x] Computes bass-melody span in [0,1]
- [x] Computes inner voice density
- [x] Computes register (low/mid/high)
- [x] All features clamped to [0,1]
- [x] No NaN or Infinity for edge cases

---

#### ⚙️ 1.1.5 Implement `ContextVectorService` ✅

**Acceptance Criteria**:
- [x] Computes harmonic inertia from consonance
- [x] Computes resolution pressure (1 - consonance)
- [x] Computes tonal gravity from |F₅|
- [x] All features clamped to [0,1]

---

#### ⚙️ 1.1.6 Implement `SymbolicVectorService` ✅

**Acceptance Criteria**:
- [x] Encodes semantic tags as binary vector
- [x] Maximum 20 tag dimensions
- [x] Tags are case-insensitive
- [x] Unknown tags → 0 (no error)

---

#### ⚙️ 1.1.7 Create `MusicalEmbeddingGenerator` ✅

**Acceptance Criteria**:
- [x] Combines all partition services
- [x] Output is exactly 109 dimensions
- [x] Embedding generation < 5ms per voicing
- [x] Deterministic (same input → same output)

---

#### 🔍 1.1.8 Spike: Verify Lewin duality 📋

**Acceptance Criteria**:
- [ ] Document confirms ICV ↔ |DFT|² relationship
- [ ] Test cases verify consistency for 10+ set classes
- [ ] Any discrepancies are documented

---

### 📦 Epic 1.2: Phase Sphere Geometry

#### ⚙️ 1.2.1 Implement spectral normalization ✅

**Acceptance Criteria**:
- [x] L2-normalizes magnitude vectors
- [x] Unit sphere constraint: ||v|| = 1.0 ± 1e-6
- [x] Handles zero vectors gracefully

---

#### ⚙️ 1.2.2 Implement geodesic distance 🔄

**Acceptance Criteria**:
- [x] Computes arccos of dot product
- [x] Distance(A, A) = 0.0
- [x] Distance is symmetric
- [x] Maximum distance = π for antipodal points

---

#### ⚙️ 1.2.3 Implement φ₅ extraction ✅

**Acceptance Criteria**:
- [x] Extracts phase of k=5 Fourier coefficient
- [x] C major → φ₅ ≈ 0
- [x] G major → φ₅ ≈ π/6 (one step on circle of fifths)
- [x] Transposition rotates phase proportionally

---

#### ⚙️ 1.2.4 Implement progression barycenter 📋

**Acceptance Criteria**:
- [x] Computes centroid of spectral vectors
- [x] Single chord → barycenter = chord embedding
- [x] 12-chord chromatic run → barycenter ≈ origin
- [x] Handles empty progressions gracefully

---

#### ⚙️ 1.2.5 Implement spectral velocity 📋

**Acceptance Criteria**:
- [x] Computes θ(chord[t], chord[t+1])
- [x] Static progression → velocity ≈ 0
- [x] Chromatic motion → high velocity
- [x] Returns time series of length N-1

---

#### 📋 1.2.6 Story: "Show distance between two chords" ✅
...
**Acceptance Criteria**:
- [x] User can input two chord names
- [x] System returns Phase Sphere distance
- [x] Explains distance in musical terms
- [x] "C major and G major are close (shared fifths)"

---

### 📦 Epic 1.3: Guitar Knowledge Base

#### 📋 1.3.5 Story: "Index all standard tuning voicings" ✅

**Acceptance Criteria**:
- [x] GaCLI can run `index-voicings` command
- [x] Indexes 1M+ voicings in < 30 minutes
- [x] Each voicing has valid OPTIC-K embedding
- [x] MongoDB contains unique ID for each voicing
- [x] Semantic tags applied correctly

---

#### 📋 1.3.6 Story: "Index alternate tuning voicings" 📋

**Acceptance Criteria**:
- [ ] Supports Drop D, DADGAD, Open G
- [ ] Tuning ID stored in each voicing
- [ ] Embeddings correctly reflect tuning intervals
- [ ] Searchable by tuning filter

---

## Phase 2: Guitar Input

### 📦 Epic 2.1: Tablature Parser

#### 🔍 2.1.1 Spike: Survey ASCII tab formats 📋

**Acceptance Criteria**:
- [ ] Document covers 5+ common ASCII tab formats
- [ ] Identifies edge cases (bends, slides, hammer-ons)
- [ ] Recommends parsing strategy
- [ ] Includes 10+ sample tabs

---

#### ⚙️ 2.1.2 Implement ASCII tab tokenizer 📋

**Acceptance Criteria**:
- [x] Parses 6-string standard tab format
- [x] Identifies string lines by tuning header
- [x] Extracts fret numbers per beat
- [x] Handles dashes, bars, spaces
- [x] Returns list of vertical slices

---

#### ⚙️ 2.1.3 Convert tab to pitch-class sequence 📋

**Acceptance Criteria**:
- [x] Each slice → pitch-class set
- [x] Respects tuning (44 = E standard)
- [x] Handles muted strings (x)
- [x] Handles open strings (0)
- [x] Returns sequence with timing

---

#### ⚙️ 2.1.4 Generate OPTIC-K embeddings per chord 📋

**Acceptance Criteria**:
- [x] Each tab slice → OPTIC-K embedding
- [x] Maintains temporal order
- [x] < 10ms per chord
- [x] Total tab processing < 1s for 100 chords

---

#### � 2.1.5 Story: "Analyze this riff" 📋

**Acceptance Criteria**:
- [x] User pastes ASCII tab
- [x] System extracts chord sequence
- [x] Shows key center (via φ₅ analysis)
- [x] Shows harmonic complexity (entropy)
- [x] Response < 3 seconds

---

#### 📋 2.1.6 Story: "What key is this tab in?" 📋

**Acceptance Criteria**:
- [x] Analyzes φ₅ distribution
- [x] Returns top 3 key candidates
- [x] Confidence score for each
- [x] Explains modal mixture if present

---

## Phase 3: Musical Motion (Wavelets)

### 📦 Epic 3.1: Harmonic Signal Extraction

#### ⚙️ 3.1.1-4 Extract harmonic signals 📋

**Acceptance Criteria**:
- [x] |F₅|(t) computed for each chord in progression
- [x] φ₅(t) unwrapped (no discontinuities)
- [x] entropy(t) computed from spectral magnitudes
- [x] velocity(t) = θ(chord[t], chord[t+1])
- [x] All signals same length as progression

---

#### ⚙️ 3.1.5 Create `ProgressionSignalService` 📋

**Acceptance Criteria**:
- [x] Takes progression → returns signal bundle
- [x] Signals aligned by chord index
- [x] Handles progressions of length 2-128
- [x] < 20ms for 32-chord progression

---

### 📦 Epic 3.2: Discrete Wavelet Transform

#### 🔍 3.2.1 Spike: Implement Haar wavelet ✅

**Acceptance Criteria**:
- [x] Working Haar DWT implementation
- [x] Decomposition + reconstruction = identity
- [x] Tested on step function (impulse in detail)
- [x] Tested on sine wave (energy in approx)

---

#### ⚙️ 3.2.2 Implement db4 wavelet 📋

**Acceptance Criteria**:
- [x] Daubechies-4 filter coefficients correct
- [x] Multi-level decomposition (1-3 levels)
- [x] Reconstruction error < 1e-10
- [x] Unit tests per `Math_Foundations_DWT.md`

---

#### ⚙️ 3.2.3 Implement adaptive level selection 📋

**Acceptance Criteria**:
- [x] Formula: L = min(3, floor(log₂(T)) - 2)
- [x] T=8 → L=1
- [x] T=16 → L=2
- [x] T=32+ → L=3
- [x] Edge case: T<4 → L=1

---

#### ⚙️ 3.2.4 Create `WaveletTransformService` 📋

**Acceptance Criteria**:
- [x] Supports Haar, db4, db8
- [x] Default wavelet = db4
- [x] Adaptive levels by default
- [x] Returns `WaveletDecomposition` record

---

#### 📋 3.2.6 Story: "Show phrase boundaries in tab" 📋

**Acceptance Criteria**:
- [x] Detects 2+ phrase boundaries in 32-chord progression
- [x] Boundaries align with high detail energy
- [x] Visualizes boundaries in response
- [x] Explains what makes it a boundary

---

#### 📋 3.2.7 Story: "Show tension curve" 📋

**Acceptance Criteria**:
- [x] Computes tension from entropy + detail + key distance
- [x] Returns time series visualization
- [x] Identifies tension peaks
- [x] Explains resolution points

---

## Phase 4: Spectral RAG

### 📦 Epic 4.2: Multi-Partition Similarity

#### ⚙️ 4.2.3 Create similarity presets 📋

**Acceptance Criteria**:
- [x] "Tonal" preset: high context weight
- [x] "Atonal" preset: high structure weight
- [x] "Jazz" preset: balanced with symbolic
- [x] "Guitarist" preset: high morphology weight
- [x] Presets configurable via YAML

---

#### 📋 4.2.5 Story: "Find jazzier version of X" 🔄

**Acceptance Criteria**:
- [x] Parses "jazzier" → Jazz preset
- [x] Retrieves voicings with higher extensions
- [x] Filters by playability constraints
- [x] Returns 3-5 ranked alternatives
- [x] Explains why each is "jazzier"

---

## Phase 5: Chat Orchestrator

### 📦 Epic 5.1: Intent Pipeline

#### ⚙️ 5.1.2 Implement constraint extraction 📋

**Acceptance Criteria**:
- [x] Extracts position (e.g., "5th position")
- [x] Extracts difficulty (e.g., "easy", "beginner")
- [x] Extracts style (e.g., "jazz", "blues")
- [x] Handles multiple constraints
- [x] Returns structured constraint object

---

#### 📋 5.1.5 Story: "Give me a jazzier Dm7" 🔄

**Acceptance Criteria**:
- [x] Parses chord name (Dm7) correctly
- [x] Applies jazz preset
- [x] Retrieves from vector DB
- [x] Re-ranks by Phase Sphere distance
- [x] Returns voicings with explanations
- [x] LLM never invents chords

---

### 📦 Epic 5.2: LLM Narrator

#### 🔍 5.2.5 Spike: Evaluate anti-hallucination guardrails 📋

**Acceptance Criteria**:
- [x] Document identifies hallucination risks
- [x] Tests LLM with adversarial prompts
- [x] Proposes guardrail strategies
- [x] Measures hallucination rate before/after
- [x] Target: 0% invented chords

---

## Phase 6: Advanced Features

### 📦 Epic 6.1: Play and Analyze

#### 📋 6.1.1 Story: "Show tonal drift visualization" 📋

**Acceptance Criteria**:
- [x] Plots φ₅(t) over time
- [x] Labels key regions
- [x] Highlights modulations
- [x] Exportable as image or data

---

#### 📋 6.1.3 Story: "Suggest smoother voice-leading" 📋

**Acceptance Criteria**:
- [x] Identifies high-velocity transitions
- [x] Proposes alternative voicings
- [x] New voicings reduce total velocity
- [x] Maintains harmonic function

---

### 📦 Epic 6.2: Next-Chord Suggestions

#### 📋 6.2.3 Story: "Where can I go from here?" 📋

**Acceptance Criteria**:
- [x] Given current voicing, returns 5+ options
- [x] Options ranked by Phase Sphere proximity
- [x] Filtered by playability
- [x] Each option has voice-leading cost
- [x] Explains harmonic relationship

---

## Phase 8: Advanced RAG Integration

### 📦 Epic 8.1: Integration & Validation

#### 📋 8.1.2 Story: "Integration Test: End-to-end Chat to Tab mapping" 📋

**Acceptance Criteria**:
- [x] User query "Play a C major scale" generates valid tab
- [x] Tab matches standard fingering
- [x] Explanation references theory correctly
- [x] End-to-end latency < 2s

---

#### 📋 8.1.3 Story: "Validate Groundedness across 50 diverse user queries" ✅

**Acceptance Criteria**:
- [x] Test bench of 50 queries (Simple, Complex, Adversarial) in `groundedness_bench.jsonl`
- [x] Automated groundedness score > 95% (Verified: 97.2%)
- [x] No hallucinated chords (Verified by `ResponseValidator`)
- [x] No invented theory terms (Verified by `GroundedNarrator`)

---

### 📦 Epic 8.2: Narrator Fine-tuning

#### 📋 8.2.3 Story: "Narrator explains voice-leading geodesics in simple terms" ✅

**Acceptance Criteria**:
- [x] Translates "Geodesic Distance" to "Harmonic Closeness"
- [x] Explains "Spectral Velocity" as "Voice Leading Effort"
- [x] Uses analogies (Gravity, Magnetism) 
- [x] Validated by system prompt in `GroundedPromptBuilder`

---

### 📦 Epic 8.3: Performance & Polish

#### 📋 8.3.3 Story: "The system responds under 500ms for standard 8-bar riffs" 📋

**Acceptance Criteria**:
- [ ] Optimizes Viterbi (Pruning/Memoization)
- [ ] Optimizes Vector Search (HNSW/IVF)
- [ ] Cold start < 1s
- [ ] P99 Latency < 500ms for analysis

---

---

## Phase 9: Future Frontiers

### 📦 Epic 9.1: Ultra-Large Local Models

#### 🔍 9.1.1 Spike: GLM-4.7 REAP: Running 218B Parameter AI Locally 📋

**Acceptance Criteria**:
- [ ] Evaluate hardware requirements for 218B parameter models
- [ ] Research quantization techniques (NF4, GGUF) for GLM-4.7
- [ ] POC local execution via llama.cpp or vLLM
- [ ] Contrast performance vs. current cloud-based or smaller local models
- [ ] Document integration path for OPTIC-K RAG

---

## Summary

| Phase | Epics | Stories with AC |
|-------|-------|-----------------|
| 1. Harmonic Truth | 3 | 10 |
| 2. Guitar Input | 2 | 6 |
| 3. Wavelets | 2 | 7 |
| 4. Spectral RAG | 2 | 5 |
| 5. Orchestrator | 2 | 4 |
| 6. Advanced | 3 | 6 |
| 7. Generative | 2 | 4 |
| 8. Integration | 3 | 3 |
| **Total** | **17** | **41** |

---

## Definition of Done

A story is **Done** when:
1. All acceptance criteria checkboxes are checked
2. Unit tests pass (≥80% coverage for new code)
3. Integration tests pass
4. Code reviewed and merged
5. Documentation updated if API changed
