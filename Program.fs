open System
open System.IO

let ignoreFileEntry (fileOrDirectoryName:string) =
    match (Path.GetFileName fileOrDirectoryName).ToLowerInvariant() with
    | "bin" | "obj" | "node_modules" | "download" -> true
    | x when x.StartsWith "." || x.EndsWith "dll" || x.EndsWith "exe" || x.EndsWith "map" || x.Contains ("sample") -> true
    | ("app" | "web") when fileOrDirectoryName.Contains @"\POS\" -> true
    | x when x.EndsWith "resources.resjson" && not (x.EndsWith @"strings\en-us\resources.resjson") -> true
    | _ -> false

let rec getFilesRecursively (rootDirectoryPath: string) =
    if ignoreFileEntry rootDirectoryPath || not (Directory.Exists rootDirectoryPath) then []
    else [
        for file in Directory.EnumerateFiles rootDirectoryPath do
            if ignoreFileEntry file |> not then
                file
        for dir in Directory.EnumerateDirectories rootDirectoryPath do
            if ignoreFileEntry dir |> not then
                yield! getFilesRecursively dir
        ]

let shellExecute cmd args workingDirectory =
    let startInfo = System.Diagnostics.ProcessStartInfo(cmd,args,CreateNoWindow=true,UseShellExecute=false,WorkingDirectory=System.IO.Path.GetFullPath workingDirectory,RedirectStandardOutput=true,RedirectStandardError=true)
    use proc = new System.Diagnostics.Process(StartInfo=startInfo)
    if proc.Start() then
        printfn $"Executing {cmd} {args} in {workingDirectory}"
        let err = System.Threading.Tasks.Task.Run(fun () -> proc.StandardError.ReadToEnd())
        let out = System.Threading.Tasks.Task.Run(fun () -> proc.StandardOutput.ReadToEnd())
        System.Threading.Tasks.Task.WaitAll(err, out)
        proc.WaitForExit()
        let retval = proc.ExitCode, out.Result, err.Result
        proc.Close()
        retval
    else
        failwith $"Couldn't execute {cmd} {args} in {workingDirectory}"

type Origin = Unique of string | Ambiguous of string list | New

let getMatchLength (lhs: _ array) (rhs: _ array) =
    let rec loop ix =
        if ix >= lhs.Length || ix >= rhs.Length || lhs[ix] <> rhs[ix] then ix
        else ix + 1 |> loop
    loop 0

let specificity (src:string) (dest:string) =
    getMatchLength (src.ToCharArray() |> Array.rev) (dest.ToCharArray() |> Array.rev)

let compare src dest =
    let srcByName =
        let getFileName (file:string) = Path.GetFileName file
        src |> List.groupBy getFileName |> Map.ofList
    // try to find a unique src for every dest
    [for (destFile:string) in dest do
        let fileName = Path.GetFileName destFile
        let origin =
            match srcByName |> Map.tryFind fileName with
            | None -> New
            | Some [x] -> Unique x
            | Some xs ->
                match xs |> List.groupBy (specificity destFile) |> List.sortByDescending fst with
                | (specificity, [x])::_ -> Unique x
                | (specificity, xs)::_ -> Ambiguous xs
                | _ -> Ambiguous xs
        destFile, origin
        ]

let gitDirectory = @"c:\mse\MSE.D365.FnO\"
let destBranch = "users/maxw/newExtensionModel"
let destSubdirectory = "CommerceSDK"
let destDirectory = Path.Combine(gitDirectory, destSubdirectory)
let srcBranch = "dev/commerceSDK/premerge"
let srcSubdirectories = ["RetailSDK\POS";"RetailSDK\RetailServer";"RetailSDK\Database"]
let srcDirectories = srcSubdirectories |> List.map (fun src -> Path.Combine(gitDirectory, src))
shellExecute @"git" $"checkout {destBranch}" gitDirectory
let destTemplate = getFilesRecursively destDirectory // this is where we want all our files to end up
// now do a git checkout of the original code
shellExecute @"git" $"checkout {srcBranch}" gitDirectory
let srcFiles = srcDirectories |> List.map getFilesRecursively |> List.concat // this is where we want all our files to end up
let diff = compare srcFiles destTemplate
let mutable count = 0
for line in diff do
    match line with
    | dest, Unique src ->
        let destDir = Path.GetDirectoryName dest
        if Directory.Exists destDir |> not then
            Directory.CreateDirectory destDir |> ignore
        let gitExecute cmd =
            match shellExecute @"git" cmd gitDirectory with
            | 0, _, _ -> ()
            | errNo, out, err ->
                failwith $"git {cmd}: {errNo}\n{out}\n{err}"
        gitExecute $"""mv "{src}" "{dest}" """
        count <- count + 1
        if count % 30 = 0 then
            gitExecute $"""commit -m "Pre-move #{count/30}" -m "Pre-move some files to preserve git history" """
    | _ -> ()
for line in diff do
    match line with
    | file, Ambiguous srcs when file.EndsWith "js" = false ->
        printfn $"{file} {srcs.Length}"
        for src in srcs do
            printfn $"    {src}"
    | _ -> ()
