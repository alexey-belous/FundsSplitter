namespace FundsSplitter.Logic.Bot

module Types = 
    open System.Threading
    
    open FundsSplitter.Logic.Storage

    type BotConfig = 
        {
            Token: string
        }

    type BotContext = 
        {
            BotId: string
            Storage: Storage
            CancellationToken: CancellationToken
        }

    type UpdateHandler = BotContext -> Update -> (unit -> Async<Result<unit, unit>>) option