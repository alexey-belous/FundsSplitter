namespace FundsSplitter.Core.Bot

module Message = 
    open System 
    open Telegram.Bot
    open Telegram.Bot.Types
    open Telegram.Bot.Types.Enums

    type LanguageCode = | En | Ru
    let languageCodeToStr = function
        | En -> "en"
        | Ru -> "ru"
    let (|LanguageCode|) = function
        | "en" -> En
        | "ru" -> Ru
        | _ -> Ru

    let getLanguageCode (update: Update) = 
        let msg = if update.Message <> null then update.Message else if update.EditedMessage <> null then update.EditedMessage else null
        if msg <> null then
            msg.From.LanguageCode |> (|LanguageCode|)
        else En

    let exctractEntitiesText entityType (message: Message) = 
        message.Entities 
        |> Array.filter (fun e -> e.Type = entityType)
        |> Array.map (fun e -> message.Text.Substring(e.Offset, e.Length))

    let extractCmd (message: Message) = 
        match exctractEntitiesText MessageEntityType.BotCommand message |> Array.tryHead with
        | None -> None
        | Some cmd -> cmd.Replace("@funds_splitter_bot", String.Empty) |> Some

    let extractMentions (message: Message) = 
        exctractEntitiesText MessageEntityType.Mention message

    let sendAnswerWithParseMode parseMode (client: TelegramBotClient) (msg: Message) cts res = 
        let answer text = 
            client.SendTextMessageAsync(new ChatId(msg.Chat.Id), text, parseMode, true, false, msg.MessageId, null, cts)
            |> Async.AwaitTask

        match res with
        | Ok r ->   answer r
                    |> Async.map (fun _ -> Ok())
        | Error e ->    answer e |> Async.map (fun _ -> Error ())

    let sendAnswer (client: TelegramBotClient) (msg: Message) cts res = 
        sendAnswerWithParseMode (Enums.ParseMode.Default) client msg cts res
    let sendMarkdownAnswer (client: TelegramBotClient) (msg: Message) cts res = 
        sendAnswerWithParseMode (Enums.ParseMode.Markdown) client msg cts res

    let formatUser (user: FundsSplitter.Core.Transactions.Types.User) = 
        sprintf "%s (@%s)" (user.Name) (user.Username)