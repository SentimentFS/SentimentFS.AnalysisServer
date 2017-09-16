namespace SentimentFS.AnalysisServer.Core

module TweetsStorage =
    open SentimentFS.AnalysisServer.Domain.Tweets
    open MongoDB.Driver
    open MongoDB.Bson
    open MongoDB.Driver
    open MongoDB.Driver.Linq
    open Akka.Actor

    let private TweetsCollection (db:IMongoDatabase) = db.GetCollection<Tweet>("tweets")

    let private store (tweets: Tweets) (col: IMongoCollection<Tweet>) =
        async {
            return! col.InsertManyAsync(tweets.value) |> Async.AwaitTask
        }

    let private getByKey (key:string) (col: IMongoCollection<Tweet>) =
        async {
            let! result = col.Find(fun tweet -> tweet.Key = key).ToListAsync() |> Async.AwaitTask
            return match result |> Seq.toList with
                   | [] -> None
                   | l -> Some { value = l }
        }

    let private getSearchKeys (col: IMongoCollection<Tweet>) =
        async {
            let! result = col.Distinct<string>(FieldDefinition<_,_>.op_Implicit("Key"), FilterDefinition.op_Implicit("{}")).ToListAsync() |> Async.AwaitTask
            return match result |> Seq.toList with
                   | [] -> None
                   | l -> Some l
        }

    type TweetsStorageActor(db: IMongoDatabase) as this =
        inherit ReceiveActor()
        do
            this.ReceiveAsync<TweetsStorageMessage>(fun msg -> this.Handle(msg))
        member this.Handle(msg: TweetsStorageMessage) =
            let sender = this.Sender
            async {
                match msg with
                | Store tweets ->
                    do! db |> TweetsCollection |> store(tweets)
                | GetByKey key ->
                    let! res = db |> TweetsCollection |> getByKey key
                    sender.Tell(res)
                | GetSearchKeys ->
                    let! res = db |> TweetsCollection |> getSearchKeys
                    sender.Tell(res)
                return 0
            } |> Async.StartAsTask :> System.Threading.Tasks.Task



module TwitterApiClient =
    open SentimentFS.AnalysisServer.Domain.Tweets
    open SentimentFS.AnalysisServer.Domain.Sentiment
    open System
    open Akka.Actor
    open Tweetinvi
    open Tweetinvi.Models
    open Tweetinvi.Parameters

    let private spawn(credentials: ITwitterCredentials) =
        MailboxProcessor.Start(fun agent ->
            let rec loop () =
                async {
                    let! msg = agent.Receive()
                    match msg with
                    | GetTweets(key, reply) ->
                        let options = SearchTweetsParameters(key)
                        options.SearchType <- Nullable<SearchResultType>(SearchResultType.Recent)
                        options.Lang <- Nullable<LanguageFilter>(LanguageFilter.English)
                        options.Filters <- TweetSearchFilters.None
                        options.MaximumNumberOfResults <- 1000
                        let! queryResult = SearchAsync.SearchTweets(options) |> Async.AwaitTask
                        let result = queryResult
                                        |> Seq.map(fun tweet -> { IdStr = tweet.TweetDTO.IdStr; Text = tweet.TweetDTO.Text; Lang = tweet.TweetDTO.Language.ToString();  Key = key; Date = tweet.TweetDTO.CreatedAt; Longitude = 0.0; Latitude = 0.0; Sentiment = Sentiment.Neutral })
                                        |> Seq.toList
                        match result with
                        | [] -> reply.Reply(None)
                        | list -> reply.Reply(Some { value = list })
                    return! loop()
                }
            loop()
        )

    type TwitterApiActor(credentials: ITwitterCredentials) as this =
        inherit ReceiveActor()
        do
            this.ReceiveAsync<GetTweetsByKey>(fun msg -> this.Handle(msg))
        let agent = spawn(credentials)
        member this.Handle(msg: GetTweetsByKey) =
            let sender = this.Sender
            async {
                let! result = agent.PostAndAsyncReply(fun ch -> GetTweets(msg.key, ch))
                sender.Tell(result)
                return 0
            } |> Async.StartAsTask :> System.Threading.Tasks.Task

