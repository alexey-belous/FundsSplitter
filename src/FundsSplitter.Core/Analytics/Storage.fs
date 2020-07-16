namespace FundsSplitter.Core.Analytics

module Storage = 
    open System
    open Types
    open FundsSplitter.Core.Json

    open MongoDB.Driver
    open MongoDB.Bson

    type RecordFilter = 
        {
            Day: DateTime
        }

    let createRecordFilter dayFilter = 
        BsonDocumentFilterDefinition(BsonDocument.Parse(dayFilter))

    let upsertDailyMessagesRecord (records: IMongoCollection<BsonDocument>) cts (record: DailyMessagesAnalytics) = 
        let filter = { Day = record.Day } |> Serializer.serialize |> createRecordFilter
        let replaceOptions = ReplaceOptions()
        replaceOptions.IsUpsert <- true
        let serialized = record |> Serializer.serialize |> BsonDocument.Parse
        records.ReplaceOne(filter, serialized, replaceOptions, cts)
        |> ignore
        record

    let tryFindDailyMessagesRecord (records: IMongoCollection<BsonDocument>) cts (day: DateTime) = async {
        let filter = { Day = day.ToUniversalTime().Date } |> Serializer.serialize |> createRecordFilter
        let! foundRecorsCursor = records.FindAsync<BsonDocument>(filter, null, cts) |> Async.AwaitTask
        let! cursor = foundRecorsCursor.ToListAsync(cts)  |> Async.AwaitTask
        return
            cursor 
            |> Seq.map (fun doc -> 
                doc.Remove("_id")
                doc.ToJson()
                |> Serializer.deserialize<DailyMessagesAnalytics> ) 
            |> Seq.tryFind (fun c -> c.Day = day)
    }

    let upsertDailyUsersRecord (records: IMongoCollection<BsonDocument>) cts (record: DailyUsersAnalytics) = 
        let filter = { Day = record.Day } |> Serializer.serialize |> createRecordFilter
        let replaceOptions = ReplaceOptions()
        replaceOptions.IsUpsert <- true
        let serialized = record |> Serializer.serialize |> BsonDocument.Parse
        records.ReplaceOne(filter, serialized, replaceOptions, cts)
        |> ignore
        record

    let tryFindDailyUsersRecord (records: IMongoCollection<BsonDocument>) cts (day: DateTime) = async {
        let filter = { Day = day.ToUniversalTime().Date } |> Serializer.serialize |> createRecordFilter
        let! foundRecorsCursor = records.FindAsync<BsonDocument>(filter, null, cts) |> Async.AwaitTask
        let! cursor = foundRecorsCursor.ToListAsync(cts)  |> Async.AwaitTask
        return
            cursor 
            |> Seq.map (fun doc -> 
                doc.Remove("_id")
                doc.ToJson()
                |> Serializer.deserialize<DailyUsersAnalytics> ) 
            |> Seq.tryFind (fun c -> c.Day = day)
    }