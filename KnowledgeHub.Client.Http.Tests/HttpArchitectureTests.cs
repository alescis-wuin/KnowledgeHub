using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace KnowledgeHub.Client.Http.Tests;

public sealed class HttpArchitectureTests
{
    [Theory]
    [InlineData("KnowledgeHub.Contracts.Http", "")]
    [InlineData("KnowledgeHub.Client.Http", "KnowledgeHub.Contracts.Http")]
    [InlineData("KnowledgeHub.Client.Http.Tests", "KnowledgeHub.Client.Http,KnowledgeHub.Contracts.Http")]
    public void ProjectReferencesMatchFrozenBoundary(string projectName, string commaSeparatedExpected)
    {
        var path = Path.Combine(RepositoryRoot(), projectName, projectName + ".csproj");
        var document = XDocument.Load(path);
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
    [InlineData("KnowledgeHub.Contracts.Http")]
    [InlineData("KnowledgeHub.Client.Http")]
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
    public void StructuralFixtureDoesNotClaimWireQualification()
    {
        var fixturePath = Path.Combine(RepositoryRoot(), "KnowledgeHub.Client.Http.Tests", "Fixtures", "architecture.json");
        using var json = JsonDocument.Parse(File.ReadAllText(fixturePath));
        Assert.Equal("ARCH-A1.2", json.RootElement.GetProperty("phase").GetString());
        Assert.False(json.RootElement.GetProperty("wireRuntimeQualified").GetBoolean());
        Assert.Equal(22, json.RootElement.GetProperty("futureSdkOperations").GetInt32());
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
