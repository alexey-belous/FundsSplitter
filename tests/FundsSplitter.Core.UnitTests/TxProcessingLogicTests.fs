namespace FundsSplitter.Core.UnitTests

module TxProcessingLogicTests = 
    open System
    open FsUnit
    open Xunit

    open FundsSplitter.Core.Transactions.Types
    open FundsSplitter.Core.Transactions.ProcessingLogic

    let users = [
        {
            Id = 1
            Name = "user1"
        }
        {
            Id = 2
            Name = "user2"
        }
        {
            Id = 3
            Name = "user3"
        }
        {
            Id = 4
            Name = "user4"
        }
        {
            Id = 5
            Name = "user5"
        }
    ]

    let createTx user txType amount = 
        let emptyMsg = { Id = 1; Text = String.Empty }
        { Id = Guid.NewGuid(); User = user; Message = emptyMsg; Type = txType; Amount = amount; SplittingSubset = [] }

    [<Fact>]
    let ``Should compute correct sum of all payments`` () =
        let me = users.[0]
        let txs = [
            createTx me Payment 10.0M
            createTx me Payment 20.0M
            createTx me SettlingUp 10.0M
        ]
        let chat = {
            Id = 1
            Title = "chat1"

            TotalUsersAmount = 2
            KnownUsers = []

            Transactions = txs
        }

        let paymentsSum = getPaymentsSum chat

        Assert.Equal(30.0M, paymentsSum)

    [<Fact>]
    let ``Should compute total payments of each user`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        let u3 = users.[2]
        let txs = [
            createTx u1 Payment 10.0M
            createTx u1 Payment 10.0M
            
            createTx u2 Payment 20.0M
            createTx u2 Payment 20.0M

            createTx u3 Payment 30.0M
            createTx u3 Payment 30.0M
        ]
        let chat = {
            Id = 1
            Title = "chat1"

            TotalUsersAmount = 3
            KnownUsers = [u1; u2; u3]

            Transactions = txs
        }

        let userPayments = groupTotalUserPayments chat

        Assert.Equal(20.0M, userPayments.[0] |> snd)
        Assert.Equal(40.0M, userPayments.[1] |> snd)
        Assert.Equal(60.0M, userPayments.[2] |> snd)

    [<Fact>]
    let ``Should create appropriate debts matrix for particular chat`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        let u3 = users.[2]
        let txs = [
            createTx u1 Payment 10.0M
            createTx u1 Payment 10.0M
            
            createTx u2 Payment 20.0M
            createTx u2 Payment 20.0M
        ]
        let chat = {
            Id = 1
            Title = "chat1"

            TotalUsersAmount = 3
            KnownUsers = [u1; u2; u3]

            Transactions = txs
        }

        let matrix = createInitialDebtsMatrix chat

        Assert.Equal(20.0M, matrix.Givers.[0] |> snd)
        Assert.Equal(-20.0M, matrix.Receivers.[0] |> snd)


    [<Fact>]
    let ``Should compute debts resolving transaction for two users`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        
        let matrix = {
            Givers = [(u1, 10.0M)]
            Receivers = [(u2, -10.0M)]
        }

        let debts = getDebts matrix

        Assert.Equal(u2, debts.[0].From)
        Assert.Equal(u1, debts.[0].To)
        Assert.Equal(10.0M, debts.[0].Amount)

    [<Fact>]
    let ``Should compute debts resolving transaction for five users`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        let u3 = users.[2]
        let u4 = users.[3]
        let u5 = users.[4]
        
        let matrix = {
            Givers = [(u1, 228.0M); (u3, 178.0M);]
            Receivers = [(u2, -72.0M); (u4, -172.0M); (u5, -162.0M)]
        }

        let debts = getDebts matrix

        let expectedDebts = 
            [
                {From = {Id = 2; Name = "user2";}; To = {Id = 3; Name = "user3";}; Amount = 16.00M;}; 
                {From = {Id = 2; Name = "user2";}; To = {Id = 1; Name = "user1";}; Amount = 56.0M;}; 
                {From = {Id = 5; Name = "user5";}; To = {Id = 3; Name = "user3";}; Amount = 162.00M;};
                {From = {Id = 4; Name = "user4";}; To = {Id = 1; Name = "user1";}; Amount = 172.00M;}]

    
        expectedDebts |> should equal debts

