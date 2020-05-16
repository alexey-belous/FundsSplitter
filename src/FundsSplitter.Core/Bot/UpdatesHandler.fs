namespace FundsSplitter.Core.Bot

module UpdatesHandler = 
    open System
    open System.Threading

    open Types

    open Microsoft.FSharpLu.Json
    open Telegram.Bot
    open Telegram.Bot.Types
    open Telegram.Bot.Types

    let handleUpdates botConfig body = async {
            let cts = new CancellationTokenSource()
            let client = new TelegramBotClient(botConfig.Token, new System.Net.Http.HttpClient())
            let update = Compact.deserialize<Update> body
            printfn "Request body: %A" (update |> Compact.serialize)

            if update.Message <> null 
            then
                let! _ = 
                    client.SendTextMessageAsync(new ChatId(update.Message.Chat.Id), update.Message.Text, Enums.ParseMode.Default, true, false, update.Message.MessageId, null, cts.Token)
                    |> Async.AwaitTask
                return String.Empty
            else
                return String.Empty
        }
        