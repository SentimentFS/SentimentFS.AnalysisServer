namespace SentimentFS.AnalysisServer.WebApi

module Analysis =
    open Akka.Actor
    open Suave
    open Suave.Filters
    open Suave.Operators
    open Suave.Successful
    open SentimentFS.AnalysisServer.Core.Analysis
    open Cassandra
    open Tweetinvi

    let analysisController() =
        let actorSystem =
            ActorSystem.Create("sentimentfs")

        let cluster =
            Cluster
                .Builder()
                .AddContactPoint("127.0.0.1")
                .WithPort(8881)
                .WithDefaultKeyspace("sentiment_fs")
                .Build()

        let session = cluster.ConnectAndCreateDefaultKeyspaceIfNotExists()

        let analysisActor =
            actorSystem.ActorOf(Props.Create<AnalysisActor>())

        let credientials = Auth.SetUserCredentials("", "", "", "")

        let getAnalysisResultByKey(key):WebPart =
            fun (x : HttpContext) ->
                async {
                    let! result = analysisActor.Ask<string>({ key = key }) |> Async.AwaitTask
                    return! OK result x
                }

        pathStarts "/api/analysis" >=> choose [
            GET >=> choose [ pathScan "/api/analysis/result/%s" getAnalysisResultByKey ]
        ]
