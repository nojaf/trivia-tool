#r "paket: groupref build //"
#load ".fake/build.fsx/intellisense.fsx"

open System
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
let clientProject = clientPath </> "client.fsproj"
let serverPath = pwd </> "src" </> "server"
let serverProject = (serverPath </> "server.fsproj")

let publishPath = pwd </> "deploy"
let yarnSetParams = (fun (c: Yarn.YarnParams) -> { c with WorkingDirectory = clientPath })

let fantomasConfig =
    match CodeFormatter.ReadConfiguration(Shell.pwd()) with
    | Success c -> c
    | _ -> failwith "Cannot parse fantomas-config.json"

let fsharpFiles = !!"src/**/*.fs" -- "src/**/obj/**" -- "src/**/node_modules/**" -- "src/**/.fable/**"
let javaScriptFiles = ["webpack.config.js"; "js/*.js"]

Target.create "Clean" (fun _ ->
    Shell.rm_rf (clientPath </> ".fable")
    Shell.rm_rf (clientPath </> "bin")
    Shell.rm_rf (clientPath </> "obj")
    Shell.rm_rf (serverPath </> "bin")
    Shell.rm_rf (serverPath </> "obj")
    Shell.rm_rf publishPath)

Target.create "Restore" (fun _ ->
    DotNet.restore id serverProject
    DotNet.restore id clientProject)

Target.create "Format" (fun _ ->
    try
        fsharpFiles
        |> FakeHelpers.formatCode fantomasConfig
        |> Async.RunSynchronously
        |> printfn "Formatted F# files: %A"
    with
    | exn ->
        printfn "%A" exn

    javaScriptFiles
    |> List.iter (fun js -> Yarn.exec (sprintf "prettier %s --write" js) yarnSetParams))

Target.create "CheckCodeFormat" (fun _ ->
    let result =
        fsharpFiles
        |> FakeHelpers.checkCode fantomasConfig
        |> Async.RunSynchronously

    if result.IsValid then
        Trace.log "No files need formatting"
    elif result.NeedsFormatting then
        Trace.log "The following files need formatting:"
        List.iter Trace.log result.Formatted
        failwith "Some files need formatting, check output for more info"
    else
        Trace.logf "Errors while formatting: %A" result.Errors

    javaScriptFiles
    |> List.iter (fun js -> Yarn.exec (sprintf "prettier %s --check" js) yarnSetParams))

Target.create "Yarn" (fun _ -> Yarn.installFrozenLockFile yarnSetParams)

Target.create "BuildClient" (fun _ ->
    let functionUrl = sprintf "https://%s.azurewebsites.net" (Environment.environVar "AZ_FUNCTIONAPP")
    Environment.setEnvironVar "BACKEND" functionUrl
    Yarn.exec "build" yarnSetParams)

Target.create "BuildServer" (fun _ ->
    DotNet.build (fun config -> { config with Configuration = DotNet.BuildConfiguration.Release }) serverProject)

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

    let func parameters =
        let funcPath = ProcessUtils.findPath [] "func"
        CreateProcess.fromRawCommand funcPath parameters
        |> CreateProcess.withWorkingDirectory serverPath
        |> Proc.run
        |> ignore

Target.create "DeployServer" (fun _ ->
    let resourceGroup = Environment.environVar "AZ_RESOURCE_GROUP"
    let armFile = pwd </> "infrastructure" </> "azuredeploy.json"
    let functionappName = Environment.environVar "AZ_FUNCTIONAPP"
    let serverFarmName = Environment.environVar "AZ_SERVERFARM"
    let applicationInsightsName = Environment.environVar "AZ_APPINSIGHTS"
    let storageName = Environment.environVar "AZ_STORAGE"
    let corsUrl = Environment.environVar "AZ_CORS"

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

    DotNet.publish (fun config -> { config with
                                        Configuration = DotNet.BuildConfiguration.Release
                                        OutputPath = Some publishPath }) serverProject

    Zip.createZip "./deploy" "func.zip" "" Zip.DefaultZipLevel false (!! "./deploy/*.*" ++ "./deploy/**/*.*")
    Shell.mv "func.zip" "./deploy/func.zip"

    Azure.az ["functionapp";"deployment";"source";"config-zip";"-g";resourceGroup;"-n";functionappName;"--src";"./deploy/func.zip"]
)

Target.create "Deploy" ignore

Target.create "Watch" (fun _ ->
    let azFuncPort = Environment.environVarOrDefault "AZFUNC_PORT" "8099"
    let cors = sprintf "http://localhost:%s" (Environment.environVarOrDefault "FRONTEND_PORT" "8080")

    let compileFable = async { do Yarn.exec "start" yarnSetParams }

    let stopFunc() = System.Diagnostics.Process.GetProcessesByName("func") |> Seq.iter (fun p -> p.Kill())

    let rec startFunc() =
        let dirtyWatcher: IDisposable ref = ref null

        let watcher =
            !!(serverPath </> "*.fs") ++ (serverPath </> "*.fsproj")
            |> ChangeWatcher.run (fun changes ->
                printfn "FILE CHANGE %A" changes
                if !dirtyWatcher <> null then
                    (!dirtyWatcher).Dispose()
                    stopFunc()
                    startFunc())

        dirtyWatcher := watcher

        Azure.func ["start"; "-p";azFuncPort; "--cors"; cors]

    let runAzureFunction = async { startFunc() }

    Async.Parallel [ runAzureFunction; compileFable ]
    |> Async.Ignore
    |> Async.RunSynchronously
)

"Yarn" ==> "Format"
"Yarn" ==> "CheckCodeFormat"
"Yarn" ==> "BuildClient"
"CheckCodeFormat" ==> "BuildClient"
"CheckCodeFormat" ==> "BuildServer"
"BuildClient" ==> "Build"
"BuildServer" ==> "Build"
"BuildClient" ==> "DeployClient"
"DeployServer" ==> "DeployClient" ==> "Deploy"

"Clean" ==> "CheckCodeFormat" ==> "Build"

Target.runOrDefault "Build"
