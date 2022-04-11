// Project 4 (ii) | COP 5616 : Implement a Tweeter system that is basically a "Twitter Clone" over F sharp utilising the AKKA Actor model whilst using WebSharper web framework to implement WebSocket interface.
// Author : Khosla, Apoorv | UFID : 3025-1514
// Deadline : December 15 2021

open System
open Akka.FSharp
open FSharp.Json
open Akka.Actor
open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open System.IO
open System.Net
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Newtonsoft.Json
open System.Threading
open Suave.RequestErrors
open Newtonsoft.Json.Serialization

// Json import successful for NewtonSoft and FSharp via CLI

// Trying a different approach wherein Actors are for handling the live feed coming into deployment later into LOCs
let system = ActorSystem.Create("TweeterSystem")

let mutable completedUsers = Map.empty
let mutable listFollowers = Map.empty
let mutable hashtagList = Map.empty
let mutable mentionList = Map.empty


// For maintaning a global counter of total number of processes
let processCounter = Environment.ProcessorCount

type ReplyServer = {
  userID: string
  message: string
  service: string
  code: string
}

type RequestMotto = {
  userID: string
  value: string
}

type Comms = 
  | NewUserRegister of (string)
  | SubscriberList of (string*string)
  | UpActiveUser of (string*WebSocket)
  | DownActiveUser of (string)
  | PushFeed of (string*string*string)

type TweetMania = 
  | KickTweet of (IActorRef)
  | PushRegisteredUser of (string)
  | NewTweetReq of (string*string*string)

type CummulativeResourcePoint<'a> = {
    StartUp : RequestMotto -> string
}

let tweeterHero = MailboxProcessor<string*WebSocket>.Start(fun inbox ->
  let rec looperForMsg() = async {
    let! msg, socketWeb = inbox.Receive()
    let byteMeas =
      msg
      |> System.Text.Encoding.ASCII.GetBytes
      |> ByteSegment
    let! _ = socketWeb.send Text byteMeas true
    return! looperForMsg()
  }
  looperForMsg()
)

let TweetFeedActor (mailbox:Actor<_>) = 
  // Map and variable declarations
  let mutable followerList = Map.empty
  let mutable onlineUserList = Map.empty
  let mutable mapForFeeds = Map.empty
  //   override x.OnReceive(MessageText)
  let rec loop () = actor {
      let! msg = mailbox.Receive() 
      match msg with
      | NewUserRegister(userId) ->
        followerList <- Map.add userId Set.empty followerList
        mapForFeeds <- Map.add userId List.empty mapForFeeds
      | SubscriberList(userId, followerKey) ->
        // Checking if the follower already exists in followers-list
        if followerList.ContainsKey followerKey then
          let mutable setForFollowerData = Set.empty
          setForFollowerData <- followerList.[followerKey]
          setForFollowerData <- Set.add userId setForFollowerData
          followerList <- Map.remove followerKey followerList 
          followerList <- Map.add followerKey setForFollowerData followerList
          let mutable jsonDataExcerpt: ReplyServer = 
            {userID = followerKey; service= "Follow"; code = "OK"; message = sprintf "User %s started following you!" userId}
          let mutable jsonConsoleMng = Json.serialize jsonDataExcerpt
          tweeterHero.Post (jsonConsoleMng,onlineUserList.[followerKey])
      | UpActiveUser(userId,webSocketUser) ->
        // Checking if user is Active currently   
        if onlineUserList.ContainsKey userId then  
          onlineUserList <- Map.remove userId onlineUserList
        onlineUserList <- Map.add userId webSocketUser onlineUserList 
        let mutable feedInPublic = ""
        let mutable msgHUD = ""
        if mapForFeeds.ContainsKey userId then
          let mutable feedInUp = ""
          let mutable feedLimit = 10
          let feedDataList:List<string> = mapForFeeds.[userId]
          if feedDataList.Length = 0 then
            msgHUD <- "Follow"
            feedInPublic <- sprintf "No feeds yet!!"
          else
            if feedDataList.Length < 10 then
                feedLimit <- feedDataList.Length
            // printfn"feedLimit = %d" feedLimit
            for i in [0..(feedLimit-1)] do
              // printfn "feed %d = %s" i mapForFeeds.[userId].[i]
              feedInUp <- "-" + mapForFeeds.[userId].[i] + feedInUp

            feedInPublic <- feedInUp
            msgHUD <- "WatchFeed"
          // printfn "feeds pub = %A" feedInPublic
          let jsonDataExcerpt: ReplyServer = {userID = userId; message = feedInPublic; code = "OK"; service=msgHUD}
          let jsonConsoleMng = Json.serialize jsonDataExcerpt
          tweeterHero.Post (jsonConsoleMng,webSocketUser) 
      | DownActiveUser(userId) ->
        if onlineUserList.ContainsKey userId then  
          onlineUserList <- Map.remove userId onlineUserList
        //   Online user is remove from the active list of users currently online if message passed
      | PushFeed(userId,msgInTweet,msgHUD) ->
        if followerList.ContainsKey userId then
          let mutable localHUD = ""
          if msgHUD = "Tweet" then
            localHUD <- sprintf "%s tweeted:" userId
          else 
            localHUD <- sprintf "%s re-tweeted:" userId
          for checkFollow in followerList.[userId] do 
            // Couldn't run elif here because both blocks shall check and run for conditions returnval1ectively if the followers have the checkFollow user
            if followerList.ContainsKey checkFollow then
              if onlineUserList.ContainsKey checkFollow then
                let twt = sprintf "%s^%s" localHUD msgInTweet
                let jsonDataExcerpt: ReplyServer = {userID = checkFollow; service=msgHUD; code="OK"; message = twt}
                let jsonConsoleMng = Json.serialize jsonDataExcerpt
                tweeterHero.Post (jsonConsoleMng,onlineUserList.[checkFollow])
              let mutable elementosForMapFeed = []
              if mapForFeeds.ContainsKey checkFollow then
                  elementosForMapFeed <- mapForFeeds.[checkFollow]
              elementosForMapFeed  <- (sprintf "%s^%s" localHUD msgInTweet) :: elementosForMapFeed
              mapForFeeds <- Map.remove checkFollow mapForFeeds
              mapForFeeds <- Map.add checkFollow elementosForMapFeed mapForFeeds
      return! loop()
  }
  loop()

