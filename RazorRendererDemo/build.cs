using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

// Browser-extension builder for a Blazor/SpawnJS WASM app. Pure C# - replaces the old .bat pair.
//
//   dotnet run --file build.cs                 (Release, zipped)
//   dotnet run --file build.cs -- Debug        (Debug, zipped)
//   dotnet run --file build.cs -- Release --no-zip
//
// Run from the extension project folder (the one holding the .csproj + wwwroot/).
//
// Pipeline (per browser found as wwwroot/manifest.<browser>.json):
//   1. dotnet publish the project.
//   2. Assemble  <out>/<browser>/  with the app under app/ and manifest.json at the root.
//   3. Merge manifest.<browser>.json onto the base manifest (System.Text.Json, indented,
//      no HTML-escaping).
//   4. Zip with System.IO.Compression.ZipFile -> FORWARD-SLASH entry paths.
//      (PowerShell Compress-Archive writes backslashes, which Firefox 404s on every
//       subfolder file: "Loading failed for the <script> moz-extension://.../app/x.js".)

var configuration = "Release";
var zip = true;
foreach (var a in args)
{
    if (a.Equals("--no-zip", StringComparison.OrdinalIgnoreCase)) zip = false;
    else if (!a.StartsWith("--")) configuration = a;
}

var projectDir = Directory.GetCurrentDirectory();
var csproj = Directory.GetFiles(projectDir, "*.csproj").FirstOrDefault();
if (csproj is null)
{
    Console.Error.WriteLine($"No .csproj found in {projectDir}. Run this from the extension project folder.");
    return 1;
}

var outputPath = Path.Combine(projectDir, "bin", $"Publish{configuration}");
var wwwroot = Path.Combine(outputPath, "wwwroot");
var baseManifest = Path.Combine(wwwroot, "manifest.json");

// 1. Publish
Console.WriteLine($"Publishing {Path.GetFileName(csproj)} ({configuration})...");
if (Directory.Exists(outputPath)) Directory.Delete(outputPath, recursive: true);
if (Run("dotnet", $"publish \"{csproj}\" --nologo --configuration {configuration} --output \"{outputPath}\"") != 0)
{
    Console.Error.WriteLine("dotnet publish failed.");
    return 1;
}
if (!File.Exists(baseManifest))
{
    Console.Error.WriteLine($"Base manifest not found at {baseManifest}.");
    return 1;
}

// 2/3/4. One target per browser-specific manifest; fallback to a generic 'browser' target with no merge.
var specificManifests = Directory.GetFiles(wwwroot, "manifest.*.json");
if (specificManifests.Length == 0)
{
    BuildTarget("browser", null);
}
else
{
    foreach (var spec in specificManifests)
    {
        // "manifest.firefox.json" -> "firefox"
        var browser = Path.GetFileNameWithoutExtension(spec)["manifest.".Length..];
        BuildTarget(browser, spec);
    }
}

Console.WriteLine("\nBuild complete.");
return 0;

void BuildTarget(string browser, string? specificManifest)
{
    Console.WriteLine($"\nProcessing browser target: {browser}");
    var targetDir = Path.Combine(outputPath, browser);
    var targetAppDir = Path.Combine(targetDir, "app");

    if (Directory.Exists(targetDir)) Directory.Delete(targetDir, recursive: true);
    Directory.CreateDirectory(targetAppDir);

    // Copy the published wwwroot into app/
    CopyDirectory(wwwroot, targetAppDir);

    // Base manifest.json -> extension root
    var rootManifest = Path.Combine(targetDir, "manifest.json");
    File.Move(Path.Combine(targetAppDir, "manifest.json"), rootManifest);

    // Merge the browser-specific manifest onto the base (shallow top-level override)
    if (specificManifest is not null)
    {
        Console.WriteLine($"  Merging {Path.GetFileName(specificManifest)} -> manifest.json");
        MergeManifest(rootManifest, specificManifest);
    }

    // Drop every manifest.*.json (and any stray base copy) from app/
    foreach (var m in Directory.GetFiles(targetAppDir, "manifest.*.json")) File.Delete(m);
    var strayBase = Path.Combine(targetAppDir, "manifest.json");
    if (File.Exists(strayBase)) File.Delete(strayBase);

    // Zip (forward-slash entries)
    if (zip)
    {
        var zipPath = targetDir + ".zip";
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(targetDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        Console.WriteLine($"  Zipped -> {Path.GetFileName(zipPath)}");
    }
    else
    {
        Console.WriteLine("  Zipping disabled.");
    }
}

static void MergeManifest(string basePath, string specificPath)
{
    var docOpts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    var baseObj = JsonNode.Parse(File.ReadAllText(basePath), documentOptions: docOpts)!.AsObject();
    var specObj = JsonNode.Parse(File.ReadAllText(specificPath), documentOptions: docOpts)!.AsObject();

    foreach (var kv in specObj)
        baseObj[kv.Key] = kv.Value?.DeepClone(); // detach from specObj before re-parenting

    var writeOpts = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // no < / ' noise
    };
    File.WriteAllText(basePath, baseObj.ToJsonString(writeOpts), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static void CopyDirectory(string src, string dst)
{
    foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(Path.Combine(dst, Path.GetRelativePath(src, dir)));
    foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(dst, Path.GetRelativePath(src, f));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(f, target, overwrite: true);
    }
}

static int Run(string file, string arguments)
{
    var psi = new ProcessStartInfo(file, arguments) { UseShellExecute = false };
    using var p = Process.Start(psi)!;
    p.WaitForExit();
    return p.ExitCode;
}
