using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.Configuration;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Steamworks;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace StripAndUploadRimRef
{
    public class Program
    {
        public static async Task<int> Main()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            ILog logger;

            bool isSilent;
            if (bool.TryParse(config["IsSilent"], out isSilent) && !isSilent)
            {
                logger = new Logger();
            }
            else
            {
                logger = new VoidLogger();
                isSilent = true;
            }

            logger.Log("Starting build of Krafs.Rimworld.Ref.");
            bool isDryRun;
            if (!bool.TryParse(config["IsDryRun"], out isDryRun))
            {
                isDryRun = true;
            }

            string outputPath = config["OutputFolderPath"];
            if (isDryRun)
            {
                logger.Log($"\nDry-run detected.");
                logger.Log($"Output path: {outputPath}");
            }
            else
            {
                logger.Log($"\nDry-run not detected.");
                logger.Log($"Output: nuget.org");
            }

            string rimWorldFolder = config["RimWorldFolderPath"];

            string versionFile = Path.Combine(rimWorldFolder, "Version.txt");
            string rawVersion = File.ReadAllText(versionFile);

            string version = rawVersion.Substring(0, rawVersion.IndexOf(' '));
            if (IsRimWorldUnstableBranch(logger))
            {
                version += "-beta";
            }

            logger.Log("Resolved package version: " + version);

            string rimWorldManagedFolderRelative = Path.Combine("RimWorldWin64_Data", "Managed");
            string dllFolder = Path.Combine(rimWorldFolder, rimWorldManagedFolderRelative);
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            var nuspecPath = Directory.EnumerateFiles(basePath).FirstOrDefault(file => file.EndsWith(".nuspec"));
            using FileStream nuspecStream = new FileStream(nuspecPath, FileMode.Open);
            Manifest manifest = Manifest.ReadFrom(nuspecStream, true);
            PackageBuilder package = new PackageBuilder();
            package.Populate(manifest.Metadata);
            package.Version = NuGetVersion.Parse(version);

            if (!isDryRun)
            {
                logger.Log("Checking package version on nuget.org.");
                using SourceCacheContext cache = new SourceCacheContext();
                SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
                FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();
                var nugetVersion = NuGetVersion.Parse(version);
                bool versionExists = await resource.DoesPackageExistAsync(
                    manifest.Metadata.Id,
                    nugetVersion,
                    cache,
                    NullLogger.Instance,
                    CancellationToken.None);

                if (versionExists)
                {
                    logger.Log("Package version {version} already on nuget.org. Terminating application.");
                    Console.ReadKey();
                    return 0;
                }
                else
                {
                    logger.Log("Package version {version} not found on nuget.org. Proceeding with package build.");
                }
            }

            NuGetVersion net472TransitionVersion = new NuGetVersion(1, 1, 0);
            string targetFramework = package.Version < net472TransitionVersion ? "net35" : "net472";

            logger.Log("\nGenerating reference assemblies...");
            string[] assemblyPaths = Directory.EnumerateFiles(dllFolder).Where(x => x.EndsWith(".dll")).ToArray();
            foreach (string managedFile in assemblyPaths)
            {
                var bytes = MakeReferenceAssembly(managedFile);
                var packageFile = new InMemoryFile(bytes, @$"{PackagingConstants.Folders.Ref}\{targetFramework}\{Path.GetFileName(managedFile)}");
                package.Files.Add(packageFile);
            }
            logger.Log($"Successfully generated {assemblyPaths.Length} reference assemblies.");

            var licenseFileBytes = File.ReadAllBytes(Path.Combine(basePath, package.LicenseMetadata.License));
            var licenseFile = new InMemoryFile(licenseFileBytes, package.LicenseMetadata.License);
            package.Files.Add(licenseFile);

            var iconFileBytes = File.ReadAllBytes(Path.Combine(basePath, package.Icon));
            var iconFile = new InMemoryFile(iconFileBytes, package.Icon);
            package.Files.Add(iconFile);

            using MemoryStream nupkgStream = new MemoryStream();
            package.Save(nupkgStream);

            if (isDryRun)
            {
                Directory.CreateDirectory(outputPath);
                string filePath = Path.Combine(outputPath, $"{package.Id}.{package.Version}{NuGetConstants.PackageExtension}");
                await File.WriteAllBytesAsync(filePath, nupkgStream.ToArray());
                logger.Log($"\nSaved package to disk: {filePath}.");
            }
            else
            {
                using HttpClient httpClient = new HttpClient();
                var packageFeedUrl = NuGetConstants.V2FeedUrl + "/package";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, packageFeedUrl);
                request.Headers.Add("X-NuGet-Protocol-Version", "4.1.0");

                string nugetOrgApiKey = config["NuGetApiKey"];
                request.Headers.Add(ProtocolConstants.ApiKeyHeader, nugetOrgApiKey);
                MultipartFormDataContent content = new MultipartFormDataContent();
                nupkgStream.Position = 0;
                StreamContent streamContent = new StreamContent(nupkgStream);
                streamContent.Headers.ContentType = new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Octet);
                content.Add(streamContent, "package", "package.nupkg");
                request.Content = content;
                HttpResponseMessage response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    logger.Log($"\nSuccessfully uploaded package to nuget.org.");
                }
                else
                {
                    logger.Log($"\nSomething went wrong uploading package to nuget.org:");
                    logger.Log(response.StatusCode + ": " + response.ReasonPhrase);
                }
            }

            logger.Log($"\nDone. Press any key to exit.");
            if (!isSilent)
            {
                Console.ReadKey();
            }
            return 0;
        }

        private static bool IsRimWorldUnstableBranch(ILog logger)
        {
            string steamAppId = "294100";
            Environment.SetEnvironmentVariable("SteamAppId", steamAppId);
            try
            {
                logger.Log("\nInitializing Steam connection for Rimworld branch detection.");
                if (SteamAPI.Init())
                {
                    bool isBetaBranch = SteamApps.GetCurrentBetaName(out string _, 100);

                    logger.Log($"\nIs beta branch: {isBetaBranch}");
                    return isBetaBranch;
                }
                else
                {
                    throw new InvalidOperationException("Unable to initialize Steam connection to resolve Rimworld branch.");
                }
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }

        private static byte[] MakeReferenceAssembly(string file)
        {
            FileStream stream = new FileStream(file, FileMode.Open);

            using ModuleDef assembly = ModuleDefMD.Load(stream);
            foreach (MethodDef method in assembly.GetTypes().SelectMany(x => x.Methods))
            {
                method.Body = new CilBody();
                Instruction returnInstruction = new(OpCodes.Ret);
                method.Body.Instructions.Add(returnInstruction);
            }

            using MemoryStream memoryStream = new MemoryStream();
            ModuleWriter moduleWriter = new ModuleWriter(assembly);
            moduleWriter.Write(memoryStream);

            return memoryStream.ToArray();
        }
    }
}