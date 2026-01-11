namespace GA.MusicTheory.DSL.LSP

open Newtonsoft.Json.Linq

/// <summary>
/// LSP type definitions shared across LSP modules
/// </summary>
module LspTypes =

    // ============================================================================
    // LSP MESSAGE TYPES
    // ============================================================================

    /// LSP message
    type LspMessage =
        { JsonRpc: string
          Id: int option
          Method: string
          Params: JObject option }

    /// LSP response
    type LspResponse =
        { JsonRpc: string
          Id: int
          Result: JObject option
          Error: JObject option }

    /// Text document identifier
    type TextDocumentIdentifier =
        { Uri: string }

    /// Position in a text document
    type Position =
        { Line: int
          Character: int }

    /// Range in a text document
    type Range =
        { Start: Position
          End: Position }

    /// Text document item
    type TextDocumentItem =
        { Uri: string
          LanguageId: string
          Version: int
          Text: string }

    /// Diagnostic severity
    type DiagnosticSeverity =
        | Error = 1
        | Warning = 2
        | Information = 3
        | Hint = 4

    /// Diagnostic
    type Diagnostic =
        { Range: Range
          Severity: DiagnosticSeverity
          Message: string
          Source: string option }

    /// Completion item kind
    type CompletionItemKind =
        | Text = 1
        | Method = 2
        | Function = 3
        | Constructor = 4
        | Field = 5
        | Variable = 6
        | Class = 7
        | Interface = 8
        | Module = 9
        | Property = 10
        | Unit = 11
        | Value = 12
        | Enum = 13
        | Keyword = 14
        | Snippet = 15
        | Color = 16
        | File = 17
        | Reference = 18

    /// Completion item
    type CompletionItem =
        { Label: string
          Kind: CompletionItemKind
          Detail: string option
          Documentation: string option
          InsertText: string option }

