module GA.MusicTheory.LSP.Extensions.GaMarkdownHandler

open System.Text.RegularExpressions

// ============================================================================
// GA Markdown Handler — detect and translate ga{} blocks in .md files
// ============================================================================
//
// LSP Approach A (self-contained block detection):
//   · The LSP server registers as the handler for 'markdown' documents.
//   · On each text document event, it finds all ```ga ... ``` fenced blocks.
//   · It translates inner-block positions to document-level positions
//     for completion, hover, diagnostics, and semantic tokens.
//   · Compatible with: VS Code, Neovim (fenced block detection), Zed, Claude Code.
// ============================================================================

/// A detected ga fenced block within a markdown document.
[<Struct>]
type GaBlock =
    { /// 0-based line index of the opening ``` fence
      FenceStartLine  : int
      /// 0-based line index of the closing ``` fence
      FenceEndLine    : int
      /// 0-based line of the first content line (FenceStartLine + 1)
      ContentStartLine: int
      /// The script content (lines between the fences, joined with \n)
      Content         : string }

/// Find all ```ga...``` fenced blocks in a markdown document.
let findGaFencedBlocks (documentText: string) : GaBlock list =
    let lines = documentText.Split('\n')
    let blocks = System.Collections.Generic.List<GaBlock>()

    let mutable i = 0
    while i < lines.Length do
        let trimmed = lines.[i].TrimStart()
        // Match opening fence: ``` or ~~~, followed by ga or ga\s+...
        if Regex.IsMatch(trimmed, @"^(`{3,}|~{3,})\s*ga\b") then
            let fenceStart = i
            let fence = Regex.Match(trimmed, @"^(`{3,}|~{3,})").Value
            // Find closing fence (same or longer fence of same type)
            let mutable j = i + 1
            let mutable found = false
            while j < lines.Length && not found do
                let closeTrimmed = lines.[j].TrimStart()
                if closeTrimmed.StartsWith(fence) && Regex.IsMatch(closeTrimmed, @"^(`{3,}|~{3,})\s*$") then
                    let contentLines = lines.[fenceStart + 1 .. j - 1]
                    blocks.Add(
                        { FenceStartLine   = fenceStart
                          FenceEndLine     = j
                          ContentStartLine = fenceStart + 1
                          Content          = String.concat "\n" contentLines })
                    found <- true
                    i <- j + 1
                else
                    j <- j + 1
            if not found then i <- i + 1
        else
            i <- i + 1

    List.ofSeq blocks

// ============================================================================
// Position translation helpers
// ============================================================================

/// Translate a (line, char) position inside a ga block's content to the
/// equivalent position in the containing markdown document.
let innerToDocumentPosition (block: GaBlock) (innerLine: int) (innerChar: int) =
    (block.ContentStartLine + innerLine, innerChar)

/// Check if a document (line, char) position falls inside a ga block.
let isInsideBlock (block: GaBlock) (docLine: int) =
    docLine >= block.ContentStartLine && docLine < block.FenceEndLine

/// Find the block containing a given document line, if any.
let findBlockAt (blocks: GaBlock list) (docLine: int) : GaBlock option =
    blocks |> List.tryFind (fun block -> isInsideBlock block docLine)

/// Translate a document (line, char) to an inner-block offset.
let documentToInnerPosition (block: GaBlock) (docLine: int) (docChar: int) =
    (docLine - block.ContentStartLine, docChar)

// ============================================================================
// Diagnostic translation: inner-block diagnostics → document coordinates
// ============================================================================

/// Translate a diagnostic range from inner-block coordinates to document coordinates.
let translateDiagnosticRange (block: GaBlock) (innerStartLine: int) (innerStartChar: int) (innerEndLine: int) (innerEndChar: int) =
    let (dl0, dc0) = innerToDocumentPosition block innerStartLine innerStartChar
    let (dl1, dc1) = innerToDocumentPosition block innerEndLine innerEndChar
    (dl0, dc0, dl1, dc1)
