namespace FundsSplitter.Core.Bot.Handlers

module HelpHandler = 
    open FundsSplitter.Core.Storage
    open FundsSplitter.Core.Bot.Message
    open FundsSplitter.Core.Bot.Types

    open Telegram.Bot
    open Telegram.Bot.Types

    [<Literal>]
    let helpMessageEn = """
       Funds Splitter v1.0
To start work with this bot, it should be added to the Telegram chat.
Then, each user must send /join command to joint the group.

- `/help` - Shows a description of commands and how bot works.
- `/join` - Joins user to a **splitting group**.

- `/pay 100 description` - Adds payment of particular user. 

- `/payback @user1 100` - Add payback of a user (`/payback @user1 10` - @user1 gave 10 UAH to the author of the message).
- `/debts` - Shows all debts.

If you found a bug or have a suggestion, feel free to write it here: https://github.com/alexey-belous/FundsSplitter/issues.
    """

    let helpMessageRu = """
       Funds Splitter v1.0
Для начала пользования этим ботом, он должен быть добавлен в Telegram чат.
После чего все учасники должны выполнить комманду /join что б присоедениться к группе.

- `/help` - показывает описание команд и то, как работает бот.
- `/join` - Добавляет пользователя к группе.

- `/pay 100 описание` - Добавляет платеж пользователя, отправившего сообщение 

- `/payback @user1 100` - Добавляет возврат средств пользователя в группу (`/payback @user1 10` - @user1 вернул 10 грн автору сообщения).
- `/debts` - Показывает все долги в группе.

Если у вы нашли баг или у вас есть предложение по улучшению, можете написать это здесь: https://github.com/alexey-belous/FundsSplitter/issues.
    """

    let helpMessage (lang: LanguageCode) = 
        match lang with
        | Ru -> helpMessageRu
        | En -> helpMessageEn

    let handlerFunction botContext (update: Update) = 
        fun () -> async {
            let lang = update |> getLanguageCode
            let msg = update.Message
            let client = botContext.BotClient
            let cts = botContext.CancellationToken

            let! _ =  
                client.SendTextMessageAsync(new ChatId(msg.Chat.Id), (helpMessage lang), Enums.ParseMode.Markdown, true, false, msg.MessageId, null, cts)
                |> Async.AwaitTask
            return Ok()
        } |> Some