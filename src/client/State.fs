module TriviaTool.Client.State

open Browser
open Browser.Types
open Fable.Core
open Fable.Core.JsInterop
open Elmish
open Fetch
open TriviaTool.Client.Model
open TriviaTool.Shared

[<Emit("process.env.BACKEND")>]
let private backend: string = jsNative

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

let private initialModel =
    { ActiveTab = ByTriviaNodes
      SourceCode = ""
      Exception = None
      IsLoading = false
      Trivia = []
      TriviaNodes = []
      ActiveByTriviaIndex = 0
      ActiveByTriviaNodeIndex = 0 }

let init _ =
    let cmd = Cmd.OfPromise.either fetchTrivia initialModel TriviaReceived NetworkError
    initialModel, cmd

let private selectRange (range: Range) _ =
    let data =
        jsOptions<CustomEventInit> (fun o ->
            o.detail <-
                {| startColumn = range.StartColumn + 1
                   startLineNumber = range.StartLine
                   endColumn = range.EndColumn + 1
                   endLineNumber = range.EndLine |})

    let event = CustomEvent.Create("trivia_select_range", data)
    Dom.window.dispatchEvent (event) |> ignore

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
              TriviaNodes = result.TriviaNodes
              ActiveByTriviaIndex = 0
              ActiveByTriviaNodeIndex = 0 }, Cmd.none
    | NetworkError err ->
        { initialModel with Exception = Some err }, Cmd.none
    | ActiveItemChange(tab, index) ->
        let model, range =
            match tab with
            | ByTriviaNodes ->
                let range =
                    List.tryItem index model.TriviaNodes |> Option.map (fun t -> t.Range)
                { model with ActiveByTriviaNodeIndex = index }, range
            | ByTrivia ->
                let range =
                    List.tryItem index model.Trivia |> Option.map (fun tv -> tv.Range)
                { model with ActiveByTriviaIndex = index }, range

        let cmd =
            range
            |> Option.map (fun r -> Cmd.ofSub (selectRange r))
            |> Option.defaultValue Cmd.none

        model, cmd
