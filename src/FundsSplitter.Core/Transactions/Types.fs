namespace FundsSplitter.Core.Transactions

module Types =
    open System

    type User = 
        {
            Id: int
            Name: string
        }

    type Message = 
        {
            Id: int
            Text: string
        }

    type TxType = 
        | Payment 
        | SettlingUp
    let txTypeToStr = function
        | Payment -> "Payment"
        | SettlingUp -> "SettlingUp"

    let (|TxType|) = function
        | "Payment" -> Payment
        | "SettlingUp" -> SettlingUp
        | _ -> failwith "Invalid tx type"

    type Tx = 
        {
            Id: Guid

            User: User
            Message: Message

            Type: TxType
            Amount: decimal
            /// **CURRENTLY THIS FIELD IS IGNORED IN COMPUTATIONS**
            SplittingSubset: User list
        }

    type Chat = 
        {
            Id: int
            Title: string

            TotalUsersAmount: int
            KnownUsers: User list

            Transactions: Tx list
        }

    type Debt = 
        {
            From: User
            To: User
            Amount: decimal
        }