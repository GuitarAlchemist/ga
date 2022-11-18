namespace GA.Business.Querying

open GA.Business.Core.Notes
open GA.Business.Core.Intervals
open GA.Business.Core.Fretboard
open System.Collections.Generic

[<AutoOpen>]
module FretboardQueries =
    (* See https://thesharperdev.com/posts/fsharp-fibonacci-five-ways/ *)
    let memoize f =
        let dict = Dictionary<_, _>();
        fun key ->
            let exist, value = dict.TryGetValue key
            match exist with
            | true -> value
            | false -> 
                let value = f key
                dict.Add(key, value)
                value

    let rec fib(n: int):int = 
        match n with
        | 1 | 2 -> n
        | n -> fib (n-1) + fib (n-2)

    let memoFib = memoize fib

    let fib2 n = 
        let mutable last = 0
        let mutable next = 1
        seq {
            0
            1
            for i in 1 .. (n - 1) do
                let temp = last + next
                last <- next
                next <- temp
                next
            }

    let inline add2 (x, y) = x + y
