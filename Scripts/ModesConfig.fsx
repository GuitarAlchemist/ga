#I @"../Common/GA.Business.Config"
#r "nuget: GA.Business.Config, 1.0.0"

open System // String.Join

open GA.Business.Config
open ModesConfig

// Your test functions here
let testGetVersion() =
    let version = GetVersion()
    printfn $"Current version: %A{version}"

let testGetAllModes() =
    let modes = GetAllModes()
    printfn $"Number of modes: %d{modes.Count}"
    modes |> Seq.iter (fun mode -> 
        printfn $"Mode: %s{mode.Name}"
        printfn $"  Interval Class Vector: %s{mode.IntervalClassVector}"
        printfn $"  Notes: %s{mode.Notes}"
        mode.Description |> Option.iter (fun desc -> printfn $"  Description: %s{desc}")
        mode.AlternateNames |> Option.iter (fun names ->
            // The separator has to be bound outside the interpolation: a quoted
            // literal inside {...} of a single-quote interpolated string is FS3373.
            let alternateNames = String.Join(", ", names)
            printfn $"  Alternate Names: %s{alternateNames}")
        printfn ""
    )

// Run tests
testGetVersion()
testGetAllModes()