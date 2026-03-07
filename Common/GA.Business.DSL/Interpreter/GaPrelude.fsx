// GA Language Prelude — loaded automatically into every FSI session
// This file is executed once when a GaFsiSession is created.
// It opens the core GA DSL namespaces and registers builtins.

#r "GA.Business.DSL.dll"

open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaComputationExpression
open GA.Business.DSL.Closures.GaClosureRegistry

// Force builtin registration (module init via `do register()`)
open GA.Business.DSL.Closures.BuiltinClosures.DomainClosures
open GA.Business.DSL.Closures.BuiltinClosures.PipelineClosures
open GA.Business.DSL.Closures.BuiltinClosures.AgentClosures
open GA.Business.DSL.Closures.BuiltinClosures.IoClosures

// Convenience: run a GaAsync synchronously (for top-level FSI scripts)
let run (m: GaAsync<'T>) : Result<'T, GaError> = m |> Async.RunSynchronously

// Convenience: invoke a closure by name
let invoke name inputs = GaClosureRegistry.Global.Invoke(name, inputs) |> run

// Convenience: list available closures
let listClosures () =
    GaClosureRegistry.Global.List()
    |> List.iter (fun c -> printfn "%s [%A] — %s" c.Name c.Category c.Description)
