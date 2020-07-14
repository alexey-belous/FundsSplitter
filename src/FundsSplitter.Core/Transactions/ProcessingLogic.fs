namespace FundsSplitter.Core.Transactions

module ProcessingLogic = 
    open FundsSplitter.Core.Transactions.Types

    type DebtsMatrix = 
        {
            Givers: (User*decimal) list
            Receivers: (User*decimal) list
        }

    let calculateTransactionDebts (tx: Tx) = 
        let idealDebt = tx.Amount / decimal(tx.SplittingSubset.Length)
        let txDebts = 
            tx.SplittingSubset
            |> List.map (fun u -> 
                if u.Id = tx.User.Id 
                then (u, tx.Amount - idealDebt) 
                else (u, 0.0m - idealDebt))
        if tx.SplittingSubset |> List.tryFind (fun u -> u.Id = tx.User.Id) |> Option.isSome then
            txDebts
        else
            (tx.User, tx.Amount) :: txDebts

    let calculateUserSettlingUps txs = 
        txs
        |> List.filter (fun tx -> tx.Type = SettlingUp)
        |> List.groupBy (fun tx -> tx.User.Id)

    let getUserDebtAmount (txDebts: (User * decimal) list) uId = 
        match txDebts |> List.tryFind (fun (u, _) -> u.Id = uId) with
        | None -> 0.0m
        | Some (_, a) -> a

    let createInitialDebtsMatrix chat =         
        let rec calculateUserDebts (matrix: (User * decimal) list) (txsDebts: (User * decimal) list list) = 
            match txsDebts with
            | [] -> matrix
            | txDebts :: otherDebts ->
                let m' = 
                    matrix 
                    |> List.map (fun (u, a) -> (u, a + (getUserDebtAmount txDebts u.Id)))
                calculateUserDebts m' otherDebts

        let userDebts = 
            chat.Transactions 
            |> List.filter (fun tx -> tx.Type = Payment)
            |> List.map calculateTransactionDebts
            |> calculateUserDebts (chat.KnownUsers |> List.map (fun u -> (u, 0.0m)))

        {
            Givers = userDebts |> List.filter (fun (_, amount) -> amount > 0.0m)
            Receivers = userDebts |> List.filter (fun (_, amount) -> amount < 0.0m)
        }

    let getDebts debtsMatrix = 
        let replaceUser newAmount (u: User) ((u': User), a) = 
            if u.Id = u'.Id then (u, newAmount) else (u', a)

        let rec getDebtsInner matrix transactions = 
            // printfn "==============START-OF-ITERATION================"
            if 
                System.Math.Round(matrix.Givers |> List.sumBy snd, 2) = 0.0m 
                && System.Math.Round(matrix.Receivers |> List.sumBy snd, 2) = 0.0m then
                transactions
            else
                let maxGiver = matrix.Givers |> List.maxBy snd
                let minReceiver = matrix.Receivers |> List.minBy snd

                let delta = (maxGiver |> snd) + (minReceiver |> snd)
                let newGiverAmount = System.Math.Max(delta, 0.0m)
                let newReceiverAmount = System.Math.Min(delta, 0.0m)

                let newMatrix = 
                    { matrix with 
                        Receivers = matrix.Receivers 
                            |> List.map (minReceiver |> fst |> replaceUser newReceiverAmount)
                        Givers = matrix.Givers
                            |> List.map (maxGiver |> fst |> replaceUser newGiverAmount)
                    }

                // printfn "New matrix: %A" newMatrix

                let paymentAmount = System.Math.Min(maxGiver |> snd, -1.0m * (minReceiver |> snd))
                let newTransaction = {
                    From = minReceiver |> fst
                    To = maxGiver |> fst
                    Amount = paymentAmount
                } 

                // printfn "New transaction: %A" newTransaction
                newTransaction :: transactions
                |> getDebtsInner newMatrix

        getDebtsInner debtsMatrix []

    let addSttlingUpsToDebts chat debts =
        let userSettlingsUps = 
            chat.Transactions
            |> calculateUserSettlingUps

        debts 
        |> List.map (fun d -> 
            match userSettlingsUps |> List.tryFind (fun (uId, _) -> uId = d.From.Id) with
            | Some (uId, sups) -> 
                let sups' = 
                    sups
                    |> List.map (fun s -> s.SplittingSubset.[0], s.Amount)
                let amount = getUserDebtAmount sups' (d.To.Id)
                { d with Amount = d.Amount - amount }
            | None -> d )
    