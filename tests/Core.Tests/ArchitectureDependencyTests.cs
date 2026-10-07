using AiGameStudioWorkstation.Tests;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void Core_has_no_project_dependencies()
    {
        ArchitectureAssertions.AssertProjectReferences(
            Path.Combine("src", "Core", "AiGameStudioWorkstation.Core.csproj"));
        ArchitectureAssertions.AssertNoPackageReference(
            Path.Combine("src", "Core", "AiGameStudioWorkstation.Core.csproj"),
            "Microsoft.Data.Sqlite");
    }
}
