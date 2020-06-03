namespace FundsSplitter.Core.UnitTests

module TestHelpers = 
    open System
    
    open FundsSplitter.Core.Transactions.Types

    let users = [
        {
            Id = 1
            Username="user1";
            Name = "user1"
        }
        {
            Id = 2
            Username="user2";
            Name = "user2"
        }
        {
            Id = 3
            Username="user3";
            Name = "user3"
        }
        {
            Id = 4
            Username="user4";
            Name = "user4"
        }
        {
            Id = 5
            Username="user5";
            Name = "user5"
        }
    ]

    let createTx user txType amount splittingSubset = 
        let emptyMsg = { Id = 1; Text = String.Empty }
        { 
            Id = Guid.NewGuid()
            User = user
            Message = emptyMsg
            Type = txType
            Amount = amount 
            SplittingSubset = splittingSubset 
        }