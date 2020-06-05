namespace FundsSplitter.Core.Bot.Handlers

module DebtsHandler = 
    open FundsSplitter.Core
    open FundsSplitter.Core.Storage
    open FundsSplitter.Core.Bot.Types
    open FundsSplitter.Core.Bot.Message
    open FundsSplitter.Core.Transactions.Types
    open FundsSplitter.Core.Transactions.CrudOperations
    open FundsSplitter.Core.Transactions.ProcessingLogic

    open Telegram.Bot
    open Telegram.Bot.Types

    let ChatNotFoundError = "There's no Funds Splitter group in this chat."
    let NoDebtsMessage = "All debts are settled up!"
    let DebtsAnswerRow userFrom userTo amount = 
        sprintf "%s --> %s: %s" userFrom userTo (amount.ToString())

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

            let formatDebts (debts: FundsSplitter.Core.Transactions.Types.Debt list) = 
                if debts.Length = 0 then
                    NoDebtsMessage
                else
                debts
                |> List.map (fun d -> DebtsAnswerRow (formatUser d.From) (formatUser d.To) d.Amount)
                |> String.concat "\n"

            let! res = 
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.map (createInitialDebtsMatrix >> getDebts)
                |> AsyncResult.map formatDebts
                |> Async.bind (sendMarkdownAnswer client msg cts)

            return ()
        } |> Some