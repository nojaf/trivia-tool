namespace TriviaTool.Server

open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open System.IO
open FSharp.Compiler.SourceCodeServices
open Fantomas
open Fantomas.FormatConfig
open System.Net
open System.Net.Http

module GetTrivia =

    let private sendJson json =
        new HttpResponseMessage(HttpStatusCode.OK,
                                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"))

    let private sendText text =
        new HttpResponseMessage(HttpStatusCode.OK,
                                Content = new StringContent(text, System.Text.Encoding.UTF8, "application/text"))

    let private sendInternalError err =
        new HttpResponseMessage(HttpStatusCode.InternalServerError,
                                Content = new StringContent(err, System.Text.Encoding.UTF8, "application/text"))

    let private getProjectOptionsFromScript file source defines (checker: FSharpChecker) =
        async {
            let otherFlags =
                defines
                |> Seq.map (fun d -> sprintf "-d:%s" d)
                |> Seq.toArray

            let! (opts, _) = checker.GetProjectOptionsFromScript
                                 (file, source, otherFlags = otherFlags, assumeDotNetFramework = true)
            return opts
        }


    let private collectAST (log: ILogger) defines source =
        async {
            let fileName = "script.fsx"
            let sourceText = FSharp.Compiler.Text.SourceText.ofString (source)
            let checker = FSharpChecker.Create(keepAssemblyContents = false)
            let! checkOptions = getProjectOptionsFromScript fileName sourceText defines checker
            let parsingOptions = checker.GetParsingOptionsFromProjectOptions(checkOptions) |> fst
            let! ast = checker.ParseFile(fileName, sourceText, parsingOptions)

            match ast.ParseTree with
            | Some tree -> return (Result.Ok tree)
            | None ->
                log.LogError
                    (sprintf "Error file getting project options:\nSource:\n%s\n\nErrors:\n%A" source ast.Errors)
                return Error ast.Errors
        }

    [<FunctionName("GetTrivia")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "post", Route = null)>] req: HttpRequest) (log: ILogger) =
        async {
            log.LogInformation("F# HTTP trigger function processed a request.")

            use stream = new StreamReader(req.Body)
            let! reqBody = stream.ReadToEndAsync() |> Async.AwaitTask

            log.LogInformation(sprintf "Body text: %s" reqBody)

            let content = reqBody
            let defines = []
            let (tokens, lineCount) = TokenParser.tokenize defines content
            let! astResult = collectAST log defines content

            match astResult with
            | Result.Ok ast ->
                let trivia = Trivia.collectTrivia FormatConfig.Default tokens lineCount ast
                return sendText (sprintf "%A" trivia)
            | Error err ->
                return sendInternalError (sprintf "%A" err)
        }
        |> Async.StartAsTask
