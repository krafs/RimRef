using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace RimRef.Generator;

internal sealed record PackageAssembly(string FileName, byte[] Content);

internal static class Package
{
    private static readonly string TemplateDir = Path.Combine(AppContext.BaseDirectory, "package");

    private static readonly string[] MetadataFiles = ["README.md", "LICENSE.md", "icon.png"];

    public static string Write(string outputDir, string version, NuGetFramework targetFramework, IReadOnlyList<PackageAssembly> assemblies)
    {
        var year = DateTime.UtcNow.Year;
        var builder = new PackageBuilder
        {
            Id = "Krafs.Rimworld.Ref",
            Version = NuGetVersion.Parse(version),
            Title = "RimRef",
            Description = "RimWorld reference assemblies. Compile mods without referencing the game.",
            Copyright = $"Ludeon Studios (c) {year}",
            ProjectUrl = new Uri("https://github.com/krafs/RimRef"),
            Readme = "README.md",
            Icon = "icon.png",
            LicenseMetadata = new LicenseMetadata(LicenseType.File, "LICENSE.md", null, null, LicenseMetadata.CurrentVersion),
        };
        builder.Authors.Add("krafs");
        builder.Authors.Add("Ludeon Studios");
        builder.Tags.Add("Rimworld");
        builder.Tags.Add("Mod");

        var dependencyGroup = new PackageDependencyGroup(targetFramework, []);
        builder.DependencyGroups.Add(dependencyGroup);

        foreach (var fileName in MetadataFiles)
        {
            var file = new PhysicalPackageFile
            {
                SourcePath = Path.Combine(TemplateDir, fileName),
                TargetPath = fileName,
            };
            builder.Files.Add(file);
        }

        var folder = targetFramework.GetShortFolderName();
        foreach (var assembly in assemblies)
        {
            var content = new MemoryStream(assembly.Content);
            var file = new PhysicalPackageFile(content)
            {
                TargetPath = $"ref/{folder}/{assembly.FileName}",
            };
            builder.Files.Add(file);
        }

        using var package = new MemoryStream();
        builder.Save(package);

        Directory.CreateDirectory(outputDir);
        var packageFileName = $"{builder.Id}.{builder.Version.ToNormalizedString()}.nupkg";
        var packagePath = Path.Combine(outputDir, packageFileName);
        File.WriteAllBytes(packagePath, package.ToArray());
        return packagePath;
    }
}
