open System
open GA.MusicTheory.DSL.LSP

/// <summary>
/// Music Theory DSL Language Server
/// Provides LSP support for chord progressions, fretboard navigation,
/// scale transformations, and Grothendieck operations
/// </summary>
[<EntryPoint>]
let main argv =
    eprintfn "Starting Music Theory DSL Language Server..."
    eprintfn "Listening on stdin/stdout for LSP messages..."
    
    try
        // Run the LSP server
        LanguageServer.run ()
        0
    with ex ->
        eprintfn "Fatal error: %s" ex.Message
        eprintfn "Stack trace: %s" ex.StackTrace
        1

