module TriviaTool.Client.Model

type ActiveTab =
    | ByTrivia
    | ByContent

type Model =
    { ActiveTab: ActiveTab }

type Msg = SelectTab of ActiveTab
