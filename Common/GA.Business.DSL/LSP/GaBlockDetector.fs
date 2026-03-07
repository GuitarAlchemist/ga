namespace GA.Business.DSL.LSP

open GA.Business.DSL.Closures.GaClosureRegistry

// ============================================================================
// GaBlockDetector — locate ```ga ... ``` fenced blocks in markdown/plain text
//
// LSP positions are 0-based line numbers.  A block is represented as:
//   { StartLine; EndLine; InnerStart; InnerEnd }
// where:
//   StartLine / EndLine  — document lines of the opening/closing ``` fences
//   InnerStart / InnerEnd — first/last content line inside the block
// ============================================================================

/// A fenced ```ga``` block detected inside a document.
type GaFencedBlock =
    { /// 0-based line index of the opening "```ga" fence
      StartLine  : int
      /// 0-based line index of the closing "```" fence
      EndLine    : int
      /// 0-based line index of the first content line (StartLine + 1)
      InnerStart : int
      /// 0-based line index of the last content line (EndLine - 1)
      InnerEnd   : int }

module GaBlockDetector =

    /// Detect all ```ga ... ``` fenced blocks in a document string.
    /// Returns blocks in document order; unclosed blocks are ignored.
    let findGaFencedBlocks (text: string) : GaFencedBlock list =
        let lines = text.Split('\n')
        let blocks = System.Collections.Generic.List<GaFencedBlock>()
        let mutable openStart = -1

        for i in 0 .. lines.Length - 1 do
            let trimmed = lines.[i].TrimStart()
            if trimmed.StartsWith("```ga") && openStart < 0 then
                openStart <- i
            elif trimmed.StartsWith("```") && not (trimmed.StartsWith("```ga")) && openStart >= 0 then
                let b = { StartLine  = openStart
                          EndLine    = i
                          InnerStart = openStart + 1
                          InnerEnd   = i - 1 }
                blocks.Add(b)
                openStart <- -1

        blocks |> Seq.toList

    /// Return true if the given 0-based line number falls inside any ga block's content.
    let isInsideGaBlock (blocks: GaFencedBlock list) (line: int) : bool =
        blocks |> List.exists (fun b -> line >= b.InnerStart && line <= b.InnerEnd)

    /// Given a line inside a ga block, return the block-relative (0-based) line index.
    let toBlockRelativeLine (block: GaFencedBlock) (docLine: int) : int =
        docLine - block.InnerStart

    /// Given a block-relative line index, convert to document-level line index.
    let toDocumentLine (block: GaFencedBlock) (blockLine: int) : int =
        blockLine + block.InnerStart

    /// Extract the source text of a block's content (lines between the fences).
    let blockContent (text: string) (block: GaFencedBlock) : string =
        let lines = text.Split('\n')
        if block.InnerStart > block.InnerEnd then ""
        else
            lines.[block.InnerStart .. block.InnerEnd]
            |> String.concat "\n"

    // ── Semantic token helpers ─────────────────────────────────────────────────
    // LSP semantic tokens/full uses a delta-encoded flat array:
    //   [deltaLine, deltaStart, length, tokenType, tokenModifiers]

    /// Token type index for "function" (per LSP SemanticTokensLegend).
    [<Literal>]
    let TokenTypeFunction = 0

    /// Build semantic token data for closure-name identifiers inside all ga blocks.
    /// Returns the flat delta-encoded int array expected by LSP.
    let buildSemanticTokens (text: string) (blocks: GaFencedBlock list) : int list =
        let closureNames =
            GaClosureRegistry.Global.List()
            |> List.map (fun c -> c.Name)
            |> Set.ofList

        let lines = text.Split('\n')
        let tokens = System.Collections.Generic.List<int * int * int>() // line, col, len

        for block in blocks do
            for docLine in block.InnerStart .. block.InnerEnd do
                if docLine < lines.Length then
                    let line = lines.[docLine]
                    // Find all identifiers (word.word style) on the line
                    let mutable i = 0
                    while i < line.Length do
                        if System.Char.IsLetter(line.[i]) || line.[i] = '_' then
                            let start = i
                            while i < line.Length && (System.Char.IsLetterOrDigit(line.[i]) || line.[i] = '_' || line.[i] = '.') do
                                i <- i + 1
                            let word = line.[start .. i - 1]
                            if closureNames.Contains(word) then
                                tokens.Add(docLine, start, word.Length)
                        else
                            i <- i + 1

        // Delta-encode: sort by line then col, emit deltas
        let sorted = tokens |> Seq.toList |> List.sortBy (fun (l, c, _) -> l, c)
        let data = System.Collections.Generic.List<int>()
        let mutable prevLine = 0
        let mutable prevCol  = 0
        for (l, c, len) in sorted do
            let deltaLine = l - prevLine
            let deltaCol  = if deltaLine = 0 then c - prevCol else c
            data.Add(deltaLine)
            data.Add(deltaCol)
            data.Add(len)
            data.Add(TokenTypeFunction)
            data.Add(0) // no modifiers
            prevLine <- l
            prevCol  <- c
        data |> Seq.toList
