// Lists the bin/obj build folders under a root, deepest match first.
// Usage: dotnet fsi Scripts/BinObj.fsx <root>   (defaults to the current directory)
open System
open System.IO

let EnumerateDirectories path =
    Directory.EnumerateDirectories(path) |> Seq.toList

/// True when the folder itself is named bin or obj.
/// EnumerateDirectories yields full paths, so testing the whole path with
/// EndsWith also matched any folder whose *name* merely ends in those three
/// letters — "cabin" and "Robin" were reported as build output.
let isObjOrBinFolder (path: string) =
    let name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
    String.Equals(name, "obj", StringComparison.OrdinalIgnoreCase)
    || String.Equals(name, "bin", StringComparison.OrdinalIgnoreCase)

let rec getFoldersToDelete path =
    match EnumerateDirectories path with
    | [] -> []
    | subfolders ->
        let targetFolders = subfolders |> List.filter isObjOrBinFolder

        let targets =
            subfolders
            |> List.filter (isObjOrBinFolder >> not)
            |> List.collect getFoldersToDelete
            |> List.append targetFolders

        targets

// Entry point — without it the script defined the walk and then did nothing.
let root =
    match fsi.CommandLineArgs with
    | [| _ |] -> Directory.GetCurrentDirectory()
    | args -> args.[1]

getFoldersToDelete root |> List.iter (printfn "%s")
