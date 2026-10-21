namespace Lego

open System
open System.Collections.Generic
open System.Globalization

/// A request's outcome: the scene, or every reason it was refused, and its JSON either way.
[<Sealed>]
type Described internal (root: BlockId, seed: uint64, result: Result<Scene, Problem list>) =
    member _.Root = root
    member _.Seed = seed

    /// Whether the scene passed validation.
    member _.Valid = Result.isOk result

    /// The scene, when Valid.
    member _.Scene =
        match result with
        | Ok scene -> scene
        | Error _ -> invalidOp "the scene was refused; see Problems"

    /// Why the scene was refused; empty when Valid.
    member _.Problems: IReadOnlyList<Problem> =
        match result with
        | Ok _ -> Array.empty
        | Error problems -> Array.ofList problems

    /// lego-scene/1 when Valid, lego-errors/1 when not.
    member _.Json =
        match result with
        | Ok scene -> Describe.scene scene
        | Error problems -> Describe.errors root seed problems

/// The way in for hosts and tools: a block named as text, arguments as name=value, a seed and limits, generated then
/// validated. Nothing here touches an engine, a file or the network.
[<AbstractClass; Sealed>]
type Api =
    /// The prototype's blocks.
    static member Library = Polis.library

    /// name@version, or a name alone for its highest version in the library.
    static member Resolve(library: Library, spec: string) : Result<BlockId, Issue> =
        match spec.Split '@' with
        | [| name |] ->
            match library.Blocks.Keys |> Seq.filter (fun id -> id.Name = name) |> Seq.sortByDescending _.Version |> Seq.tryHead with
            | Some id -> Ok id
            | None -> Error(BadArgument("block", $"no block named '{name}' in the library"))
        | [| name; version |] ->
            match Int32.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture) with
            | true, v -> Ok { Name = name; Version = v }
            | _ -> Error(BadArgument("block", $"'{spec}' is not name@version"))
        | _ -> Error(BadArgument("block", $"'{spec}' is not name or name@version"))

    /// Each argument's value: a whole number when it reads as one, a real one otherwise.
    static member private Argument(text: string) : Result<string * Value, Issue> =
        match text.Split('=', 2) with
        | [| name; value |] ->
            match Int32.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) with
            | true, n -> Ok(name, Whole n)
            | _ ->
                match Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture) with
                | true, x -> Ok(name, Real x)
                | _ -> Error(BadArgument(name, $"'{value}' is not a number"))
        | _ -> Error(BadArgument(text, "expected name=value"))

    /// spec generated with these arguments (name=value), this seed and these limits, then validated.
    static member Describe(library: Library, spec: string, seed: uint64, args: string seq, limits: Limits) =
        let refused (id: BlockId) issues =
            Described(id, seed, Error(issues |> List.map (fun issue -> ({ Path = id.Name; Block = id; Issue = issue }: Problem))))

        let parsed = args |> Seq.map Api.Argument |> List.ofSeq
        let wrong = parsed |> List.choose (function Error issue -> Some issue | Ok _ -> None)

        match Api.Resolve(library, spec) with
        | Error issue -> refused { Name = spec; Version = 0 } (issue :: wrong)
        | Ok root when not wrong.IsEmpty -> refused root wrong
        | Ok root ->
            let given = parsed |> List.choose (function Ok arg -> Some arg | Error _ -> None)
            Described(root, seed, Generate.run library limits seed root given |> Validate.check)

    /// spec from the prototype's library, within the standard limits.
    static member Describe(spec: string, seed: uint64, args: string seq) =
        Api.Describe(Polis.library, spec, seed, args, Limits.Standard)
