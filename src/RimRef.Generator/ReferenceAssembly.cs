using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using JetBrains.Refasmer;
using JetBrains.Refasmer.Filters;

namespace RimRef.Generator;

internal static class ReferenceAssembly
{
    private static readonly LoggerBase Logger = new(new ConsoleLogger());

    // Keeps non-public members too, so that publicizers can expose them to mods.
    public static byte[] Create(string path)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        var filter = new AllowAll();
        return MetadataImporter.MakeRefasm(metadata, peReader, Logger, filter, omitNonApiMembers: false);
    }

    private sealed class ConsoleLogger : ILogger
    {
        public void Log(LogLevel logLevel, string message)
        {
            Console.Error.WriteLine($"refasmer {logLevel}: {message}");
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Warning;
        }
    }
}
