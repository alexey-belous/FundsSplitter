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
        let froms = [
            (if update.Message <> null then Some update.Message.From else None);
            (if update.EditedMessage <> null then Some update.EditedMessage.From else None);
            (if update.CallbackQuery <> null then Some update.CallbackQuery.Message.From else None);
            (if update.InlineQuery <> null then Some update.InlineQuery.From else None )
        ]
        match froms |> List.tryFind Option.isSome with
        | Some from -> from.Value.LanguageCode |> (|LanguageCode|)
        | None -> En

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
        | Ok r when r <> String.Empty ->   
                        answer r
                        |> Async.map (fun _ -> Ok())
        | Error e ->    answer e |> Async.map (fun _ -> Error ())
        | _ -> async { return Ok () }

    let sendAnswer (client: TelegramBotClient) (msg: Message) cts res = 
        sendAnswerWithParseMode (Enums.ParseMode.Default) client msg cts res
    let sendMarkdownAnswer (client: TelegramBotClient) (msg: Message) cts res = 
        sendAnswerWithParseMode (Enums.ParseMode.Markdown) client msg cts res

    let formatUser (user: FundsSplitter.Core.Transactions.Types.User) = 
        sprintf "%s (@%s)" (user.Name) (user.Username)