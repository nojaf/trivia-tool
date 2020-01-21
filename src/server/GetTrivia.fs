namespace Company.Function

open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open System.IO

module GetTrivia =

    [<FunctionName("GetTrivia")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "post", Route = null)>] req: HttpRequest) (log: ILogger) =
        async {
            log.LogInformation("F# HTTP trigger function processed a request.")

            use stream = new StreamReader(req.Body)
            let! reqBody = stream.ReadToEndAsync() |> Async.AwaitTask

            log.LogInformation(sprintf "Body text: %s" reqBody)

            return OkObjectResult("Message received") :> IActionResult
        }
        |> Async.StartAsTask
