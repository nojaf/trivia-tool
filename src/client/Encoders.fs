module TriviaTool.Client.Encoders

open Thoth.Json
open TriviaTool.Shared

let encodeParseRequest pr =
    Encode.object
        [ "sourceCode", Encode.string pr.SourceCode
          "defines", List.map Encode.string pr.Defines |> Encode.list
          "fileName", Encode.string pr.FileName ]
