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