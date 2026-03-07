namespace GA.Business.DSL

/// <summary>
/// Main entry point for the Music Theory DSL library
/// Provides parsers, LSP server, and grammar management
/// </summary>
module MusicTheoryDsl =

    open GA.Business.DSL.Types.GrammarTypes
    open GA.Business.DSL.Parsers
    // Temporarily commented out - LSP files have build errors
    // open GA.Business.DSL.LSP

    // ============================================================================
    // PARSER API
    // ============================================================================

    /// Parse a chord progression string
    let parseChordProgression (input: string) : Result<ChordProgression, string> = ChordProgressionParser.parse input

    /// Parse a fretboard navigation command
    let parseNavigation (input: string) : Result<NavigationCommand, string> = FretboardNavigationParser.parse input

    /// Parse a scale transformation command
    let parseScaleTransform (input: string) : Result<Scale * ScaleTransformation list, string> =
        ScaleTransformationParser.parse input

    /// Parse a Grothendieck operation
    let parseGrothendieck (input: string) : Result<GrothendieckOperation, string> =
        GrothendieckOperationsParser.parse input

    /// Parse any DSL command (tries all parsers)
    let parse (input: string) : Result<DslCommand, string> =
        match ChordProgressionParser.parseCommand input with
        | Ok cmd -> Ok cmd
        | Error _ ->
            match FretboardNavigationParser.parseCommand input with
            | Ok cmd -> Ok cmd
            | Error _ ->
                match ScaleTransformationParser.parseCommand input with
                | Ok cmd -> Ok cmd
                | Error _ ->
                    match GrothendieckOperationsParser.parseCommand input with
                    | Ok cmd -> Ok cmd
                    | Error e -> Error e

    // ============================================================================
    // VALIDATION API
    // ============================================================================

    /// Validate a DSL command
    let validate (command: DslCommand) : Result<DslCommand, string> = Types.DslCommand.validate command

    // Temporarily commented out - LSP files have build errors
    // /// Get diagnostics for a text input
    // let getDiagnostics (text: string) : LanguageServer.Diagnostic list =
    //     DiagnosticsProvider.validate text

    // ============================================================================
    // FORMATTING API
    // ============================================================================

    /// Format a DSL command as a string
    let format (command: DslCommand) : string = Types.DslCommand.format command

    // ============================================================================
    // LSP SERVER API
    // ============================================================================

    // Temporarily commented out - LSP files have build errors
    // /// Run the Language Server Protocol server
    // let runLspServer () =
    //     LanguageServer.run ()

    // ============================================================================
    // GRAMMAR MANAGEMENT API
    // ============================================================================

    /// Load a grammar from a file
    let loadGrammar (path: string) : Result<string * GrammarMetadata, string> =
        Adapters.TarsGrammarAdapter.loadFromFile path

    /// List all grammars in a directory
    let listGrammars (directory: string) : string list =
        Adapters.TarsGrammarAdapter.listGrammars directory

    /// Build a grammar index for a directory
    let buildGrammarIndex (directory: string) : Adapters.TarsGrammarAdapter.GrammarIndexEntry list =
        Adapters.TarsGrammarAdapter.buildIndex directory

    // ============================================================================
    // VERSION INFO
    // ============================================================================

    /// Get the version of the DSL library
    let version = "1.0.0"

    /// Get the supported DSL types
    let supportedDsls =
        [ "ChordProgression"
          "FretboardNavigation"
          "ScaleTransformation"
          "GrothendieckOperations" ]

// ============================================================================
// CLOSURE BOOTSTRAP
// ============================================================================

/// Ensures all builtin GA DSL closures are registered in GaClosureRegistry.Global.
/// F# module do-bindings are lazy — call this once at application startup
/// so Claude Code / MCP tools can find domain.*, tab.*, agent.*, io.* closures.
/// Safe to call multiple times (RegisterAll is idempotent).
module GaClosureBootstrap =

    open GA.Business.DSL.Closures.BuiltinClosures

    let init () =
        DomainClosures.register ()
        TabClosures.register ()
        PipelineClosures.register ()
        AgentClosures.register ()
        IoClosures.register ()
