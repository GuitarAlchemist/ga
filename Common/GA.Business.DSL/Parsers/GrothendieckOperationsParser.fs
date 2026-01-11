namespace GA.MusicTheory.DSL.Parsers

open System
open FParsec
open GA.MusicTheory.DSL.Types.GrammarTypes

/// <summary>
/// Parser for Grothendieck Operations DSL using FParsec
/// Implements the GrothendieckOperations.ebnf grammar
/// </summary>
module GrothendieckOperationsParser =

    // ============================================================================
    // BASIC PARSERS
    // ============================================================================

    let ws = spaces
    let ws1 = spaces1
    let str s = pstring s .>> ws
    let ch c = pchar c .>> ws
    let identifier : Parser<string, unit> =
        let isIdentifierFirstChar c = isLetter c
        let isIdentifierChar c = isLetter c || isDigit c || c = '_'
        many1Satisfy2L isIdentifierFirstChar isIdentifierChar "identifier" .>> ws

    // ============================================================================
    // MUSICAL OBJECT PARSERS
    // ============================================================================

    /// Parse note letter (A-G)
    let noteLetter : Parser<char, unit> =
        anyOf ['A'; 'B'; 'C'; 'D'; 'E'; 'F'; 'G'] .>> ws

    /// Parse accidental
    let accidental : Parser<Accidental, unit> =
        choice [
            str "##" >>% DoubleSharp
            str "bb" >>% DoubleFlat
            str "x" >>% DoubleSharp
            ch '♯' >>% Sharp
            ch '♭' >>% Flat
            ch '#' >>% Sharp
            ch 'b' >>% Flat
        ]

    /// Parse note
    let note : Parser<Note, unit> =
        pipe2 noteLetter (opt accidental) (fun letter acc ->
            { Letter = letter; Accidental = acc; Octave = None })

    /// Parse chord quality
    let chordQuality : Parser<ChordQuality, unit> =
        choice [
            attempt (str "maj9") >>% Major9
            attempt (str "min9") >>% Minor9
            attempt (str "dom9") >>% Dominant9
            attempt (str "maj7") >>% Major7
            attempt (str "min7") >>% Minor7
            attempt (str "dom7") >>% Dominant7
            attempt (str "dim7") >>% Diminished7
            attempt (str "aug7") >>% Augmented7
            attempt (str "add9") >>% Add9
            attempt (str "6/9") >>% ChordQuality.Custom "6/9"
            attempt (str "maj") >>% Major
            attempt (str "min") >>% Minor
            attempt (str "dim") >>% Diminished
            attempt (str "aug") >>% Augmented
            attempt (str "sus2") >>% Sus2
            attempt (str "sus4") >>% Sus4
        ]

    /// Parse chord object
    let chordObject : Parser<MusicalObject, unit> =
        pipe2 note (opt chordQuality) (fun root qualityOpt ->
            let quality = match qualityOpt with Some q -> q | None -> Major
            ChordObject { Root = root; Quality = quality; Extensions = []; Duration = None })

    /// Parse scale name and intervals
    let scaleNameAndIntervals : Parser<string * int list, unit> =
        choice [
            str "harmonic minor" >>% ("harmonic minor", [0; 2; 3; 5; 7; 8; 11])
            str "melodic minor" >>% ("melodic minor", [0; 2; 3; 5; 7; 9; 11])
            str "whole tone" >>% ("whole tone", [0; 2; 4; 6; 8; 10])
            str "major" >>% ("major", [0; 2; 4; 5; 7; 9; 11])
            str "minor" >>% ("minor", [0; 2; 3; 5; 7; 8; 10])
            str "dorian" >>% ("dorian", [0; 2; 3; 5; 7; 9; 10])
            str "phrygian" >>% ("phrygian", [0; 1; 3; 5; 7; 8; 10])
            str "lydian" >>% ("lydian", [0; 2; 4; 6; 7; 9; 11])
            str "mixolydian" >>% ("mixolydian", [0; 2; 4; 5; 7; 9; 10])
            str "aeolian" >>% ("aeolian", [0; 2; 3; 5; 7; 8; 10])
            str "locrian" >>% ("locrian", [0; 1; 3; 5; 6; 8; 10])
            str "pentatonic" >>% ("pentatonic", [0; 2; 4; 7; 9])
            str "blues" >>% ("blues", [0; 3; 5; 6; 7; 10])
            str "diminished" >>% ("diminished", [0; 2; 3; 5; 6; 8; 9; 11])
        ]

    /// Parse scale object
    let scaleObject : Parser<MusicalObject, unit> =
        pipe2 note scaleNameAndIntervals (fun root (name, intervals) ->
            ScaleObject { Root = root; Intervals = intervals; Name = Some name })

    /// Parse roman numeral
    let romanNumeral : Parser<string, unit> =
        let accidentalPrefix = opt (pchar 'b' <|> pchar '#')
        let numeral = choice [
            str "VII"; str "VI"; str "V"; str "IV"; str "III"; str "II"; str "I"
            str "vii"; str "vi"; str "v"; str "iv"; str "iii"; str "ii"; str "i"
        ]
        let suffix = opt (choice [str "maj7"; str "min7"; pstring "°"; pstring "ø"; pstring "+"; pstring "7"])
        pipe3 accidentalPrefix numeral suffix (fun acc num suf ->
            let accStr = match acc with Some c -> string c | None -> ""
            let sufStr = match suf with Some s -> s | None -> ""
            accStr + num + sufStr)

    /// Forward declaration for recursive parsers
    let musicalObject, musicalObjectRef = createParserForwardedToRef<MusicalObject, unit>()

    /// Parse chord list
    let chordList : Parser<MusicalObject list, unit> =
        sepBy1 chordObject (ch ',')

    /// Parse roman numeral list
    let romanNumeralList : Parser<string list, unit> =
        sepBy1 romanNumeral (ch '-')

    /// Parse progression object
    let progressionObject : Parser<MusicalObject, unit> =
        choice [
            between (ch '{') (ch '}') chordList |>> (fun chords ->
                let progChords = chords |> List.choose (function ChordObject c -> Some (AbsoluteChord c) | _ -> None)
                ProgressionObject { Chords = progChords; Key = None; TimeSignature = None; Tempo = None; Metadata = Map.empty })
            romanNumeralList |>> (fun numerals ->
                ProgressionObject { Chords = []; Key = None; TimeSignature = None; Tempo = None; Metadata = Map.empty }) // TODO: Convert roman numerals to chords
        ]

    /// Parse fret number
    let fretNumber : Parser<int, unit> = pint32 .>> ws

    /// Parse string number
    let stringNumber : Parser<int, unit> = pint32 .>> ws

    /// Parse position (string:fret)
    let position : Parser<int * int, unit> =
        pipe3 stringNumber (ch ':') fretNumber (fun str _ fret -> (str, fret))

    /// Parse position list
    let positionList : Parser<(int * int) list, unit> =
        sepBy1 position (ch ',')

    /// Parse voicing object
    let voicingObject : Parser<MusicalObject, unit> =
        between (ch '{') (ch '}') positionList |>> (fun positions ->
            VoicingObject (positions |> List.map (fun (str, fret) ->
                { String = str; Fret = fret; Finger = None })))

    /// Parse pitch class (0-11)
    let pitchClass : Parser<int, unit> =
        pint32 >>= (fun n ->
            if n >= 0 && n <= 11 then preturn n
            else fail "Pitch class must be between 0 and 11")

    /// Parse pitch class list
    let pitchClassList : Parser<int list, unit> =
        sepBy1 pitchClass (ch ',')

    /// Parse Forte number (e.g., "3-11")
    let forteNumber : Parser<string, unit> =
        pipe3 pint32 (ch '-') pint32 (fun n1 _ n2 -> $"%d{n1}-%d{n2}")

    /// Parse set class object
    let setClassObject : Parser<MusicalObject, unit> =
        choice [
            str "PC" >>. between (ch '{') (ch '}') pitchClassList |>> SetClassObject
            str "Forte" >>. forteNumber |>> (fun _ -> SetClassObject []) // TODO: Convert Forte to pitch classes
        ]

    /// Complete musical object parser
    do musicalObjectRef :=
        choice [
            attempt setClassObject
            attempt voicingObject
            attempt progressionObject
            attempt scaleObject
            attempt chordObject
            note |>> NoteObject
        ]

    // ============================================================================
    // MORPHISM AND EXPRESSION PARSERS
    // ============================================================================

    /// Parse morphism expression
    let morphismExpression : Parser<MorphismExpression, unit> =
        choice [
            str "transpose" >>. ws1 >>. pint32 |>> TransposeExpr
            str "invert" >>% InvertExpr
            str "rotate" >>. ws1 >>. pint32 |>> RotateExpr
            str "reflect" >>% ReflectExpr
            identifier |>> CustomExpr
        ]

    /// Parse morphism (legacy support)
    let morphism : Parser<Morphism, unit> =
        choice [
            str "transpose" >>. ws1 >>. pint32 |>> TransposeMorphism
            str "invert" >>% InvertMorphism
            str "rotate" >>. ws1 >>. pint32 |>> RotateMorphism
            str "reflect" >>% ReflectMorphism None
            identifier |>> CustomMorphism
        ]

    // ============================================================================
    // CATEGORY AND FUNCTOR PARSERS
    // ============================================================================

    /// Parse category name
    let categoryName : Parser<CategoryName, unit> =
        choice [
            str "Notes"
            str "Chords"
            str "Scales"
            str "Progressions"
            str "Voicings"
            str "PitchClasses"
            str "SetClasses"
            str "Intervals"
            identifier
        ]

    /// Parse morphism mapping rule
    let morphismRule : Parser<string * MorphismExpression, unit> =
        pipe3 identifier (str "->") morphismExpression (fun name _ expr -> (name, expr))

    /// Parse morphism mapping
    let morphismMapping : Parser<(string * MorphismExpression) list, unit> =
        sepBy1 morphismRule (ch ';')

    /// Parse functor definition
    let functorDefinition : Parser<GrothendieckOperation, unit> =
        choice [
            // functor name: Category -> Category
            pipe5
                (str "functor")
                identifier
                (ch ':')
                (pipe3 categoryName (str "->") categoryName (fun src _ tgt -> (src, tgt)))
                (preturn None)
                (fun _ name _ (src, tgt) mappings ->
                    DefineFunctor { Name = name; SourceCategory = src; TargetCategory = tgt; MorphismMappings = mappings })
            // define functor name { mappings }
            pipe4
                (str "define" >>. ws1 >>. str "functor")
                identifier
                (between (ch '{') (ch '}') morphismMapping)
                (preturn ("", ""))
                (fun _ name mappings (src, tgt) ->
                    DefineFunctor { Name = name; SourceCategory = src; TargetCategory = tgt; MorphismMappings = Some mappings })
        ]

    /// Parse functor application
    let functorApplication : Parser<GrothendieckOperation, unit> =
        choice [
            // functor(object)
            pipe3 identifier (ch '(') (musicalObject .>> ch ')') (fun name _ obj -> ApplyFunctor (name, obj))
            // apply functor to object
            pipe4 (str "apply") identifier (str "to") musicalObject (fun _ name _ obj -> ApplyFunctor (name, obj))
        ]

    /// Parse functor list
    let functorList : Parser<FunctorName list, unit> =
        sepBy1 identifier (ch ',')

    /// Parse functor composition
    let functorComposition : Parser<GrothendieckOperation, unit> =
        choice [
            // functor1 ∘ functor2
            pipe3 identifier (ch '∘') identifier (fun f1 _ f2 -> ComposeFunctors [f1; f2])
            // functor1 compose functor2
            pipe3 identifier (str "compose") identifier (fun f1 _ f2 -> ComposeFunctors [f1; f2])
            // compose(functor1, functor2, ...)
            str "compose" >>. between (ch '(') (ch ')') functorList |>> ComposeFunctors
        ]

    /// Parse functor operation
    let functorOperation : Parser<GrothendieckOperation, unit> =
        choice [
            attempt functorDefinition
            attempt functorComposition
            attempt functorApplication
        ]

    // ============================================================================
    // NATURAL TRANSFORMATION PARSERS
    // ============================================================================

    /// Parse natural transformation component
    let natTransComponent : Parser<NatTransComponent, unit> =
        pipe3 musicalObject (str "->") morphismExpression (fun obj _ morph ->
            { Object = obj; Morphism = morph })

    /// Parse component mapping
    let componentMapping : Parser<NatTransComponent list, unit> =
        sepBy1 natTransComponent (ch ';')

    /// Parse natural transformation
    let naturalTransformation : Parser<GrothendieckOperation, unit> =
        choice [
            // natural transformation name: functor1 => functor2
            pipe5
                (str "natural" >>. ws1 >>. str "transformation")
                identifier
                (ch ':')
                (pipe3 identifier (str "=>") identifier (fun f1 _ f2 -> (f1, f2)))
                (preturn None)
                (fun _ name _ (f1, f2) comps ->
                    DefineNatTrans { Name = name; SourceFunctor = f1; TargetFunctor = f2; Components = comps })
            // define nat_trans name { components }
            pipe3
                (str "define" >>. ws1 >>. str "nat_trans")
                identifier
                (between (ch '{') (ch '}') componentMapping)
                (fun _ name comps ->
                    DefineNatTrans { Name = name; SourceFunctor = ""; TargetFunctor = ""; Components = Some comps })
            // name(object)
            pipe3 identifier (ch '(') (musicalObject .>> ch ')') (fun name _ obj -> ApplyNatTrans (name, obj))
        ]

    // ============================================================================
    // LIMIT AND COLIMIT PARSERS
    // ============================================================================

    /// Parse object list
    let objectList : Parser<MusicalObject list, unit> =
        sepBy1 musicalObject (ch ',')

    /// Parse diagram specification
    let diagramSpec : Parser<DiagramSpec, unit> =
        choice [
            between (ch '{') (ch '}') objectList |>> ObjectList
            identifier |>> NamedDiagram
        ]

    /// Parse limit operation
    let limitOperation : Parser<GrothendieckOperation, unit> =
        choice [
            // limit of diagram
            str "limit" >>. ws1 >>. str "of" >>. ws1 >>. diagramSpec |>> Limit
            // lim diagram
            str "lim" >>. ws1 >>. diagramSpec |>> Limit
            // pullback(obj1, morphism, obj2)
            pipe5
                (str "pullback" >>. ch '(')
                musicalObject
                (ch ',' >>. morphismExpression)
                (ch ',' >>. musicalObject)
                (ch ')')
                (fun _ obj1 morph obj2 _ -> Pullback (obj1, morph, obj2))
            // obj pullback morphism
            pipe3 musicalObject (str "pullback") morphismExpression (fun obj _ morph ->
                Pullback (obj, morph, obj))
            // equalizer(morph1, morph2)
            pipe5
                (str "equalizer" >>. pstring "(" <|> str "eq" >>. pstring "(")
                morphismExpression
                (pstring ",")
                morphismExpression
                (pstring ")")
                (fun _ m1 _ m2 _ -> Equalizer (m1, m2))
        ]

    /// Parse colimit operation
    let colimitOperation : Parser<GrothendieckOperation, unit> =
        choice [
            // colimit of diagram
            str "colimit" >>. ws1 >>. str "of" >>. ws1 >>. diagramSpec |>> Colimit
            // colim diagram
            str "colim" >>. ws1 >>. diagramSpec |>> Colimit
            // pushout(obj1, morphism, obj2)
            pipe5
                (str "pushout" >>. ch '(')
                musicalObject
                (ch ',' >>. morphismExpression)
                (ch ',' >>. musicalObject)
                (ch ')')
                (fun _ obj1 morph obj2 _ -> Pushout (obj1, morph, obj2))
            // obj pushout morphism
            pipe3 musicalObject (str "pushout") morphismExpression (fun obj _ morph ->
                Pushout (obj, morph, obj))
            // coequalizer(morph1, morph2)
            pipe5
                (str "coequalizer" >>. pstring "(" <|> str "coeq" >>. pstring "(")
                morphismExpression
                (pstring ",")
                morphismExpression
                (pstring ")")
                (fun _ m1 _ m2 _ -> Coequalizer (m1, m2))
        ]

    // ============================================================================
    // TOPOS OPERATION PARSERS
    // ============================================================================

    /// Parse topos operation
    let toposOperation : Parser<GrothendieckOperation, unit> =
        choice [
            // Ω or Ω(object)
            ch 'Ω' >>. opt (between (ch '(') (ch ')') musicalObject) |>> SubobjectClassifier
            // truth_value of object
            str "truth_value" >>. ws1 >>. str "of" >>. ws1 >>. musicalObject |>> (fun obj -> SubobjectClassifier (Some obj))
            // P(object) or power(object)
            (str "P" <|> str "power") >>. between (ch '(') (ch ')') musicalObject |>> PowerObject
            // subobjects of object
            str "subobjects" >>. ws1 >>. str "of" >>. ws1 >>. musicalObject |>> PowerObject
            // Hom(obj1, obj2)
            pipe5
                (str "Hom" >>. ch '(')
                musicalObject
                (ch ',')
                musicalObject
                (ch ')')
                (fun _ obj1 _ obj2 _ -> InternalHom (obj1, obj2))
            // obj1 => obj2
            pipe3 musicalObject (str "=>") musicalObject (fun obj1 _ obj2 -> InternalHom (obj1, obj2))
        ]

    // ============================================================================
    // SHEAF OPERATION PARSERS
    // ============================================================================

    /// Parse space specification
    let spaceSpec : Parser<SpaceSpec, unit> =
        choice [
            str "fretboard" >>% Fretboard
            str "circle_of_fifths" >>% CircleOfFifths
            str "tonnetz" >>% Tonnetz
            str "pitch_class_space" >>% PitchClassSpace
            identifier |>> CustomSpace
        ]

    /// Parse sheaf section
    let sheafSection : Parser<SheafSection, unit> =
        pipe3 identifier (str "->") musicalObject (fun openSet _ obj ->
            { OpenSet = openSet; Value = obj })

    /// Parse section mapping
    let sectionMapping : Parser<SheafSection list, unit> =
        sepBy1 sheafSection (ch ';')

    /// Parse gluing rule
    let gluingRule : Parser<GluingRule, unit> =
        pipe5
            identifier
            (pstring "∩")
            identifier
            (str "->")
            morphismExpression
            (fun set1 _ set2 _ morph ->
                { OpenSet1 = set1; OpenSet2 = set2; Morphism = morph })

    /// Parse gluing data
    let gluingData : Parser<GluingRule list, unit> =
        between (ch '{') (ch '}') (sepBy1 gluingRule (ch ';'))

    /// Parse section list
    let sectionList : Parser<MusicalObject list, unit> =
        sepBy1 musicalObject (ch ',')

    /// Parse sheaf operation
    let sheafOperation : Parser<GrothendieckOperation, unit> =
        choice [
            // sheaf name on space
            pipe4
                (str "sheaf")
                identifier
                (str "on")
                spaceSpec
                (fun _ name _ space ->
                    DefineSheaf { Name = name; Space = space; Sections = None })
            // define sheaf name { sections }
            pipe3
                (str "define" >>. ws1 >>. str "sheaf")
                identifier
                (between (ch '{') (ch '}') sectionMapping)
                (fun _ name sections ->
                    DefineSheaf { Name = name; Space = CustomSpace ""; Sections = Some sections })
            // sheaf | openset or restrict sheaf to openset
            pipe3 identifier (pstring "|" <|> (str "restrict" >>. ws1 >>. str "to" >>. ws1 >>% "|")) identifier (fun sheaf _ openSet ->
                SheafRestriction (sheaf, openSet))
            // glue { sections } or glue sections along gluing_data
            choice [
                str "glue" >>. ws1 >>. between (ch '{') (ch '}') sectionList |>> (fun sections ->
                    SheafGluing (sections, None))
                pipe4
                    (str "glue")
                    sectionList
                    (str "along")
                    gluingData
                    (fun _ sections _ rules -> SheafGluing (sections, Some rules))
            ]
        ]

    // ============================================================================
    // CATEGORY OPERATION PARSERS
    // ============================================================================

    /// Parse tensor product
    let tensorProduct : Parser<GrothendieckOperation, unit> =
        choice [
            // obj1 ⊗ obj2
            pipe3 musicalObject (ch '⊗') musicalObject (fun obj1 _ obj2 -> TensorProduct (obj1, obj2))
            // obj1 tensor obj2
            pipe3 musicalObject (str "tensor") musicalObject (fun obj1 _ obj2 -> TensorProduct (obj1, obj2))
            // tensor(obj1, obj2)
            pipe5
                (str "tensor" >>. ch '(')
                musicalObject
                (ch ',')
                musicalObject
                (ch ')')
                (fun _ obj1 _ obj2 _ -> TensorProduct (obj1, obj2))
        ]

    /// Parse direct sum
    let directSum : Parser<GrothendieckOperation, unit> =
        choice [
            // obj1 ⊕ obj2
            pipe3 musicalObject (ch '⊕') musicalObject (fun obj1 _ obj2 -> DirectSum (obj1, obj2))
            // obj1 direct_sum obj2
            pipe3 musicalObject (str "direct_sum") musicalObject (fun obj1 _ obj2 -> DirectSum (obj1, obj2))
            // direct_sum(obj1, obj2)
            pipe5
                (str "direct_sum" >>. ch '(')
                musicalObject
                (ch ',')
                musicalObject
                (ch ')')
                (fun _ obj1 _ obj2 _ -> DirectSum (obj1, obj2))
        ]

    /// Parse product
    let productOp : Parser<GrothendieckOperation, unit> =
        choice [
            // obj1 × obj2
            pipe3 musicalObject (ch '×') musicalObject (fun obj1 _ obj2 -> Product [obj1; obj2])
            // obj1 product obj2
            pipe3 musicalObject (str "product") musicalObject (fun obj1 _ obj2 -> Product [obj1; obj2])
            // product(obj1, obj2, ...)
            str "product" >>. between (ch '(') (ch ')') objectList |>> Product
        ]

    /// Parse coproduct
    let coproductOp : Parser<GrothendieckOperation, unit> =
        choice [
            // obj1 + obj2
            pipe3 musicalObject (ch '+') musicalObject (fun obj1 _ obj2 -> Coproduct [obj1; obj2])
            // obj1 coproduct obj2
            pipe3 musicalObject (str "coproduct") musicalObject (fun obj1 _ obj2 -> Coproduct [obj1; obj2])
            // coproduct(obj1, obj2, ...)
            str "coproduct" >>. between (ch '(') (ch ')') objectList |>> Coproduct
        ]

    /// Parse exponential
    let exponentialOp : Parser<GrothendieckOperation, unit> =
        choice [
            // obj1 ^ obj2
            pipe3 musicalObject (ch '^') musicalObject (fun obj1 _ obj2 -> Exponential (obj1, obj2))
            // exp(obj1, obj2)
            pipe5
                (str "exp" >>. ch '(')
                musicalObject
                (ch ',')
                musicalObject
                (ch ')')
                (fun _ obj1 _ obj2 _ -> Exponential (obj1, obj2))
        ]

    /// Parse category operation
    let categoryOperation : Parser<GrothendieckOperation, unit> =
        choice [
            attempt tensorProduct
            attempt directSum
            attempt productOp
            attempt coproductOp
            attempt exponentialOp
        ]

    // ============================================================================
    // MAIN GROTHENDIECK OPERATION PARSER
    // ============================================================================

    /// Parse a complete Grothendieck operation
    let grothendieckOperation : Parser<GrothendieckOperation, unit> =
        choice [
            attempt categoryOperation
            attempt functorOperation
            attempt naturalTransformation
            attempt limitOperation
            attempt colimitOperation
            attempt toposOperation
            attempt sheafOperation
        ]

    // ============================================================================
    // PUBLIC API
    // ============================================================================

    /// Parse a Grothendieck operation string
    let parse input =
        match run (ws >>. grothendieckOperation .>> eof) input with
        | Success (result, _, _) -> Result.Ok result
        | Failure (errorMsg, _, _) -> Result.Error errorMsg

    /// Parse a Grothendieck operation string and return a DslCommand
    let parseCommand input =
        parse input
        |> Result.map GrothendieckCommand

    /// Try to parse a Grothendieck operation string
    let tryParse input =
        match parse input with
        | Result.Ok result -> Some result
        | Result.Error _ -> None

