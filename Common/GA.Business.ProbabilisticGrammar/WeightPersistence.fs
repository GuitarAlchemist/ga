namespace GA.Business.ProbabilisticGrammar

open System
open System.IO
open Newtonsoft.Json

/// JSON-serializable DTO for a single weighted rule (avoids DU serialization issues)
[<CLIMutable>]
type WeightedRuleDto =
    { RuleId: string
      Production: string
      Alpha: float
      Beta: float
      Weight: float
      Source: string
      MusicalContext: string }   // empty string = None

/// JSON-serializable DTO for a persisted grammar weight file
[<CLIMutable>]
type GrammarWeightFile =
    { Grammar: string
      Version: string
      Timestamp: string
      Rules: WeightedRuleDto[] }

module WeightPersistence =

    // -----------------------------------------------------------------------
    // Paths
    // -----------------------------------------------------------------------

    let private defaultWeightsDir () =
        let home =
            match Environment.GetEnvironmentVariable("GA_WEIGHTS_DIR") with
            | null | "" ->
                let userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                Path.Combine(userHome, ".ga", "grammar_weights")
            | dir -> dir
        home

    let private weightFilePath (dir: string) (grammarName: string) =
        Path.Combine(dir, $"{grammarName}.weights.json")

    // -----------------------------------------------------------------------
    // DTO conversion
    // -----------------------------------------------------------------------

    let private sourceToString (source: MusicRuleSource) =
        match source with
        | ChordGrammar      -> "chord"
        | ScaleGrammar      -> "scale"
        | VoicingGrammar    -> "voicing"
        | HarmonyConstraint -> "harmony"

    let private stringToSource (s: string) =
        match s with
        | "scale"   -> ScaleGrammar
        | "voicing" -> VoicingGrammar
        | "harmony" -> HarmonyConstraint
        | _         -> ChordGrammar

    let private ruleToDto (r: WeightedMusicRule) : WeightedRuleDto =
        { RuleId       = r.RuleId
          Production   = r.Production
          Alpha        = r.Alpha
          Beta         = r.Beta
          Weight       = r.Weight
          Source       = sourceToString r.Source
          MusicalContext = r.MusicalContext |> Option.defaultValue "" }

    let private dtoToRule (dto: WeightedRuleDto) : WeightedMusicRule =
        { RuleId        = dto.RuleId
          Production    = dto.Production
          Alpha         = dto.Alpha
          Beta          = dto.Beta
          Weight        = dto.Weight
          Source        = stringToSource dto.Source
          MusicalContext = if String.IsNullOrEmpty dto.MusicalContext then None
                           else Some dto.MusicalContext }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// Save grammar weights to `~/.ga/grammar_weights/<grammarName>.weights.json`.
    /// Returns Ok () on success or Error message on failure.
    let save (grammarName: string) (rules: WeightedMusicRule list) : Result<unit, string> =
        try
            let dir = defaultWeightsDir()
            Directory.CreateDirectory(dir) |> ignore
            let file =
                { Grammar   = grammarName
                  Version   = "1.0"
                  Timestamp = DateTimeOffset.UtcNow.ToString("o")
                  Rules     = rules |> List.map ruleToDto |> Array.ofList }
            let json = JsonConvert.SerializeObject(file, Formatting.Indented)
            File.WriteAllText(weightFilePath dir grammarName, json)
            Ok ()
        with ex ->
            Error ex.Message

    /// Load grammar weights from `~/.ga/grammar_weights/<grammarName>.weights.json`.
    /// Returns Ok rules on success, Ok [] if file not found, or Error message on I/O failure.
    let load (grammarName: string) : Result<WeightedMusicRule list, string> =
        try
            let path = weightFilePath (defaultWeightsDir()) grammarName
            if not (File.Exists path) then
                Ok []
            else
                let json = File.ReadAllText path
                let file = JsonConvert.DeserializeObject<GrammarWeightFile>(json)
                let rules = file.Rules |> Array.toList |> List.map dtoToRule
                Ok rules
        with ex ->
            Error ex.Message

    /// List all persisted grammar names (file stems under the weights directory).
    let listGrammars () : string list =
        try
            let dir = defaultWeightsDir()
            if not (Directory.Exists dir) then []
            else
                Directory.GetFiles(dir, "*.weights.json")
                |> Array.toList
                |> List.map (fun f -> Path.GetFileNameWithoutExtension(f).Replace(".weights", ""))
        with _ -> []

    /// Delete persisted weights for a grammar (returns Ok () if file didn't exist).
    let delete (grammarName: string) : Result<unit, string> =
        try
            let path = weightFilePath (defaultWeightsDir()) grammarName
            if File.Exists path then File.Delete path
            Ok ()
        with ex ->
            Error ex.Message

    /// Load existing weights or build fresh defaults from seed rules.
    /// Useful for initialisation: call once at startup and cache the result.
    let loadOrInit (grammarName: string) (defaults: WeightedMusicRule list) : WeightedMusicRule list =
        match load grammarName with
        | Ok [] | Error _ -> defaults
        | Ok rules        -> rules
