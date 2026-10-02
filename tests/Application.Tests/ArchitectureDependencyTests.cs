using AiGameStudioWorkstation.Tests;

namespace AiGameStudioWorkstation.Application.Tests;

public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void Application_depends_only_on_Core()
    {
        ArchitectureAssertions.AssertProjectReferences(
            Path.Combine("src", "Application", "AiGameStudioWorkstation.Application.csproj"),
            "AiGameStudioWorkstation.Core");
    }
}
