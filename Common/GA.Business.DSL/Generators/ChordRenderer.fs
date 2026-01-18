namespace GA.Business.DSL.Generators

open GA.Business.DSL.Types

module ChordRenderer =
    let renderAccidental = function
        | Natural -> ""
        | Sharp -> "#"
        | Flat -> "b"
        | DoubleSharp -> "##"
        | DoubleFlat -> "bb"

    let renderQuality = function
        | Major -> "maj"
        | Minor -> "m"
        | Diminished -> "dim"
        | Augmented -> "aug"
        | Suspended -> "sus"
        | Dominant -> "" // Implied by 7 extension usually

    let renderComponent = function
        | Extension s -> s
        | Alteration (acc, deg) -> (renderAccidental acc) + deg
        | Omission deg -> "(no " + deg + ")"
        | Alt -> "alt"

    let render (ast: ChordAst) =
        let root = ast.Root + (renderAccidental ast.RootAccidental)
        let qual = ast.Quality |> Option.map renderQuality |> Option.defaultValue ""
        let comps = ast.Components |> List.map renderComponent |> String.concat ""
        let bass = 
            match ast.Bass with
            | Some (n, acc) -> "/" + n.ToUpper() + (renderAccidental acc)
            | None -> ""
        
        root + qual + comps + bass
