module GA.Business.DSL.Closures.GaClosureRegistry

open System.Collections.Concurrent
open GA.Business.DSL.Closures.GaAsync

// ============================================================================
// Closure categories — mirrors TARS v1 closure factory pattern
// ============================================================================

[<RequireQualifiedAccess>]
type GaClosureCategory =
    | Domain    // Pure music-theory operations (chord analysis, scale queries)
    | Pipeline  // Data pipeline steps (pull BSP rooms, embed, store)
    | Agent     // Agent invocation wrappers (TheoryAgent, TabAgent, etc.)
    | Io        // I/O closures (file, HTTP, Qdrant, MongoDB)

// ============================================================================
// GaClosure — a named, typed, discoverable computation unit
// ============================================================================

/// A GA closure: named, categorized, typed, and executable.
/// I/O is heterogeneous (Map<string,obj>) following the TARS v1 closure factory pattern.
/// This enables dynamic composition without requiring generic instantiation.
type GaClosure =
    { Name        : string
      Category    : GaClosureCategory
      Description : string
      Tags        : string list
      InputSchema : Map<string, string>   // param name → type description
      OutputType  : string
      Exec        : Map<string, obj> -> GaAsync<obj> }

// ============================================================================
// GaClosureRegistry — concurrent, singleton-backed registry
// ============================================================================

type GaClosureRegistry() =

    let store = ConcurrentDictionary<string, GaClosure>(System.StringComparer.OrdinalIgnoreCase)

    /// Register a closure, overwriting any existing entry with the same name.
    member _.Register(closure: GaClosure) : unit =
        store.[closure.Name] <- closure

    /// Register multiple closures at once.
    member this.RegisterAll(closures: GaClosure seq) : unit =
        closures |> Seq.iter this.Register

    /// Try to retrieve a closure by name.
    member _.TryGet(name: string) : GaClosure option =
        match store.TryGetValue(name) with
        | true, c -> Some c
        | _       -> None

    /// List closures, optionally filtered by category.
    member _.List(?category: GaClosureCategory) : GaClosure list =
        store.Values
        |> Seq.filter (fun c ->
            category |> Option.forall (fun cat -> c.Category = cat))
        |> Seq.toList

    /// Invoke a closure by name with a typed input map.
    member this.Invoke(name: string, inputs: Map<string, obj>) : GaAsync<obj> =
        match this.TryGet(name) with
        | None   -> fail (GaError.DomainError $"Closure not found: '{name}'")
        | Some c -> c.Exec inputs

    /// Remove a closure by name. Returns true if it was present.
    member _.Unregister(name: string) : bool =
        store.TryRemove(name) |> fst

    member _.Count = store.Count

    /// Process-wide singleton registry. Builtins register themselves at module init.
    static member val Global = GaClosureRegistry() with get, set

// ============================================================================
// Helper to build closures concisely
// ============================================================================

module Closure =

    let make
        (name: string)
        (category: GaClosureCategory)
        (description: string)
        (inputSchema: (string * string) list)
        (outputType: string)
        (exec: Map<string, obj> -> GaAsync<obj>)
        : GaClosure =
        { Name        = name
          Category    = category
          Description = description
          Tags        = []
          InputSchema = Map.ofList inputSchema
          OutputType  = outputType
          Exec        = exec }

    let withTags (tags: string list) (c: GaClosure) : GaClosure = { c with Tags = tags }

    /// Convenience: wrap a typed function as an obj-in/obj-out closure.
    let typed<'TIn, 'TOut>
        (name: string)
        (category: GaClosureCategory)
        (description: string)
        (fn: 'TIn -> GaAsync<'TOut>)
        : GaClosure =
        { Name        = name
          Category    = category
          Description = description
          Tags        = []
          InputSchema = Map.ofList [ "input", typeof<'TIn>.Name ]
          OutputType  = typeof<'TOut>.Name
          Exec        = fun inputs ->
              let input =
                  match inputs.TryFind "input" with
                  | Some v -> v :?> 'TIn
                  | None   -> failwith $"Closure '{name}' requires 'input' parameter"
              fn input |> GaAsyncOps.map box }
