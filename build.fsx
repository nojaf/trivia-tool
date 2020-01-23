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
open Fake.DotNet

let pwd = Shell.pwd()
let clientPath = pwd </> "src" </> "client"
let serverPath = pwd </> "src" </> "server"
let yarnSetParams = (fun (c: Yarn.YarnParams) -> { c with WorkingDirectory = clientPath })

let fantomasConfig =
    match CodeFormatter.ReadConfiguration(Shell.pwd()) with
    | Success c -> c
    | _ -> failwith "Cannot parse fantomas-config.json"

let fsharpFiles = !!"src/**/*.fs" -- "src/**/obj/**" -- "src/**/node_modules/**" -- "src/**/.fable/**"

Target.create "Clean" (fun _ ->
    Shell.rm_rf (clientPath </> ".fable")
    Shell.rm_rf (clientPath </> "bin")
    Shell.rm_rf (clientPath </> "obj")
    Shell.rm_rf (serverPath </> "bin")
    Shell.rm_rf (serverPath </> "obj"))

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

Target.create "BuildClient" (fun _ -> Yarn.exec "build" yarnSetParams)

Target.create "BuildServer" (fun _ ->
    DotNet.build (fun config -> { config with Configuration = DotNet.BuildConfiguration.Release })
        (serverPath </> "server.fsproj"))

Target.create "Build" ignore

Target.create "DeployClient" (fun _ ->
    let repo = sprintf "https://x-access-token:%s@github.com/nojaf/trivia-tool.git" (Environment.environVar "GH_TOKEN")
    let command = sprintf "deploy -u \"%s\" --repo \"%s\"" "github-actions-bot <support+actions@github.com>" repo
    Yarn.exec command yarnSetParams)


module Azure =
    let az parameters =
        let azPath = ProcessUtils.findPath [] "az"
        CreateProcess.fromRawCommand azPath parameters
        |> Proc.run
        |> ignore

Target.create "DeployServer" (fun _ ->
    let userName = Environment.environVar "AZ_USER_NAME"
    let resourceGroup = Environment.environVar "AZ_RESOURCE_GROUP"
    let password = Environment.environVar "AZ_PASSWORD"
    let tenant = Environment.environVar "AZ_TENANT"
    let armFile = pwd </> "infrastructure" </> "azuredeploy.json"
    let functionappName = Environment.environVar "AZ_FUNCTIONAPP"
    let serverFarmName = Environment.environVar "AZ_SERVERFARM"
    let applicationInsightsName = Environment.environVar "AZ_APPINSIGHTS"
    let storageName = Environment.environVar "AZ_STORAGE"
    let corsUrl = Environment.environVar "AZ_CORS"

    Azure.az ["login";"--service-principal"
              "--username";userName
              "--password"; password
              "--tenant";tenant]

    Azure.az ["group";"deployment"; "validate";"-g"
              resourceGroup; "--template-file"; armFile
              "--parameters"; (sprintf "functionappName=%s" functionappName)
              "--parameters"; (sprintf "serverFarmName=%s" serverFarmName)
              "--parameters"; (sprintf "applicationInsightsName=%s" applicationInsightsName)
              "--parameters"; (sprintf "storageName=%s" storageName)
              "--parameters"; (sprintf "appUrl=%s" corsUrl)]

    Azure.az ["group";"deployment"; "create";"-g"
              resourceGroup; "--template-file"; armFile
              "--parameters"; (sprintf "functionappName=%s" functionappName)
              "--parameters"; (sprintf "serverFarmName=%s" serverFarmName)
              "--parameters"; (sprintf "applicationInsightsName=%s" applicationInsightsName)
              "--parameters"; (sprintf "storageName=%s" storageName)
              "--parameters"; (sprintf "appUrl=%s" corsUrl)]

    // TODO deploy application
)

"Yarn" ==> "Format"
"Yarn" ==> "BuildClient"
"BuildClient" ==> "Build"
"BuildServer" ==> "Build"
"BuildClient" ==> "DeployClient"

"Clean" ==> "Yarn" ==> "CheckCodeFormat" ==> "Build"

Target.runOrDefault "Build"
