namespace SentimentFS.AnalysisServer.Core.Api
open Akka.Actor
open Akka.Routing
open SentimentFS.AnalysisServer.Core.Config
open SentimentFS.AnalysisServer.Core.Actor
open Cassandra
open SentimentFS.AnalysisServer.Core.Tweets.TweetsMaster
open SentimentFS.AnalysisServer.Core.Tweets.Messages
open SentimentFS.AnalysisServer.Core.Analysis
open SentimentFS.AnalysisServer.Core.Sentiment.Messages
open SentimentFS.AnalysisServer.Core.Sentiment.Actor

type ApiMasterActor(config: AppConfig, cluster: Cluster) as this =
    inherit ReceiveActor()
    do
        this.Receive<Train>(this.HandleTrainQuery)
        this.Receive<Classify>(this.HandleClassifyQuery)
        this.Receive<GetTweetsByKey>(this.HandleGetTweetsByKey)
        this.Receive<GetAnalysisForKey>(this.HandleGetAnalysisForKey)
        this.Receive<GetKeys>(this.HandleGetSearchKeys)

    let mutable sentimentActor: IActorRef = null
    let mutable tweetsMasterActor: IActorRef = null
    let mutable analysisActor: IActorRef = null

    override this.PreStart() =
            sentimentActor <- Akka.Actor.Internal.InternalCurrentActorCellKeeper.Current.ActorOf(Props.Create<SentimentActor>(config.Sentiment.InitFileUrl, Some defaultClassificatorConfig), Actors.sentimentActor.Name)
            tweetsMasterActor <- Akka.Actor.Internal.InternalCurrentActorCellKeeper.Current.ActorOf(Props.Create<TweetsMasterActor>(cluster, config.TwitterApiCredentials).WithRouter(FromConfig.Instance), Actors.tweetsMaster.Name)
            analysisActor <- Akka.Actor.Internal.InternalCurrentActorCellKeeper.Current.ActorOf(Props.Create<AnalysisActor>().WithRouter(FromConfig.Instance), Actors.analysisActor.Name)
            base.PreStart()

    member this.HandleTrainQuery(msg: Train) =
        sentimentActor.Forward(msg)
        true

    member this.HandleClassifyQuery(msg: Classify) =
        sentimentActor.Forward(msg)
        true

    member this.HandleGetTweetsByKey(msg: GetTweetsByKey) =
        tweetsMasterActor.Forward(msg)
        true

    member this.HandleGetSearchKeys(msg: GetKeys) =
        tweetsMasterActor.Forward(msg)
        true

    member this.HandleGetAnalysisForKey(msg: GetAnalysisForKey) =
        analysisActor.Forward(msg)
        true
