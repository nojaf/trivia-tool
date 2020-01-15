module TriviaTool.Client.State

open Elmish

let init _ = null, Cmd.none

let update msg model = model, Cmd.none