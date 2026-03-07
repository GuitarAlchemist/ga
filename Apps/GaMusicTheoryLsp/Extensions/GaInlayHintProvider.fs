module GA.MusicTheory.LSP.Extensions.GaInlayHintProvider

open GA.MusicTheory.LSP.Extensions.GaMarkdownHandler
open GA.Business.DSL.Closures.GaClosureRegistry

// ============================================================================
// Inlay hints for ga{} blocks
// ============================================================================
// Provides type hints and closure descriptions inline within ga scripts
// embedded in markdown documents. Inlay hints appear as virtual text
// annotations in editors that support LSP 3.17 inlayHints.
// ============================================================================

[<Struct>]
type InlayHint =
    { /// 0-based document line
      Line        : int
      /// 0-based character offset (hint appears after this position)
      Character   : int
      /// The hint text to display
      Label       : string
      /// Whether the hint is a type annotation (true) or parameter name (false)
      IsType      : bool }

/// Known closure patterns for automatic type hints.
let private closureCallRe = System.Text.RegularExpressions.Regex(@"GaClosureRegistry\.Global\.Invoke\s*\(\s*""([^""]+)""")
let private letBangRe     = System.Text.RegularExpressions.Regex(@"let!\s+(\w+)\s*=")
let private doubleBangRe  = System.Text.RegularExpressions.Regex(@"do!\s+")

/// Generate inlay hints for a single ga block.
let private hintsForBlock (block: GaBlock) : InlayHint list =
    let lines = block.Content.Split('\n')
    let hints = System.Collections.Generic.List<InlayHint>()

    for innerLine = 0 to lines.Length - 1 do
        let line = lines.[innerLine]
        let docLine = block.ContentStartLine + innerLine

        // Closure name hints: show output type after Invoke("closure.name", ...)
        let invokeMatches = closureCallRe.Matches(line)
        for m in invokeMatches do
            let closureName = m.Groups.[1].Value
            let registry = GaClosureRegistry.Global
            match registry.TryGet closureName with
            | Some closure ->
                hints.Add(
                    { Line      = docLine
                      Character = m.Index + m.Length
                      Label     = $": GaAsync<{closure.OutputType}>"
                      IsType    = true })
            | None ->
                hints.Add(
                    { Line      = docLine
                      Character = m.Index + m.Length
                      Label     = ": GaAsync<?> (closure not found)"
                      IsType    = true })

        // let! binding hints: label the binding as async result
        let letMatches = letBangRe.Matches(line)
        for m in letMatches do
            hints.Add(
                { Line      = docLine
                  Character = m.Index + m.Length
                  Label     = " (* await *)"
                  IsType    = false })

    List.ofSeq hints

/// Generate all inlay hints for ga blocks in a markdown document.
let getInlayHints (documentText: string) : InlayHint list =
    let blocks = findGaFencedBlocks documentText
    blocks |> List.collect hintsForBlock

/// Serialize an InlayHint to LSP JSON format.
let toJson (hint: InlayHint) : Newtonsoft.Json.Linq.JObject =
    let obj = Newtonsoft.Json.Linq.JObject()
    let pos = Newtonsoft.Json.Linq.JObject()
    pos.["line"]      <- Newtonsoft.Json.Linq.JValue(hint.Line)
    pos.["character"] <- Newtonsoft.Json.Linq.JValue(hint.Character)
    obj.["position"]  <- pos
    obj.["label"]     <- Newtonsoft.Json.Linq.JValue(hint.Label)
    obj.["kind"]      <- Newtonsoft.Json.Linq.JValue(if hint.IsType then 1 else 2) // 1=Type, 2=Parameter
    obj
