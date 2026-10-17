using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NuGet.Frameworks;

namespace RimRef.Generator;

internal sealed partial record Game(string Version, NuGetFramework TargetFramework, IReadOnlyCollection<string> Assemblies)
{
    public static Game Load(string gameDir)
    {
        var version = ReadVersion(gameDir);
        var managedDir = Path.Combine(gameDir, "RimWorldWin64_Data", "Managed");
        var runtimeConfigPath = Path.Combine(gameDir, "RimWorld.runtimeconfig.json");
        var isUnity = Directory.Exists(managedDir);
        var isDotNet = File.Exists(runtimeConfigPath);

        if (isDotNet)
        {
            return LoadDotNet(version, gameDir, runtimeConfigPath);
        }

        if (isUnity)
        {
            return LoadUnity(version, managedDir);
        }

        throw new GeneratorException($"Unrecognized game layout in {gameDir}.");
    }

    private static string ReadVersion(string gameDir)
    {
        var path = Path.Combine(gameDir, "Version.txt");
        if (!File.Exists(path))
        {
            throw new GeneratorException($"Missing {path}.");
        }

        var text = File.ReadAllText(path).Trim();
        var match = VersionRegex().Match(text);
        if (!match.Success)
        {
            throw new GeneratorException($"Unrecognized format of Version.txt: '{text}'.");
        }

        return match.Groups["version"].Value;
    }

    private static Game LoadUnity(string version, string managedDir)
    {
        var assemblies = Directory.GetFiles(managedDir, "*.dll");
        var targetFramework = ReadUnityTargetFramework(managedDir);
        return new Game(version, targetFramework, assemblies);
    }

    private static NuGetFramework ReadUnityTargetFramework(string managedDir)
    {
        var mscorlibPath = Path.Combine(managedDir, "mscorlib.dll");
        var mscorlib = AssemblyName.GetAssemblyName(mscorlibPath);
        var tfm = mscorlib.Version?.Major switch
        {
            2 => "net35",
            4 => "net472",
            _ => throw new GeneratorException($"The game's mscorlib has version {mscorlib.Version}, but only 2.0 and 4.0 are supported."),
        };
        return NuGetFramework.Parse(tfm);
    }

    private static Game LoadDotNet(string version, string gameDir, string runtimeConfigPath)
    {
        var runtimeConfig = ReadJson(runtimeConfigPath);
        var runtimeOptions = runtimeConfig["runtimeOptions"];
        var tfm = runtimeOptions?["tfm"]?.GetValue<string>();
        if (tfm != "net10.0")
        {
            throw new GeneratorException($"The game targets '{tfm}', but only net10.0 is supported.");
        }

        var targetFramework = NuGetFramework.Parse(tfm);
        var assemblies = ReadGameAssemblies(gameDir);
        return new Game(version, targetFramework, assemblies);
    }

    // Everything the game ships except its build of .NET itself, which mods get from the .NET SDK.
    private static HashSet<string> ReadGameAssemblies(string gameDir)
    {
        var depsPath = Path.Combine(gameDir, "RimWorld.deps.json");
        var deps = ReadJson(depsPath);
        var runtimeTarget = deps["runtimeTarget"]?["name"]?.GetValue<string>();
        if (runtimeTarget is null)
        {
            throw new GeneratorException("RimWorld.deps.json has no runtime target.");
        }

        var targets = deps["targets"]?[runtimeTarget]?.AsObject();
        var libraries = deps["libraries"]?.AsObject();
        if (targets is null || libraries is null)
        {
            throw new GeneratorException("Unrecognized format of RimWorld.deps.json.");
        }

        var assemblies = new HashSet<string>();
        foreach (var (libraryName, library) in targets)
        {
            var libraryType = libraries[libraryName]?["type"]?.GetValue<string>();
            if (libraryType == "runtimepack")
            {
                continue;
            }

            var runtimeAssets = library?["runtime"]?.AsObject();
            if (runtimeAssets is null)
            {
                continue;
            }

            foreach (var (assetPath, _) in runtimeAssets)
            {
                var fileName = Path.GetFileName(assetPath);
                var path = Path.Combine(gameDir, fileName);
                assemblies.Add(path);
            }
        }

        return assemblies;
    }

    private static JsonNode ReadJson(string path)
    {
        var fileName = Path.GetFileName(path);
        if (!File.Exists(path))
        {
            throw new GeneratorException($"Missing {fileName}.");
        }

        var text = File.ReadAllText(path);
        try
        {
            return JsonNode.Parse(text) ?? throw new GeneratorException($"{fileName} is empty.");
        }
        catch (JsonException e)
        {
            throw new GeneratorException($"{fileName} is not valid JSON: {e.Message}");
        }
    }

    [GeneratedRegex(@"^(?<version>\d+\.\d+\.\d+) rev\d+$")]
    private static partial Regex VersionRegex();
}
