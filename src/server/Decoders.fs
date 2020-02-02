module TriviaTool.Server.Decoders

open Thoth.Json.Net
open TriviaTool.Shared

let private parseRequestDecoder: Decoder<ParseRequest> =
    Decode.object (fun get ->
        { SourceCode = get.Required.Field "sourceCode" Decode.string
          Defines = get.Required.Field "defines" (Decode.list Decode.string)
          FileName = get.Required.Field "fileName" Decode.string
          KeepNewlineAfter = get.Required.Field "keepNewlineAfter" Decode.bool })

let decodeParseRequest value =
    Decode.fromString parseRequestDecoder value
