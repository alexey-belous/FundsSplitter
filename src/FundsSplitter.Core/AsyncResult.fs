namespace FundsSplitter.Core

module AsyncResult = 

    let fromResult f r = async {
        match r with
        | Ok r' -> return! f r
        | Error e -> return Error e
    }

    let bind f r = async {
        let! r' = r
        return
            match r' with
            | Ok r'' -> r'' |> f
            | Error e -> e |> Error
    }

    let bindAsync (f: 'a -> Async<Result<'b, _>>) r = async {
        let! r' = r
        match r' with
        | Ok r'' -> return! f r''
        | Error e -> return Error e
    }

    let map f r = async {
        let! r' = r
        return 
            match r' with
            | Ok r'' -> r'' |> f |> Ok
            | Error e -> e |> Error
    }
