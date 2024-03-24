namespace GA.Business.Querying

open System.Collections.Generic
open System

(* http://dungpa.github.io/fsharp-cheatsheet/ *)

[<AutoOpen>]
module Common =
    /// <summary>Parses a string into a value.</summary>
    /// <param name="str">The string to parse.</param>
    /// <typeparam name="TSelf">The type to parse the string into (Must implement IParsable{TSelf} interface).</typeparam>
    /// <returns>The result of parsing <paramref name="s" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s" /> is <c>null</c>.</exception>
    /// <exception cref="FormatException"><paramref name="s" /> is not in the correct format.</exception>
    /// <exception cref="OverflowException"><paramref name="s" /> is not representable by <typeparamref name="TSelf" />.</exception>
    let parse<'TSelf when 'TSelf :> IParsable<'TSelf> and 'TSelf : not struct> (str: string): 'TSelf option =
        let success, value = 'TSelf.TryParse(str, null)
        match success with
        | true -> Some value
        | false ->  None

    let memoize f =
        let dict = Dictionary<_, _>();
        fun key ->
            let exists, value = dict.TryGetValue key
            match exists with
            | true -> value
            | false -> 
                let value = f key
                dict.Add(key, value)
                value