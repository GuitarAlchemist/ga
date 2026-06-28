# Beyond-LLM: GA Product Scenario Map

**Status: AI-drafted evaluation for human review** — issue #483 (P1, max_autonomy: draft). Verdicts are recommendations, not decisions.

Related: GuitarAlchemist/tars#104, GuitarAlchemist/ix#212, GuitarAlchemist/ix#205.

## Purpose

This document maps the candidate "beyond-LLM" scenarios from issue #483 onto **surfaces that already exist in Guitar Alchemist**. It is the product-scenario half of the evaluation; the architecture-family verdicts and tracer bullets live in [`world-models-diffusion-ga-eval.md`](./world-models-diffusion-ga-eval.md).

Framing rules (from the issue's non-goals, kept front-of-mind):

- **Local-first.** GA runs without required cloud services. Any scenario that only works behind a paid hosted API is a non-starter for the core product.
- **No architecture replacement.** OPTIC-K (the 240-dim musical embedding) and the 5-layer architecture stay. AI code lives in `GA.Business.ML` (layer 4); orchestration in layer 5. Nothing here proposes moving that boundary.
- **Product value over demos.** Every scenario is judged by "what changes for a guitarist using GA," not by how impressive the model is.

## GA surfaces referenced

| Surface | Where it lives | What it does today |
|---|---|---|
| Hand-pose service | `Apps/hand-pose-service/` (FastAPI + MediaPipe Hands, 21 keypoints/hand, maps keypoints → string/fret) | Detects a hand pose from an image and maps it to fretboard positions. Reactive only — no prediction. |
| Sound-bank service | `Apps/sound-bank-service/` (FastAPI + MusicGen, async job queue, sample cache) | Generates guitar samples from parameters; caches identical requests. |
| Prime Radiant 3D scene | `Apps/ga-client` (React + R3F / Three.js; ForceGraph3D, skybox, instanced nodes, some WebGPU/TSL) | Renders governance + domain visualizations in 3D. |
| Godot guitar scene | Godot integration (3D guitar/fretboard scene) | Interactive 3D fretboard / instrument model. |
| Fretboard / chord / scale viz | `GA.Business.Core.Fretboard`, React fretboard components | Renders chords, scales, voicings statically and interactively. |
| OPTIC-K voicing index | `Common/GA.Business.ML/Embeddings`, `state/voicings/optick.index` (~313k voicings) | Musical-cosine voicing search; the existing embedding spine. |

## Scenario evaluations

Seven candidate scenarios; all evaluated (the issue requires ≥5). Each gets: GA surface mapping today, user pain, and what "good" looks like.

### 1. `hand_pose_prediction_for_chords`

- **GA surface today:** `Apps/hand-pose-service/` already detects 21 keypoints/hand and maps to string/fret. It is purely **reactive** — it tells you where the hand *is*, never where it *should go next*.
- **User pain:** A learner sees the current chord shape but gets no anticipatory guidance for the *next* chord in a progression. Transition fumbles (e.g. C → F barre) are the hardest part of early practice, and current GA gives zero help on the motion between shapes.
- **What "good" looks like:** Given the current detected pose + a target chord, predict a plausible *finger trajectory* (or at least the target keypoint configuration) and overlay it as a "ghost hand" guide. Latency must be interactive (sub-frame-budget for overlay; pose detection itself already runs per-frame). Predictions must be musically valid (reachable fingerings consistent with `GA.Business.Core` voicing rules), not just visually smooth.

### 2. `gesture_to_strumming_simulation`

- **GA surface today:** No direct surface. Hand-pose service sees the fretting hand; there is no strumming-hand → audio path. Sound-bank generates samples but not from gesture.
- **User pain:** Rhythm and dynamics are taught abstractly. A learner can fret a chord correctly but has no feedback on whether their strumming gesture produces the intended rhythmic/dynamic pattern.
- **What "good" looks like:** A strum gesture (pose-sequence or input device) drives a simulated strum — per-string onset timing, direction (down/up), velocity → which strings sound, how hard. Output feeds existing audio playback. "Good" = the simulated strum is recognizably the user's gesture (down-up-down feel preserved) and runs in real time locally.

### 3. `3d_guitar_scene_world_model`

- **GA surface today:** Godot 3D guitar scene + R3F Prime Radiant. These are **rendered** scenes with scripted/animation behavior, not learned dynamics. There is no model that predicts "what the scene looks like one step ahead."
- **User pain:** Mostly a *developer/engine* pain rather than an end-user pain. The current 3D scene is fully hand-authored; there is no obvious gap a learned world model fills for the guitarist today.
- **What "good" looks like:** Honestly, unclear at product level. A learned world model of a deterministic, hand-authored 3D guitar scene predicts dynamics we already fully specify in code — low marginal value. "Good" would require a genuinely stochastic/physical element (e.g. string vibration, hand-instrument contact physics) where prediction beats simulation, and that bar is high. Flagged as the weakest product fit.

### 4. `music_game_environment_simulation`

- **GA surface today:** No game-environment surface ships today. The Godot scene + R3F are the nearest substrate. GA has rich music-theory state (chords, scales, voicings, OPTIC-K) that a game could consume.
- **User pain:** Practice is not engaging for many learners; gamified, adaptive challenges (rhythm-game-style, but theory-aware) could drive retention. The pain is "practice is boring / I don't know what to practice next."
- **What "good" looks like:** An environment that generates the *next* practice challenge adaptively based on the player's recent performance — difficulty, chord vocabulary, tempo tuned to keep the learner in the smart zone. This is more a *sequence/recommendation* problem than a generative-simulation one; the "world model" framing is a stretch.

### 5. `audio_texture_or_soundbank_generation`

- **GA surface today:** `Apps/sound-bank-service/` already does this with MusicGen + an async job queue + caching. This is the **most mature** surface for a beyond-LLM model — it already exists.
- **User pain:** Sample generation is slow (MusicGen is heavy), the dependency is large, and "guitar sound from parameters" quality/controllability is variable. Users want fast, controllable, on-device timbres, not minute-long cloud-ish generations.
- **What "good" looks like:** Faster, more controllable local generation of short guitar textures/one-shots with predictable timbral knobs (brightness, pick attack, body resonance). "Good" = sub-second for a one-shot on commodity hardware, with a controllability handle the current MusicGen path lacks. This is the surface where a *smaller specialist local model* could replace a heavy general one.

### 6. `visual_feedback_generation`

- **GA surface today:** Fretboard/chord/scale viz + R3F overlays render *correctness* feedback today (right/wrong notes, suggested fingerings) from deterministic theory, not from a generative model.
- **User pain:** Feedback is currently binary/rule-based ("this note is out of the scale"). It does not synthesize *novel* visual guidance (e.g. a generated heat-map of likely error zones, or an illustrative overlay tuned to the user's mistakes).
- **What "good" looks like:** Richer, learner-specific visual feedback overlays. But note: most valuable feedback here is **deterministic** (theory says the note is wrong) and needs no generative model. "Good" is mostly achievable with existing rule-based viz; a generative model adds value only at the margins (style/illustration), so product value is modest.

### 7. `practice_session_replay_and_prediction`

- **GA surface today:** No dedicated replay/prediction surface. GA captures musical state and (via hand-pose) can observe play, but there is no timeline model of a practice session.
- **User pain:** Learners can't see their own trajectory ("am I getting better at C→F?") and GA can't anticipate the next mistake or recommend the next drill from the session timeline. Practice is un-instrumented.
- **What "good" looks like:** Record a session as a timeline (poses, notes, timing, errors), then (a) replay it and (b) predict the next likely error / recommend the next drill. This is a **sequence-model** problem over GA's own captured timelines — local, small training data, directly product-valuable, and it reuses the existing hand-pose + theory stack.

## Scenario ranking (product fit, AI-drafted)

| Rank | Scenario | Product fit | Why |
|---|---|---|---|
| 1 | `hand_pose_prediction_for_chords` | High | Reuses live hand-pose service; clear unmet pain (transitions); ghost-hand UX is concrete and measurable. |
| 2 | `practice_session_replay_and_prediction` | High | Sequence model over GA's own data; small data need; directly instruments practice. |
| 3 | `audio_texture_or_soundbank_generation` | Medium-High | Surface already exists; the win is *smaller/faster/local*, not new capability. |
| 4 | `gesture_to_strumming_simulation` | Medium | Compelling but needs a strumming-hand capture path GA doesn't have yet. |
| 5 | `music_game_environment_simulation` | Medium | Strong retention upside; mostly a recommendation problem, not a world model. |
| 6 | `visual_feedback_generation` | Low-Medium | Best feedback is deterministic; generative adds only marginal value. |
| 7 | `3d_guitar_scene_world_model` | Low | Predicts dynamics we already author deterministically; weakest product fit. |

## Handoff

The architecture-family verdicts (adopt/watch/experiment/defer/reject), scoring against the issue's 10 criteria, the concrete tracer bullet, and the dependency/cost/latency/integration-risk section are in [`world-models-diffusion-ga-eval.md`](./world-models-diffusion-ga-eval.md).
