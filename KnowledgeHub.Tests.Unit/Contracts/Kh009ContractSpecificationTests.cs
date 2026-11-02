using System.Text.Json;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Contracts;

/// <summary>
/// KH-009.0 SPECIFICATION tests. These tests validate the contract and its
/// security acceptance matrix; they do not assert that unimplemented API
/// routes, JWT authentication or tenant isolation already work at runtime.
/// </summary>
public sealed class Kh009ContractSpecificationTests
{
    private const string ContractPath = "docs/KH009-0-api-contract.v1.json";
    private const string CasesPath = "evaluation/kh009-contract-v1/security-cases.json";
    private const string BasePath = "/api/v1/workspaces";

    private static readonly HashSet<string> AllowedPermissions = new(StringComparer.Ordinal)
    {
        "Authenticated", "AnyMember", "VIEWER+", "MEMBER+", "EDITOR+",
        "ADMIN+", "OWNER", "OWNER|ADMIN", "OWNER|ADMIN|EDITOR"
    };

    private static JsonDocument Load(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KnowledgeHub.slnx")))
            {
                return JsonDocument.Parse(File.ReadAllText(
                    Path.Combine(directory.FullName,
                        relativePath.Replace('/', Path.DirectorySeparatorChar))));
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("KnowledgeHub.slnx not found in ancestor directories.");
    }

    [Fact]
    public void Contract_Has_Exactly46UniqueOperationsAndRoutes()
    {
        using var document = Load(ContractPath);
        var root = document.RootElement;
        Assert.Equal("KnowledgeHub.Workspaces.API.v1", root.GetProperty("contractId").GetString());
        Assert.Equal("KH009_0_BASELINE_NOT_IMPLEMENTED", root.GetProperty("status").GetString());
        Assert.Equal(BasePath, root.GetProperty("basePath").GetString());
        var routes = root.GetProperty("routes").EnumerateArray().ToArray();
        Assert.Equal(46, routes.Length);

        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        var methodPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes)
        {
            var operationId = route.GetProperty("operationId").GetString()!;
            var method = route.GetProperty("method").GetString()!;
            var path = route.GetProperty("path").GetString()!;
            Assert.True(operationIds.Add(operationId), $"Duplicate operationId: {operationId}");
            Assert.True(methodPaths.Add($"{method} {path}"), $"Duplicate method/path: {method} {path}");
            Assert.StartsWith(BasePath, path, StringComparison.Ordinal);
            Assert.DoesNotContain("//", path, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(route.GetProperty("group").GetString()));
            Assert.InRange(route.GetProperty("success").GetProperty("httpStatus").GetInt32(), 200, 299);
        }
    }

