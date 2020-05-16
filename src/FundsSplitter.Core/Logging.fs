namespace FundsSplitter.Core

module Logging = 
    let logResult msgSuccess msgError res = 
        match res with
        | Ok _ -> 
            printfn "%s" msgSuccess
            res
        | Error text -> 
            printfn "%s Error details: %s" msgError text
            res