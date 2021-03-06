namespace FundsSplitter.Core

module Storage = 
    open MongoDB.Bson
    open MongoDB.Driver

    type CollectionsType = 
        {
            Chats: string
            MessagesAnalytics: string
            UsersAnalytics: string
        }

    let Collections = 
        {
            Chats = "Chats"
            MessagesAnalytics = "MessagesAnalytics"
            UsersAnalytics = "UsersAnalytics"
        }
    let collections = [Collections.Chats; Collections.MessagesAnalytics; Collections.UsersAnalytics]

    type Storage = 
        {
            ConnectionString: string
            Client: MongoClient
            Database: IMongoDatabase
        }

    let initializeStorage connectionString = 
        let client = new MongoClient(connectionString = connectionString)
        let db = client.GetDatabase("fundssplitter")
        let existingCollections = db.ListCollectionNames().ToList()

        collections 
        |> List.iter (fun c -> 
            if existingCollections |> Seq.contains c |> not
            then db.CreateCollection(c)
            else ())

        {
            ConnectionString = connectionString
            Client = client
            Database = db
        }
        