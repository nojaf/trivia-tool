module TriviaTool.Client.State

open Fable.Core
open Fable.Core.JsInterop
open Elmish
open Fetch
open TriviaTool.Client.Model
open TriviaTool.Shared

[<Emit("process.env.BACKEND")>]
let private backend: string = jsNative

let init _ =
    { ActiveTab = ByTrivia
      SourceCode = ""
      Exception = None
      IsLoading = false
      Trivia = []
      TriviaNodes = [] }, Cmd.none

let private fetchTrivia model =
    let url = sprintf "%s/api/GetTrivia" backend
    Fetch.fetch url
        [ RequestProperties.Body(!^model.SourceCode)
          RequestProperties.Method HttpMethod.POST ]
    |> Promise.bind (fun res -> res.text())
    |> Promise.map (fun json ->
        match Decoders.decodeResult json with
        | Ok r -> r
        | Error err -> failwithf "failed to decode result: %A" err)

let update msg model =
    match msg with
    | SelectTab tab ->
        { model with ActiveTab = tab }, Cmd.none
    | UpdateSourceCode code ->
        { model with SourceCode = code }, Cmd.none
    | GetTrivia ->
        { model with IsLoading = true }, Cmd.OfPromise.either fetchTrivia model TriviaReceived NetworkError
    | TriviaReceived result ->
        { model with
              IsLoading = false
              Trivia = result.Trivia
              TriviaNodes = result.TriviaNodes }, Cmd.none
    | NetworkError err ->
        { model with Exception = Some err }, Cmd.none
