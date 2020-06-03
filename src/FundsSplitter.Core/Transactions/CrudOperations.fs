namespace FundsSplitter.Core.Transactions

module CrudOperations = 
    open FundsSplitter.Core.Json
    open FundsSplitter.Core.Transactions.Types

    open MongoDB.Driver
    open MongoDB.Bson

    let addTransaction chat tx = 
        { chat with 
            Transactions = tx :: chat.Transactions
            KnownUsers = (tx.User :: chat.KnownUsers) |> List.distinctBy (fun u -> u.Id) }

    let editTransaction chat (tx: Tx) = 
        let replace (tx': Tx) = 
            if tx'.Id = tx.Id then tx else tx'
        { chat with Transactions = chat.Transactions |> List.map replace }

    let upsertUser (user: User) chat = 
        match chat.KnownUsers |> List.tryFind (fun u -> u.Id = user.Id) with
        | Some u -> { chat with KnownUsers = chat.KnownUsers |> List.map (fun u -> if u.Id = user.Id then user else u) }
        | None -> { chat with KnownUsers = user :: chat.KnownUsers }

    let createChatFilter chatId = 
        BsonDocumentFilterDefinition(BsonDocument.Parse(sprintf """{ "id": %i}""" chatId))

    let upsertChat cts (chats: IMongoCollection<BsonDocument>) chat =
        let filter = createChatFilter chat.Id
        let replaceOptions = ReplaceOptions()
        replaceOptions.IsUpsert <- true
        let serialized = chat |> Serializer.serialize |> BsonDocument.Parse
        chats.ReplaceOne(filter, serialized, replaceOptions, cts)
        |> ignore
        chat

    let tryFindChat (chats: IMongoCollection<BsonDocument>) cts chatId = async {
        let filter = createChatFilter chatId
        let! foundChatsCursor = chats.FindAsync<BsonDocument>(filter, null, cts) |> Async.AwaitTask
        let! cursor = foundChatsCursor.ToListAsync(cts)  |> Async.AwaitTask
        return
            cursor 
            |> Seq.map (fun doc -> 
                doc.Remove("_id")
                doc.ToJson()
                |> Serializer.deserialize<Chat> ) 
            |> Seq.tryFind (fun c -> c.Id = chatId)
    }