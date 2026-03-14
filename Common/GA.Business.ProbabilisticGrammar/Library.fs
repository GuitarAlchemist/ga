/// GA.Business.ProbabilisticGrammar
/// Public API for probabilistic grammar weights in Guitar Alchemist.
///
/// Entry points:
///   ProbabilisticGrammar.forGrammar   – load or initialise a named grammar
///   ProbabilisticGrammar.update       – Bayesian weight update from feedback
///   ProbabilisticGrammar.generate     – probabilistic chord progression generation
///   ProbabilisticGrammar.evolve       – replicator-dynamics evolution pass
module GA.Business.ProbabilisticGrammar.ProbabilisticGrammar

open System
open GA.Business.DSL.Types.GrammarTypes

// ---------------------------------------------------------------------------
// Seed rules for the built-in ChordProgression grammar
// ---------------------------------------------------------------------------

let private chordProgressionDefaults () : WeightedMusicRule list =
    let mk id prod ctx =
        WeightedMusicRule.create id prod ChordGrammar (Some ctx)
    [ mk "cp_I_IV_V_I"    "I IV V I"           "major"
      mk "cp_I_V_vi_IV"   "I V vi IV"          "major"
      mk "cp_ii_V_I"      "ii V I"             "jazz"
      mk "cp_I_vi_IV_V"   "I vi IV V"          "major"
      mk "cp_i_VII_VI_VII" "i VII VI VII"       "minor"
      mk "cp_I_III_IV_iv"  "I III IV iv"        "major"
      mk "cp_vi_IV_I_V"    "vi IV I V"          "major"
      mk "cp_i_iv_VII_III" "i iv VII III"       "minor" ]

let private scaleChoiceDefaults () : WeightedMusicRule list =
    let mk id prod ctx =
        WeightedMusicRule.create id prod ScaleGrammar (Some ctx)
    [ mk "sc_major"        "major"              "classical"
      mk "sc_dorian"       "dorian"             "jazz"
      mk "sc_mixolydian"   "mixolydian"         "blues"
      mk "sc_pentatonic_m" "minor pentatonic"   "blues"
      mk "sc_lydian"       "lydian"             "jazz"
      mk "sc_diminished"   "whole-half dim"     "jazz" ]

// ---------------------------------------------------------------------------
// Top-level API
// ---------------------------------------------------------------------------

/// Load persisted weights for a grammar, or use built-in defaults if none saved.
/// grammarName should match the EBNF file stem, e.g. "ChordProgression".
let forGrammar (grammarName: string) : WeightedMusicRule list =
    let defaults =
        match grammarName with
        | "ChordProgression" -> chordProgressionDefaults()
        | "ScaleTransformation" -> scaleChoiceDefaults()
        | _ -> []
    WeightPersistence.loadOrInit grammarName defaults

/// Apply a Bayesian weight update for a rule, persist, and return updated list.
let update (grammarName: string) (ruleId: string) (success: bool) (rules: WeightedMusicRule list) : WeightedMusicRule list =
    let updated =
        rules
        |> List.map (fun r ->
            if r.RuleId = ruleId then WeightedMusicRule.bayesianUpdate r success
            else r)
    WeightPersistence.save grammarName updated |> ignore
    updated

/// Generate a chord progression using weighted grammar rules.
let generate (config: GenerationConfig) (rules: WeightedMusicRule list) : Chord list =
    ConstrainedGeneration.generateProgression rules config

/// Generate scale choices for a set of chord changes.
let scaleChoices (changes: (Chord * int) list) (rules: WeightedMusicRule list) : Scale list =
    ConstrainedGeneration.generateScaleChoices rules changes

/// Run one full evolution cycle (Bayesian updates + replicator dynamics).
/// outcomes is a list of (ruleId, wasGood) feedback pairs.
/// Returns the evolved rules (also persisted to disk).
let evolve (grammarName: string) (outcomes: (string * bool) list) (rules: WeightedMusicRule list) : SimResult =
    let result = MusicReplicator.evolveFromPreferences rules outcomes
    // Persist updated weights derived from the evolution
    let evolved =
        result.FinalSpecies
        |> List.choose (fun s ->
            rules |> List.tryFind (fun r -> r.RuleId = s.RuleId)
            |> Option.map (fun r -> { r with Weight = s.Proportion }))
    WeightPersistence.save grammarName evolved |> ignore
    result

/// Return the current top-N rules by weight for a grammar.
let topRules (n: int) (rules: WeightedMusicRule list) : WeightedMusicRule list =
    WeightedMusicRule.topN n rules
