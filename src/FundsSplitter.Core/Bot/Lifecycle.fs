namespace FundsSplitter.Core.Bot

module Lifecycle = 
    open System
    open Microsoft.FSharpLu.Json
    open FSharp.Data

    open FundsSplitter.Core.Logging
    open Types

    type SetWebhookRequest = 
        {
            url: string
        }

    let setTelegramApiRequest botConfig methodName requestBody = async {
        let url = sprintf "https://api.telegram.org/bot%s/%s" botConfig.Token methodName
        let body = requestBody
                    |> HttpRequestBody.TextRequest
        let! response = Http.AsyncRequest(
                            url, 
                            headers = ["content-type","application/json"], 
                            httpMethod =  "POST", 
                            body = body,
                            silentHttpErrors = true)

        match response.StatusCode with
        | c when c >= 200 && c < 300 -> return Ok()
        | _ -> match response.Body with
                | Text text -> return Error text
                | Binary _ -> return failwith "Couldn't parse response"
    }

    let setUpWebhookAsync botConfig = async {
        let body =  { url = botConfig.ApiURL }
                    |> Compact.serialize 
        return! setTelegramApiRequest botConfig "setWebhook" body
    }

    let startBot botConfig = async {
        printfn "Starting bot..."
        printfn "Setting Webhook up..."
        return! 
            setUpWebhookAsync botConfig
            |> Async.map (logResult "WebHook has been set up successfully" "Couldn't set up a Webhook")
    }

    let deleteWebhook botConfig = async {
        return! setTelegramApiRequest botConfig "deleteWebhook" (String.Empty)
    }

    let stopBot botConfig = async {
        printfn "Stoping bot..."
        printfn "Removing Webhook..."

        return! 
            deleteWebhook botConfig
            |> Async.map (logResult "WebHook has been removed successfully" "Couldn't remove a Webhook")
    }
