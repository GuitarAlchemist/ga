module GA.Business.DSL.Closures.GaComputationExpression

open GA.Business.DSL.Closures.GaAsync

// ============================================================================
// GaBuilder — monadic CE over GaAsync<'T>
// ============================================================================
//
// Delay returns a thunk (unit -> GaAsync<'T>) so that branching (if/match)
// inside ga { } is evaluated lazily. Run executes the thunk.
// This matches the pattern used by FsToolkit.ErrorHandling's asyncResultCE.
//
// Critical invariants:
//   · Zero() = Ok ()   — not Error; otherwise bare `do!` short-circuits
//   · For() wraps While in TryFinally to dispose IEnumerator
//   · BindReturn/MergeSources enable applicative-style let! ... and let! ...
// ============================================================================

type GaBuilder() =

    member _.Return(x: 'T) : GaAsync<'T> = ok x

    member _.ReturnFrom(m: GaAsync<'T>) : GaAsync<'T> = m

    member _.Zero() : GaAsync<unit> = ok ()

    member _.Bind(m: GaAsync<'T>, f: 'T -> GaAsync<'U>) : GaAsync<'U> = bind f m

    /// Fused map — avoids an extra Bind wrapper for `let x = expr` (no !).
    member _.BindReturn(m: GaAsync<'T>, f: 'T -> 'U) : GaAsync<'U> = map f m

    /// Applicative zip — enables parallel `let! a = ... and! b = ...` syntax.
    member _.MergeSources(a: GaAsync<'A>, b: GaAsync<'B>) : GaAsync<'A * 'B> = zip a b

    /// Lift plain Async<'T> into GaAsync<'T> (source overload).
    member _.Source(a: Async<'T>) : GaAsync<'T> = fromAsync a

    /// Lift Result<'T, GaError> into GaAsync<'T> (source overload).
    member _.Source(r: Result<'T, GaError>) : GaAsync<'T> = ofResult r

    member _.Delay(f: unit -> GaAsync<'T>) : unit -> GaAsync<'T> = f

    member _.Run(f: unit -> GaAsync<'T>) : GaAsync<'T> = f ()

    member _.Combine(a: unit -> GaAsync<unit>, b: unit -> GaAsync<'T>) : unit -> GaAsync<'T> =
        fun () -> bind (fun () -> b ()) (a ())

    member _.TryWith(m: unit -> GaAsync<'T>, h: exn -> GaAsync<'T>) : unit -> GaAsync<'T> =
        fun () -> async.TryWith(m (), h)

    member _.TryFinally(m: unit -> GaAsync<'T>, compensation: unit -> unit) : unit -> GaAsync<'T> =
        fun () -> async.TryFinally(m (), compensation)

    member this.While(guard: unit -> bool, body: unit -> GaAsync<unit>) : unit -> GaAsync<unit> =
        fun () ->
            async {
                if guard () then
                    match! body () with
                    | Error e -> return Error e
                    | Ok ()   -> return! (this.While(guard, body)) ()
                else
                    return Ok ()
            }

    member this.For(xs: 'T seq, body: 'T -> unit -> GaAsync<unit>) : unit -> GaAsync<unit> =
        fun () ->
            let e = xs.GetEnumerator()
            (this.TryFinally(
                this.While(e.MoveNext, this.Delay(fun () -> body e.Current ())),
                fun () -> e.Dispose()))()

    member _.Using(resource: 'T :> System.IDisposable, body: 'T -> unit -> GaAsync<'U>) : unit -> GaAsync<'U> =
        fun () ->
            async {
                use _ = resource
                return! (body resource) ()
            }

/// Singleton builder — the primary entry point for ga { } blocks.
let ga = GaBuilder()

// ============================================================================
// GaDslBuilder — extends GaBuilder with pipeline-oriented custom operations
// ============================================================================
//
// fanOut  — launches multiple branches in parallel, collecting all results.
//           MaintainsVariableSpace = false: branches are independent.
// sink    — passes a value through (side-effect step, returns same value).
//           MaintainsVariableSpace = true: downstream still sees the binding.
// ============================================================================

type GaDslBuilder() =
    inherit GaBuilder()

    /// Parallel fan-out: run `branches` in parallel; fail with PartialFailure if any fail.
    [<CustomOperation("fanOut", MaintainsVariableSpace = false)>]
    member _.FanOut(m: unit -> GaAsync<'T>, [<ProjectionParameter>] branches: 'T -> GaAsync<'U> list) : unit -> GaAsync<'U list> =
        fun () ->
            async {
                match! m () with
                | Error e -> return Error e
                | Ok v    -> return! fanOutAll (branches v)
            }

    /// Sink: apply a side-effecting GaAsync step, pass the original value through.
    [<CustomOperation("sink", MaintainsVariableSpace = true)>]
    member _.Sink(m: unit -> GaAsync<'T>, [<ProjectionParameter>] effect: 'T -> GaAsync<unit>) : unit -> GaAsync<'T> =
        fun () ->
            async {
                match! m () with
                | Error e -> return Error e
                | Ok v    ->
                    match! effect v with
                    | Error e -> return Error e
                    | Ok ()   -> return Ok v
            }

/// Singleton pipeline builder — for pipeline { } blocks.
let pipeline = GaDslBuilder()
