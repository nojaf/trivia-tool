module TriviaTool.Client.App

open Elmish
open Elmish.React
open TriviaTool.Client

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Fable.Core.JsInterop.importSideEffects "./style.css"

Program.mkProgram State.init State.update View.view
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactBatched "elmish-app"
|> Program.run
