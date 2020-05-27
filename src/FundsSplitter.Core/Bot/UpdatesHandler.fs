namespace FundsSplitter.Core.Bot

module UpdatesHandler = 
    open System
    open System.Threading

    open Types

    open Microsoft.FSharpLu.Json
    open Telegram.Bot
    open Telegram.Bot.Types
    open Telegram.Bot.Types.Enums

    let handleCommand cmdName handler = 
        { CmdName = cmdName; Handler = handler}

    let extractCmd (message: Message) = 
        match message.Entities |> Array.tryFind (fun e -> e.Type = MessageEntityType.BotCommand) with
        | Some cmd -> 
            message.Text.Substring(cmd.Offset, cmd.Length) |> Some
        | None -> None

    let routeCommands botContext (update: Update) routes = async {
        let msg = update.Message
        if msg <> null && msg.Entities <> null then 
            let handler = 
                update.Message 
                |> extractCmd 
                |> Option.bind (fun cmd' -> routes |> List.tryFind (fun r -> r.CmdName = cmd'))
            match handler with
            | Some h -> do! h.Handler botContext msg
            | _ -> return ()
        else
            return ()
    }

    let handleUpdates botConfig storage body = async {
            let cts = new CancellationTokenSource()
            let client = new TelegramBotClient(botConfig.Token, new System.Net.Http.HttpClient())
            let update = Compact.deserialize<Update> body
            printfn "Request body: %A" (update |> Compact.serialize)

            let routes = [
                Handlers.HelpHandler.handler
            ]

            let context = 
                {
                    CancellationToken = cts.Token
                    BotClient = client
                    Storage = storage
                }
            do! routeCommands context update routes

            return String.Empty

        }
        