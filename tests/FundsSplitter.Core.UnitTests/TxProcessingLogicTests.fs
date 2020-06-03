namespace FundsSplitter.Core.UnitTests

module TxProcessingLogicTests = 
    open System
    open FsUnit
    open Xunit

    open TestHelpers

    open FundsSplitter.Core.Transactions.Types
    open FundsSplitter.Core.Transactions.ProcessingLogic

    [<Fact>]
    let ``Should calculate transaction debts for tx with two users `` () = 
        let u1 = users.[0]
        let u2 = users.[1]
        let splittingSubset = [u1; u2]
        let tx = createTx u1 Payment 100.0m splittingSubset

        let debts = calculateTransactionDebts tx

        let expectedDebts = [
            ({Id = 1; Username="user1"; Name = "user1";}, 50.0m)
            ({Id = 2; Username="user2"; Name = "user2";}, -50.0m)]

        expectedDebts |> should equal debts

    [<Fact>]
    let ``Should calculate debts matrix for chat with two users`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        let splittingSubset = [u1; u2]

        let txs = [
            createTx u1 Payment 50.0m splittingSubset
            createTx u1 Payment 50.0m splittingSubset
        ]
        let chat = {
            Id = 1L
            Title = "chat1"

            KnownUsers = [u1; u2]

            Transactions = txs
        }

        let matrix = createInitialDebtsMatrix chat

        let expectedMatrix = {
            Givers = [(u1, 50.0M)];
            Receivers = [(u2, -50.0M)];}

        expectedMatrix |> should equal matrix


    [<Fact>]
    let ``Should compute debts resolving transaction for two users`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        
        let matrix = {
            Givers = [(u1, 10.0m)]
            Receivers = [(u2, -10.0m)]
        }

        let debts = getDebts matrix

        Assert.Equal(u2, debts.[0].From)
        Assert.Equal(u1, debts.[0].To)
        Assert.Equal(10.0m, debts.[0].Amount)

    [<Fact>]
    /// See ./docs/unit-tests-task-solutions.md for more info about assetions#Task 1
    let ``Should compute debts resolving transaction for five users`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        let u3 = users.[2]
        let u4 = users.[3]
        let u5 = users.[4]
        
        let matrix = {
            Givers = [(u1, 228.0m); (u3, 178.0m);]
            Receivers = [(u2, -72.0m); (u4, -172.0m); (u5, -162.0m)]
        }

        let debts = getDebts matrix

        let expectedDebts = 
            [
                {From = {Id = 2; Name = "user2"; Username="user2";}; To = {Id = 3; Name = "user3"; Username="user3";}; Amount = 16.00M;}; 
                {From = {Id = 2; Name = "user2"; Username="user2";}; To = {Id = 1; Name = "user1"; Username="user1";}; Amount = 56.0m;}; 
                {From = {Id = 5; Name = "user5"; Username="user5";}; To = {Id = 3; Name = "user3"; Username="user3";}; Amount = 162.00M;};
                {From = {Id = 4; Name = "user4"; Username="user4";}; To = {Id = 1; Name = "user1"; Username="user1";}; Amount = 172.00M;}]

    
        expectedDebts |> should equal debts

    [<Fact>]
    /// See ./docs/unit-tests-task-solutions.md for more info about assetions#Task 2
    let ``Should calculate debts matrix for three users with different splitting rules`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        let u3 = users.[2]
        
        let txs = [
            createTx u1 Payment 100.0m [u1; u2; u3]
            createTx u2 Payment 100.0m [u1; u2; u3]
            createTx u3 Payment 100.0m [u1; u3]
        ]
        let chat = {
            Id = 1L
            Title = "chat1"

            KnownUsers = [u1; u2; u3]

            Transactions = txs
        }

        let matrix = createInitialDebtsMatrix chat

        let expectedMatrix = {
            Givers = [(u2, 33.333333333333333333333333334M)];
            Receivers = [
                (u1, -16.666666666666666666666666666M);
                (u3, -16.666666666666666666666666666M)];}
        
        expectedMatrix |> should equal matrix

    [<Fact>]
    /// See ./docs/unit-tests-task-solutions.md for more info about assetions#Task 2
    let ``Should compute debts resolving transaction for three users with different splitting rules`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        let u3 = users.[2]
        
        let txs = [
            createTx u1 Payment 100.0m [u1; u2; u3]
            createTx u2 Payment 100.0m [u1; u2; u3]
            createTx u3 Payment 100.0m [u1; u3]
        ]
        let chat = {
            Id = 1L
            Title = "chat1"

            KnownUsers = [u1; u2; u3]

            Transactions = txs
        }

        let matrix = createInitialDebtsMatrix chat

        let debts = getDebts matrix

        let expectedDebts = [
            { From = u3; To = u2; Amount = 16.666666666666666666666666666M; };
            { From = u1; To = u2; Amount = 16.666666666666666666666666666M; } ]
    
        expectedDebts |> should equal debts

    [<Fact>]
    let ``Should compute debts resolving transaction for two users with settling ups`` () =
        let u1 = users.[0]
        let u2 = users.[1]
        
        let txs = [
            createTx u1 Payment 100.0m [u1; u2]

            createTx u2 SettlingUp 25.0m [u1]
        ]
        let chat = {
            Id = 1L
            Title = "chat1"

            KnownUsers = [u1; u2;]

            Transactions = txs
        }

        let matrix = createInitialDebtsMatrix chat

        let debts = getDebts matrix
                    |> addSttlingUpsToDebts chat

        let expectedDebts = [
            { From = u2; To = u1; Amount = 25.0m; } ]
    
        expectedDebts |> should equal debts
