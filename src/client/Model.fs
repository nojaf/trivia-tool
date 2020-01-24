module TriviaTool.Client.Model

open TriviaTool.Shared

type ActiveTab =
    | ByTrivia
    | ByContent

type Model =
    { ActiveTab: ActiveTab
      SourceCode: string
      Exception: exn option
      IsLoading: bool
      Trivia: Trivia list
      TriviaNodes: TriviaNode list }

type Msg =
    | SelectTab of ActiveTab
    | UpdateSourceCode of string
    | GetTrivia
    | TriviaReceived of ParseResult
    | NetworkError of exn
