namespace GA.Business.Querying

[<AutoOpen>]
module Expressions =    
    
    (* https://fsharpforfunandprofit.com/posts/computation-expressions-intro/ *)
    [<AutoOpen>]
    module ComputationExpressions =
        type OptionBuilder() =
            member this.Return(x) =
                Some x

            member this.Bind(x, f) =
                printfn $"Bind x: {x}"
                Option.bind f x

        let option = OptionBuilder()
       
        //let x(someIntOption: int option) =
        //    option {
        //        let! y = someIntOption
        //        return y
        //    }
                    

   


