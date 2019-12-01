```fsharp
[<Test>]
let ``temp`` () =
    formatSourceString false """module TriviaModule =

let env = "DEBUG"

type Config = {
    Name: string
    Level: int
}

let meh = { // this comment right
    Name = "FOO"; Level = 78 }

(* ending with block comment *)"""  config
    |> prepend newline
    |> should equal """
meh
"""
```