module GA.Business.DSL.Interpreter.GaFsiSessionPool

open System
open System.IO
open System.Threading
open System.Threading.Channels
open FSharp.Compiler.Interactive.Shell

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

type GaScriptResult =
    | GaScriptOk    of value: obj option * stdout: string
    | GaScriptError of message: string  * stdout: string

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
    member _.EvalAsync(script: string, ?cancellationToken: CancellationToken) : Async<GaScriptResult> =
        let ct = defaultArg cancellationToken CancellationToken.None
        async {
            do! gate.WaitAsync(ct) |> Async.AwaitTask
            let s = acquireSession ()
            s.Stdout.Clear() |> ignore
            s.Stderr.Clear() |> ignore
            try
                match s.Session.EvalInteractionNonThrowing script with
                | Choice1Of2 _, _ ->
                    let out = s.Stdout.ToString()
                    releaseSession s
                    gate.Release() |> ignore
                    return GaScriptOk (None, out)
                | Choice2Of2 ex, _ ->
                    let out = s.Stdout.ToString()
                    // Crashed session — discard it, replace with a fresh one
                    let fresh = createSession ()
                    releaseSession fresh
                    gate.Release() |> ignore
                    return GaScriptError (ex.Message, out)
            with ex ->
                gate.Release() |> ignore
                return GaScriptError ($"Unexpected error: {ex.Message}", "")
        }

    /// Evaluate and attempt to return the last bound value from the FSI session.
    member this.EvalWithResultAsync(script: string, ?cancellationToken: CancellationToken) : Async<GaScriptResult> =
        let ct = defaultArg cancellationToken CancellationToken.None
        async {
            do! gate.WaitAsync(ct) |> Async.AwaitTask
            let s = acquireSession ()
            s.Stdout.Clear() |> ignore
            s.Stderr.Clear() |> ignore
            try
                match s.Session.EvalExpressionNonThrowing script with
                | Choice1Of2 (Some v), _ ->
                    let out = s.Stdout.ToString()
                    releaseSession s
                    gate.Release() |> ignore
                    return GaScriptOk (Some v.ReflectionValue, out)
                | Choice1Of2 None, _ ->
                    let out = s.Stdout.ToString()
                    releaseSession s
                    gate.Release() |> ignore
                    return GaScriptOk (None, out)
                | Choice2Of2 ex, _ ->
                    let out = s.Stdout.ToString()
                    let fresh = createSession ()
                    releaseSession fresh
                    gate.Release() |> ignore
                    return GaScriptError (ex.Message, out)
            with ex ->
                gate.Release() |> ignore
                return GaScriptError ($"Unexpected error: {ex.Message}", "")
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
