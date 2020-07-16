namespace FundsSplitter.Core.Bot.Handlers

module PaymentHandler = 
    open System

    open FundsSplitter.Core
    open FundsSplitter.Core.Storage
    open FundsSplitter.Core.Strings
    open FundsSplitter.Core.Bot.Types
    open FundsSplitter.Core.Bot.Message
    open FundsSplitter.Core.Transactions.Types
    open FundsSplitter.Core.Transactions.CrudOperations
    open FundsSplitter.Core.Transactions.ProcessingLogic

    open Telegram.Bot
    open Telegram.Bot.Types

    let ChatNotFoundErrorEn = "There's no Funds Splitter group in this chat."
    let MessageDoesntContainAmountEn = "I couldn't find amount in your message."
    let NotAllMentionedUsersWasAddedToGroupEn = "Not all mentioned users was added to the splitting group."
    let AmountMustBePositiveEn = "Amount value must be possitive."
    let YouAreNotInTheGroupEn = "You aren't in the Funds Splitter group. To joint the group send me a /join command."

    let PayCommandAnswerEn amount description splittingSubset = 
        sprintf 
            "Amount: %M UAH.\n%s\nSplit between: %s." 
            amount
            (if description |> String.IsNullOrEmpty then String.Empty else description |> sprintf "Description: `%s`." )
            (splittingSubset |> List.map formatUser |> joinStr ", ")

    let ChatNotFoundErrorRu = "В этом чате нет группы Funds Splitter."
    let MessageDoesntContainAmountRu = "Я не могу найти сумму в вашем сообщении."
    let NotAllMentionedUsersWasAddedToGroupRu = "Некоторые из упомянутых пользователей не были добавлены в группу Funds Splitter."
    let AmountMustBePositiveRu = "Значение суммы должно быть положительным."
    let YouAreNotInTheGroupRu = "Вы не добавленны в группу Funds Splitter. Чтобы присоедениться к группе отправьте мне комманду /join."
    let PayCommandAnswerRu amount description splittingSubset = 
        sprintf 
            "Сумма: %M грн.\n%s\nРазделена между: %s." 
            amount
            (if description |> String.IsNullOrEmpty then String.Empty else description |> sprintf "Описание: `%s`." )
            (splittingSubset |> List.map formatUser |> joinStr ", ")

    let ChatNotFoundError = function
        | En -> ChatNotFoundErrorEn
        | Ru -> ChatNotFoundErrorRu
    let MessageDoesntContainAmount = function
        | En -> MessageDoesntContainAmountEn
        | Ru -> MessageDoesntContainAmountRu
    let NotAllMentionedUsersWasAddedToGroup = function
        | En -> NotAllMentionedUsersWasAddedToGroupEn
        | Ru -> NotAllMentionedUsersWasAddedToGroupRu
    let AmountMustBePositive = function
        | En -> AmountMustBePositiveEn
        | Ru -> AmountMustBePositiveRu
    let YouAreNotInTheGroup = function
        | En -> YouAreNotInTheGroupEn
        | Ru -> YouAreNotInTheGroupRu

    let PayCommandAnswer = function
        | En -> PayCommandAnswerEn
        | Ru -> PayCommandAnswerRu
    

    type TxRaw = 
        {
            Chat: FundsSplitter.Core.Transactions.Types.Chat
            Description: string
            Mentions: string[]
            Amount: decimal
        }

    let parseText lang (msg: Telegram.Bot.Types.Message) chat = 
        let text = msg.Text |> replaceSpaces
        let words = text.Split(' ')
        let amount = words |> Array.tryFind (fun w -> Decimal.TryParse(w) |> fst)
        match amount with
        | None -> lang |> MessageDoesntContainAmount |> Error
        | Some a -> 
            let amount' = a.Replace(",", ".") |> Decimal.Parse 
            if amount' <= 0M then lang |> AmountMustBePositive |> Error
            else

            let mentions = msg |> extractMentions
            let description = 
                mentions 
                |> Array.fold 
                    (fun acc i -> acc.Replace(i, String.Empty): string)
                    (text
                    .Replace(a, String.Empty)
                    .Replace(extractCmd msg |> Option.defaultValue String.Empty, String.Empty))
                |> replaceSpaces
                |> trim
            {
                Chat = chat
                Description = description
                Amount = amount'
                Mentions = mentions
            } |> Ok

    let composeAnswer lang (tx': Tx, tx: TxRaw) = 
        PayCommandAnswer lang tx'.Amount tx.Description tx'.SplittingSubset
        |> Ok

    let createTx lang (msg: Message) tx = 
        let author' = tx.Chat.KnownUsers |> List.tryFind (fun u -> u.Id = msg.From.Id)
        if author' |> Option.isNone 
        then lang |> YouAreNotInTheGroup |> Error
        else
        let author = author'.Value
        let splittingSubset = 
            match tx.Mentions with
            | [||] -> tx.Chat.KnownUsers
            | _ ->  tx.Mentions 
                    |> List.ofArray 
                    |> List.map (fun username -> 
                        tx.Chat.KnownUsers 
                        |> List.tryFind (fun u -> sprintf "@%s" u.Username = username))
                    |> List.filter (fun u -> u.IsSome)
                    |> List.map (fun u -> u.Value)
        
        if tx.Mentions.Length > 0 && tx.Mentions.Length <> splittingSubset.Length
        then lang |> NotAllMentionedUsersWasAddedToGroup |> Error
        else
        
        let tx' = 
            {
                Id = Guid.NewGuid()

                User = author
                Message = {
                    Id = msg.MessageId
                    Text = msg.Text
                }
                Type = Payment
                Amount = tx.Amount
                SplittingSubset = splittingSubset
            }
        (tx, tx') |> Ok

    let handlerFunction botContext (update: Update) = 
        fun () -> async {
            let msg = update.Message
            let client = botContext.BotClient
            let db = botContext.Storage.Database
            let chats = db.GetCollection(Collections.Chats)
            let cts = botContext.CancellationToken
            let lang = getLanguageCode update

            let tryFetchChat chatId = async {
                let! chat = tryFindChat chats cts chatId
                match chat with
                | Some c -> return Ok c
                | None -> return lang |> ChatNotFoundError |> Error
            }

            let addTx (tx, tx') = 
                tx'
                |> addTransaction tx.Chat
                |> upsertChat cts chats |> ignore
                (tx', tx)
                |> Ok

            return!
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.bind (parseText lang msg)
                |> AsyncResult.bind (createTx lang msg)
                |> AsyncResult.bind addTx
                |> AsyncResult.bind (composeAnswer lang)
                |> Async.bind (sendAnswer client msg cts)
        } |> Some

    let updateHandlerFunction botContext (update: Update) = 
        fun () -> async {
            let msg = update.EditedMessage
            let client = botContext.BotClient
            let db = botContext.Storage.Database
            let chats = db.GetCollection(Collections.Chats)
            let cts = botContext.CancellationToken
            let lang = getLanguageCode update

            let tryFetchChat chatId = async {
                let! chat = tryFindChat chats cts chatId
                match chat with
                | Some c -> return Ok c
                | None -> return lang |> ChatNotFoundError |> Error
            }

            let replaceTx (tx, tx': Tx) = 
                let oldTx = tx.Chat.Transactions |> List.find (fun t -> t.Message.Id = msg.MessageId)
                let newTx = { tx' with Id = oldTx.Id }

                tx'
                |> editTransaction tx.Chat
                |> upsertChat cts chats |> ignore
                (tx', tx)
                |> Ok

            return!
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.bind (parseText lang msg)
                |> AsyncResult.bind (createTx lang msg)
                |> AsyncResult.bind replaceTx
                |> AsyncResult.bind (composeAnswer lang)
                |> Async.bind (sendAnswer client msg cts)
        } |> Some
