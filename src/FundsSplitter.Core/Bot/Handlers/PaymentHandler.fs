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

    let ChatNotFoundError = "There's no Funds Splitter group in this chat."
    let MessageDoesntContainAmount = "I couldn't find amount in your message."
    let NotAllMentionedUsersWasAddedToGroup = "Not all mentioned users was added to the splitting group."
    let YouAreNotInTheGroup = "You aren't in the group."

    let PayCommandAnswer amount description splittingSubset = 
        sprintf 
            "Amount: %M UAH.\n%s\nSplit between: %s." 
            amount
            (if description |> String.IsNullOrEmpty then String.Empty else description |> sprintf "Description: `%s`." )
            (splittingSubset |> List.map formatUser |> joinStr ", ")

    type TxRaw = 
        {
            Chat: FundsSplitter.Core.Transactions.Types.Chat
            Description: string
            Mentions: string[]
            Amount: decimal
        }

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

            let addTx tx = 
                let author' = tx.Chat.KnownUsers |> List.tryFind (fun u -> u.Id = msg.From.Id)
                if author' |> Option.isNone 
                then Error YouAreNotInTheGroup
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
                then NotAllMentionedUsersWasAddedToGroup |> Error
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
                tx'
                |> addTransaction tx.Chat
                |> upsertChat cts chats |> ignore
                (tx', tx)
                |> Ok

            let composeAnswer (tx': Tx, tx: TxRaw) = 
                PayCommandAnswer tx'.Amount tx.Description tx'.SplittingSubset
                |> Ok

            let! res = 
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.bind parseText
                |> AsyncResult.bind addTx
                |> AsyncResult.bind composeAnswer
                |> Async.bind (sendAnswer client msg cts)

            return ()
        } |> Some