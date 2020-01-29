module TriviaTool.Client.Model

open TriviaTool.Shared

type ActiveTab =
    | ByTriviaNodes
    | ByTrivia

type Model =
    { ActiveTab: ActiveTab
      SourceCode: string
      Exception: exn option
      IsLoading: bool
      Trivia: Trivia list
      TriviaNodes: TriviaNode list
      ActiveByTriviaNodeIndex: int
      ActiveByTriviaIndex: int
      Defines: string }

type Msg =
    | SelectTab of ActiveTab
    | UpdateSourceCode of string
    | GetTrivia
    | TriviaReceived of ParseResult
    | NetworkError of exn
    | ActiveItemChange of ActiveTab * int
    | UpdateDefines of string
