namespace FundsSplitter.Core.Bot.Handlers

module HelpHandler = 
    open FundsSplitter.Core.Storage
    open FundsSplitter.Core.Bot.Types

    open Telegram.Bot
    open Telegram.Bot.Types

    [<Literal>]
    let helpMessage = """
       Funds Splitter v1.0
    """

    let handlerFunction botContext (update: Update) = 
        fun () -> async {
            let msg = update.Message
            let client = botContext.BotClient
            let cts = botContext.CancellationToken

            let! _ =  
                client.SendTextMessageAsync(new ChatId(msg.Chat.Id), helpMessage, Enums.ParseMode.Default, true, false, msg.MessageId, null, cts)
                |> Async.AwaitTask
            return ()
        } |> Some