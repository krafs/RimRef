using RimRef.Generator;

var isUnstable = args is [_, _, "--unstable"];
if (args.Length != 2 && !isUnstable)
{
    Console.Error.WriteLine("Usage: RimRef.Generator <game-dir> <output-dir> [--unstable]");
    return 2;
}

var gameDir = Path.GetFullPath(args[0]);
var outputDir = Path.GetFullPath(args[1]);

try
{
    var game = Game.Load(gameDir);
    var targetFramework = game.TargetFramework.GetShortFolderName();
    Console.WriteLine($"RimWorld {game.Version}, targeting {targetFramework}");

    var packageVersion = isUnstable ? $"{game.Version}-unstable" : game.Version;

    var files = new List<PackageAssembly>();
    foreach (var assemblyPath in game.Assemblies)
    {
        var bytes = ReferenceAssembly.Create(assemblyPath);
        var fileName = Path.GetFileName(assemblyPath);
        var file = new PackageAssembly(fileName, bytes);
        files.Add(file);
    }

    Console.WriteLine($"Generated {game.Assemblies.Count} reference assemblies");

    var packagePath = Package.Write(outputDir, packageVersion, game.TargetFramework, files);
    Console.WriteLine($"Created {packagePath}");

    GitHubOutput.Write("version", packageVersion);
    GitHubOutput.Write("target-framework", targetFramework);
    GitHubOutput.Write("package", packagePath);
    return 0;
}
catch (Exception e) when (e is GeneratorException or InvalidOperationException or BadImageFormatException)
{
    var isGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    var prefix = isGitHubActions ? "::error::" : "error: ";
    Console.Error.WriteLine(prefix + e.Message);
    return 1;
}
