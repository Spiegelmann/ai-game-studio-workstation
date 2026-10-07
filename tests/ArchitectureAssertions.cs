using System.Xml.Linq;

namespace AiGameStudioWorkstation.Tests;

internal static class ArchitectureAssertions
{
    public static void AssertProjectReferences(
        string projectRelativePath,
        params string[] expectedReferences)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, projectRelativePath);
        var actualReferences = XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension((string?)reference.Attribute("Include"))
                ?? throw new InvalidOperationException($"A project reference in '{projectPath}' has no Include attribute."))
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expectedReferences.OrderBy(reference => reference, StringComparer.Ordinal),
            actualReferences);
    }

    public static void AssertNoPackageReference(string projectRelativePath, string packageName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, projectRelativePath);
        var packageReferences = XDocument.Load(projectPath)
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .ToArray();

        Assert.DoesNotContain(packageReferences, package => string.Equals(
            package,
            packageName,
            StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AiGameStudioWorkstation.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate AiGameStudioWorkstation.sln from the test output directory.");
    }
}