let TweetEmActor = spawn system (sprintf "TweetFeedActor") TweetFeedActor

//  Implements AKKA Actor model
let watchFeed (socketType : WebSocket) (case: HttpContext) =
  let rec loop() =
    let mutable currentUser = ""
    socket { 
      let! msg = socketType.read()
      match msg with
      | (Text, data, true) ->
        let reqMessage = UTF8.toString data
        let localParsing = Json.deserialize<RequestMotto> reqMessage
        currentUser <- localParsing.userID
        TweetEmActor <! UpActiveUser(localParsing.userID, socketType)
        return! loop()
      | (Close, _, _) ->
        printfn "Closed WEBSOCKET"
        TweetEmActor <! DownActiveUser(currentUser)
        let dummyReply = [||] |> ByteSegment
        do! socketType.send Close dummyReply true
      | _ -> return! loop()
    }
  loop()

let onClickRegistrationForNewUser userData =
  let mutable returnval1 = ""
  if completedUsers.ContainsKey userData.userID then
    let receipt: ReplyServer = {userID = userData.userID; message = sprintf "User %s already registred" userData.userID; service = "Register"; code = "FAIL"}
    returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
  else
    completedUsers <- Map.add userData.userID userData.value completedUsers
    listFollowers <- Map.add userData.userID Set.empty listFollowers
    TweetEmActor <! NewUserRegister(userData.userID)
    let receipt: ReplyServer = {userID = userData.userID; message = sprintf "User %s registred successfully" userData.userID; service = "Register"; code = "OK"}
    returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
  returnval1

let onClickLogin userData =
  let mutable returnval1 = ""
  if completedUsers.ContainsKey userData.userID then
    if completedUsers.[userData.userID] = userData.value then
      let receipt: ReplyServer = {userID = userData.userID; message = sprintf "User %s logged in successfully" userData.userID; service = "Login"; code = "OK"}
      returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
    else 
      let receipt: ReplyServer = {userID = userData.userID; message = "Invalid userid / password"; service = "Login"; code = "FAIL"}
      returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
  else
    let receipt: ReplyServer = {userID = userData.userID; message = "Invalid userid / password"; service = "Login"; code = "FAIL"}
    returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
  returnval1


      
