#r "paket: groupref build //"
#load ".fake/build.fsx/intellisense.fsx"

open System.IO
open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fantomas.FormatConfig
open Fantomas
open Fake.JavaScript

let pwd = Shell.pwd()
let clientDir = pwd </> "src" </> "client"
let yarnSetParams = (fun (c: Yarn.YarnParams) -> { c with WorkingDirectory = clientDir })

let fantomasConfig =
    match CodeFormatter.ReadConfiguration(Shell.pwd()) with
    | Success c -> c
    | _ -> failwith "Cannot parse fantomas-config.json"

let fsharpFiles = !!"src/**/*.fs" -- "src/**/obj/**" -- "src/**/node_modules/**" -- "src/**/.fable/**"

Target.create "Format" (fun _ ->
    fsharpFiles
    |> FakeHelpers.formatCode fantomasConfig
    |> Async.RunSynchronously
    |> printfn "Formatted F# files: %A"

    Yarn.exec "prettier webpack.config.js --write" yarnSetParams)

let removeTemporary (results: FakeHelpers.FormatResult []): unit =
    let removeIfHasTemporary result =
        match result with
        | FakeHelpers.Formatted(_, tempFile) -> File.Delete(tempFile)
        | FakeHelpers.Error(_)
        | FakeHelpers.Unchanged(_) -> ()
    results |> Array.iter removeIfHasTemporary

let checkCodeAndReport (config: FormatConfig) (files: seq<string>): Async<string []> =
    async {
        let! results = files |> FakeHelpers.formatFilesAsync config
        results |> removeTemporary

        let toChange result =
            match result with
            | FakeHelpers.Formatted(file, _) -> Some(file, None)
            | FakeHelpers.Error(file, ex) -> Some(file, Some(ex))
            | FakeHelpers.Unchanged(_) -> None

        let changes =
            results |> Array.choose toChange

        let isChangeWithErrors =
            function
            | _, Some(_) -> true
            | _, None -> false

        if Array.exists isChangeWithErrors changes then raise <| FakeHelpers.CodeFormatException changes

        let formattedFilename =
            function
            | _, Some(_) -> None
            | filename, None -> Some(filename)

        return changes |> Array.choose formattedFilename
    }

Target.create "CheckCodeFormat" (fun _ ->
    let needFormatting =
        fsharpFiles
        |> checkCodeAndReport fantomasConfig
        |> Async.RunSynchronously

    match Array.length needFormatting with
    | 0 -> Trace.log "No files need formatting"
    | _ ->
        Trace.log "The following files need formatting:"
        needFormatting |> Array.iter Trace.log
        failwith "Some files need formatting, check output for more info"

    Yarn.exec "prettier webpack.config.js --check" yarnSetParams)

Target.create "Yarn" (fun _ -> Yarn.installFrozenLockFile yarnSetParams)

"Yarn" ==> "Format"

"Yarn" ==> "CheckCodeFormat"

Target.runOrDefault "CheckCodeFormat"
