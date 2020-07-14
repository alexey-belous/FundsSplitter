namespace FundsSplitter.Core.Bot.Handlers

module JoinHandler = 
    open MongoDB.Driver
    open MongoDB.Bson

    open FundsSplitter.Core
    open FundsSplitter.Core.Json
    open FundsSplitter.Core.Storage
    open FundsSplitter.Core.Transactions.CrudOperations
    open FundsSplitter.Core.Bot.Types
    open FundsSplitter.Core.Bot.Message

    open Telegram.Bot
    open Telegram.Bot.Types
    open Telegram.Bot.Types.Enums

    let invalidGroupTypeErrorRu = "Команда /join может быть выполнена только внутри группы."
    let successfullJoinMessageRu = 
        sprintf """
Вы успешно присоединились к группе.

Группа содержит следующих участников:
%s
"""

    let invalidGroupTypeErrorEn = "The /join command can be executed only inside a group."
    let successfullJoinMessageEn = 
        sprintf """
You successfully joined to the splitting group.

Group contain following members:
%s
"""

    let successfullJoinMessage = function
        | En -> successfullJoinMessageEn
        | Ru -> successfullJoinMessageRu

    let invalidGroupTypeError = function
        | En -> invalidGroupTypeErrorEn
        | Ru -> invalidGroupTypeErrorRu

    let validateMessage lang (msg: Message) = 
        if msg.Chat.Type <> ChatType.Group then
            lang |> invalidGroupTypeError |> Error 
        else
        Ok msg

    let handlerFunction botContext (update: Update) = 
        let msg = update.Message
        let client = botContext.BotClient
        let cts = botContext.CancellationToken
        let db = botContext.Storage.Database
        let lang = getLanguageCode update

        fun () -> async {
            let processCommand _ = async {
                    let chats = db.GetCollection(Collections.Chats)

                    let user = 
                        {
                            Id = msg.From.Id
                            Username = msg.From.Username 
                            Name = sprintf "%s %s" msg.From.FirstName msg.From.LastName 
                        } : Transactions.Types.User

                    let newChat = 
                            {
                                Id = msg.Chat.Id
                                Title = msg.Chat.Title
                                KnownUsers = []
                                Transactions = []
                            } : Transactions.Types.Chat

                    let! savedChat =  
                        tryFindChat chats cts msg.Chat.Id
                        |> Async.map (Option.defaultValue newChat)
                        |> Async.map (upsertUser user)
                        |> Async.map (upsertChat cts chats)

                    let responseMsg = 
                        savedChat.KnownUsers
                        |> List.map (fun u -> sprintf "- %s" (formatUser u))
                        |> String.concat "\n"
                        |> (successfullJoinMessage lang)

                    return Ok responseMsg
                }

            let! res = 
                msg
                |> (validateMessage lang)
                |> AsyncResult.fromResult processCommand
                |> Async.bind (sendAnswer client msg cts)

            return ()
        } |> Some