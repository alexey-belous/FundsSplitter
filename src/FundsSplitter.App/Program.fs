namespace FundsSplitter.App

module Entry = 
    open System
    open System.Threading

    open Suave
    open Suave.Filters
    open Suave.Operators

    open FSharp.Data
    open FundsSplitter.Core
    open FundsSplitter.Core.Bot.Types

    type Config = JsonProvider<"./config.json">

    let updatesHandler botConfig storage = 
        fun (x: HttpContext) -> async {
            let! res = 
                x.request.rawForm
                |> System.Text.Encoding.UTF8.GetString
                |> Bot.UpdatesHandler.handleUpdates botConfig storage
            return! Successful.OK res x
        }

    let routes botConfig storage = 
        choose [
            POST >=> choose [path "/api/new-update" >=> updatesHandler botConfig storage ]
            Suave.RequestErrors.NOT_FOUND "Resource you're looking for is not exists"
        ]

    [<EntryPoint>]
    let main argv =
        let config = Config.Load("./config.json")
        let botConfig = {
            Token = config.TelegramBot.Token
            ApiURL = config.TelegramBot.BotUrl
        }
        
        let startBotRes = 
            Bot.Lifecycle.startBot botConfig
            |> Async.RunSynchronously

        match startBotRes with
        | Error e -> failwith "Error during bot setup. Shutting down..."
        | _ ->

        let storage = Storage.initializeStorage config.Storage.ConnectionString
        let cts = new CancellationTokenSource()
        let listening, server = 
            startWebServerAsync
                { defaultConfig with 
                    cancellationToken = cts.Token
                    bindings = [HttpBinding.createSimple HTTP (config.Binding.Ip) (config.Binding.Port) ] } 
                (routes botConfig storage)

        Async.Start(server, cts.Token)

        let autoEvent = new AutoResetEvent(false);
        autoEvent.WaitOne() |> ignore

        Bot.Lifecycle.stopBot botConfig
        |> Async.RunSynchronously
        |> ignore

        cts.Cancel()

        0
