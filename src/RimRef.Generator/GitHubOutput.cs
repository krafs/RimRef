namespace RimRef.Generator;

internal static class GitHubOutput
{
    public static void Write(string name, string value)
    {
        var path = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        File.AppendAllText(path, $"{name}={value}{Environment.NewLine}");
    }
}
