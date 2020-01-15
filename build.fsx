#r "paket: groupref build //"
#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core

Target.create "Temp" ignore

Target.runOrDefault "Temp"