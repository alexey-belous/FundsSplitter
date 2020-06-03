namespace FundsSplitter.Core.Bot.Handlers

module JoinHandler = 
    open MongoDB.Driver
    open MongoDB.Bson

    open FundsSplitter.Core
    open FundsSplitter.Core.Json
    open FundsSplitter.Core.Storage
    open FundsSplitter.Core.Transactions.CrudOperations
    open FundsSplitter.Core.Bot.Types

    open Telegram.Bot
    open Telegram.Bot.Types
    open Telegram.Bot.Types.Enums

    let validateMessage (msg: Message) = 
        if msg.Chat.Type <> ChatType.Group then
            Error "`/join` command can be executed only inside a group."
        else
        Ok msg

    let handlerFunction botContext (msg: Message) = async {
        let client = botContext.BotClient
        let cts = botContext.CancellationToken
        let db = botContext.Storage.Database

        let answer text = 
            client.SendTextMessageAsync(new ChatId(msg.Chat.Id), text, Enums.ParseMode.Default, true, false, msg.MessageId, null, cts)
            |> Async.AwaitTask

        let sendAnswer res = 
            match res with
            | Ok r -> answer r
            | Error e -> answer e

        let processCommand _ = async {
                let chats = db.GetCollection(Collections.Chats)
                let filter = BsonDocumentFilterDefinition(BsonDocument.Parse(sprintf """{ "id": %i}""" msg.Chat.Id))
                let user = 
                    {
                        Id = msg.From.Id
                        Name = sprintf "%s %s (%s)" msg.From.FirstName msg.From.LastName msg.From.Username 
                    } : Transactions.Types.User

                let upsertChat (chat: Transactions.Types.Chat) = 
                    let replaceOptions = ReplaceOptions()
                    replaceOptions.IsUpsert <- true
                    let serialized = chat |> Serializer.serialize |> BsonDocument.Parse
                    chats.ReplaceOne(filter, serialized, replaceOptions, cts)
                    |> ignore

                let! foundChatsCursor = chats.FindAsync<BsonDocument>(filter, null, cts) |> Async.AwaitTask
                let! cursor = foundChatsCursor.ToListAsync(cts)  |> Async.AwaitTask
                let foundChats = cursor |> Seq.map (fun doc -> 
                    doc.Remove("_id")
                    doc.ToJson()
                    |> Serializer.deserialize<Transactions.Types.Chat> ) 

                match foundChats |> Seq.tryFind (fun c -> c.Id = msg.Chat.Id) with
                | Some c -> 
                    let chat' = upsertUser c user
                    upsertChat chat'
                | None -> 
                    let chat' = 
                        {
                            Id = msg.Chat.Id
                            Title = msg.Chat.Title
                            KnownUsers = [user]
                            Transactions = []
                        } : Transactions.Types.Chat
                    upsertChat chat'

                return Ok "You successfully joined to the Funds Splitter group."
            }

        let! res = 
            msg
            |> validateMessage
            |> AsyncResult.fromResult processCommand
            |> Async.bind sendAnswer

        return ()
    }

    let handler = {
        CmdName = "/join"
        Handler = handlerFunction
    }