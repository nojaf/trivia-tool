module TriviaTool.Client.ByTriviaNodes

open TriviaTool.Client.Model
open Fable.Core.JsInterop
open Fable.React
open Fable.React.Props
open Reactstrap
open TriviaTool.Shared

let private typeName =
    function
    | TriviaNodeType.Token t -> t
    | TriviaNodeType.MainNode mn -> mn

let private typeTitle =
    function
    | TriviaNodeType.Token _ -> "Token"
    | TriviaNodeType.MainNode _ -> "MainNode"

let private rangeToText (r: Range) = sprintf "(%i,%i - %i,%i)" r.StartLine r.StartColumn r.EndLine r.EndColumn

let private rangeToBadge (r: Range) =
    Badge.badge
        [ Badge.Color Dark
          Badge.Custom [ ClassName "px-2 py-1 ml-auto" ] ] [ (rangeToText r |> str) ]

let private isNotAnEmptyList = List.isEmpty >> not

let private triviaContentToDetail tc =
    let wrap outer inner =
        [ str (sprintf "%s(" outer)
          code [] [ str inner ]
          str ")" ]

    match tc with
    | Newline -> str "Newline"
    | StringContent sc -> fragment [] (wrap "StringContent" sc)
    | Comment(c) ->
        match c with
        | BlockComment(bc, _, _) -> (wrap "BlockComment" bc)
        | LineCommentOnSingleLine(lc) -> (wrap "LineCommentOnSingleLine" lc)
        | LineCommentAfterSourceCode(lc) -> (wrap "LineCommentAfterSourceCode" lc)
        |> fun inner ->
            fragment []
                [ str "Comment("
                  yield! inner
                  str ")" ]
    | Directive d -> fragment [] (wrap "Directive" d)
    | IdentOperatorAsWord ioaw -> fragment [] (wrap "IdentOperatorAsWord" ioaw)
    | IdentBetweenTicks ibt -> fragment [] (wrap "IdentBetweenTicks" ibt)
    | Number n -> fragment [] (wrap "Number" n)
    | NewlineAfter -> str "NewlineAfter"
    | Keyword kw -> fragment [] (wrap "Keyword" kw)



let private activeTriviaNode tn =
    let title = sprintf "%s %s" (typeName tn.Type) (rangeToText tn.Range)

    let contentInfo title items =
        if (isNotAnEmptyList items) then
            let listItems =
                items |> List.mapi (fun idx item -> li [ Key !!idx ] [ triviaContentToDetail item ])

            fragment []
                [ h4 [] [ str title ]
                  ul [ ClassName "list-unstyled" ] [ ofList listItems ] ]
        else
            ofOption None

    div [ ClassName "tab-pane active" ]
        [ h2 [ ClassName "mb-4" ] [ str title ]
          contentInfo "Content before" tn.ContentBefore
          contentInfo "Content itself" (Option.toList tn.ContentItself)
          contentInfo "Content after" tn.ContentAfter ]

let view (model: Model) dispatch =
    let navItems =
        model.TriviaNodes
        |> List.mapi (fun idx tn ->
            let className =
                match tn.Type with
                | TriviaNodeType.Token _ -> "nav-link-token"
                | TriviaNodeType.MainNode _ -> "nav-link-main-node"
                |> sprintf "d-flex %s %s" (if idx = model.ActiveByTriviaNodeIndex then "active" else "")

            NavItem.navItem
                [ NavItem.Custom
                    [ Key !!idx
                      Title(typeTitle tn.Type)
                      OnClick(fun ev ->
                          ev.preventDefault()
                          dispatch (ActiveItemChange(ByTriviaNodes, idx))) ] ]
                [ NavLink.navLink
                    [ NavLink.Custom
                        [ Href "#"
                          ClassName className ] ]
                      [ span [ ClassName "mr-4" ] [ str (typeName tn.Type) ]
                        rangeToBadge tn.Range ] ])

    let activeNode =
        List.tryItem model.ActiveByTriviaNodeIndex model.TriviaNodes |> Option.map activeTriviaNode

    div [ ClassName "d-flex h-100" ]
        [ Nav.nav
            [ Nav.Pills true
              Nav.Custom [ ClassName "flex-column" ] ] [ ofList navItems ]
          div [ ClassName "bg-light flex-grow-1 py-2 px-4 tab-content overflow-auto" ] [ ofOption activeNode ] ]
