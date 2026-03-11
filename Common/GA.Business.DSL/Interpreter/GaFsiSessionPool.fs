module GA.Business.DSL.Interpreter.GaFsiSessionPool

open System
open System.IO
open System.Threading
open System.Threading.Channels
open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.Diagnostics

// ============================================================================
// GaFsiSessionPool — pooled, safe FSI session management
//
// Safety constraints (confirmed by FCS issues #798 / #900 / #482):
//   · FsiEvaluationSession is NOT thread-safe for concurrent invocations.
//   · Multiple independent sessions CAN exist, but each must be accessed
//     from one thread at a time.
//   · A global SemaphoreSlim(1) gate serialises all evaluations across the
//     entire pool — this is intentional and safe.
//   · All sessions created with collectible = true to avoid type-load leaks.
//   · A session that throws ChoiceOf2 (crash) is discarded and replaced.
// ============================================================================

/// A structured diagnostic entry surfaced to MCP callers so they can self-correct.
[<Struct>]
type GaScriptDiagnostic =
    { Code      : string
      Message   : string
      Severity  : string   // "error" | "warning" | "info"
      Line      : int      // 1-based
      Column    : int }    // 1-based

type GaScriptResult =
    | GaScriptOk    of value: obj option * stdout: string * elapsedMs: float
    | GaScriptError of message: string  * stdout: string * diagnostics: GaScriptDiagnostic[] * elapsedMs: float

type private FsiSession =
    { Session  : FsiEvaluationSession
      Stdout   : System.Text.StringBuilder
      Stderr   : System.Text.StringBuilder }

type GaFsiSessionPool(poolSize: int, preludePath: string) =

    // Single global gate — FCS concurrent session bug
    let gate = new SemaphoreSlim(1, 1)

    // Bounded channel acting as the idle-session pool
    let pool  = Channel.CreateBounded<FsiSession>(poolSize)

    // Track created session count for pool pre-warming
    let mutable created = 0

    let createSession () : FsiSession =
        let stdout = System.Text.StringBuilder()
        let stderr = System.Text.StringBuilder()

        // Redirect FSI's Console.Out BEFORE session creation (FCS issue #201)
        let outWriter = new StringWriter(stdout)
        let errWriter = new StringWriter(stderr)

        let argv =
            [| "fsi"
               "--noninteractive"
               "--nologo"
               "--define:GA_DSL" |]

        let cfg = FsiEvaluationSession.GetDefaultConfiguration()

        let session =
            FsiEvaluationSession.Create(
                cfg,
                argv,
                new StringReader(""),
                outWriter,
                errWriter,
                collectible = true)

        // Load the GA prelude if it exists
        if File.Exists preludePath then
            let prelude = File.ReadAllText preludePath
            match session.EvalInteractionNonThrowing prelude with
            | Choice1Of2 _, _ -> ()
            | Choice2Of2 ex, _ ->
                eprintfn "[GaFsiSessionPool] Prelude load error: %s" ex.Message

        { Session = session; Stdout = stdout; Stderr = stderr }

    /// Map FSharpDiagnostic severity to lowercase string.
    let severityStr (d: FSharpDiagnostic) =
        match d.Severity with
        | FSharpDiagnosticSeverity.Error   -> "error"
        | FSharpDiagnosticSeverity.Warning -> "warning"
        | FSharpDiagnosticSeverity.Info    -> "info"
        | _                                -> "hidden"

    /// Convert FSharpDiagnostic[] to GaScriptDiagnostic[].
    let toDiagnostics (diags: FSharpDiagnostic[]) : GaScriptDiagnostic[] =
        diags |> Array.map (fun d ->
            { Code     = sprintf "FS%04d" d.ErrorNumber
              Message  = d.Message
              Severity = severityStr d
              Line     = d.StartLine
              Column   = d.StartColumn + 1 })  // FCS columns are 0-based; LSP uses 1-based

    /// Acquire an idle session from the pool, or create a new one up to poolSize.
    let acquireSession () : FsiSession =
        match pool.Reader.TryRead() with
        | true, s -> s
        | false, _ ->
            if created < poolSize then
                created <- created + 1
                createSession ()
            else
                // Block until a session is returned — channel is bounded
                pool.Reader.ReadAsync().AsTask() |> Async.AwaitTask |> Async.RunSynchronously

    /// Return a healthy session to the pool.
    let releaseSession (s: FsiSession) =
        pool.Writer.TryWrite(s) |> ignore

    do
        // Pre-warm one session at startup to validate prelude
        let warmSession = createSession ()
        created <- 1
        releaseSession warmSession

    /// Evaluate a ga script string. Returns GaScriptOk or GaScriptError.
    /// Serialised through the global gate — safe for concurrent callers.
    /// Script errors (type errors, syntax errors) are returned in GaScriptError.Diagnostics
    /// rather than thrown, so MCP callers can read and self-correct without a round-trip.
    member _.EvalAsync(script: string, ?cancellationToken: CancellationToken) : Async<GaScriptResult> =
        let ct = defaultArg cancellationToken CancellationToken.None
        async {
            do! gate.WaitAsync(ct) |> Async.AwaitTask
            let s = acquireSession ()
            s.Stdout.Clear() |> ignore
            s.Stderr.Clear() |> ignore
            let sw = Diagnostics.Stopwatch.StartNew()
            try
                match s.Session.EvalInteractionNonThrowing script with
                | Choice1Of2 _, diags ->
                    sw.Stop()
                    let out = s.Stdout.ToString()
                    let errors = diags |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)
                    gate.Release() |> ignore
                    if errors.Length > 0 then
                        releaseSession s
                        return GaScriptError ("Compilation errors", out, toDiagnostics errors, sw.Elapsed.TotalMilliseconds)
                    else
                        releaseSession s
                        return GaScriptOk (None, out, sw.Elapsed.TotalMilliseconds)
                | Choice2Of2 ex, diags ->
                    sw.Stop()
                    let out = s.Stdout.ToString()
                    // Crashed session — discard it, replace with a fresh one
                    let fresh = createSession ()
                    releaseSession fresh
                    gate.Release() |> ignore
                    return GaScriptError (ex.Message, out, toDiagnostics diags, sw.Elapsed.TotalMilliseconds)
            with ex ->
                sw.Stop()
                gate.Release() |> ignore
                return GaScriptError ($"Unexpected error: {ex.Message}", "", [||], sw.Elapsed.TotalMilliseconds)
        }

    interface IDisposable with
        member _.Dispose() =
            gate.Dispose()
            // Sessions dispose themselves via collectible GC

// ============================================================================
// Process-wide singleton pool
// ============================================================================

module GaFsiPool =

    let private defaultPreludePath =
        let asm = System.Reflection.Assembly.GetExecutingAssembly().Location
        Path.Combine(Path.GetDirectoryName asm, "GaPrelude.fsx")

    /// Process-wide singleton pool. Pool size 2 — one for dev, one for test.
    let Instance = new GaFsiSessionPool(2, defaultPreludePath)
