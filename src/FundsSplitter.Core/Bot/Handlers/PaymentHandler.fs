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
    let AmountMustBePositiveEn = "Amount value must be possitive."
    let YouAreNotInTheGroupEn = "You aren't in the Funds Splitter group. To joint the group send me a /join command."

    let PayCommandAnswerEn amount description = 
        sprintf 
            "Amount: %M UAH.\n%s\nChoose members to split between:" 
            amount
            (if description |> String.IsNullOrEmpty then String.Empty else description |> sprintf "Description: `%s`." )

    let ChatNotFoundErrorRu = "В этом чате нет группы Funds Splitter."
    let MessageDoesntContainAmountRu = "Я не могу найти сумму в вашем сообщении."
    let AmountMustBePositiveRu = "Значение суммы должно быть положительным."
    let YouAreNotInTheGroupRu = "Вы не добавленны в группу Funds Splitter. Чтобы присоедениться к группе отправьте мне комманду /join."
    let PayCommandAnswerRu amount description = 
        sprintf 
            "Сумма: %M грн.\n%s\nМежду кем разделить?" 
            amount
            (if description |> String.IsNullOrEmpty then String.Empty else description |> sprintf "Описание: `%s`." )

    let EverybodyButtonEn = "Everybody"
    let EverybodyButtonRu = "На всех"

    let DeleteButtonEn = "🗑 Удалить"
    let DeleteButtonRu = "🗑 Удалить"

    let DeletedMessageEn = "Deleted..."
    let DeletedMessageRu = "Удалено..."

    let SendTxEn = "Send payment"
    let SendTxRu = "Отправить расход"

    let ChatNotFoundError = function
        | En -> ChatNotFoundErrorEn
        | Ru -> ChatNotFoundErrorRu
    let MessageDoesntContainAmount = function
        | En -> MessageDoesntContainAmountEn
        | Ru -> MessageDoesntContainAmountRu
    let AmountMustBePositive = function
        | En -> AmountMustBePositiveEn
        | Ru -> AmountMustBePositiveRu
    let YouAreNotInTheGroup = function
        | En -> YouAreNotInTheGroupEn
        | Ru -> YouAreNotInTheGroupRu

    let PayCommandAnswer = function
        | En -> PayCommandAnswerEn
        | Ru -> PayCommandAnswerRu

    let EverybodyButton = function
        | En -> EverybodyButtonEn
        | Ru -> EverybodyButtonRu

    let DeleteButton = function
        | En -> DeleteButtonEn
        | Ru -> DeleteButtonRu

    let DeletedMessage = function
        | En -> DeletedMessageEn
        | Ru -> DeletedMessageRu

    let SendTx = function
        | En -> SendTxEn
        | Ru -> SendTxRu    

    type TxRaw = 
        {
            Chat: FundsSplitter.Core.Transactions.Types.Chat
            Description: string
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

            {
                Chat = chat
                Description = String.Empty // TODO: Add handling of description here
                Amount = amount'
            } |> Ok

    let createTx lang (msg: Message) tx = 
        let author' = tx.Chat.KnownUsers |> List.tryFind (fun u -> u.Id = msg.From.Id)
        if author' |> Option.isNone 
        then lang |> YouAreNotInTheGroup |> Error
        else
        let author = author'.Value
        
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
                SplittingSubset = []
            }
        (tx, tx') |> Ok

    let divideByRows rowSize list = 
        list
        |> List.fold (fun (acc: 'a list list) e -> 
                        if acc.[0].Length = rowSize then
                            [e] :: acc
                        else
                            (e :: acc.[0]) :: (acc |> List.skip 1)
                         ) [[]]

    let generateReplyMarkup lang users = 
        let userButtons = 
            users
            |> List.map (fun (u, isChecked) ->
                let icon = if isChecked then "✅" else "❌"
                let btn = ReplyMarkups.InlineKeyboardButton()
                btn.Text <- sprintf "%s %s" icon (formatUser u) // ❌ ✅
                btn.CallbackData <- sprintf "toggle_splitting_subset#%s" (u.Id.ToString())
                btn)

        let everybodyButton = ReplyMarkups.InlineKeyboardButton()
        everybodyButton.Text <- EverybodyButton lang
        everybodyButton.CallbackData <- "toggle_splitting_subset#everybody" 

        let deleteBtn = ReplyMarkups.InlineKeyboardButton()
        deleteBtn.Text <- DeleteButton lang
        deleteBtn.CallbackData <- "delete_tx"

        let rows = 
            (divideByRows 2 userButtons) @ [[everybodyButton]] @ [[deleteBtn]]
            |> Seq.map (Seq.ofList)

        ReplyMarkups.InlineKeyboardMarkup(rows)

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

            let sendReplyMessage lang (res: Result<(Tx*TxRaw), string> ) = async {
                match res with
                | Error e -> return! sendAnswer client msg cts (Error e)
                | Ok (tx': Tx, tx: TxRaw) ->
                let txt = PayCommandAnswer lang tx'.Amount tx.Description

                let replyMarkup = 
                    tx.Chat.KnownUsers
                    |> List.map (fun u -> (u, false))
                    |> generateReplyMarkup lang

                
                let! _ =  client.SendTextMessageAsync(new ChatId(msg.Chat.Id), txt, Enums.ParseMode.Default, true, false, msg.MessageId, replyMarkup, cts)
                            |> Async.AwaitTask
                return Ok ()
            }

            return!
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.bind (parseText lang msg)
                |> AsyncResult.bind (createTx lang msg)
                |> AsyncResult.bind addTx
                |> Async.bind (sendReplyMessage lang)
        } |> Some

    let replyToggleUser botContext (update: Update) = 
        fun () -> async {
            let msg = update.CallbackQuery.Message
            let client = botContext.BotClient
            let db = botContext.Storage.Database
            let chats = db.GetCollection(Collections.Chats)
            let cts = botContext.CancellationToken
            let lang = getLanguageCode update

            let tryFetchChat chatId = async {
                let! chat = tryFindChat chats cts chatId
                return chat.Value
            }

            let toggleUser (chat: Transactions.Types.Chat) = 
                let tx = chat.Transactions |> List.find (fun tx -> tx.Message.Id = update.CallbackQuery.Message.ReplyToMessage.MessageId)

                let parsedPayload = update.CallbackQuery.Data.Split('#').[1]
                if parsedPayload = "everybody" 
                then //Everybody case
                    if tx.SplittingSubset.Length = chat.KnownUsers.Length then
                        ({ tx with SplittingSubset = [] }, chat)
                    else
                    ({ tx with SplittingSubset = chat.KnownUsers }, chat)

                else // Particular member case
                let userId = Int32.Parse(update.CallbackQuery.Data.Split('#').[1])
                let user = chat.KnownUsers |> List.find (fun u -> u.Id = userId)
                if tx.SplittingSubset |> List.exists (fun u -> u.Id = userId) then
                    ({ tx with SplittingSubset = tx.SplittingSubset |> List.filter (fun u -> u.Id <> user.Id)}, chat)
                else
                    ({ tx with SplittingSubset = user :: tx.SplittingSubset }, chat)

            let saveTx (tx, chat) =  
                tx
                |> editTransaction chat
                |> upsertChat cts chats |> ignore
                (tx, chat)

            let sendReplyAnswer (tx, chat) = async {
                let replyMarkup = 
                    chat.KnownUsers 
                    |> List.map (fun u -> (u, tx.SplittingSubset |> List.exists (fun u' -> u'.Id = u.Id)))
                    |> generateReplyMarkup lang

                let! _ = client.EditMessageReplyMarkupAsync(ChatId msg.Chat.Id, msg.MessageId, replyMarkup, cts)
                            |> Async.AwaitTask

                return ()
            }

            let! res = 
                msg.Chat.Id
                |> tryFetchChat
                |> Async.map toggleUser
                |> Async.map saveTx
                |> Async.bind sendReplyAnswer

            return Ok res
        } |> Some

    let replyDeleteTx botContext (update: Update) = 
        fun () -> async {
            let msg = update.CallbackQuery.Message
            let client = botContext.BotClient
            let db = botContext.Storage.Database
            let chats = db.GetCollection(Collections.Chats)
            let cts = botContext.CancellationToken
            let lang = getLanguageCode update

            let tryFetchChat chatId = async {
                let! chat = tryFindChat chats cts chatId
                return chat.Value
            }

            let deleteTx chat =  
                let tx = chat.Transactions |> List.find (fun tx -> tx.Message.Id = msg.ReplyToMessage.MessageId)
                tx
                |> deleteTransaction chat
                |> upsertChat cts chats |> ignore
                ()

            let sendReplyAnswer () = async {
                let replyMarkup = ReplyMarkups.InlineKeyboardMarkup(Seq.empty : System.Collections.Generic.IEnumerable<ReplyMarkups.InlineKeyboardButton>)

                // let! _ = client.EditMessageReplyMarkupAsync(ChatId msg.Chat.Id, msg.MessageId, replyMarkup, cts)

                let! _ = client.EditMessageTextAsync(ChatId(msg.Chat.Id), msg.MessageId, (DeletedMessage lang), Telegram.Bot.Types.Enums.ParseMode.Default, true, replyMarkup, cts)
                            |> Async.AwaitTask

                return ()
            }

            let! res = 
                msg.Chat.Id
                |> tryFetchChat
                |> Async.map deleteTx
                |> Async.bind sendReplyAnswer

            return Ok res
        } |> Some


    let replyInlineQuery botContext (update: Update) = 
        fun () -> async {
            let client = botContext.BotClient
            let lang = getLanguageCode update

            let commandText = sprintf "/pay %s" update.InlineQuery.Query
            let results = [InlineQueryResults.InlineQueryResultArticle("1", SendTx lang,  InlineQueryResults.InputTextMessageContent(commandText)) :> InlineQueryResults.InlineQueryResultBase]
                            |> Seq.ofList
            let! _ = client.AnswerInlineQueryAsync(update.InlineQuery.Id, results)
                        |> Async.AwaitTask
        

            return Ok ()
        } |> Some
