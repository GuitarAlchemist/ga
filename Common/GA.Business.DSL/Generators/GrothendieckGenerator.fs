namespace GA.MusicTheory.DSL.Generators

open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Generator for Grothendieck Operations DSL from AST types
/// Converts our Grothendieck operation AST back to text format
/// </summary>
module GrothendieckGenerator =

    // ============================================================================
    // PRIMITIVE FORMATTERS
    // ============================================================================

    /// Format a note
    let formatNote (note: Note) =
        let letter = string note.Letter
        let accidental =
            match note.Accidental with
            | Some Sharp -> "#"
            | Some Flat -> "b"
            | Some DoubleSharp -> "##"
            | Some DoubleFlat -> "bb"
            | Some Natural -> ""
            | None -> ""

        $"%s{letter}%s{accidental}"

    /// Format a chord quality
    let formatChordQuality (quality: ChordQuality) =
        match quality with
        | Major -> "maj"
        | Minor -> "min"
        | Diminished -> "dim"
        | Augmented -> "aug"
        | Dominant7 -> "dom7"
        | Major7 -> "maj7"
        | Minor7 -> "min7"
        | Diminished7 -> "dim7"
        | HalfDiminished7 -> "ø7"
        | Augmented7 -> "aug7"
        | Sus2 -> "sus2"
        | Sus4 -> "sus4"
        | Add9 -> "add9"
        | Major9 -> "maj9"
        | Minor9 -> "min9"
        | Dominant9 -> "dom9"
        | ChordQuality.Custom s -> s

    /// Format a chord
    let formatChord (chord: Chord) =
        let root = formatNote chord.Root
        let quality = formatChordQuality chord.Quality
        $"%s{root} %s{quality}"

    /// Format a scale
    let formatScale (scale: Scale) =
        let root = formatNote scale.Root
        let name = scale.Name |> Option.defaultValue "major"
        $"%s{root} %s{name}"

    /// Format a fretboard position
    let formatFretboardPosition (pos: FretboardPosition) =
        $"%d{pos.Fret}/%d{pos.String}"

    // ============================================================================
    // MUSICAL OBJECT FORMATTERS
    // ============================================================================

    /// Format a musical object
    let rec formatMusicalObject (obj: MusicalObject) =
        match obj with
        | NoteObject note -> formatNote note
        | ChordObject chord -> formatChord chord
        | ScaleObject scale -> formatScale scale
        | ProgressionObject prog ->
            let chords =
                prog.Chords
                |> List.map (function
                    | AbsoluteChord c -> formatChord c
                    | RomanNumeralChord rn -> $"%d{rn.Degree}") // Simplified - just show degree
                |> String.concat ", "

            $"{{ %s{chords} }}"
        | VoicingObject positions ->
            let posStrs = positions |> List.map formatFretboardPosition |> String.concat ", "
            $"{{ %s{posStrs} }}"
        | SetClassObject pitchClasses ->
            let pcs = pitchClasses |> List.map string |> String.concat ", "
            $"{{ %s{pcs} }}"

    // ============================================================================
    // MORPHISM FORMATTERS
    // ============================================================================

    /// Format a morphism expression
    let formatMorphismExpression (expr: MorphismExpression) =
        match expr with
        | TransposeExpr semitones -> $"transpose(%d{semitones})"
        | InvertExpr -> "invert"
        | RotateExpr steps -> $"rotate(%d{steps})"
        | ReflectExpr -> "reflect"
        | CustomExpr name -> name

    // ============================================================================
    // CATEGORY OPERATION FORMATTERS
    // ============================================================================

    /// Format a category operation
    let formatCategoryOperation (op: GrothendieckOperation) =
        match op with
        | TensorProduct (obj1, obj2) ->
            $"%s{formatMusicalObject obj1} ⊗ %s{formatMusicalObject obj2}"
        | DirectSum (obj1, obj2) ->
            $"%s{formatMusicalObject obj1} ⊕ %s{formatMusicalObject obj2}"
        | Product objs ->
            let objStrs = objs |> List.map formatMusicalObject |> String.concat " × "
            $"product(%s{objStrs})"
        | Coproduct objs ->
            let objStrs = objs |> List.map formatMusicalObject |> String.concat ", "
            $"coproduct(%s{objStrs})"
        | Exponential (obj1, obj2) ->
            $"%s{formatMusicalObject obj1} ^ %s{formatMusicalObject obj2}"
        | _ -> ""

    // ============================================================================
    // FUNCTOR OPERATION FORMATTERS
    // ============================================================================

    /// Format a functor definition
    let formatFunctorDef (def: FunctorDef) =
        let mappings =
            match def.MorphismMappings with
            | Some maps ->
                let mapStrs =
                    maps
                    |> List.map (fun (name, expr) ->
                        $"%s{name} -> %s{formatMorphismExpression expr}")
                    |> String.concat ", "

                $" {{ %s{mapStrs} }}"
            | None -> ""

        $"functor %s{def.Name}: %s{def.SourceCategory} -> %s{def.TargetCategory}%s{mappings}"

    /// Format a functor operation
    let formatFunctorOperation (op: GrothendieckOperation) =
        match op with
        | DefineFunctor def -> formatFunctorDef def
        | ApplyFunctor (name, obj) ->
            $"%s{name}(%s{formatMusicalObject obj})"
        | ComposeFunctors functors ->
            String.concat " ∘ " functors
        | _ -> ""

    // ============================================================================
    // NATURAL TRANSFORMATION FORMATTERS
    // ============================================================================

    /// Format a natural transformation component
    let formatNatTransComponent (comp: NatTransComponent) =
        $"%s{formatMusicalObject comp.Object} -> %s{formatMorphismExpression comp.Morphism}"

    /// Format a natural transformation definition
    let formatNatTransDef (def: NatTransDef) =
        let components =
            match def.Components with
            | Some comps ->
                let compStrs = comps |> List.map formatNatTransComponent |> String.concat ", "
                $" {{ %s{compStrs} }}"
            | None -> ""

        $"nattrans %s{def.Name}: %s{def.SourceFunctor} => %s{def.TargetFunctor}%s{components}"

    /// Format a natural transformation operation
    let formatNatTransOperation (op: GrothendieckOperation) =
        match op with
        | DefineNatTrans def -> formatNatTransDef def
        | ApplyNatTrans (name, obj) ->
            $"%s{name}(%s{formatMusicalObject obj})"
        | _ -> ""

    // ============================================================================
    // LIMIT OPERATION FORMATTERS
    // ============================================================================

    /// Format a diagram specification
    let formatDiagramSpec (spec: DiagramSpec) =
        match spec with
        | ObjectList objs ->
            let objStrs = objs |> List.map formatMusicalObject |> String.concat ", "
            $"{{ %s{objStrs} }}"
        | NamedDiagram name -> name

    /// Format a limit operation
    let formatLimitOperation (op: GrothendieckOperation) =
        match op with
        | Limit spec ->
            $"limit of %s{formatDiagramSpec spec}"
        | Pullback (obj1, morph, obj2) ->
            $"pullback(%s{formatMusicalObject obj1}, %s{formatMorphismExpression morph}, %s{formatMusicalObject obj2})"
        | Equalizer (morph1, morph2) ->
            $"equalizer(%s{formatMorphismExpression morph1}, %s{formatMorphismExpression morph2})"
        | _ -> ""

    // ============================================================================
    // COLIMIT OPERATION FORMATTERS
    // ============================================================================

    /// Format a colimit operation
    let formatColimitOperation (op: GrothendieckOperation) =
        match op with
        | Colimit spec ->
            $"colimit of %s{formatDiagramSpec spec}"
        | Pushout (obj1, morph, obj2) ->
            $"pushout(%s{formatMusicalObject obj1}, %s{formatMorphismExpression morph}, %s{formatMusicalObject obj2})"
        | Coequalizer (morph1, morph2) ->
            $"coequalizer(%s{formatMorphismExpression morph1}, %s{formatMorphismExpression morph2})"
        | _ -> ""

    // ============================================================================
    // TOPOS OPERATION FORMATTERS
    // ============================================================================

    /// Format a topos operation
    let formatToposOperation (op: GrothendieckOperation) =
        match op with
        | SubobjectClassifier objOpt ->
            match objOpt with
            | Some obj -> $"Ω(%s{formatMusicalObject obj})"
            | None -> "Ω"
        | PowerObject obj ->
            $"P(%s{formatMusicalObject obj})"
        | InternalHom (obj1, obj2) ->
            $"Hom(%s{formatMusicalObject obj1}, %s{formatMusicalObject obj2})"
        | _ -> ""

    // ============================================================================
    // SHEAF OPERATION FORMATTERS
    // ============================================================================

    /// Format a space specification
    let formatSpaceSpec (spec: SpaceSpec) =
        match spec with
        | Fretboard -> "fretboard"
        | CircleOfFifths -> "circle-of-fifths"
        | Tonnetz -> "tonnetz"
        | PitchClassSpace -> "pitch-class-space"
        | CustomSpace name -> name

    /// Format a sheaf section
    let formatSheafSection (section: SheafSection) =
        $"%s{section.OpenSet}: %s{formatMusicalObject section.Value}"

    /// Format a gluing rule
    let formatGluingRule (rule: GluingRule) =
        $"%s{rule.OpenSet1} ∩ %s{rule.OpenSet2} -> %s{formatMorphismExpression rule.Morphism}"

    /// Format a sheaf definition
    let formatSheafDef (def: SheafDef) =
        let sections =
            match def.Sections with
            | Some secs ->
                let secStrs = secs |> List.map formatSheafSection |> String.concat ", "
                $" {{ %s{secStrs} }}"
            | None -> ""

        $"sheaf %s{def.Name} on %s{formatSpaceSpec def.Space}%s{sections}"

    /// Format a sheaf operation
    let formatSheafOperation (op: GrothendieckOperation) =
        match op with
        | DefineSheaf def -> formatSheafDef def
        | SheafRestriction (sheaf, openSet) ->
            $"%s{sheaf} | %s{openSet}"
        | SheafGluing (objs, rulesOpt) ->
            let objStrs = objs |> List.map formatMusicalObject |> String.concat ", "
            match rulesOpt with
            | Some rules ->
                let ruleStrs = rules |> List.map formatGluingRule |> String.concat ", "
                $"glue {{ %s{objStrs} }} along {{ %s{ruleStrs} }}"
            | None ->
                $"glue {{ %s{objStrs} }}"
        | _ -> ""

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Generate Grothendieck DSL text from an operation
    let generate (op: GrothendieckOperation) : string =
        match op with
        | TensorProduct _ | DirectSum _ | Product _ | Coproduct _ | Exponential _ ->
            formatCategoryOperation op
        | DefineFunctor _ | ApplyFunctor _ | ComposeFunctors _ ->
            formatFunctorOperation op
        | DefineNatTrans _ | ApplyNatTrans _ ->
            formatNatTransOperation op
        | Limit _ | Pullback _ | Equalizer _ ->
            formatLimitOperation op
        | Colimit _ | Pushout _ | Coequalizer _ ->
            formatColimitOperation op
        | SubobjectClassifier _ | PowerObject _ | InternalHom _ ->
            formatToposOperation op
        | DefineSheaf _ | SheafRestriction _ | SheafGluing _ ->
            formatSheafOperation op

