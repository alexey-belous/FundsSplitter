namespace FundsSplitter.Core.Bot

module UpdatesHandler = 
    open System
    open System.Threading

    open FundsSplitter.Core.Bot.Types
    open FundsSplitter.Core.Bot.Message
    open FundsSplitter.Core.Bot.Handlers
    open FundsSplitter.Core.Bot.Analytics

    open Microsoft.FSharpLu.Json
    open Telegram.Bot
    open Telegram.Bot.Types
    open Telegram.Bot.Types.Enums

    let choose (handlers: UpdateHandler seq) context message = 
        handlers
        |> Seq.tryFind (fun h -> h context message |> Option.isSome)
        |> Option.bind (fun h -> h context message)

    let commandHandler cmdName (handler: UpdateHandler) context (update: Update) = 
        let msg = update.Message
        if msg <> null && msg.Entities <> null && msg.ForwardFrom = null then 
            let cmd = msg |> extractCmd
            match cmd with
            | Some c when c = cmdName -> 
                handler context update
            | _ -> None
        else
            None

    let editedCommandHandler cmdName (handler: UpdateHandler) context (update: Update) = 
        let msg = update.EditedMessage
        if msg <> null && msg.Entities <> null then 
            let cmd = msg |> extractCmd
            match cmd with
            | Some c when c = cmdName -> 
                handler context update
            | _ -> None
        else
            None

    let botAddedToGroupHandler (handler: UpdateHandler) (context: BotContext) (update: Update) = 
        if update.Message <> null 
            && update.Message.NewChatMembers <> null 
            && update.Message.NewChatMembers |> Array.exists (fun m -> m.Id = int(context.BotId)) then
            handler context update
        else None

    let replyCallbackHandler cmdName (handler: UpdateHandler) (context: BotContext) (update: Update) = 
        if update.CallbackQuery <> null then
            let rawPayload = update.CallbackQuery.Data
            let parsed = rawPayload.Split('#')
            if parsed.Length > 0 && parsed.[0] = cmdName then
                handler context update
            else None
        else None

    let routes = 
        choose [
            commandHandler "/help" HelpHandler.handlerFunction
            botAddedToGroupHandler HelpHandler.handlerFunction
            commandHandler "/start" HelpHandler.handlerFunction

            commandHandler "/join" JoinHandler.handlerFunction
            commandHandler "/debts" DebtsHandler.handlerFunction

            commandHandler "/pay" PaymentHandler.handlerFunction
            replyCallbackHandler "toggle_splitting_subset" PaymentHandler.replyToggleUser
            replyCallbackHandler "delete_tx" PaymentHandler.replyDeleteTx

            commandHandler "/payback" SettleUpHandler.handlerFunction 
            editedCommandHandler "/payback" SettleUpHandler.updateHandlerFunction
        ]


    let handleUpdates botConfig storage body = async {
            let cts = new CancellationTokenSource()
            let client = new TelegramBotClient(botConfig.Token, new System.Net.Http.HttpClient())
            let update = Newtonsoft.Json.JsonConvert.DeserializeObject<Update>(body)
            printfn "Request body: %A" (update |> Compact.serialize)

            let context = 
                {
                    BotId = (botConfig.Token.Split(':')).[0]
                    CancellationToken = cts.Token
                    BotClient = client
                    Storage = storage
                }

            match routes context update with
            | Some h -> 
                let! res = h ()
                do! updateRequestAnalytics (Some res) context update
                return String.Empty
            | None -> 
                do! updateRequestAnalytics None context update
                return String.Empty
        }
