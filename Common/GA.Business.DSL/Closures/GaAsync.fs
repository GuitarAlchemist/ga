module GA.Business.DSL.Closures.GaAsync

open System

/// Discriminated union for GA-level errors — keeps domain errors distinct from infrastructure failures.
[<RequireQualifiedAccess>]
type GaError =
    | DomainError of message: string
    | ParseError  of source: string * message: string
    | IoError     of message: string * exn: exn option
    | AgentError  of agentId: string * message: string
    | PartialFailure of successes: obj list * errors: GaError list
    | Cancelled

    override this.ToString() =
        match this with
        | DomainError m          -> $"DomainError: {m}"
        | ParseError (s, m)      -> $"ParseError [{s}]: {m}"
        | IoError (m, Some e)    -> $"IoError: {m} ({e.Message})"
        | IoError (m, None)      -> $"IoError: {m}"
        | AgentError (id, m)     -> $"AgentError [{id}]: {m}"
        | PartialFailure (ok, e) -> $"PartialFailure: {ok.Length} ok, {e.Length} failed"
        | Cancelled              -> "Cancelled"

/// Primary async+result monad for all GA computation expressions.
/// Equivalent to: Async<Result<'T, GaError>>
type GaAsync<'T> = Async<Result<'T, GaError>>

[<AutoOpen>]
module GaAsyncOps =

    let ok (x: 'T) : GaAsync<'T> = async { return Ok x }

    let fail (e: GaError) : GaAsync<'T> = async { return Error e }

    let ofResult (r: Result<'T, GaError>) : GaAsync<'T> = async { return r }

    let ofOption (err: GaError) (opt: 'T option) : GaAsync<'T> =
        async { return opt |> Option.map Ok |> Option.defaultValue (Error err) }

    let bind (f: 'T -> GaAsync<'U>) (m: GaAsync<'T>) : GaAsync<'U> =
        async {
            match! m with
            | Ok v    -> return! f v
            | Error e -> return Error e
        }

    let map (f: 'T -> 'U) (m: GaAsync<'T>) : GaAsync<'U> =
        async {
            match! m with
            | Ok v    -> return Ok (f v)
            | Error e -> return Error e
        }

    let mapError (f: GaError -> GaError) (m: GaAsync<'T>) : GaAsync<'T> =
        async {
            match! m with
            | Ok v    -> return Ok v
            | Error e -> return Error (f e)
        }

    let apply (mf: GaAsync<'T -> 'U>) (mv: GaAsync<'T>) : GaAsync<'U> =
        async {
            match! mf with
            | Error e -> return Error e
            | Ok f    ->
                match! mv with
                | Ok v    -> return Ok (f v)
                | Error e -> return Error e
        }

    /// Run two GaAsync computations in parallel; fail if either fails.
    let zip (a: GaAsync<'A>) (b: GaAsync<'B>) : GaAsync<'A * 'B> =
        async {
            let boxA = async { let! r = a in return (r |> Result.map box) }
            let boxB = async { let! r = b in return (r |> Result.map box) }
            let! results = Async.Parallel [| boxA; boxB |]
            match results.[0], results.[1] with
            | Ok va, Ok vb    -> return Ok (unbox<'A> va, unbox<'B> vb)
            | Error e, _      -> return Error e
            | _, Error e      -> return Error e
        }

    /// Fan-out: run a list of GaAsync in parallel, collecting all results.
    /// Returns PartialFailure if some fail; Ok with all values if all succeed.
    let fanOutAll (computations: GaAsync<'T> list) : GaAsync<'T list> =
        async {
            let! results = computations |> List.map Async.StartChild |> Async.Sequential
            let! settled = results |> Async.Sequential
            let oks    = settled |> Array.choose (function Ok v -> Some v | _ -> None) |> Array.toList
            let errors = settled |> Array.choose (function Error e -> Some e | _ -> None) |> Array.toList
            if errors.IsEmpty then
                return Ok oks
            else
                return Error (GaError.PartialFailure (oks |> List.map box, errors))
        }

    let ignore (m: GaAsync<'T>) : GaAsync<unit> = map (fun _ -> ()) m

    let fromAsync (a: Async<'T>) : GaAsync<'T> =
        async {
            try
                let! v = a
                return Ok v
            with ex ->
                return Error (GaError.IoError (ex.Message, Some ex))
        }

    let toAsync (onError: GaError -> 'T) (m: GaAsync<'T>) : Async<'T> =
        async {
            match! m with
            | Ok v    -> return v
            | Error e -> return onError e
        }
