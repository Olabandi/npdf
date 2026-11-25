namespace NeroPdf.Tests;

/// <summary>Finds folders of the repository from the test output directory.</summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string TestFiles => Path.Combine(Root, "TestFiles");

    public static string Languages => Path.Combine(Root, "src", "NeroPdf.App", "Languages");

    public static string TestFile(string name) => Path.Combine(TestFiles, name);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "TestFiles")) && File.Exists(Path.Combine(dir.FullName, "NeroPdf.sln")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("Repository root with TestFiles not found.");
    }
}
