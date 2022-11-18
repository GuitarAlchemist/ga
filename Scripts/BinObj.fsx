open System.Globalization
open System.IO

let EnumerateDirectories path =
    Directory.EnumerateDirectories(path) |> Seq.toList

let isObjOrBinFolder (folderName:string) =
    folderName.EndsWith("obj", true, CultureInfo.InvariantCulture) || folderName.EndsWith("bin", true, CultureInfo.InvariantCulture)

let rec getFoldersToDelete path =
    match EnumerateDirectories path with
    | [] -> []
    | subfolders  ->
        let targetFolders = subfolders 
                            |> List.filter isObjOrBinFolder
        let targets = subfolders 
                            |> List.filter (isObjOrBinFolder >> not) 
                            |> List.collect getFoldersToDelete
                            |> List.append targetFolders
        targets