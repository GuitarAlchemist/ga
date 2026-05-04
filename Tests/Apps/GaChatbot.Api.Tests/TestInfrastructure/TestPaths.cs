namespace GaChatbot.Api.Tests;

using System.Runtime.CompilerServices;

internal static class TestPaths
{
    public static string RepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)
            ?? throw new InvalidOperationException("Source file path is unavailable."));

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AllProjects.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Repository root could not be located from {sourceFile}.");
    }

    public static string RepositoryPath(params string[] segments) =>
        Path.Combine([RepositoryRoot(), ..segments]);
}
