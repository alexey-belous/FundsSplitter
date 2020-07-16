namespace FundsSplitter.Core.Analytics

module Types = 
    open System

    type DailyMessagesAnalytics = 
        {
            Day: DateTime
            DirectMessagesCount: int
            ChatMessagesCount: int
            ProcessedMessagesCount: int
            UnsupportedMessagesCount: int
        }

    type DailyUsersAnalytics = 
        {
            Day: DateTime
            UserIds: int list
        }
        