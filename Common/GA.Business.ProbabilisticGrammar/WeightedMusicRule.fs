namespace GA.Business.ProbabilisticGrammar

open System

/// Identifies which grammar source a rule comes from
type MusicRuleSource =
    | ChordGrammar
    | ScaleGrammar
    | VoicingGrammar
    | HarmonyConstraint

/// A grammar production rule with Beta-Binomial learned weights.
/// Alpha and Beta are the Beta distribution parameters updated via Bayesian learning.
/// Weight = Alpha / (Alpha + Beta) = posterior mean probability of this rule being "good".
type WeightedMusicRule =
    { RuleId: string
      Production: string
      Alpha: float
      Beta: float
      Weight: float
      Source: MusicRuleSource
      MusicalContext: string option }

module WeightedMusicRule =

    /// Create a new rule with uniform Beta(1,1) prior (weight = 0.5)
    let create (ruleId: string) (production: string) (source: MusicRuleSource) (context: string option) : WeightedMusicRule =
        { RuleId = ruleId
          Production = production
          Alpha = 1.0
          Beta = 1.0
          Weight = 0.5
          Source = source
          MusicalContext = context }

    /// Bayesian update: success=true increments alpha (positive evidence), false increments beta.
    /// Weight is updated to the posterior mean Alpha / (Alpha + Beta).
    let bayesianUpdate (rule: WeightedMusicRule) (success: bool) : WeightedMusicRule =
        let alpha', beta' =
            if success then rule.Alpha + 1.0, rule.Beta
            else rule.Alpha, rule.Beta + 1.0
        { rule with Alpha = alpha'; Beta = beta'; Weight = alpha' / (alpha' + beta') }

    /// Compute softmax probabilities over rules, with temperature modulated by musical context.
    /// Lower temperature = sharper distribution (exploit best rules).
    /// Higher temperature = flatter distribution (more exploration).
    let softmaxByContext (rules: WeightedMusicRule list) (context: string) : float list =
        if rules.IsEmpty then []
        else
            let temperature =
                if context.Contains("jazz") then 0.8
                elif context.Contains("classical") then 1.5
                elif context.Contains("blues") then 0.9
                elif context.Contains("explore") then 2.0
                else 1.0
            let logits = rules |> List.map (fun r -> r.Weight / temperature)
            let maxLogit = List.max logits
            let expScores = logits |> List.map (fun x -> Math.Exp(x - maxLogit))
            let sumExp = List.sum expScores
            expScores |> List.map (fun e -> e / sumExp)

    /// Sample one rule from the weighted distribution (returns None for empty list).
    let selectWeighted (rules: WeightedMusicRule list) (rng: Random) : WeightedMusicRule option =
        if rules.IsEmpty then None
        else
            let probs = softmaxByContext rules ""
            let sample = rng.NextDouble()
            let mutable cumulative = 0.0
            let mutable selected = None
            for (rule, prob) in List.zip rules probs do
                cumulative <- cumulative + prob
                if selected.IsNone && sample <= cumulative then
                    selected <- Some rule
            selected |> Option.orElse (Some (List.last rules))

    /// Normalize weights to sum to 1.0 (for display/comparison purposes only; does not affect Alpha/Beta).
    let normalize (rules: WeightedMusicRule list) : WeightedMusicRule list =
        let total = rules |> List.sumBy (fun r -> r.Weight)
        if total < 1e-10 then rules
        else rules |> List.map (fun r -> { r with Weight = r.Weight / total })

    /// Return top-N rules by current weight
    let topN (n: int) (rules: WeightedMusicRule list) : WeightedMusicRule list =
        rules
        |> List.sortByDescending (fun r -> r.Weight)
        |> List.truncate n
