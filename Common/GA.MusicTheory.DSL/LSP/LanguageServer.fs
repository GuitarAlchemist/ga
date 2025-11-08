namespace GA.MusicTheory.DSL.LSP

open System
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open GA.MusicTheory.DSL.Types.GrammarTypes
open GA.MusicTheory.DSL.Parsers
open GA.MusicTheory.DSL.LSP.LspTypes
open GA.MusicTheory.DSL.LSP.CompletionProvider
open GA.MusicTheory.DSL.LSP.DiagnosticsProvider

/// <summary>
/// Language Server Protocol implementation for Music Theory DSL
/// Provides syntax highlighting, auto-completion, and diagnostics
/// </summary>
module LanguageServer =

    // ============================================================================
    // DOCUMENT MANAGEMENT
    // ============================================================================

    /// Document store
    type DocumentStore() =
        let documents = System.Collections.Concurrent.ConcurrentDictionary<string, TextDocumentItem>()

        member _.Open(doc: TextDocumentItem) =
            documents.[doc.Uri] <- doc

        member _.Close(uri: string) =
            documents.TryRemove(uri) |> ignore

        member _.Update(uri: string, text: string, version: int) =
            match documents.TryGetValue(uri) with
            | true, doc ->
                documents.[uri] <- { doc with Text = text; Version = version }
            | false, _ -> ()

        member _.Get(uri: string) =
            match documents.TryGetValue(uri) with
            | true, doc -> Some doc
            | false, _ -> None

        member _.GetAll() =
            documents.Values |> Seq.toList

    // ============================================================================
    // LSP SERVER STATE
    // ============================================================================

    /// LSP server state
    type ServerState =
        { Documents: DocumentStore
          Capabilities: JObject
          InitializeParams: JObject option }

    /// Create initial server state
    let createServerState () =
        { Documents = DocumentStore()
          Capabilities = JObject()
          InitializeParams = None }

    // ============================================================================
    // MESSAGE PARSING
    // ============================================================================

    /// Parse LSP message from JSON
    let parseMessage (json: string) : Result<LspMessage, string> =
        try
            let obj = JObject.Parse(json)
            let message =
                { JsonRpc = obj.["jsonrpc"].ToString()
                  Id = if obj.ContainsKey("id") then Some (obj.["id"].ToObject<int>()) else None
                  Method = obj.["method"].ToString()
                  Params = if obj.ContainsKey("params") then Some (obj.["params"] :?> JObject) else None }
            Ok message
        with ex ->
            Error $"Failed to parse message: %s{ex.Message}"

    /// Create LSP response
    let createResponse (id: int) (result: JObject option) (error: JObject option) : string =
        let response =
            { JsonRpc = "2.0"
              Id = id
              Result = result
              Error = error }
        JsonConvert.SerializeObject(response)

    /// Create success response
    let successResponse (id: int) (result: JObject) : string =
        createResponse id (Some result) None

    /// Create error response
    let errorResponse (id: int) (code: int) (message: string) : string =
        let error = JObject()
        error.["code"] <- JValue(code)
        error.["message"] <- JValue(message)
        createResponse id None (Some error)

    // ============================================================================
    // INITIALIZE
    // ============================================================================

    /// Handle initialize request
    let handleInitialize (state: ServerState) (parameters: JObject) : ServerState * string =
        let capabilities = JObject()

        // Text document sync
        capabilities.["textDocumentSync"] <- JValue(1)  // Full sync

        // Completion provider
        let completionProvider = JObject()
        completionProvider.["resolveProvider"] <- JValue(false)
        completionProvider.["triggerCharacters"] <- JArray([| "-"; ">"; ":"; " " |])
        capabilities.["completionProvider"] <- completionProvider

        // Diagnostic provider
        capabilities.["diagnosticProvider"] <- JValue(true)

        // Hover provider
        capabilities.["hoverProvider"] <- JValue(true)

        let result = JObject()
        result.["capabilities"] <- capabilities

        let newState = { state with Capabilities = capabilities; InitializeParams = Some parameters }
        (newState, successResponse 1 result)

    // ============================================================================
    // TEXT DOCUMENT SYNC
    // ============================================================================

    /// Handle text document open
    let handleTextDocumentOpen (state: ServerState) (parameters: JObject) : ServerState =
        let textDocument = parameters.["textDocument"].ToObject<TextDocumentItem>()
        state.Documents.Open(textDocument)
        state

    /// Handle text document change
    let handleTextDocumentChange (state: ServerState) (parameters: JObject) : ServerState =
        let uri = parameters.["textDocument"].["uri"].ToString()
        let version = parameters.["textDocument"].["version"].ToObject<int>()
        let text = parameters.["contentChanges"].[0].["text"].ToString()
        state.Documents.Update(uri, text, version)
        state

    /// Handle text document close
    let handleTextDocumentClose (state: ServerState) (parameters: JObject) : ServerState =
        let uri = parameters.["textDocument"].["uri"].ToString()
        state.Documents.Close(uri)
        state

    // ============================================================================
    // DIAGNOSTICS
    // ============================================================================

    /// Get diagnostics for a document
    let getDiagnostics (text: string) : Diagnostic list =
        let diagnostics = ResizeArray<Diagnostic>()

        // Try to parse as chord progression
        match ChordProgressionParser.parse text with
        | Error error ->
            let diagnostic =
                { Range = { Start = { Line = 0; Character = 0 };
                           End = { Line = 0; Character = text.Length } }
                  Severity = DiagnosticSeverity.Error
                  Message = error
                  Source = Some "chord-progression-parser" }
            diagnostics.Add(diagnostic)
        | Ok _ -> ()

        List.ofSeq diagnostics

    /// Publish diagnostics for a document
    let publishDiagnostics (uri: string) (diagnostics: Diagnostic list) : string =
        let notification = JObject()
        notification.["jsonrpc"] <- JValue("2.0")
        notification.["method"] <- JValue("textDocument/publishDiagnostics")

        let parameters = JObject()
        parameters.["uri"] <- JValue(uri)
        parameters.["diagnostics"] <- JArray(diagnostics |> List.map JsonConvert.SerializeObject |> List.map JToken.Parse)
        notification.["params"] <- parameters

        JsonConvert.SerializeObject(notification)

    // ============================================================================
    // MESSAGE HANDLING
    // ============================================================================

    // ============================================================================
    // COMPLETION
    // ============================================================================

    /// Handle completion request
    let handleCompletion (state: ServerState) (parameters: JObject) (id: int) : string =
        let uri = parameters.["textDocument"].["uri"].ToString()
        let position = parameters.["position"]
        let line = position.["line"].ToObject<int>()
        let character = position.["character"].ToObject<int>()

        match state.Documents.Get(uri) with
        | Some doc ->
            // Calculate absolute position in the document
            let lines = doc.Text.Split([| '\n'; '\r' |], StringSplitOptions.None)
            let absolutePosition =
                let mutable pos = 0
                for i in 0 .. (min line (lines.Length - 1)) - 1 do
                    pos <- pos + lines.[i].Length + 1  // +1 for newline
                pos + character

            // Get completions from CompletionProvider
            let completions = CompletionProvider.getCompletions doc.Text absolutePosition
            let result = JObject()
            result.["items"] <- CompletionProvider.toJson completions
            successResponse id result
        | None ->
            let result = JObject()
            result.["items"] <- JArray()
            successResponse id result

    // ============================================================================
    // HOVER
    // ============================================================================

    /// Handle hover request
    let handleHover (state: ServerState) (parameters: JObject) (id: int) : string =
        let uri = parameters.["textDocument"].["uri"].ToString()
        let position = parameters.["position"]
        let line = position.["line"].ToObject<int>()
        let character = position.["character"].ToObject<int>()

        match state.Documents.Get(uri) with
        | Some doc ->
            // Simple hover: show document type
            let result = JObject()
            let contents = JObject()
            contents.["kind"] <- JValue("markdown")
            contents.["value"] <- JValue("**Music Theory DSL**\n\nSupports:\n- Chord Progressions\n- Fretboard Navigation\n- Scale Transformations\n- Grothendieck Operations")
            result.["contents"] <- contents
            successResponse id result
        | None ->
            successResponse id (JObject())

    /// Handle LSP message
    let handleMessage (state: ServerState) (message: LspMessage) : ServerState * string option =
        match message.Method with
        | "initialize" ->
            let (newState, response) = handleInitialize state (message.Params.Value)
            (newState, Some response)

        | "initialized" ->
            (state, None)

        | "textDocument/didOpen" ->
            let newState = handleTextDocumentOpen state (message.Params.Value)
            let uri = message.Params.Value.["textDocument"].["uri"].ToString()
            let text = message.Params.Value.["textDocument"].["text"].ToString()
            let diagnostics = DiagnosticsProvider.validate text
            let notification = publishDiagnostics uri diagnostics
            (newState, Some notification)

        | "textDocument/didChange" ->
            let newState = handleTextDocumentChange state (message.Params.Value)
            let uri = message.Params.Value.["textDocument"].["uri"].ToString()
            match newState.Documents.Get(uri) with
            | Some doc ->
                let diagnostics = DiagnosticsProvider.validate doc.Text
                let notification = publishDiagnostics uri diagnostics
                (newState, Some notification)
            | None ->
                (newState, None)

        | "textDocument/didClose" ->
            let newState = handleTextDocumentClose state (message.Params.Value)
            (newState, None)

        | "textDocument/completion" ->
            let response = handleCompletion state (message.Params.Value) (message.Id.Value)
            (state, Some response)

        | "textDocument/hover" ->
            let response = handleHover state (message.Params.Value) (message.Id.Value)
            (state, Some response)

        | "shutdown" ->
            (state, Some (successResponse (message.Id.Value) (JObject())))

        | "exit" ->
            (state, None)

        | _ ->
            (state, None)

    // ============================================================================
    // SERVER LOOP
    // ============================================================================

    /// Run the LSP server
    let run () =
        let mutable state = createServerState ()
        let mutable running = true

        while running do
            try
                // Read message from stdin
                let contentLength =
                    let mutable line = Console.ReadLine()
                    while not (line.StartsWith("Content-Length:")) do
                        line <- Console.ReadLine()
                    int (line.Substring(16).Trim())

                // Skip blank line
                Console.ReadLine() |> ignore

                // Read message content
                let buffer = Array.zeroCreate contentLength
                Console.OpenStandardInput().Read(buffer, 0, contentLength) |> ignore
                let json = System.Text.Encoding.UTF8.GetString(buffer)

                // Parse and handle message
                match parseMessage json with
                | Ok message ->
                    let (newState, response) = handleMessage state message
                    state <- newState

                    match response with
                    | Some resp ->
                        let contentLength = System.Text.Encoding.UTF8.GetByteCount(resp)
                        Console.WriteLine $"Content-Length: %d{contentLength}\r\n\r\n%s{resp}"
                    | None -> ()

                    if message.Method = "exit" then
                        running <- false

                | Error error ->
                    eprintfn $"Error parsing message: %s{error}"

            with ex ->
                eprintfn $"Error in server loop: %s{ex.Message}"

