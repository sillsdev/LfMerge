// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------
#r "paket: groupref build //"
#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api

// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "LfMerge"

let summary = function
    | "LfMerge" -> "Command line program to do Send/Receive for Language Forge"
    | "FixFwData" -> "Command line program to fix some problems in FieldWorks XML data files"
    | _ -> ""

let gitOwner = "sillsdev"
let gitName = "LfMerge"
let gitHome = "https://github.com/" + gitOwner

// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------

let buildDir  = "./tmp-build/"
let nugetDir  = "./out/"


System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
// let release = ReleaseNotes.parse (System.IO.File.ReadAllLines "RELEASE_NOTES.md")
let mutable versionInfo = System.Collections.Generic.Dictionary<string,obj>()
let getVersionInfo key =
    match versionInfo.TryGetValue key with
    | false, _ -> ""
    | true, value -> value.ToString()

// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------
let isNullOrWhiteSpace = System.String.IsNullOrWhiteSpace

let exec cmd args dir =
    let proc =
        CreateProcess.fromRawCommandLine cmd args
        |> CreateProcess.ensureExitCodeWithMessage (sprintf "Error while running '%s' with args: %s" cmd args)
    (if isNullOrWhiteSpace dir then proc
    else proc |> CreateProcess.withWorkingDirectory dir)
    |> Proc.run
    |> ignore

let getBuildParam = Environment.environVar
let DoNothing = ignore
// --------------------------------------------------------------------------------------
// Build Targets
// --------------------------------------------------------------------------------------

Target.create "Clean" (fun _ ->
    Shell.cleanDirs [buildDir; nugetDir]
)

Target.create "GitVersion" (fun _ ->
    let result =
        CreateProcess.fromRawCommand "dotnet" ["dotnet-gitversion"; "/output"; "json"]
        |> CreateProcess.redirectOutput
        |> Proc.run
    if result.ExitCode <> 0 then
        Trace.traceErrorfn "Error running GitVersion: %A" result.Result.Error
        failwith "Error running GitVersion: see logs above"
    Trace.logfn "GitVersion result: %s" result.Result.Output
    versionInfo <- System.Text.Json.JsonSerializer.Deserialize result.Result.Output

    // Calculate and add a Debian package version
    let preReleaseLabel = getVersionInfo "PreReleaseLabel"
    let preReleaseNumber = getVersionInfo "PreReleaseNumber"
    let preReleaseTag =
        if preReleaseLabel = "" || preReleaseNumber = ""
        then ""
        else sprintf "~%s.%s" preReleaseLabel preReleaseNumber
    versionInfo.["PreReleaseTag"] <- preReleaseTag
    let buildNumber =
        match BuildServer.buildServer with
        | Jenkins -> BuildServer.jenkinsBuildNumber
        | TeamCity -> BuildServer.tcBuildNumber
        | LocalBuild -> getVersionInfo "CommitsSinceVersionSource"
        | _ -> getVersionInfo "CommitsSinceVersionSource"
    versionInfo.["BuildNumber"] <- buildNumber
    let packageVersion = sprintf "%s%s.%s" (getVersionInfo "MajorMinorPatch") preReleaseTag buildNumber
    versionInfo.["PackageVersion"] <- packageVersion
    Trace.tracefn "Building packages for version %s" packageVersion

    let home = Environment.environVarOrNone "HOME"
    match home with
    | Some home -> Trace.tracefn "HOME is %s" home
    | None -> Trace.traceErrorfn "HOME env var *not found* (!) This will cause a NuGet error"
)

Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        let baseAttributes =
            [ AssemblyInfo.Title projectName
              AssemblyInfo.Product project
              AssemblyInfo.Description (summary projectName)
              AssemblyInfo.Company "SIL International"
              AssemblyInfo.Copyright "Copyright (c) 2016-2020 SIL International"
              AssemblyInfo.Version (getVersionInfo "AssemblySemVer")
              AssemblyInfo.FileVersion (getVersionInfo "AssemblySemFileVer") ]
        let extraAttributes =
            match projectName with
            | "LfMerge" ->
                [ AssemblyInfo.InternalsVisibleTo "LfMerge.Tests"
                  AssemblyInfo.InternalsVisibleTo "LfMerge.TestApp" ]
                //   AssemblyInfo.Attribute("AssemblyLicense", "\"This software is licensed under the MIT License (http://opensource.org/licenses/MIT)\"", "", "System.String") ]
            | "LfMerge.Core" ->
                [ AssemblyInfo.InternalsVisibleTo "LfMerge.Core.Tests"
                  AssemblyInfo.InternalsVisibleTo "LfMerge.TestApp" ]
            | "FixFwData" -> []
                // [ AssemblyInfo.Attribute("AssemblyLicense", "\"This software is licensed under the LGPL 2.1 (https://opensource.org/licenses/LGPL-2.1)\"", "", "System.String") ]
            | _ -> []
                // [ AssemblyInfo.Attribute("AssemblyLicense", "\"This software is licensed under the MIT License (http://opensource.org/licenses/MIT)\"", "", "System.String") ]
        baseAttributes @ extraAttributes

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
        match projFileName with
        | proj when proj.EndsWith("fsproj") -> AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
        | proj when proj.EndsWith("csproj") -> AssemblyInfoFile.createCSharp ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | proj when proj.EndsWith("vbproj") -> AssemblyInfoFile.createVisualBasic ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | _ -> ()
        )
)