let onClickFollow userData =
  let mutable returnval1 = ""
  if userData.value <> userData.userID then
    if listFollowers.ContainsKey userData.value then
        if not (listFollowers.[userData.value].Contains userData.userID) then
            let mutable goingTemp1 = listFollowers.[userData.value]
            goingTemp1 <- Set.add userData.userID goingTemp1
            listFollowers <- Map.remove userData.value listFollowers
            listFollowers <- Map.add userData.value goingTemp1 listFollowers
            TweetEmActor <! SubscriberList(userData.userID,userData.value) 
            let receipt: ReplyServer = {userID = userData.userID; service="Follow"; message = sprintf "You started following %s!" userData.value; code = "OK"}
            returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
        else 
            let receipt: ReplyServer = {userID = userData.userID; service="Follow"; message = sprintf "You are already following %s!" userData.value; code = "FAIL"}
            returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString      
    else
      let receipt: ReplyServer = {userID = userData.userID; service="Follow"; message = sprintf "Invalid request, No such user (%s)." userData.value; code = "FAIL"}
      returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
  else
    let receipt: ReplyServer = {userID = userData.userID; service="Follow"; message = sprintf "You cannot follow yourself."; code = "FAIL"}
    returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString    
  returnval1
  
let onClickTweet userData =
  let mutable returnval1 = ""
  if completedUsers.ContainsKey userData.userID then
    let mutable hashtag = ""
    let mutable userMentions = ""
    let convertedValue = userData.value.Split ' '
    // printfn "convertedValue = %A" convertedValue
    for checkingCovert in convertedValue do
      if checkingCovert.Length > 0 then
        if checkingCovert.[0] = '#' then
          hashtag <- checkingCovert.[1..(checkingCovert.Length-1)]
        else if checkingCovert.[0] = '@' then
          userMentions <- checkingCovert.[1..(checkingCovert.Length-1)]

    if userMentions <> "" then
      if completedUsers.ContainsKey userMentions then
        if not (mentionList.ContainsKey userMentions) then
            mentionList <- Map.add userMentions List.empty mentionList
        let mutable localMentionHolder = mentionList.[userMentions]
        localMentionHolder <- (sprintf "%s tweeted:^%s" userData.userID userData.value) :: localMentionHolder
        mentionList <- Map.remove userMentions mentionList
        mentionList <- Map.add userMentions localMentionHolder mentionList
        TweetEmActor <! PushFeed(userData.userID,userData.value,"Tweet")
        let receipt: ReplyServer = {userID = userData.userID; service="Tweet"; message = (sprintf "%s tweeted:^%s" userData.userID userData.value); code = "OK"}
        returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
      else 
        let receipt: ReplyServer = {userID = userData.userID; service="Tweet"; message = sprintf "Invalid request, mentioned user (%s) is not registered" userMentions; code = "FAIL"}
        returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
    else
      TweetEmActor <! PushFeed(userData.userID,userData.value,"Tweet")
      let receipt: ReplyServer = {userID = userData.userID; service="Tweet"; message = (sprintf "%s tweeted:^%s" userData.userID userData.value); code = "OK"}
      returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString

    if hashtag <> "" then
      if not (hashtagList.ContainsKey hashtag) then
        hashtagList <- Map.add hashtag List.empty hashtagList
      let mutable tList = hashtagList.[hashtag]
      tList <- (sprintf "%s tweeted:^%s" userData.userID userData.value) :: tList
      hashtagList <- Map.remove hashtag hashtagList
      hashtagList <- Map.add hashtag tList hashtagList
  else  
    let receipt: ReplyServer = {userID = userData.userID; service="Tweet"; message = sprintf "Invalid request by user %s, Not registered yet!" userData.userID; code = "FAIL"}
    returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
  returnval1

let onClickReTweet userData =
  let mutable returnval1 = ""
  if completedUsers.ContainsKey userData.userID then
    TweetEmActor <! PushFeed(userData.userID,userData.value,"ReTweet")
    let receipt: ReplyServer = {userID = userData.userID; service="ReTweet"; message = (sprintf "%s re-tweeted:^%s" userData.userID userData.value); code = "OK"}
    returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
  else  
    let receipt: ReplyServer = {userID = userData.userID; service="ReTweet"; message = sprintf "Invalid request by user %s, Not registered yet!" userData.userID; code = "FAIL"}
    returnval1 <- receipt |> Json.toJson |> System.Text.Encoding.UTF8.GetString
  returnval1

