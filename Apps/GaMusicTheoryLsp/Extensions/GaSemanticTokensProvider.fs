module GA.MusicTheory.LSP.Extensions.GaSemanticTokensProvider

open GA.MusicTheory.LSP.Extensions.GaMarkdownHandler

// ============================================================================
// Semantic tokens provider for ga{} blocks
// ============================================================================
// Provides syntax highlighting for ga computation expression scripts embedded
// in markdown files. Token types follow LSP 3.17 SemanticTokensLegend.
// ============================================================================

[<Literal>]
let TokenTypeKeyword    = 0
[<Literal>]
let TokenTypeOperator   = 1
[<Literal>]
let TokenTypeVariable   = 2
[<Literal>]
let TokenTypeString     = 3
[<Literal>]
let TokenTypeComment    = 4
[<Literal>]
let TokenTypeFunction   = 5
[<Literal>]
let TokenTypeNumber     = 6
[<Literal>]
let TokenTypeProperty   = 7

/// LSP semantic token legend — must match the registration in initialize.
let tokenTypeLegend = [|
    "keyword"; "operator"; "variable"; "string";
    "comment"; "function"; "number"; "property"
|]

let tokenModifierLegend = [| "declaration"; "readonly"; "async" |]

[<Struct>]
type SemanticToken =
    { DeltaLine      : int
      DeltaStartChar : int
      Length         : int
      TokenType      : int
      TokenModifiers : int }

/// GA DSL keywords for highlighting.
let private gaKeywords =
    Set.ofList
        [ "ga"; "pipeline"; "let"; "let!"; "do!"; "return"; "return!"; "yield"
          "yield!"; "match"; "with"; "if"; "then"; "else"; "for"; "in"; "while"
          "do"; "use"; "use!"; "and!"; "fanOut"; "sink" ]

/// Classify a word token at a given position.
let private classifyWord (word: string) =
    if gaKeywords.Contains word then Some TokenTypeKeyword
    elif word.StartsWith "\"" || word.StartsWith "'" then Some TokenTypeString
    elif System.Double.TryParse(word, ref 0.0) then Some TokenTypeNumber
    elif word.EndsWith "Agent" || word.EndsWith "Registry" || word.EndsWith "Service" then Some TokenTypeFunction
    elif word.Contains "." then Some TokenTypeProperty
    else None

/// Generate semantic tokens for the content of a single ga block.
/// Returns absolute (line, char, length, type, mods) tuples.
let tokenizeBlock (block: GaBlock) : (int * int * int * int * int) list =
    let lines = block.Content.Split('\n')
    let tokens = System.Collections.Generic.List<int * int * int * int * int>()
    let wordRe = System.Text.RegularExpressions.Regex(@"[a-zA-Z_!][a-zA-Z0-9_!.]*|""[^""]*""|//[^\n]*")

    for innerLine = 0 to lines.Length - 1 do
        let line = lines.[innerLine]
        let docLine = block.ContentStartLine + innerLine
        let matches = wordRe.Matches(line)
        for m in matches do
            let word = m.Value
            // Comment token
            if word.StartsWith "//" then
                tokens.Add(docLine, m.Index, word.Length, TokenTypeComment, 0)
            // String token
            elif word.StartsWith "\"" then
                tokens.Add(docLine, m.Index, word.Length, TokenTypeString, 0)
            else
                match classifyWord word with
                | Some tokenType -> tokens.Add(docLine, m.Index, word.Length, tokenType, 0)
                | None -> ()

    List.ofSeq tokens

/// Encode a list of absolute token positions into LSP delta encoding.
let encodeDelta (tokens: (int * int * int * int * int) list) : int[] =
    let result = System.Collections.Generic.List<int>()
    let mutable prevLine = 0
    let mutable prevChar = 0

    for (line, char, len, typ, mods) in tokens do
        let deltaLine = line - prevLine
        let deltaChar = if deltaLine = 0 then char - prevChar else char
        result.AddRange([| deltaLine; deltaChar; len; typ; mods |])
        prevLine <- line
        prevChar <- char

    result.ToArray()

/// Build the SemanticTokens response data for all ga blocks in a document.
let getSemanticTokensData (documentText: string) : int[] =
    let blocks = findGaFencedBlocks documentText
    let allTokens =
        blocks
        |> List.collect tokenizeBlock
        |> List.sortBy (fun (line, ch, _, _, _) -> (line, ch))
    encodeDelta allTokens
