namespace SentimentFS.AnalysisServer.WebApi

module JSON =
    open Newtonsoft.Json

    let jsonConverter = Fable.JsonConverter() :> JsonConverter

    let toJson value = JsonConvert.SerializeObject(value, [|jsonConverter|])

    let ofJson<'a>(json: string) = JsonConvert.DeserializeObject<'a>(json, [|jsonConverter|])

