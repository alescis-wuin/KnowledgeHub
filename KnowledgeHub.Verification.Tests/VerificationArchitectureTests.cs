using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace KnowledgeHub.Verification.Tests;

public sealed class VerificationArchitectureTests
{
    [Theory]
    [InlineData("KnowledgeHub.Rag.Contracts", "")]
    [InlineData("KnowledgeHub.Verification", "KnowledgeHub.Rag.Contracts")]
    [InlineData("KnowledgeHub.Verification.Tests", "KnowledgeHub.Rag.Contracts,KnowledgeHub.Verification")]
    public void ProjectReferencesMatchFrozenBoundary(string projectName, string commaSeparatedExpected)
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), projectName, projectName + ".csproj"));
        var actual = document.Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/').Split('/').Last()))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = commaSeparatedExpected.Length == 0
            ? Array.Empty<string>()
            : commaSeparatedExpected.Split(',').OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal("net10.0", (string?)document.Descendants("TargetFramework").Single());
    }

    [Theory]
    [InlineData("KnowledgeHub.Rag.Contracts")]
    [InlineData("KnowledgeHub.Verification")]
    public void ProductionAssemblyIsAnEmptyStructuralShell(string assemblyName)
    {
        var assembly = Assembly.Load(new AssemblyName(assemblyName));
        Assert.Equal(assemblyName, assembly.GetName().Name);
        Assert.Equal("ARCH-A1.2", assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "KnowledgeHub.ArchitecturePhase").Value);
        Assert.Empty(assembly.GetExportedTypes());

        var project = XDocument.Load(Path.Combine(RepositoryRoot(), assemblyName, assemblyName + ".csproj"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
    }

    [Fact]
    public void NewProjectsAreInSolutionWithoutChangingExistingOnes()
    {
        var slnx = XDocument.Load(Path.Combine(RepositoryRoot(), "KnowledgeHub.slnx"));
        var actual = slnx.Descendants("Project")
            .Select(project => (string?)project.Attribute("Path"))
            .Where(path => path is not null)
            .Select(path => Path.GetFileNameWithoutExtension(path!))
            .OrderBy(project => project, StringComparer.Ordinal)
            .ToArray();
        var expected = new[]
        {
            "KnowledgeHub.Api",
            "KnowledgeHub.Application",
            "KnowledgeHub.Client",
            "KnowledgeHub.Client.Http",
            "KnowledgeHub.Client.Http.Tests",
            "KnowledgeHub.Contracts.Http",
            "KnowledgeHub.CorpusImporter",
            "KnowledgeHub.Domain",
            "KnowledgeHub.Infrastructure",
            "KnowledgeHub.Rag.Contracts",
            "KnowledgeHub.Tests.Integration",
            "KnowledgeHub.Tests.Unit",
            "KnowledgeHub.Verification",
            "KnowledgeHub.Verification.Tests"
        }.OrderBy(project => project, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("IClaimProposalGenerator")]
    [InlineData("IQualifiedEvidenceAssessor")]
    public void RagPortMovementMapRecordsDeclaredSourceNamespace(string symbol)
    {
        var mapPath = Path.Combine(RepositoryRoot(), "docs", "architecture", "ARCH-A1.2-type-movement-v1.json");
        using var json = JsonDocument.Parse(File.ReadAllText(mapPath));
        var entry = json.RootElement.GetProperty("entries").EnumerateArray()
            .Single(candidate => candidate.GetProperty("symbol").GetString() == symbol
                && candidate.GetProperty("kind").GetString() == "rag_port");
        Assert.Equal("KnowledgeHub.Application.Interfaces", entry.GetProperty("source_namespace").GetString());
        Assert.Equal($"KnowledgeHub.Application/Interfaces/{symbol}.cs", entry.GetProperty("source_file").GetString());
    }

    [Fact]
    public void FixturePreservesNoSemanticAssessorClaim()
    {
        var path = Path.Combine(RepositoryRoot(), "KnowledgeHub.Verification.Tests", "Fixtures", "architecture.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("ARCH-A1.2", json.RootElement.GetProperty("phase").GetString());
        Assert.False(json.RootElement.GetProperty("semanticAssessorQualified").GetBoolean());
        Assert.Equal("ABSTAIN", json.RootElement.GetProperty("unqualifiedAssessorPolicy").GetString());
    }

    private static string RepositoryRoot()
    {
        var explicitRoot = Environment.GetEnvironmentVariable("KNOWLEDGEHUB_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(explicitRoot) && File.Exists(Path.Combine(explicitRoot, "KnowledgeHub.slnx")))
        {
            return explicitRoot;
        }
        DirectoryInfo? current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "KnowledgeHub.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Set KNOWLEDGEHUB_REPOSITORY_ROOT to the repository path.");
    }
}
