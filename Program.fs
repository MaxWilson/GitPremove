open System
open System.IO

let ignoreFileEntry (fileOrDirectoryName:string) =
    match (Path.GetFileName fileOrDirectoryName).ToLowerInvariant() with
    | "bin" | "obj" -> true
    | x when x.StartsWith "." || x.EndsWith "dll" || x.EndsWith "exe" -> true
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
        proc.ExitCode, out.Result, err.Result
    else
        failwith $"Couldn't execute {cmd} {args} in {workingDirectory}"

shellExecute @"git" "status RetailSDK\RetailServer\MSE.D365.RetailServer.Extensions\MSE.D365.RetailServer.Extensions\SalesOrderController.cs" @"c:\mse\MSE.D365.FnO\"

let finalTemplate = getFilesRecursively "c:\mse\MSE.D365.FnO\CommerceSDK" // this is where we want all our files to end up
// now do a git checkout of the original code