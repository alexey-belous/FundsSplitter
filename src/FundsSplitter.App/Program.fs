namespace FundsSplitter.App

module Entry = 
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.AspNetCore
    open Giraffe
    open FSharp.Data
    open Telegram.Bot
    open Telegram.Bot.Types

    [<Literal>]
    let SETTINGS_FILENAME = "appsettings.json"
    type Config = JsonProvider<SETTINGS_FILENAME>

    let configureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore

    let configureApp (botToken: string) (app : IApplicationBuilder) =
        let handleWebhook (next: HttpFunc) (ctx: HttpContext) =
            task {
                let botClient = new TelegramBotClient(botToken)
                let! update = ctx.BindJsonAsync<Update>()
                if update.Message <> null && update.Message.Text <> null 
                then
                    let msg = update.Message
                    let response = sprintf "%s said: %s" msg.From.FirstName msg.Text
                    do! botClient.SendTextMessageAsync(msg.Chat.Id, response) |> Async.AwaitTask |> Async.Ignore
                return! next ctx
            }
        app.UseGiraffe(
            choose [
                route "/webhook" >=> POST >=> handleWebhook
                setStatusCode 404 >=> text "Not Found"
            ])

    [<EntryPoint>]
    let main argv =
        let config = Config.Load(SETTINGS_FILENAME)
        let botClient = new TelegramBotClient(config.BotSettings.Token)
        let setWebhookTask = botClient.SetWebhookAsync(config.BotSettings.WebHookUrl)
        setWebhookTask.Wait()

        let host = 
            WebHost.CreateDefaultBuilder()
                .UseKestrel()
                .ConfigureServices(configureServices)
                .Configure(configureApp config.BotSettings.Token)
                .Build()

        host.Run()

        0
