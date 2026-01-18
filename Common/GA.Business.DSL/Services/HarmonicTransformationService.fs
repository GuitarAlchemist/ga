namespace GA.Business.DSL.Services

open GA.Business.DSL.Types.MusicalSetTypes

/// <summary>
/// Implements algebraic musical transformations using functional patterns.
/// </summary>
type HarmonicTransformationService() =
    
    let normalize pc = ((pc % 12) + 12) % 12

    /// <summary>
    /// Transposes a set of pitch classes.
    /// </summary>
    member _.Transpose (interval: Interval) (pcs: PitchClassSet) : PitchClassSet =
        pcs |> Set.map (fun pc -> normalize (pc + interval))

    /// <summary>
    /// Inverts a set of pitch classes around an axis.
    /// Formula: I(x) = (2*axis - x) mod 12
    /// </summary>
    member _.Invert (axis: PitchClass) (pcs: PitchClassSet) : PitchClassSet =
        pcs |> Set.map (fun pc -> normalize (2 * axis - pc))

    /// <summary>
    /// Calculates the "Negative Harmony" equivalent of a chord.
    /// Usually mirrored across the C-G axis (axis = 3.5 semitones?)
    /// For integer math, we use axis = 7 (G) and map PC -> (7 - PC + 0) ?
    /// Common axis is Eb/G (3 and 7). Sum = 10.
    /// Formula: Neg(x) = (SumAxis - x) mod 12
    /// </summary>
    member _.ApplyNegativeHarmony (sumAxis: int) (pcs: PitchClassSet) : PitchClassSet =
        pcs |> Set.map (fun pc -> normalize (sumAxis - pc))

    /// <summary>
    /// Calculates the Forte Prime Form of a pitch class set.
    /// (Simplified version: Normal order transposed to zero)
    /// </summary>
    member this.GetNormalForm (pcs: PitchClassSet) : int list =
        if Set.isEmpty pcs then []
        else
            let sorted = pcs |> Set.toList |> List.sort
            // Find rotation with smallest span
            let n = List.length sorted
            let rotations = [0 .. n-1] |> List.map (fun i ->
                let rot = List.append (List.skip i sorted) (List.take i sorted |> List.map (fun x -> x + 12))
                let span = List.last rot - List.head rot
                (span, rot))
            
            let (_, bestRot) = rotations |> List.minBy fst
            let baseVal = List.head bestRot
            bestRot |> List.map (fun x -> x - baseVal)
