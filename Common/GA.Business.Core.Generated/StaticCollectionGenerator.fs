(* Experimental *)

module YourModule.StaticCollectionGenerator

open System
open Microsoft.FSharp.Core.CompilerServices
open System.Reflection
open ProviderImplementation.ProvidedTypes
open GA.Core.Collections.Abstractions

(*
public static class Assets
{
    public static IReadOnlyCollection<Note.Flat> Notes => Note.Flat.Items;
    public static IReadOnlyCollection<Note.Sharp> Notes => Note.Sharp.Items;
}
*)

(*
let findImplementingTypes (assembly: Assembly) =
    assembly.GetTypes()
    |> Seq.filter (fun t -> 
        t.GetInterfaces() |> Seq.exists (fun i -> 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() = typeof<GA.Core.Collections.Abstractions.IStaticReadonlyCollection<_>>))
    |> Seq.toList


[<TypeProvider>]
type StaticCollectionProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config)
    
    let assemblyToScan = Assembly.Load("GA.Business.Core")    
    let thisAssembly = Assembly.GetExecutingAssembly()
    let rootNamespace = "GA.Business.Core.Generated"
    
    let interfaceNamespace = "YourNamespace.ContainingIStaticReadonlyCollection"
    let interfaceFullName = interfaceNamespace + ".IStaticReadonlyCollection"

    // This method finds all relevant types that implement IStaticReadonlyCollection
    let findAllStaticCollections() =
        assemblyToScan.GetTypes() 
        |> Seq.filter (fun t -> t.GetInterfaces() |> Seq.exists (fun i -> i.FullName = interfaceFullName))
        |> Seq.toList

    let generatePropertyForType (t: Type) =
        let propertyName = t.Name + "Instance"
        let propertyType = typeof<int> // Placeholder: Determine the actual property type
        ProvidedProperty(propertyName, propertyType)

    let generateType() =
        let myType = ProvidedTypeDefinition(
            thisAssembly,
            name = generatedNamespace + ".AllStaticCollections",
            baseType = Some(typeof<obj>)
        )

        // Generate a static property for each type found
        findAllStaticCollections()
        |> List.iter (fun t -> 
            let prop = generatePropertyForType t
            myType.AddMember(prop)
        )

        myType

    do
        // Add the generated type to the provided namespace
        this.AddNamespace(generatedNamespace, [generateType()])

// Mark the assembly as containing a type provider
[<assembly:TypeProviderAssembly>]
do()

*)
