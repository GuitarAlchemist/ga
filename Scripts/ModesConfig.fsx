#I @"../Common/GA.Business.Config"
#r "nuget: GA.Business.Config, 1.0.0"

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
            printfn $"  Alternate Names: %s{String.Join(", ", names)}")
        printfn ""
    )

// Run tests
testGetVersion()
testGetAllModes()