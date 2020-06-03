namespace FundsSplitter.Core.Bot

module UpdatesHandler = 
    open System
    open System.Threading

    open FundsSplitter.Core.Bot.Types
    open FundsSplitter.Core.Bot.Handlers

    open Microsoft.FSharpLu.Json
    open Telegram.Bot
    open Telegram.Bot.Types
    open Telegram.Bot.Types.Enums

    let extractCmd (message: Message) = 
        match message.Entities |> Array.tryFind (fun e -> e.Type = MessageEntityType.BotCommand) with
        | Some cmd -> 
            message.Text.Substring(cmd.Offset, cmd.Length) |> Some
        | None -> None

    let choose (handlers: UpdateHandler seq) context message = 
        handlers
        |> Seq.tryFind (fun h -> h context message |> Option.isSome)
        |> Option.bind (fun h -> h context message)

    let commandHandler cmdName (handler: UpdateHandler) context (update: Update) = 
        let msg = update.Message
        if msg <> null && msg.Entities <> null then 
            let cmd = update.Message |> extractCmd
            match cmd with
            | Some c when c = cmdName -> 
                handler context update
            | _ -> None
        else
            None

    let routes = 
        choose [
            commandHandler "/help" HelpHandler.handlerFunction
            commandHandler "/join" JoinHandler.handlerFunction
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
                do! h ()
                return String.Empty
            | None -> return String.Empty
        }
