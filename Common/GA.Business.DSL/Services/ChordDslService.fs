namespace GA.Business.DSL.Services

open GA.Business.DSL.Parsers
open GA.Business.DSL.Generators
open GA.Business.DSL.Types

type ChordDslService() =
    /// <summary>
    /// Parses any real-world chord string into a structured AST.
    /// </summary>
    member _.Parse(chordStr: string) =
        ChordParser.parse chordStr

    /// <summary>
    /// Renders an AST back into a canonical Guitar Alchemist chord string.
    /// </summary>
    member _.Render(ast: ChordAst) =
        ChordRenderer.render ast

    /// <summary>
    /// Normalizes a messy human chord string into a canonical one.
    /// </summary>
    member this.Normalize(chordStr: string) =
        match this.Parse chordStr with
        | Ok ast -> Ok (this.Render ast)
        | Error err -> Error err
