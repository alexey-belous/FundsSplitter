namespace FundsSplitter.Core

module Json = 
    open Newtonsoft.Json
    open Microsoft.FSharpLu.Json

    type Settings =
        static member settings =
            let s = JsonSerializerSettings()
            s.NullValueHandling <- NullValueHandling.Ignore
            s.MissingMemberHandling <- MissingMemberHandling.Error
            s.ContractResolver <- new Serialization.CamelCasePropertyNamesContractResolver()
            s.Converters.Add(CompactUnionJsonConverter())
            s
        static member formatting = Formatting.None

    type Serializer = With<Settings>