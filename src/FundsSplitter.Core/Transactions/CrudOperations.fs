namespace FundsSplitter.Core.Transactions

module CrudOperations = 
    open FundsSplitter.Core.Transactions.Types

    let addTransaction chat tx = 
        { chat with 
            Transactions = tx :: chat.Transactions
            KnownUsers = (tx.User :: chat.KnownUsers) |> List.distinctBy (fun u -> u.Id) }

    let editTransaction chat (tx: Tx) = 
        let replace (tx': Tx) = 
            if tx'.Id = tx.Id then tx else tx'
        { chat with Transactions = chat.Transactions |> List.map replace }