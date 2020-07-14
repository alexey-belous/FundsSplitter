namespace FundsSplitter.Core.Bot

module Message = 
    open Telegram.Bot
    open Telegram.Bot.Types
    open Telegram.Bot.Types.Enums

    let exctractEntitiesText entityType (message: Message) = 
        message.Entities 
        |> Array.filter (fun e -> e.Type = entityType)
        |> Array.map (fun e -> message.Text.Substring(e.Offset, e.Length))

    let extractCmd (message: Message) = 
        exctractEntitiesText MessageEntityType.BotCommand message |> Array.tryHead

    let extractMentions (message: Message) = 
        exctractEntitiesText MessageEntityType.Mention message

    let sendAnswerWithParseMode parseMode (client: TelegramBotClient) (msg: Message) cts res = 
        let answer text = 
            client.SendTextMessageAsync(new ChatId(msg.Chat.Id), text, parseMode, true, false, msg.MessageId, null, cts)
            |> Async.AwaitTask

        match res with
        | Ok r -> answer r
        | Error e -> answer e

    let sendAnswer (client: TelegramBotClient) (msg: Message) cts res = 
        sendAnswerWithParseMode (Enums.ParseMode.Default) client msg cts res
    let sendMarkdownAnswer (client: TelegramBotClient) (msg: Message) cts res = 
        sendAnswerWithParseMode (Enums.ParseMode.Markdown) client msg cts res

    let formatUser (user: FundsSplitter.Core.Transactions.Types.User) = 
        sprintf "%s (@%s)" (user.Name) (user.Username)