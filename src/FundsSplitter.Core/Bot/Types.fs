namespace FundsSplitter.Core.Bot

module Types = 
    open System.Threading
    
    open FundsSplitter.Core.Storage
    open Telegram.Bot
    open Telegram.Bot.Types

    type BotConfig = 
        {
            Token: string
            ApiURL: string
        }

    type BotContext = 
        {
            BotId: string
            BotClient: TelegramBotClient
            Storage: Storage
            CancellationToken: CancellationToken
        }

    type UpdateHandler = BotContext -> Update -> (unit -> Async<Result<unit, unit>>) option