namespace GA.Business.Querying

(* http://dungpa.github.io/fsharp-cheatsheet/ *)

[<AutoOpen>]
module Fib =
    (* See https://thesharperdev.com/posts/fsharp-fibonacci-five-ways/ *)
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
