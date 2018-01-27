namespace SentimentFS.AnalysisServer
open System
open SentimentFS.AnalysisServer.Common.Messages.Sentiment
open Cassandra
open SentimentFS.AnalysisServer.Common.Messages.Twitter

module Dto =

    [<CLIMutable>]
    type TweetDto = { IdStr: string
                      Text: string
                      HashTags: string seq
                      Date: DateTime
                      Lang: string
                      Longitude: double
                      Latitude: double
                      Sentiment: Emotion } with
        static member FromCassandraRow(x: Row) = { IdStr = x.GetValue<string>("id_str")
                                                   Text = x.GetValue<string>("text")
                                                   HashTags = x.GetValue<string seq>("key")
                                                   Date = x.GetValue<DateTime>("creation_date")
                                                   Lang = x.GetValue<string>("lang")
                                                   Longitude = x.GetValue<double>("longitude")
                                                   Latitude = x.GetValue<double>("latitude")
                                                   Sentiment = (enum<Emotion>(x.GetValue<int>("sentiment"))) }
        static member FromTweet(x: Tweet) =
            { IdStr = x.IdStr
              HashTags = x.HashTags
              Text = x.Text
              Date = x.CreationDate
              Lang = x.Language
              Longitude = match x.Coordinates with | Some c -> c.Longitude | None -> 0.0
              Latitude = match x.Coordinates with | Some c -> c.Latitude | None -> 0.0
              Sentiment = match x.Sentiment with | Some e -> e | None -> Emotion.Neutral
            }
        static member ToTweet(x: TweetDto):Tweet =
            { IdStr = x.IdStr
              HashTags = x.HashTags
              Text = x.Text
              CreationDate = x.Date
              Language = x.Lang
              Coordinates = if x.Longitude = 0.0 && x.Latitude = 0.0 then None else Some { Longitude = x.Longitude; Latitude = x.Latitude }
              Sentiment = Some x.Sentiment
            }
        static member Zero () = { IdStr = ""
                                  Text = ""
                                  HashTags = [|""|]
                                  Date = DateTime.Now
                                  Lang = ""
                                  Longitude = 0.0
                                  Latitude = 0.0
                                  Sentiment = Emotion.Neutral }


module CassandraDb =
    open Cassandra

    let private createTweetsCollectionIfNotExists (session: ISession) =
        session.Execute("""
                          CREATE TABLE IF NOT EXISTS tweets (
                            id_str varchar,
                            text text,
                            hashtags list<text>,
                            creation_date timestamp,
                            lang varchar,
                            longitude double,
                            latitude double,
                            sentiment int,
                            PRIMARY KEY(id_str)
                          );
                        """)
