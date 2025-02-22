#r "nuget: FSharp.Configuration"
#r "nuget: System.Collections.Immutable"

// Adjust the path if necessary
#load "Common/GA.Business.Config/ModesConfig.fs"

open GA.Business.Config
open ModesConfig

// Helper function to print all modes
let printAllModes() =
    let allModes = GetAllModes()
    printfn "Number of modes: %d" allModes.Count
    allModes |> Seq.iter (fun mode -> 
        printfn "Mode: %s" mode.Name
        printfn "  Interval Class Vector: %s" mode.IntervalClassVector
        printfn "  Notes: %s" mode.Notes
        printfn "  Description: %s" (mode.Description |> Option.defaultValue "N/A")
        printfn "  Alternate Names: %A" (mode.AlternateNames |> Seq.toList)
        printfn ""
    )

// Print a message to confirm the script has loaded
printfn "ModesConfig module loaded and ready for testing!"
printfn "Use 'printAllModes()' to see all modes."