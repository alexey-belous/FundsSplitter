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

    let ChatNotFoundErrorEn = "There's no Funds Splitter group in this chat."
    let NoDebtsMessageEn = "All debts are settled up!"
    let DebtsAnswerRow userFrom userTo amount = 
        sprintf "%s --> %s: %s" userFrom userTo (amount.ToString())

    let ChatNotFoundErrorRu = "В этом чате нет группы Funds Splitter."
    let NoDebtsMessageRu = "Все долги погашены!"

    let ChatNotFoundError = function
        | En -> ChatNotFoundErrorEn
        | Ru -> ChatNotFoundErrorRu

    let NoDebtsMessage = function
        | En -> NoDebtsMessageEn
        | Ru -> NoDebtsMessageRu

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

            let formatDebts (debts: FundsSplitter.Core.Transactions.Types.Debt list) = 
                if debts.Length = 0 || debts |> List.exists (fun d -> d.Amount > 0M) |> not then
                    NoDebtsMessage lang
                else
                debts
                |> List.map (fun d -> DebtsAnswerRow (formatUser d.From) (formatUser d.To) (System.Math.Round(d.Amount, 2)))
                |> String.concat "\n"

            return!
                msg.Chat.Id
                |> tryFetchChat
                |> AsyncResult.map (fun chat -> chat |> createInitialDebtsMatrix |> getDebts |> (addSettlingUpsToDebts chat))
                |> AsyncResult.map formatDebts
                |> Async.bind (sendAnswer client msg cts)
        } |> Some