// Target.create "Restore" (fun _ ->
//     DotNet.restore id ""
// )

Target.create "Build" (fun _ ->
    DotNet.build id ""
)

Target.create "Test" (fun _ ->
    exec "mono" "packages/NUnit.Runners.Net4/tools/nunit-console.exe /home/rmunn/code/LfMerge/output/Release/LfMerge.Tests.dll /home/rmunn/code/LfMerge/output/Release/LfMerge.Core.Tests.dll" "."
)

// --------------------------------------------------------------------------------------
// Release Targets
// --------------------------------------------------------------------------------------
Target.create "BuildRelease" (fun _ ->
    DotNet.build (fun p ->
        { p with
            Configuration = DotNet.BuildConfiguration.Release
            OutputPath = Some buildDir
            MSBuildParams = { p.MSBuildParams with Properties = [("Version", getVersionInfo "NuGetVersion"); ("PackageReleaseNotes", "")]}  // TODO: Release notes go here
        }
    ) "LfMerge.sln"
)


Target.create "Pack" (fun _ ->
    DotNet.pack (fun p ->
        { p with
            Configuration = DotNet.BuildConfiguration.Release
            OutputPath = Some nugetDir
            MSBuildParams = { p.MSBuildParams with Properties = [("Version", getVersionInfo "NuGetVersion"); ("PackageReleaseNotes", "")]}  // TODO: Release notes go here
        }
    ) "LfMerge.sln"
)

Target.create "ReleaseGitHub" (fun _ ->
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s" (getVersionInfo "NuGetVersion"))
    Git.Branches.pushBranch "" remote (Git.Information.getBranchName "")


    Git.Branches.tag "" (getVersionInfo "NuGetVersion")
    Git.Branches.pushTag "" remote (getVersionInfo "NuGetVersion")

    let client =
        let user =
            match getBuildParam "github-user" with
            | s when not (isNullOrWhiteSpace s) -> s
            | _ -> UserInput.getUserInput "Username: "
        let pw =
            match getBuildParam "github-pw" with
            | s when not (isNullOrWhiteSpace s) -> s
            | _ -> UserInput.getUserPassword "Password: "

        // Git.createClient user pw
        GitHub.createClient user pw
    let files = !! (nugetDir </> "*.nupkg")

    // release on github
    let cl =
        client
        |> GitHub.draftNewRelease gitOwner gitName (getVersionInfo "NuGetVersion") ((getVersionInfo "PreReleaseTag") <> "") Seq.empty  // TODO: Release notes go here in place of Seq.empty
    (cl,files)
    ||> Seq.fold (fun acc e -> acc |> GitHub.uploadFile e)
    |> GitHub.publishDraft//releaseDraft
    |> Async.RunSynchronously
)

Target.create "Push" (fun _ ->
    let key =
        match getBuildParam "nuget-key" with
        | s when not (isNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserPassword "NuGet Key: "
    Paket.push (fun p -> { p with WorkingDir = nugetDir; ApiKey = key }))

// --------------------------------------------------------------------------------------
// Build order
// --------------------------------------------------------------------------------------
Target.create "Default" DoNothing
Target.create "Release" DoNothing

"Clean"
  ==> "GitVersion"
  ==> "AssemblyInfo"
//   ==> "Restore"
  ==> "Build"
  ==> "Test"
  ==> "Default"

"Clean"
 ==> "GitVersion"
 ==> "AssemblyInfo"
//  ==> "Restore"
 ==> "BuildRelease"

"Default"
  ==> "Pack"
  ==> "ReleaseGitHub"
  ==> "Push"
  ==> "Release"

Target.runOrDefault "Default"
