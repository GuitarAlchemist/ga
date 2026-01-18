namespace GA.Business.DSL.Parsers

open FParsec
open GA.Business.DSL.Types

module ChordParser =
    let pNote = anyOf "CDEFGABcdefgab" |>> string
    
    let pDoubleSharp = (stringReturn "##" DoubleSharp) <|> (stringReturn "x" DoubleSharp) <|> (stringReturn "𝄪" DoubleSharp)
    let pSharp = (stringReturn "#" Sharp) <|> (stringReturn "♯" Sharp)
    let pDoubleFlat = (stringReturn "bb" DoubleFlat) <|> (stringReturn "𝄫" DoubleFlat)
    let pFlat = (stringReturn "b" Flat) <|> (stringReturn "♭" Flat)
    let pNatural = (stringReturn "♮" Natural)
    
    let pAccidental = choice [
        attempt pDoubleSharp
        attempt pDoubleFlat
        pSharp
        pFlat
        pNatural
    ]

    let pQuality = choice [
        // Major (Check maj first because it contains 'm')
        attempt (choice [ stringReturn "maj" Major; stringReturn "MAJ" Major; stringReturn "Maj" Major; stringReturn "M" Major; stringReturn "Δ" Major; stringReturn "ma" Major ])
        // Minor
        choice [ stringReturn "min" Minor; stringReturn "MIN" Minor; stringReturn "mi" Minor; stringReturn "-" Minor; stringReturn "m" Minor ]
        // Dim
        choice [ stringReturn "dim" Diminished; stringReturn "DIM" Diminished; stringReturn "°" Diminished; stringReturn "o" Diminished ]
        // Aug
        choice [ stringReturn "aug" Augmented; stringReturn "AUG" Augmented; stringReturn "+" Augmented ]
        // Sus
        choice [ stringReturn "sus" Suspended; stringReturn "SUS" Suspended ]
    ]

    let pExtension = choice [
        // Multi-digit numbers
        attempt (stringReturn "13" (Extension "13"))
        attempt (stringReturn "11" (Extension "11"))
        
        // Complex strings
        attempt (stringReturn "maj7" (Extension "maj7"))
        attempt (stringReturn "maj9" (Extension "maj9"))
        attempt (stringReturn "maj11" (Extension "maj11"))
        attempt (stringReturn "maj13" (Extension "maj13"))
        attempt (stringReturn "m7b5" (Extension "m7b5"))
        attempt (stringReturn "-7b5" (Extension "m7b5"))
        attempt (stringReturn "6/9" (Extension "6/9"))
        attempt (stringReturn "add9" (Extension "add9"))
        attempt (stringReturn "add11" (Extension "add11"))
        attempt (stringReturn "add13" (Extension "add13"))
        attempt (stringReturn "add2" (Extension "add2"))
        attempt (stringReturn "add4" (Extension "add4"))
        
        // Simple ones
        anyOf "796245" |>> (fun c -> Extension (string c))
    ]

    let pAlteration = choice [
        stringReturn "alt" Alt
        stringReturn "ALT" Alt
        pipe2 (opt pAccidental) (choice [ attempt (pstring "13"); attempt (pstring "11"); attempt (pstring "9"); attempt (pstring "5") ]) (fun acc deg -> 
            Alteration(defaultArg acc Natural, deg))
    ]

    let pChord = 
        pipe5 
            pNote 
            (opt pAccidental) 
            (opt pQuality) 
            (many (pExtension <|> pAlteration))
            (opt (pstring "/" >>. pipe2 pNote (opt pAccidental) (fun n acc -> (n, defaultArg acc Natural))))
            (fun root acc qual comps bass -> 
                { Root = root.ToUpper(); RootAccidental = defaultArg acc Natural; Quality = qual; Components = comps; Bass = bass })

    let parse chordStr =
        match run pChord chordStr with
        | Success(result, _, _) -> Result.Ok result
        | Failure(errorMsg, _, _) -> Result.Error errorMsg
