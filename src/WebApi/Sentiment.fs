namespace SentimentFS.AnalysisServer.WebApi

module SentimentApi =
    open Akka.Actor
    open Suave
    open Suave.Filters
    open Suave.Operators
    open Suave.Successful
    open SentimentFS.AnalysisServer.Core.Actor
    open SentimentFS.AnalysisServer.Core.Config
    open SentimentFS.NaiveBayes.Dto
    open SentimentFS.AnalysisServer.Core.Sentiment.Messages
    open SentimentFS.AnalysisServer.Core.Sentiment.Dto

    let sentimentController (system: ActorSystem) =
        let classify(query: Classify):WebPart =
            fun (x : HttpContext) ->
                async {
                    let api = system.ActorSelection(Actors.apiActor.Path)
                    let! result = api.Ask<ClassificationScore<Emotion>>(query) |> Async.AwaitTask
                    return! (SuaveJson.toJson result) x
                }
        let train (query: TrainingQuery<Emotion>): WebPart =
            fun (x: HttpContext) ->
                async {
                    let api = system.ActorSelection(Actors.apiActor.Path)
                    api.Tell({ trainQuery =  { value = query.value; category = query.category; weight = match query.weight with weight when weight > 1 -> Some weight | _ -> None } })
                    return! OK "" x
                }

        pathStarts "/api/sentiment" >=> choose [
            POST >=> choose [ path "/api/sentiment/classification" >=> request (SuaveJson.getResourceFromReq >> classify) ]
            PUT >=> choose [ path "/api/sentiment/trainer" >=> request (SuaveJson.getResourceFromReq >> train)]
        ]
