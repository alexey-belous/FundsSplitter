namespace FundsSplitter.Core

module AsyncResult = 

    let fromResult f r = async {
        match r with
        | Ok r' -> return! f r
        | Error e -> return Error e
    }

    let map f r = async {
        let! r' = r
        return 
            match r' with
            | Ok r'' -> r'' |> f |> Ok
            | Error e -> e |> Error
    }
