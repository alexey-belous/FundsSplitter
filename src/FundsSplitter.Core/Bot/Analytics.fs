
namespace FundsSplitter.Core.Bot

module Analytics = 
    open Types
    open FundsSplitter.Core.Storage
    open FundsSplitter.Core.Analytics.Types
    open FundsSplitter.Core.Analytics.Storage

    open Telegram.Bot.Types
    open Telegram.Bot.Types.Enums
    
    let isError res = 
        match res with
        | Ok _ -> false
        | Error _ -> true

    // That's may not work correctly with parallel messages requests
    let updateRequestAnalytics (handlerResult: Result<unit, unit> option) (context: BotContext) (update: Update) = async {
        let cts = (new System.Threading.CancellationTokenSource()).Token
        let msg = if update.Message |> isNull |> not then update.Message else update.EditedMessage
        if msg |> isNull |> not
        then
            let today = (System.DateTime.Today.ToUniversalTime().Date)
            let isDirectMessage = msg.Chat.Type = ChatType.Private
            let isMessageProcessed = handlerResult.IsSome && handlerResult.Value |> isError |> not
            let messagesAnalytics = context.Storage.Database.GetCollection(Collections.MessagesAnalytics)
            let! record = tryFindRecord messagesAnalytics cts today
            match record with
            | Some r -> 
                { r with 
                    DirectMessagesCount = r.DirectMessagesCount + (if isDirectMessage then 1 else 0)
                    ChatMessagesCount = r.ChatMessagesCount + (if not isDirectMessage then 1 else 0)
                    ProcessedMessagesCount = r.ProcessedMessagesCount + (if isMessageProcessed then 1 else 0)
                    UnsupportedMessagesCount = r.UnsupportedMessagesCount + (if not isMessageProcessed then 1 else 0) }
                |> upsertDailyMessagesRecord messagesAnalytics cts |> ignore
            | None -> 
                {   Day = today
                    DirectMessagesCount = (if isDirectMessage then 1 else 0)
                    ChatMessagesCount = (if not isDirectMessage then 1 else 0)
                    ProcessedMessagesCount = (if isMessageProcessed then 1 else 0)
                    UnsupportedMessagesCount = (if not isMessageProcessed then 1 else 0) }
                |> upsertDailyMessagesRecord messagesAnalytics cts |> ignore
            return ()
        else
            return ()
    }