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
            Error "/join command can be executed only inside a group."
        else
        Ok msg

    let handlerFunction botContext (update: Update) = 
        let msg = update.Message
        let client = botContext.BotClient
        let cts = botContext.CancellationToken
        let db = botContext.Storage.Database

        fun () -> async {
            let answer text = 
                client.SendTextMessageAsync(new ChatId(msg.Chat.Id), text, Enums.ParseMode.Markdown, true, false, msg.MessageId, null, cts)
                |> Async.AwaitTask

            let sendAnswer res = 
                match res with
                | Ok r -> answer r
                | Error e -> answer e

            let processCommand _ = async {
                    let chats = db.GetCollection(Collections.Chats)
                    let user = 
                        {
                            Id = msg.From.Id
                            Name = sprintf "%s %s (%s)" msg.From.FirstName msg.From.LastName msg.From.Username 
                        } : Transactions.Types.User

                    let newChat = 
                            {
                                Id = msg.Chat.Id
                                Title = msg.Chat.Title
                                KnownUsers = []
                                Transactions = []
                            } : Transactions.Types.Chat

                    do! tryFindChat chats cts msg.Chat.Id
                        |> Async.map (Option.defaultValue newChat)
                        |> Async.map (upsertUser user)
                        |> Async.map (upsertChat cts chats)

                    return Ok "You successfully joined to the Funds Splitter group."
                }

            let! res = 
                msg
                |> validateMessage
                |> AsyncResult.fromResult processCommand
                |> Async.bind sendAnswer

            return ()
        } |> Some