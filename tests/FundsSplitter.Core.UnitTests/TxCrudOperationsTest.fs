namespace FundsSplitter.Core.UnitTests

module TxCrudOperationsTests = 
    open System
    open FsUnit
    open Xunit

    open TestHelpers

    open FundsSplitter.Core.Transactions.Types
    open FundsSplitter.Core.Transactions.CrudOperations

    [<Fact>]
    let ``Add tx to the chat and tx author to the known users`` () =
        let chat = {
            Id = 1
            Title = "chat1"

            KnownUsers = []

            Transactions = []
        }
        let tx = createTx users.[0] Payment 10.0M

        let chat' = addTransaction chat tx

        Assert.Equal(users.[0].Id, chat'.KnownUsers.[0].Id)
        Assert.Equal(tx.Id, chat'.Transactions.[0].Id)

    [<Fact>]
    let ``Add tx to the chat and tx author to the known users without duplicates`` () =
        let chat = {
            Id = 1
            Title = "chat1"

            KnownUsers = []

            Transactions = []
        }
        let tx = createTx users.[0] Payment 10.0M
        let tx2 = createTx users.[0] Payment 10.0M

        let chat' = addTransaction chat tx
        let chat'' = addTransaction chat' tx2

        Assert.Equal(users.[0].Id, chat''.KnownUsers.[0].Id)
        Assert.Equal(tx2.Id, chat''.Transactions.[0].Id)
        Assert.Equal(1, chat''.KnownUsers.Length)
        Assert.Equal(2, chat''.Transactions.Length)
