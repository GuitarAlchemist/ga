# Routing ambiguity diagnostic — 2026-06-16

Source anchors: `routing-anchors-2026-06-16.json` · extension: `ix.duckdb_extension` · engine: DuckDB + ix UDFs.

The `SemanticIntentRouter` routes a query to the intent whose example/description
embedding it is closest to (cosine). Low **silhouette** = an intent's example
prompts sit close to other intents' → fragile routing. High **nearest wrong-neighbour
cosine** between two intents names a confusable PAIR; fix by contrasting their
example prompts (the semantic, no-keyword-rule lever).

Sidecars (machine-readable): `routing-silhouette-by-intent.json`,
`routing-confusable-pairs.json`, `routing-pca-coords.json`.
|         section         |
|-------------------------|
| ## Overall separability |
| overall_mean_silhouette | n_example_anchors | n_intents |
|------------------------:|------------------:|----------:|
| 0.0389                  | 352               | 30        |
|                           section                           |
|-------------------------------------------------------------|
| ## Per-intent separability (lowest first = most confusable) |
|          intent_id          | n_examples | mean_silhouette | min_silhouette |
|-----------------------------|-----------:|----------------:|---------------:|
| skill.chordsubstitution     | 12         | -0.0518         | -0.1821        |
| skill.modes                 | 37         | -0.0422         | -0.2053        |
| skill.interval              | 13         | -0.0407         | -0.151         |
| skill.scaleinfo             | 14         | -0.0283         | -0.2599        |
| skill.transpose             | 13         | -0.0279         | -0.1483        |
| skill.chordinfo             | 26         | -0.0275         | -0.1074        |
| skill.genreessentials       | 8          | -0.0262         | -0.1865        |
| skill.chordvoicings         | 12         | -0.0248         | -0.1591        |
| skill.rememberthis          | 7          | -0.0123         | -0.0626        |
| skill.progressioncompletion | 5          | -0.0064         | -0.0828        |
| skill.circleoffifths        | 10         | -0.0044         | -0.1766        |
| skill.grothendieckdelta     | 10         | 0.0055          | -0.0739        |
| skill.relativekey           | 12         | 0.0062          | -0.2383        |
| skill.improvisation         | 10         | 0.0127          | -0.0436        |
| skill.icvneighbors          | 10         | 0.0355          | -0.083         |
| skill.diatonicchords        | 20         | 0.04            | -0.0537        |
| skill.practiceroutine       | 9          | 0.0537          | -0.1247        |
| skill.whatcanyoudo          | 14         | 0.0539          | -0.0425        |
| skill.commontones           | 9          | 0.0542          | -0.3047        |
| skill.grothendieckparse     | 10         | 0.0648          | -0.0423        |
| skill.icvshortestpath       | 10         | 0.0765          | -0.0283        |
| skill.intervalclassvector   | 10         | 0.0774          | -0.1176        |
| skill.voiceleading          | 10         | 0.0919          | -0.0174        |
| skill.alternatetunings      | 10         | 0.0965          | -0.0279        |
| skill.keyidentification     | 7          | 0.1058          | 0.0934         |
| skill.progressionmood       | 15         | 0.1161          | -0.0437        |
| skill.capo                  | 10         | 0.204           | 0.0775         |
| skill.beginnerchords        | 7          | 0.2795          | 0.1581         |
| skill.settheoryequivalence  | 5          | 0.3048          | 0.1628         |
| skill.theorycomparison      | 7          | 0.4788          | 0.3661         |
|                                 section                                 |
|-------------------------------------------------------------------------|
| ## Top confusable intent pairs (nearest wrong-neighbour, avg cos > 0.5) |
|        a_intent         |        b_intent         | n_anchors_nearest | avg_nearest_cos | max_cos |
|-------------------------|-------------------------|------------------:|----------------:|--------:|
| skill.relativekey       | skill.circleoffifths    | 1                 | 0.9773          | 0.9773  |
| skill.relativekey       | skill.theorycomparison  | 1                 | 0.8966          | 0.8966  |
| skill.icvneighbors      | skill.chordinfo         | 1                 | 0.8807          | 0.8807  |
| skill.chordinfo         | skill.chordsubstitution | 2                 | 0.8596          | 0.8901  |
| skill.transpose         | skill.scaleinfo         | 1                 | 0.8545          | 0.8545  |
| skill.chordsubstitution | skill.relativekey       | 1                 | 0.853           | 0.853   |
| skill.icvneighbors      | skill.grothendieckdelta | 2                 | 0.8499          | 0.8892  |
| skill.diatonicchords    | skill.scaleinfo         | 1                 | 0.8481          | 0.8481  |
| skill.scaleinfo         | skill.diatonicchords    | 1                 | 0.8481          | 0.8481  |
| skill.chordsubstitution | skill.chordinfo         | 3                 | 0.846           | 0.8901  |
| skill.theorycomparison  | skill.relativekey       | 7                 | 0.8428          | 0.8966  |
| skill.grothendieckdelta | skill.icvneighbors      | 3                 | 0.8423          | 0.8892  |
| skill.chordinfo         | skill.transpose         | 1                 | 0.8319          | 0.8319  |
| skill.icvneighbors      | skill.commontones       | 1                 | 0.8315          | 0.8315  |
| skill.relativekey       | skill.chordsubstitution | 2                 | 0.8309          | 0.853   |
| skill.commontones       | skill.chordinfo         | 6                 | 0.8225          | 0.881   |
| skill.relativekey       | skill.commontones       | 1                 | 0.8221          | 0.8221  |
| skill.scaleinfo         | skill.relativekey       | 5                 | 0.8207          | 0.8697  |
| skill.icvneighbors      | skill.chordsubstitution | 1                 | 0.819           | 0.819   |
| skill.scaleinfo         | skill.chordinfo         | 3                 | 0.8185          | 0.8699  |
| skill.transpose         | skill.chordinfo         | 2                 | 0.8106          | 0.8319  |
| skill.scaleinfo         | skill.theorycomparison  | 1                 | 0.8102          | 0.8102  |
| skill.icvshortestpath   | skill.grothendieckdelta | 4                 | 0.8077          | 0.8748  |
| skill.relativekey       | skill.scaleinfo         | 6                 | 0.8075          | 0.8697  |
| skill.icvneighbors      | skill.improvisation     | 1                 | 0.8024          | 0.8024  |
|                                      section                                       |
|------------------------------------------------------------------------------------|
| ## Worst individual anchor collisions (an example prompt closer to a wrong intent) |
|        a_intent         |                   a_text                    |        b_intent         |                   b_text                    |  cos   |
|-------------------------|---------------------------------------------|-------------------------|---------------------------------------------|-------:|
| skill.relativekey       | How many sharps in D major                  | skill.circleoffifths    | How many sharps does D major have?          | 0.9773 |
| skill.circleoffifths    | How many sharps does D major have?          | skill.relativekey       | How many sharps in D major                  | 0.9773 |
| skill.theorycomparison  | Major versus minor                          | skill.relativekey       | Relative major of A minor                   | 0.8966 |
| skill.relativekey       | Relative major of A minor                   | skill.theorycomparison  | Major versus minor                          | 0.8966 |
| skill.theorycomparison  | Major vs minor                              | skill.relativekey       | Relative major of A minor                   | 0.894  |
| skill.chordinfo         | tell me about a Cmaj7 chord                 | skill.chordsubstitution | Alternative chord for Cmaj7                 | 0.8901 |
| skill.chordsubstitution | Alternative chord for Cmaj7                 | skill.chordinfo         | tell me about a Cmaj7 chord                 | 0.8901 |
| skill.icvneighbors      | what chords are harmonically close to Cmaj7 | skill.grothendieckdelta | how close are Cmaj7 and Fmaj7 harmonically  | 0.8892 |
| skill.grothendieckdelta | how close are Cmaj7 and Fmaj7 harmonically  | skill.icvneighbors      | what chords are harmonically close to Cmaj7 | 0.8892 |
| skill.chordinfo         | What notes are in a Cmaj7?                  | skill.commontones       | What notes do Cmaj7 and Am7 share?          | 0.881  |
| skill.commontones       | What notes do Cmaj7 and Am7 share?          | skill.chordinfo         | What notes are in a Cmaj7?                  | 0.881  |
| skill.chordinfo         | What is a C major chord?                    | skill.icvneighbors      | nearby chords to C major                    | 0.8807 |
| skill.icvneighbors      | nearby chords to C major                    | skill.chordinfo         | What is a C major chord?                    | 0.8807 |
| skill.grothendieckdelta | harmonic distance from Cmaj7 to G7          | skill.icvshortestpath   | shortest harmonic path from Cmaj7 to G7     | 0.8748 |
| skill.icvshortestpath   | shortest harmonic path from Cmaj7 to G7     | skill.grothendieckdelta | harmonic distance from Cmaj7 to G7          | 0.8748 |
| skill.scaleinfo         | What is C major?                            | skill.chordinfo         | What is a C major chord?                    | 0.8699 |
| skill.relativekey       | Relative minor of D major                   | skill.scaleinfo         | What is D minor?                            | 0.8697 |
| skill.scaleinfo         | What is D minor?                            | skill.relativekey       | Relative minor of D major                   | 0.8697 |
| skill.voiceleading      | best voicing from G7 to Cmaj7               | skill.chordvoicings     | voicings for Cmaj7                          | 0.8622 |
| skill.chordvoicings     | voicings for Cmaj7                          | skill.voiceleading      | best voicing from G7 to Cmaj7               | 0.8622 |

