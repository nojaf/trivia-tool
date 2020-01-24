module TriviaTool.Server.Encoders

open FSharp.Compiler.Range
open Thoth.Json.Net
open Fantomas.TriviaTypes
open TriviaTool

let private mapToSharedType (tnt: TriviaNodeType) =
    match tnt with
    | MainNode mn -> Shared.MainNode mn
    | Token t -> Shared.Token t.TokenInfo.TokenName

let private typeEncoder = Encode.Auto.generateEncoder<Shared.TriviaNodeType>()

let private encodeTriviaNodeType = mapToSharedType >> typeEncoder

let private mapToComment comment =
    match comment with
    | LineCommentAfterSourceCode c -> Shared.LineCommentAfterSourceCode c
    | LineCommentOnSingleLine c -> Shared.LineCommentOnSingleLine c
    | BlockComment(c, nb, na) -> Shared.BlockComment(c, nb, na)

let private mapToTriviaContent (tc: TriviaContent) =
    match tc with
    | Keyword kw -> Shared.Keyword kw.TokenInfo.TokenName
    | Number n -> Shared.Number n
    | StringContent sc -> Shared.StringContent sc
    | IdentOperatorAsWord i -> Shared.IdentOperatorAsWord i
    | IdentBetweenTicks i -> Shared.IdentBetweenTicks i
    | Comment c -> Shared.Comment(mapToComment c)
    | Newline -> Shared.Newline
    | Directive d -> Shared.Directive d
    | NewlineAfter -> Shared.NewlineAfter

let private triviaContentEncoder = Encode.Auto.generateEncoder<Shared.TriviaContent>()
let private encodeTriviaContent = mapToTriviaContent >> triviaContentEncoder

let private encodeRange (range: range) =
    Encode.object
        [ "startLine", Encode.int range.Start.Line
          "startColumn", Encode.int range.Start.Column
          "endLine", Encode.int range.End.Line
          "endColumn", Encode.int range.End.Column ]

let private encodeTriviaNode (tn: TriviaNode) =
    Encode.object
        [ "type", encodeTriviaNodeType tn.Type
          "contentBefore", List.map encodeTriviaContent tn.ContentBefore |> Encode.list
          "contentItself", Option.map mapToTriviaContent tn.ContentItself |> Encode.option triviaContentEncoder
          "contentAfter", List.map encodeTriviaContent tn.ContentAfter |> Encode.list
          "range", encodeRange tn.Range ]

let private encodeTrivia (t: Trivia) =
    Encode.object
        [ "item", encodeTriviaContent t.Item
          "range", encodeRange t.Range ]

let encodeParseResult trivia triviaNodes =
    Encode.object
        [ "trivia", List.map encodeTrivia trivia |> Encode.list
          "triviaNodes", List.map encodeTriviaNode triviaNodes |> Encode.list ]
    |> Encode.toString 4
