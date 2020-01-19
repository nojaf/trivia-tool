module TriviaTool.Client.State

open Elmish
open TriviaTool.Client.Model

let init _ =
    { ActiveTab = ByTrivia }, Cmd.none

let update msg model =
    match msg with
    | SelectTab tab ->
        { model with ActiveTab = tab }, Cmd.none