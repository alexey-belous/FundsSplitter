namespace FundsSplitter.Core.Bot.Handlers

module SettleUpHandler = 
    open System

    open FundsSplitter.Core
    open FundsSplitter.Core.Strings
    open FundsSplitter.Core.Storage
    open FundsSplitter.Core.Bot.Types
    open FundsSplitter.Core.Bot.Message
    open FundsSplitter.Core.Transactions.Types
    open FundsSplitter.Core.Transactions.CrudOperations
    open FundsSplitter.Core.Transactions.ProcessingLogic

    open Telegram.Bot
    open Telegram.Bot.Types

    let ChatNotFoundErrorEn = "There's no Funds Splitter group in this chat."
    let MessageDoesntContainAmountEn = "I couldn't find amount in your message."
    let MessageDoesntContainMentionEn = "Message doesn't contain a sender of the payment."
    let TooManyMentionsEn = "Message contains too many mentioned users (should be only one sender)."
    let YouAreNotInTheGroupEn = "You aren't in the group."
    let SenderIsNotInTheGroupEn = "Sender is not in the group."
    let AmountIsBiggerThatDebtEn sender amount = sprintf "Amount is bigger than debt (%M UAH) of %s" amount sender

    let SettlingUpAnswerEn from receiver amount = 
        sprintf "%s give back %M UAH -> %s" from amount receiver

    let ChatNotFoundErrorRu = "В этом чате нет группы Funds Splitter."
    let MessageDoesntContainAmountRu = "Я не могу найти сумму в вашем сообщении."
    let MessageDoesntContainMentionRu = "Сообщение не содержит отправителя платежа."
    let TooManyMentionsRu = "Сообщение содержит слишком много упомянутых пользователей (должен быть только один отправитель)."
    let YouAreNotInTheGroupRu = "Вы не добавленны в группу Funds Splitter. Чтобы присоедениться к группе отправьте мне комманду /join."
    let SenderIsNotInTheGroupRu = "Отправитель не добавлен в группу Funds Splitter."
    let AmountIsBiggerThatDebtRu sender amount = sprintf "Сумма возврата долга больше чем долг (%M грн) пользователя %s" amount sender

    let SettlingUpAnswerRu from receiver amount = 
        sprintf "%s вернул(а) %M грн -> %s" from amount receiver

    let ChatNotFoundError = function
        | En -> ChatNotFoundErrorEn
        | Ru -> ChatNotFoundErrorRu
    let MessageDoesntContainAmount = function
        | En -> MessageDoesntContainAmountEn
        | Ru -> MessageDoesntContainAmountRu
    let MessageDoesntContainMention = function
        | En -> MessageDoesntContainMentionEn
        | Ru -> MessageDoesntContainMentionRu
    let TooManyMentions = function
        | En -> TooManyMentionsEn
        | Ru -> TooManyMentionsRu
    let YouAreNotInTheGroup = function
        | En -> YouAreNotInTheGroupEn
        | Ru -> YouAreNotInTheGroupRu
    let SenderIsNotInTheGroup = function
        | En -> SenderIsNotInTheGroupEn
        | Ru -> SenderIsNotInTheGroupRu
    let AmountIsBiggerThatDebt = function
        | En -> AmountIsBiggerThatDebtEn
        | Ru -> AmountIsBiggerThatDebtRu

    let SettlingUpAnswer = function
        | En -> SettlingUpAnswerEn
        | Ru -> SettlingUpAnswerRu

    let parseText lang (msg: Message) chat = 
        let text = msg.Text |> replaceSpaces
        let words = text.Split(' ')
        let amount = words |> Array.tryFind (fun w -> Decimal.TryParse(w) |> fst)
        match amount with
        | None -> lang |> MessageDoesntContainAmount |> Error
        | Some a -> 
            let amount' = a.Replace(",", ".") |> Decimal.Parse 

            let mentions = msg |> extractMentions
            match mentions with
            | [||] -> lang |> MessageDoesntContainMention |> Error
            | [|sender|] -> (chat, sender, amount') |> Ok
            | _ -> lang |> TooManyMentions |> Error

    let createTx lang (msg: Message) (chat, sender, amount) = 
        let author' = chat.KnownUsers |> List.tryFind (fun u -> u.Id = msg.From.Id)
        if author' |> Option.isNone 
        then lang |> YouAreNotInTheGroup |> Error
        else
        let author = author'.Value
        
        let sender'' = chat.KnownUsers |> List.tryFind (fun u -> sprintf "@%s" u.Username = sender)
        if sender'' |> Option.isNone then lang |> SenderIsNotInTheGroup |> Error
        else
        let sender' = sender''.Value
        
        let tx' = 
            {
                Id = Guid.NewGuid()

                User = sender'
                Message = {
                    Id = msg.MessageId
                    Text = msg.Text
                }
                Type = SettlingUp
                Amount = amount
                SplittingSubset = [author]
            }
        (chat, tx')
        |> Ok

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

            let validateTx (chat, (tx: Tx)) = 
                let debts = chat |> createInitialDebtsMatrix |> getDebts |> (addSettlingUpsToDebts chat)
                match debts
                    |> List.tryFind (fun d -> 
                        d.From.Id = tx.User.Id && d.To.Id = tx.SplittingSubset.[0].Id) with
                | Some d when d.Amount >= tx.Amount -> (chat, (tx: Tx)) |> Ok
                | Some d -> AmountIsBiggerThatDebt lang (formatUser tx.User) d.Amount |> Error
                | None -> AmountIsBiggerThatDebt lang (formatUser tx.User) 0M |> Error

            let saveTx (chat, (tx: Tx)) = 
                tx
                |> addTransaction chat
                |> upsertChat cts chats |> ignore
                tx |> Ok

            let composeAnswer (tx: Tx) = 
                SettlingUpAnswer lang (formatUser tx.User) (formatUser tx.SplittingSubset.[0]) tx.Amount
                |> Ok

            return! 
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.bind (parseText lang msg)
                |> AsyncResult.bind (createTx lang msg)
                |> AsyncResult.bind validateTx
                |> AsyncResult.bind saveTx
                |> AsyncResult.bind composeAnswer
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

            let validateTx (chat, (tx: Tx)) = 
                let oldTx = chat.Transactions |> List.find (fun t -> t.Message.Id = msg.MessageId)
                let debts = chat |> createInitialDebtsMatrix |> getDebts |> (addSettlingUpsToDebts chat)
                match debts
                    |> List.tryFind (fun d -> 
                        d.From.Id = tx.User.Id && d.To.Id = tx.SplittingSubset.[0].Id) with
                | Some d when (d.Amount + oldTx.Amount) >= tx.Amount -> (chat, tx, oldTx) |> Ok
                | Some d -> AmountIsBiggerThatDebt lang (formatUser tx.User) (d.Amount + oldTx.Amount) |> Error
                | None -> AmountIsBiggerThatDebt lang (formatUser tx.User) 0M |> Error

            let saveTx (chat, tx: Tx, oldTx: Tx) = 
                {tx with Id = oldTx.Id}
                |> editTransaction chat
                |> upsertChat cts chats |> ignore
                tx |> Ok

            let composeAnswer (tx: Tx) = 
                SettlingUpAnswer lang (formatUser tx.User) (formatUser tx.SplittingSubset.[0]) tx.Amount
                |> Ok

            return! 
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.bind (parseText lang msg)
                |> AsyncResult.bind (createTx lang msg)
                |> AsyncResult.bind validateTx
                |> AsyncResult.bind saveTx
                |> AsyncResult.bind composeAnswer
                |> Async.bind (sendAnswer client msg cts)
        } |> Some