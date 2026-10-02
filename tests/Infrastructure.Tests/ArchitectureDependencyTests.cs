using AiGameStudioWorkstation.Tests;

namespace AiGameStudioWorkstation.Infrastructure.Tests;

public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void Infrastructure_depends_only_on_Application_and_Core()
    {
        ArchitectureAssertions.AssertProjectReferences(
            Path.Combine("src", "Infrastructure", "AiGameStudioWorkstation.Infrastructure.csproj"),
            "AiGameStudioWorkstation.Application",
            "AiGameStudioWorkstation.Core");
    }
}
