using AiGameStudioWorkstation.Tests;

namespace AiGameStudioWorkstation.Host.Tests;

public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void Project_references_follow_the_architectural_dependency_direction()
    {
        ArchitectureAssertions.AssertProjectReferences(
            Path.Combine("src", "Core", "AiGameStudioWorkstation.Core.csproj"));
        ArchitectureAssertions.AssertProjectReferences(
            Path.Combine("src", "Application", "AiGameStudioWorkstation.Application.csproj"),
            "AiGameStudioWorkstation.Core");
        ArchitectureAssertions.AssertProjectReferences(
            Path.Combine("src", "Infrastructure", "AiGameStudioWorkstation.Infrastructure.csproj"),
            "AiGameStudioWorkstation.Application",
            "AiGameStudioWorkstation.Core");
        ArchitectureAssertions.AssertProjectReferences(
            Path.Combine("src", "Host", "AiGameStudioWorkstation.Host.csproj"),
            "AiGameStudioWorkstation.Application",
            "AiGameStudioWorkstation.Core",
            "AiGameStudioWorkstation.Infrastructure");
    }
}
