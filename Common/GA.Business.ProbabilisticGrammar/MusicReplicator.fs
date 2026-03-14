namespace GA.Business.ProbabilisticGrammar

/// A grammar rule species competing in the evolutionary ecosystem.
/// Replicator dynamics drives proportions toward rules with above-average fitness.
type MusicSpecies =
    { RuleId: string
      Proportion: float    // fraction of the population this species represents (sums to 1)
      Fitness: float       // musical quality metric for this rule
      Genre: string option }

/// Result of a replicator simulation run
type SimResult =
    { FinalSpecies: MusicSpecies list
      StableIdioms: MusicSpecies list     // species above the ESS threshold
      Generations: int
      ConvergenceAchieved: bool }

module MusicReplicator =

    let private meanFitness (species: MusicSpecies list) : float =
        species |> List.sumBy (fun s -> s.Proportion * s.Fitness)

    /// One step of continuous-time replicator dynamics.
    /// dp_i/dt = p_i * (f_i - <f>)
    /// Normalized after each step to keep proportions summing to 1.
    let step (species: MusicSpecies list) (dt: float) : MusicSpecies list =
        let mean = meanFitness species
        if mean < 1e-10 then species
        else
            let updated =
                species
                |> List.map (fun s ->
                    let newProportion = s.Proportion + dt * s.Proportion * (s.Fitness - mean)
                    { s with Proportion = max 0.0 newProportion })
            let total = updated |> List.sumBy (fun s -> s.Proportion)
            if total < 1e-10 then updated
            else updated |> List.map (fun s -> { s with Proportion = s.Proportion / total })

    /// Detect evolutionarily stable strategies: species with proportion >= threshold.
    /// These represent "established musical idioms" that dominate the rule ecosystem.
    let detectStableIdioms (species: MusicSpecies list) (threshold: float) : MusicSpecies list =
        species
        |> List.filter (fun s -> s.Proportion >= threshold)
        |> List.sortByDescending (fun s -> s.Proportion)

    /// Convert weighted rules to initial species (uniform proportions, fitness = rule weight)
    let private rulesToSpecies (rules: WeightedMusicRule list) : MusicSpecies list =
        let n = rules.Length
        if n = 0 then []
        else
            let uniformProportion = 1.0 / float n
            rules
            |> List.map (fun r ->
                { RuleId = r.RuleId
                  Proportion = uniformProportion
                  Fitness = r.Weight
                  Genre = r.MusicalContext })

    /// Update species fitness from rule Bayesian weights after applying outcomes
    let private applyOutcomes (rules: WeightedMusicRule list) (outcomes: (string * bool) list) : WeightedMusicRule list =
        outcomes
        |> List.fold
            (fun rs (ruleId, success) ->
                rs
                |> List.map (fun r ->
                    if r.RuleId = ruleId then WeightedMusicRule.bayesianUpdate r success
                    else r))
            rules

    /// Run replicator dynamics until convergence or maxGenerations reached.
    let private runUntilConvergence
            (species: MusicSpecies list)
            (maxGenerations: int)
            (dt: float)
            : MusicSpecies list * int * bool =
        let tol = 1e-6
        let mutable current = species
        let mutable gen = 0
        let mutable converged = false
        while gen < maxGenerations && not converged do
            let next = step current dt
            let maxDelta =
                List.zip current next
                |> List.map (fun (a, b) -> abs (b.Proportion - a.Proportion))
                |> List.max
            converged <- maxDelta < tol
            current <- next
            gen <- gen + 1
        current, gen, converged

    /// Full evolution pipeline: apply Bayesian updates from outcomes, then run replicator dynamics.
    /// Returns simulation result including stable idioms (ESS candidates).
    let evolveFromPreferences
            (rules: WeightedMusicRule list)
            (outcomes: (string * bool) list)
            : SimResult =
        let updatedRules = applyOutcomes rules outcomes
        let species = rulesToSpecies updatedRules
        let finalSpecies, gens, converged = runUntilConvergence species 1000 0.01
        { FinalSpecies = finalSpecies
          StableIdioms = detectStableIdioms finalSpecies 0.1
          Generations = gens
          ConvergenceAchieved = converged }

    /// Merge species from two different genres by averaging proportions (for cross-genre learning)
    let mergeGenres (speciesA: MusicSpecies list) (speciesB: MusicSpecies list) : MusicSpecies list =
        let mapB = speciesB |> List.map (fun s -> s.RuleId, s) |> Map.ofList
        speciesA
        |> List.map (fun a ->
            match Map.tryFind a.RuleId mapB with
            | Some b ->
                { a with
                    Proportion = (a.Proportion + b.Proportion) / 2.0
                    Fitness = (a.Fitness + b.Fitness) / 2.0 }
            | None -> a)
