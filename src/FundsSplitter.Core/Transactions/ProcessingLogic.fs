namespace FundsSplitter.Core.Transactions

module ProcessingLogic = 
    open FundsSplitter.Core.Transactions.Types
    
    let addTransaction chat tx = 
        { chat with Transactions = tx :: chat.Transactions }

    let editTransaction chat (tx: Tx) = 
        let replace (tx': Tx) = 
            if tx'.Id = tx.Id then tx else tx'
        { chat with Transactions = chat.Transactions |> List.map replace }

    let getPaymentsSum chat = 
        chat.Transactions 
        |> List.filter (fun tx -> tx.Type = Payment)
        |> List.sumBy (fun tx -> tx.Amount)

    let groupTotalUserPayments chat = 
        let getUserById userId = 
            chat.KnownUsers |> List.find (fun u -> u.Id = userId)
        let totalUserPayments = 
            chat.Transactions 
            |> List.groupBy (fun tx -> tx.User.Id)
            |> List.map (fun (uId, txs) -> (uId, txs |> List.sumBy (fun tx -> tx.Amount)))

        let getUserPayment userId = 
            match totalUserPayments |> List.tryFind (fun (uId, _) -> uId = userId) with
            | Some (_, amount) -> amount
            | None -> 0.0M

        chat.KnownUsers
        |> List.map (fun u -> (u, getUserPayment u.Id))

    type DebtsMatrix = 
        {
            Givers: (User*decimal) list
            Receivers: (User*decimal) list
        }

    let createInitialDebtsMatrix chat = 
        let idealDebt = getPaymentsSum chat / (decimal(chat.TotalUsersAmount))
        let userTotalPayments = groupTotalUserPayments chat 
        let userDebts = 
            userTotalPayments 
            |> List.map (fun (u, sum) -> (u, sum - idealDebt))
        {
            Givers = userDebts |> List.filter (fun (_, amount) -> amount > 0.0M)
            Receivers = userDebts |> List.filter (fun (_, amount) -> amount < 0.0M)
        }

    let getDebts debtsMatrix = 
        let replaceUser newAmount (u: User) ((u': User), a) = 
            if u.Id = u'.Id then (u, newAmount) else (u', a)

        let rec getDebtsInner matrix transactions = 
            // printfn "==============START-OF-ITERATION================"
            if 
                matrix.Givers |> List.sumBy snd = 0.0M 
                && matrix.Receivers |> List.sumBy snd = 0.0M then
                transactions
            else
                let maxGiver = matrix.Givers |> List.maxBy snd
                let minReceiver = matrix.Receivers |> List.minBy snd

                let delta = (maxGiver |> snd) + (minReceiver |> snd)
                let newGiverAmount = System.Math.Max(delta, 0.0M)
                let newReceiverAmount = System.Math.Min(delta, 0.0M)

                // printfn "Participants: %A %A" maxGiver minReceiver

                let newMatrix = 
                    { matrix with 
                        Receivers = matrix.Receivers 
                            |> List.map (minReceiver |> fst |> replaceUser newReceiverAmount)
                        Givers = matrix.Givers
                            |> List.map (maxGiver |> fst |> replaceUser newGiverAmount)
                    }

                // printfn "New matrix: %A" newMatrix

                let paymentAmount = System.Math.Min(maxGiver |> snd, -1.0M * (minReceiver |> snd))
                let newTransaction = {
                    From = minReceiver |> fst
                    To = maxGiver |> fst
                    Amount = paymentAmount
                } 

                // printfn "New transaction: %A" newTransaction
                newTransaction :: transactions
                |> getDebtsInner newMatrix

        getDebtsInner debtsMatrix []

    