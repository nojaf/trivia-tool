module TriviaTool.Client.View

open Fable.Core.JsInterop
open Fable.React
open Fable.React.Props
open Reactstrap
open Reactstrap
open TriviaTool.Client.Model

let private navigation =
    Navbar.navbar [Navbar.Light true; Navbar.Custom [ClassName "bg-light"]] [
        NavbarBrand.navbarBrand [NavbarBrand.Custom [ClassName "py-0"]] [str "Trivia tool"]
        div [ClassName "navbar-text py1"] [
            Button.button [ Button.Custom [Href "https://github.com/nojaf/trivia-tool"
                                           Target "_blank"
                                           ClassName "text-white"]
                            Button.Color Dark ] [
                i [ClassName "fab fa-github mr-1 mt-1"] []
                str "GitHub"
            ]
        ]
    ]

let private settings model dispatch =
    let fileExtensionButton onClick active label =
        let className = if active then "rounded-0 text-white" else "rounded-0"
        Button.button [ Button.Custom [ClassName className
                                       OnClick onClick]
                        Button.Outline (not active) ] [str label]

    Col.col [ Col.Xs (Col.mkCol !^4); Col.Custom [ ClassName "border-right h-100 d-flex flex-column" ] ] [
        div [Id "source"; ClassName "flex-grow-1"] [
            textarea [ClassName "h-100 w-100"] []
        ]
        form [Id "settings"] [
            div [ClassName "d-flex py-1"] [
                Input.input [Input.Custom [Placeholder "Enter your defines separated with a space"
                                           ClassName "rounded-0 d-inline-block"]]
                div [ClassName "d-inline-block"] [
                    ButtonGroup.buttonGroup [ButtonGroup.Custom [ClassName "btn-group-toggle"]] [
                        fileExtensionButton ignore true "*.fs"
                        fileExtensionButton ignore false "*.fsi"
                    ]
                ]
            ]
            Button.button [Button.Color Primary
                           Button.Custom [ClassName "w-100 rounded-0"]] [
                i [ClassName "fas fa-code mr-1"] []
                str "Get tokens"
            ]
        ]
    ]

let private loader model =
    div [Id "loader"; ClassName "d-none"] [
        div [ClassName "inner"] [
           Spinner.spinner [ Spinner.Color Primary ] []
        ]
    ]

let private tabToId tab =
    match tab with
    | ByTrivia -> "trivia"
    | ByContent -> "content"

let private tab activeTab tabType tabContent =
    let tabClassName =
        match activeTab with
        | t when (t = tabType) -> "active show"
        | _ -> System.String.Empty
        |> sprintf "fade h-100 %s"

    TabPane.tabPane [ TabPane.TabId (!^(tabToId tabType))
                      TabPane.Custom [ClassName tabClassName] ] [
        tabContent
    ]



let private byTrivia model dispatch =
    tab model.ActiveTab ByTrivia (str "trivia tab")

let private byContent model dispatch =
    tab model.ActiveTab ByContent (str "content tab")

let private results model dispatch =
    let tabHeader label tabType =
        let isActive = tabType = model.ActiveTab
        NavItem.navItem [ NavItem.Custom [OnClick (fun _ -> dispatch (Msg.SelectTab tabType))
                                          ClassName "pointer"] ] [
            NavLink.navLink [NavLink.Active isActive; NavLink.Custom [ClassName "rounded-0"]] [
                str label
            ]
        ]



    Col.col [ Col.Xs (Col.mkCol !^8); Col.Custom [ClassName "h-100"] ] [
        loader model
        div [ClassName "h-100 d-flex flex-column"; Id "results"] [
            Nav.nav [ Nav.Tabs true; Nav.Pills true
                      Nav.Custom [ClassName "border-bottom border-primary"] ] [
                tabHeader "By trivia" ByTrivia
                tabHeader "By content" ByContent
            ]
            TabContent.tabContent [TabContent.Custom [ClassName "flex-grow-1"]
                                   TabContent.ActiveTab (!^(tabToId model.ActiveTab)) ] [
                byTrivia model dispatch
                byContent model dispatch
            ]
        ]
    ]

let view model dispatch =
    div [ClassName "d-flex flex-column h-100"] [
        navigation
        main [ClassName "flex-grow-1"] [
            Row.row [Row.Custom [ClassName "h-100 no-gutters"]] [
                settings model dispatch
                results model dispatch
            ]
        ]
    ]