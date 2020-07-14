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

    let ChatNotFoundError = "There's no Funds Splitter group in this chat."
    let MessageDoesntContainAmount = "I couldn't find amount in your message."
    let MessageDoesntContainMention = "Message doesn't contain a sender of the payment."
    let TooManyMentions = "Message contains too many mentioned users (should be only one sender)."
    let YouAreNotInTheGroup = "You aren't in the group."
    let SenderIsNotInTheGroup = "Sender is not in the group."
    let AmountIsBiggerThatDebt sender amount = sprintf "Amount is bigger than debt (%M UAH) of %s" amount sender

    let SettlingUpAnswer from receiver amount = 
        sprintf "%s give back %M to %s" from amount receiver

    let handlerFunction botContext (update: Update) = 
        fun () -> async {
            let msg = update.Message
            let client = botContext.BotClient
            let db = botContext.Storage.Database
            let chats = db.GetCollection(Collections.Chats)
            let cts = botContext.CancellationToken

            let tryFetchChat chatId = async {
                let! chat = tryFindChat chats cts chatId
                match chat with
                | Some c -> return Ok c
                | None -> return Error ChatNotFoundError
            }

            let parseText chat = 
                let text = msg.Text |> replaceSpaces
                let words = text.Split(' ')
                let amount = words |> Array.tryFind (fun w -> Decimal.TryParse(w) |> fst)
                match amount with
                | None -> Error MessageDoesntContainAmount
                | Some a -> 
                    let amount' = a.Replace(",", ".") |> Decimal.Parse 

                    let mentions = msg |> extractMentions
                    match mentions with
                    | [||] -> Error MessageDoesntContainMention
                    | [|sender|] -> (chat, sender, amount') |> Ok
                    | _ -> Error TooManyMentions

            let createTx (chat, sender, amount) = 
                let author' = chat.KnownUsers |> List.tryFind (fun u -> u.Id = msg.From.Id)
                if author' |> Option.isNone 
                then Error YouAreNotInTheGroup
                else
                let author = author'.Value
                
                let sender'' = chat.KnownUsers |> List.tryFind (fun u -> sprintf "@%s" u.Username = sender)
                if sender'' |> Option.isNone then Error SenderIsNotInTheGroup
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

            let validateTx (chat, (tx: Tx)) = 
                let debts = chat |> createInitialDebtsMatrix |> getDebts |> (addSettlingUpsToDebts chat)
                match debts
                    |> List.tryFind (fun d -> 
                        d.From.Id = tx.User.Id && d.To.Id = tx.SplittingSubset.[0].Id) with
                | Some d when d.Amount >= tx.Amount -> (chat, (tx: Tx)) |> Ok
                | Some d -> AmountIsBiggerThatDebt (formatUser tx.User) d.Amount |> Error
                | None -> AmountIsBiggerThatDebt (formatUser tx.User) 0M |> Error

            let saveTx (chat, (tx: Tx)) = 
                tx
                |> addTransaction chat
                |> upsertChat cts chats |> ignore
                tx |> Ok

            let composeAnswer (tx: Tx) = 
                SettlingUpAnswer (formatUser tx.User) (formatUser tx.SplittingSubset.[0]) tx.Amount
                |> Ok

            let! res = 
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.bind parseText
                |> AsyncResult.bind createTx
                |> AsyncResult.bind validateTx
                |> AsyncResult.bind saveTx
                |> AsyncResult.bind composeAnswer
                |> Async.bind (sendAnswer client msg cts)

            return ()
        } |> Some