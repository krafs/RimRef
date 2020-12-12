using NuGet.Frameworks;
using NuGet.Packaging;
using System;
using System.IO;
using System.Runtime.Versioning;

namespace StripAndUploadRimRef
{
    public class InMemoryFile : IPackageFile
    {
        private readonly byte[] fileBytes;

        public InMemoryFile(byte[] fileBytes, string path)
        {
            this.fileBytes = fileBytes;
            Path = path;
        }

        public string Path { get; }

        public string EffectivePath { get; }

        public FrameworkName TargetFramework { get; }

        public DateTimeOffset LastWriteTime { get; private set; }

        public Stream GetStream()
        {
            LastWriteTime = DateTime.UtcNow;

            return new MemoryStream(this.fileBytes);
        }

        public NuGetFramework NuGetFramework { get; }
    }
}