    [Fact]
    public void Routes_RequireAuthenticationAndDefineServerPermissions()
    {
        using var document = Load(ContractPath);
        foreach (var route in document.RootElement.GetProperty("routes").EnumerateArray())
        {
            var operationId = route.GetProperty("operationId").GetString()!;
            var permission = route.GetProperty("permission").GetString()!;
            Assert.Contains(permission, AllowedPermissions);
            var path = route.GetProperty("path").GetString()!;
            if (operationId is not ("listMyWorkspaces" or "createWorkspace" or "acceptInvitation"))
                Assert.Contains("{workspaceId}", path, StringComparison.Ordinal);
        }
        var security = document.RootElement.GetProperty("security");
        Assert.Empty(security.GetProperty("publicAuthEndpoints").EnumerateArray());
        Assert.Contains("issuer", security.GetProperty("identityKey").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryMutationWithPrecondition_HasIfMatchHeader()
    {
        using var document = Load(ContractPath);
        var root = document.RootElement;
        var routes = root.GetProperty("routes").EnumerateArray()
            .ToDictionary(route => route.GetProperty("operationId").GetString()!, StringComparer.Ordinal);
        var concurrency = root.GetProperty("conventions");
        var operations = concurrency.GetProperty("IfMatchRequiredFor")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(5, operations.Length);
        foreach (var id in operations)
        {
            Assert.True(routes.ContainsKey(id), $"Missing operation: {id}");
            var headers = routes[id].GetProperty("requiredHeaders")
                .EnumerateArray().Select(x => x.GetString()!).ToArray();
            Assert.Contains("If-Match", headers);
            Assert.Contains(routes[id].GetProperty("method").GetString()!,
                new[] { "PATCH", "POST", "PUT", "DELETE" });
        }
        Assert.Equal("If-Match required but missing",
            concurrency.GetProperty("httpStatusMeaning").GetProperty("428").GetString());
    }

    [Fact]
    public void RawAndBinaryRoutes_AreExplicitExceptionsToEnvelope()
    {
        using var document = Load(ContractPath);
        var conventions = document.RootElement.GetProperty("conventions");
        var ids = document.RootElement.GetProperty("routes").EnumerateArray()
            .Select(x => x.GetProperty("operationId").GetString()!).ToHashSet(StringComparer.Ordinal);
        var raw = conventions.GetProperty("legacyRawJsonRoutes")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(5, raw.Length);
        Assert.All(raw, x => Assert.Contains(x, ids));
        var binary = conventions.GetProperty("binaryRoutes")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(new[] { "getAssetContent" }, binary);
        Assert.All(binary, x => Assert.Contains(x, ids));
        var success = conventions.GetProperty("successEnvelope");
        var failure = conventions.GetProperty("errorEnvelope");
        Assert.True(success.GetProperty("success").GetBoolean());
        Assert.False(failure.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void WorkspaceRoleOrder_IsStableAndOwnedByServer()
    {
        using var document = Load(ContractPath);
        var security = document.RootElement.GetProperty("security");
        var order = security.GetProperty("roleOrder")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(new[] { "VIEWER", "MEMBER", "EDITOR", "ADMIN", "OWNER" }, order);
        var roles = security.GetProperty("roles");
        Assert.All(order, role => Assert.True(roles.TryGetProperty(role, out _), $"Unknown role: {role}"));
        Assert.Contains("membership", security.GetProperty("authorizationSemantics")
            .GetProperty("roleSource").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SensitiveRoutes_HaveExpectedMinimumPermissions()
    {
        using var document = Load(ContractPath);
        var routes = document.RootElement.GetProperty("routes").EnumerateArray()
            .ToDictionary(route => route.GetProperty("operationId").GetString()!,
                route => route.GetProperty("permission").GetString()!, StringComparer.Ordinal);
        Assert.Equal("MEMBER+", routes["askVerifiedRagScoped"]);
        Assert.Equal("MEMBER+", routes["askClassicRagScoped"]);
        Assert.Equal("EDITOR+", routes["importDocumentScoped"]);
        Assert.Equal("ADMIN+", routes["generateEmbeddingScoped"]);
        Assert.Equal("ADMIN+", routes["getDatabaseStatusScoped"]);
        Assert.Equal("OWNER", routes["archiveWorkspace"]);
        Assert.Equal("VIEWER+", routes["getDocumentScoped"]);
    }

    [Fact]
    public void SecurityMatrix_HasUniquePendingExecutableRequirements()
    {
        using var document = Load(CasesPath);
        var root = document.RootElement;
        Assert.Equal("TEST_REQUIREMENTS_NOT_EXECUTED", root.GetProperty("state").GetString());
        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(36, cases.Length);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var scenarios = new HashSet<string>(StringComparer.Ordinal);
        var stages = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scenario in cases)
        {
            var id = scenario.GetProperty("id").GetString()!;
            Assert.True(ids.Add(id), $"Duplicate security case ID {id}");
            Assert.True(scenarios.Add(scenario.GetProperty("scenario").GetString()!));
            Assert.Equal("PENDING", scenario.GetProperty("implementationStatus").GetString());
            var stage = scenario.GetProperty("targetStage").GetString()!;
            Assert.StartsWith("KH-009.", stage, StringComparison.Ordinal);
            stages.Add(stage);
            Assert.False(string.IsNullOrWhiteSpace(scenario.GetProperty("expected").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(scenario.GetProperty("description").GetString()));
        }
        Assert.Contains("KH-009.1", stages);
        Assert.Contains("KH-009.2", stages);
        Assert.Contains("KH-009.3", stages);
        Assert.Contains("KH-009.4", stages);
        Assert.Contains("KH-009.5", stages);
    }

    [Fact]
    public void SecurityMatrix_ContainsCriticalIsolationRequirements()
    {
        using var document = Load(CasesPath);
        var matrix = document.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(x => x.GetProperty("scenario").GetString()!,
                x => x.GetProperty("expected").GetString()!, StringComparer.Ordinal);
        Assert.Equal("NO_LEAK", matrix["vector-cache-cross-workspace"]);
        Assert.Equal("ABSTAIN", matrix["verified-rag-cross-workspace-evidence"]);
        Assert.Equal("ABSTAIN", matrix["verified-rag-cross-workspace-revalidation"]);
        Assert.Equal("NO_LEAK", matrix["legacy-routes-reveal-scoped-records"]);
        Assert.Equal("404", matrix["read-cross-workspace-document"]);
        Assert.Equal("DENY", matrix["foreign-key-cross-workspace"]);
    }
}
