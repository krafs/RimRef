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
        private static IConfiguration config;

        public static async Task<int> Main()
        {
            config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            bool dryRun = bool.Parse(config["IsDryRun"]);

            Console.WriteLine("Starting");
            string rimWorldFolder = config["RimWorldFolderPath"];

            string versionFile = Path.Combine(rimWorldFolder, "Version.txt");
            string rawVersion = File.ReadAllText(versionFile);

            string version = rawVersion.Substring(0, rawVersion.IndexOf(' '));
            if (IsRimWorldUnstableBranch())
            {
                version += config["PreReleaseSuffix"];
            }

            Console.WriteLine("Established Version: " + version);

            using SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();
            var nugetVersion = NuGetVersion.Parse(version);
            string packageId = config["PackageId"];
            bool versionExists = await resource.DoesPackageExistAsync(packageId, nugetVersion, cache, NullLogger.Instance, CancellationToken.None);

            if (versionExists)
            {
                Console.WriteLine($"Version {nugetVersion} exists on nuget.org.");
                if (!dryRun)
                {
                    Console.WriteLine("Terminating program.");
                    Console.ReadKey();
                    return 0;
                }
                else
                {
                    Console.WriteLine("Process is dry-run. Continuing.");
                }
            }
            else
            {
                Console.WriteLine($"Version {nugetVersion} does not exists on nuget.org. Proceeding with program.");
            }

            string rimWorldManagedFolderRelative = @"RimWorldWin64_Data\Managed";
            string dllFolder = Path.Combine(rimWorldFolder, rimWorldManagedFolderRelative);
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            var nuspecPath = Directory.EnumerateFiles(basePath).FirstOrDefault(file => file.EndsWith(".nuspec"));
            using FileStream nuspecStream = new FileStream(nuspecPath, FileMode.Open);
            var manifest = Manifest.ReadFrom(nuspecStream, true);
            PackageBuilder package = new PackageBuilder();
            package.Populate(manifest.Metadata);
            package.Version = nugetVersion;

            NuGetVersion net472TransitionVersion = new NuGetVersion(1, 1, 0);
            string targetFramework = package.Version < net472TransitionVersion ? "net35" : "net472";

            foreach (var managedFile in Directory.EnumerateFiles(dllFolder))
            {
                if (managedFile.EndsWith(".dll") is false)
                {
                    continue;
                }

                var bytes = MakeReferenceAssembly(managedFile);
                var packageFile = new InMemoryFile(bytes, @$"{PackagingConstants.Folders.Ref}\{targetFramework}\{Path.GetFileName(managedFile)}");
                package.Files.Add(packageFile);
            }
            var licenseFileBytes = File.ReadAllBytes(Path.Combine(basePath, package.LicenseMetadata.License));
            var licenseFile = new InMemoryFile(licenseFileBytes, package.LicenseMetadata.License);
            package.Files.Add(licenseFile);

            var iconFileBytes = File.ReadAllBytes(Path.Combine(basePath, package.Icon));
            var iconFile = new InMemoryFile(iconFileBytes, package.Icon);
            package.Files.Add(iconFile);

            using MemoryStream nupkgStream = new MemoryStream();
            package.Save(nupkgStream);

            if (dryRun)
            {
                string outputPath = config["OutputFolderPath"];
                Directory.CreateDirectory(outputPath);
                Console.WriteLine("Output folder is: " + outputPath);
                string filePath = Path.Combine(outputPath, $"{package.Id}.{package.Version}{NuGetConstants.PackageExtension}");
                await File.WriteAllBytesAsync(filePath, nupkgStream.ToArray());
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
                content.Add(streamContent, "package", "package.nupkg"); // Can these strings be removed?
                request.Content = content;
                var response = await httpClient.SendAsync(request);

                Console.WriteLine(response.StatusCode + " : " + response.ReasonPhrase);
            }

            Console.WriteLine($"Done.");
            Console.ReadKey(); // Remove if silent
            return 0;
        }

        private static bool IsRimWorldUnstableBranch()
        {
            string steamAppId = config["SteamAppId"];
            Environment.SetEnvironmentVariable("SteamAppId", steamAppId);
            try
            {
                if (SteamAPI.Init())
                {
                    Console.WriteLine("Successfully initialized.");

                    return SteamApps.GetCurrentBetaName(out string _, 100);
                }
                else
                {
                    Console.WriteLine("Steam API failed to initialize.");
                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: ");
                Console.Write(e.Message);
                throw;
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