let checkingAccount (userData:string) = 
  let mutable tags = ""
  let mutable mentions = ""
  let mutable returnval1 = ""
  let mutable sizeCap = 10
  if userData.Length > 0 then
    if userData.[0] = '@' then
      let searchElemento = userData.[1..(userData.Length-1)]
      if mentionList.ContainsKey searchElemento then
        let localServerMap:List<string> = mentionList.[searchElemento]
        if (localServerMap.Length < 10) then
          sizeCap <- localServerMap.Length
        for i in [0..(sizeCap-1)] do
          mentions <- mentions + "-" + localServerMap.[i]
        let receipt: ReplyServer = {userID = ""; service="Search"; message = mentions; code = "OK"}
        returnval1 <- Json.serialize receipt
      else 
        let receipt: ReplyServer = {userID = ""; service="Search"; message = "-No tweets found for the mentioned user"; code = "OK"}
        returnval1 <- Json.serialize receipt
    else
      let searchElemento = userData
      if hashtagList.ContainsKey searchElemento then
        let localServerMap:List<string> = hashtagList.[searchElemento]
        if (localServerMap.Length < 10) then
            sizeCap <- localServerMap.Length
        for i in [0..(sizeCap-1)] do
            tags <- tags + "-" + localServerMap.[i]
        let receipt: ReplyServer = {userID = ""; service="Search"; message = tags; code = "OK"}
        returnval1 <- Json.serialize receipt
      else 
        let receipt: ReplyServer = {userID = ""; service="Search"; message = "-No tweets found for the hashtag"; code = "OK"}
        returnval1 <- Json.serialize receipt
  else
    let receipt: ReplyServer = {userID = ""; service="Search"; message = "Type something to search"; code = "FAIL"}
    returnval1 <- Json.serialize receipt
  returnval1

let workWithJson j =     
    let jsonSerializerSettings = JsonSerializerSettings()
    jsonSerializerSettings.ContractResolver <- CamelCasePropertyNamesContractResolver()
    JsonConvert.SerializeObject(j, jsonSerializerSettings)
    |> OK 
    >=> Writers.setMimeType "application/json; charset=utf-8"

let workInJson (j:string) =     
    let jsonSerializerSettings = JsonSerializerSettings()
    jsonSerializerSettings.ContractResolver <- CamelCasePropertyNamesContractResolver()
    JsonConvert.SerializeObject(j, jsonSerializerSettings)
    |> OK 
    >=> Writers.setMimeType "application/json; charset=utf-8"

let convoWithJson<'a> json =
        JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a    

let fetchResourceRequested<'a> (requestData : HttpRequest) = 
    let formatToString (rawForm:byte[]) = System.Text.Encoding.UTF8.GetString(rawForm)
    let arrayForSearch:byte[] = requestData.rawForm
    arrayForSearch |> formatToString |> convoWithJson<RequestMotto>

let StartUpStatus resourceTag resource = 
  let rPathway = "/" + resourceTag

  let uponCompletedRegister userData =
    let userRegistrationFiling = resource.StartUp userData
    userRegistrationFiling

  choose [
    path rPathway >=> choose [
      POST >=> request (fetchResourceRequested >> uponCompletedRegister >> workWithJson)
    ]
  ]

let TimesNewUser = StartUpStatus "register" {
  StartUp = onClickRegistrationForNewUser
}

let TimesLoginUser = StartUpStatus "login" {
  StartUp = onClickLogin
}

let TimesFollowUser = StartUpStatus "follow" {
  StartUp = onClickFollow
}

let TimesTweetUser = StartUpStatus "tweet" {
  StartUp = onClickTweet
}

let TimesRetweetuser = StartUpStatus "retweet" {
  StartUp = onClickReTweet
}

let loader = 
  choose [
    path "/watchfeed" >=> handShake watchFeed
    TimesNewUser
    TimesLoginUser
    TimesFollowUser
    TimesTweetUser
    TimesRetweetuser
    pathScan "/search/%s"
      (fun searchElemento ->
        let lookup = (sprintf "%s" searchElemento)
        let answer = checkingAccount lookup
        OK answer) 
    GET >=> choose [path "/" >=> file "TweeterPage.html"; browseHome]
  ]

[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } loader
  0

//   The END
