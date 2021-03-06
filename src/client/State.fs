module TriviaTool.Client.State

open System
open Browser
open Browser.Types
open Fable.Core
open Fable.Core.JsInterop
open Elmish
open Fetch
open TriviaTool.Client.Model
open TriviaTool.Client.Encoders
open TriviaTool.Client.Decoders
open TriviaTool.Shared
open Thoth.Json

[<Emit("process.env.BACKEND")>]
let private backend: string = jsNative

let private fetchTrivia (payload: ParseRequest) =
    let url = sprintf "%s/api/get-trivia" backend
    let json = encodeParseRequest payload |> Encode.toString 4
    Fetch.fetch url
        [ RequestProperties.Body(!^json)
          RequestProperties.Method HttpMethod.POST ]
    |> Promise.bind (fun res -> res.text())
    |> Promise.map (fun json ->
        match decodeResult json with
        | Ok r -> r
        | Error err -> failwithf "failed to decode result: %A" err)

let private fetchFSCVersion() =
    let url = sprintf "%s/api/version" backend
    Fetch.fetch url []
    |> Promise.bind (fun response -> response.text())
    |> Promise.map (fun json ->
        match decodeVersion json with
        | Ok v -> v
        | Error err -> failwithf "failed to decode version: %A" err)

let private initialModel =
    { ActiveTab = ByTriviaNodes
      SourceCode = ""
      Exception = None
      IsLoading = false
      Trivia = []
      TriviaNodes = []
      ActiveByTriviaIndex = 0
      ActiveByTriviaNodeIndex = 0
      Defines = ""
      FSCVersion = "???"
      IsFsi = false
      KeepNewlineAfter = false }

let private encodeUrl (x: string): string = import "compressToEncodedURIComponent" "./js/urlUtils.js"
let private decodeUrl (x: string): string = import "decompressFromEncodedURIComponent" "./js/urlUtils.js"

let private restoreModelFromUrl() =
    match window.location.search with
    | x when System.String.IsNullOrWhiteSpace(x) -> initialModel, None
    | x ->
        let search = x.Substring(1) // remove ?

        let keyValues =
            search.Split('&')
            |> Array.map (fun kv -> kv.Split('=').[0], kv.Split('=').[1])
            |> Map.ofArray
        match Map.tryFind "data" keyValues with
        | Some data ->
            let json = JS.decodeURIComponent (data) |> decodeUrl
            let urlInfo = Decode.fromString decodeParseRequest json
            match urlInfo with
            | Result.Ok u ->
                { initialModel with
                      SourceCode = u.SourceCode
                      Defines = String.concat " " u.Defines
                      IsFsi = u.FileName.EndsWith(".fsi")
                      IsLoading = true
                      KeepNewlineAfter = u.KeepNewlineAfter }, Some u
            | Error err ->
                printfn "%A" err
                initialModel, None
        | None -> initialModel, None

let init _ =
    let model, parseRequest = restoreModelFromUrl()

    let cmd =
        let parseCmd =
            match parseRequest with
            | Some pr -> Cmd.OfPromise.either fetchTrivia pr TriviaReceived NetworkError
            | None -> Cmd.none

        let versionCmd = Cmd.OfPromise.either fetchFSCVersion () FSCVersionReceived NetworkError
        Cmd.batch [ versionCmd; parseCmd ]

    model, cmd

let private setGetParam (key, value): unit = import "setGetParam" "./js/urlUtils.js"

let private splitDefines (value: string) =
    value.Split([| ' '; ';' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

let private modelToParseRequest (model: Model) =
    { SourceCode = model.SourceCode
      Defines = splitDefines model.Defines
      FileName =
          if model.IsFsi then "script.fsi" else "script.fsx"
      KeepNewlineAfter = model.KeepNewlineAfter }

let private updateUrl (model: Model) _ =
    let json = Encode.toString 2 ((modelToParseRequest >> encodeParseRequest) model)
    setGetParam ("data", encodeUrl json)


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
        let parseRequest = modelToParseRequest model

        let cmd =
            Cmd.batch
                [ Cmd.OfPromise.either fetchTrivia parseRequest TriviaReceived NetworkError
                  Cmd.ofSub (updateUrl model) ]

        { model with IsLoading = true }, cmd
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
    | UpdateDefines d ->
        { model with Defines = d }, Cmd.none
    | FSCVersionReceived version ->
        { model with FSCVersion = version }, Cmd.none
    | SetFsiFile v ->
        { model with IsFsi = v }, Cmd.none
    | SetKeepNewlineAfter kna ->
        { model with KeepNewlineAfter = kna }, Cmd.none
