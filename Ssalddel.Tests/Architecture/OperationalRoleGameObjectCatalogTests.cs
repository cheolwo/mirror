using System.Text.Json;
using Ssalddel.ApiMetadata;

namespace Ssalddel.Tests.Architecture;

public sealed class OperationalRoleGameObjectCatalogTests
{
    [Fact]
    public void 생성대장은_서버의_모든_업무역할을_명시적으로_분류한다()
    {
        using var document = ReadCatalog();
        var candidates = document.RootElement.GetProperty("roleObjectCandidates")
            .EnumerateArray()
            .ToArray();
        var mappedActorCodes = candidates
            .SelectMany(candidate => candidate.GetProperty("sourceActorCodes").EnumerateArray())
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal("operational-unity-transfer-catalog.v3", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(Enum.GetNames<SsalddelActor>().Order(StringComparer.Ordinal), mappedActorCodes.Order(StringComparer.Ordinal));
        Assert.All(mappedActorCodes, actorCode => Assert.True(Enum.TryParse<SsalddelActor>(actorCode, out _), actorCode));
    }

    [Fact]
    public void 생성대장은_운영역할과_시설_차량_업무객체를_서로_다른_원형으로_보존한다()
    {
        using var document = ReadCatalog();
        var candidates = document.RootElement.GetProperty("roleObjectCandidates")
            .EnumerateArray()
            .ToDictionary(
                candidate => candidate.GetProperty("objectArchetypeId").GetString()!,
                StringComparer.Ordinal);

        string[] requiredIds =
        [
            "operational-object:actor:orderer.v1",
            "operational-object:actor:restaurant-owner.v1",
            "operational-object:actor:food-courier.v1",
            "operational-object:actor:cargo-driver.v1",
            "operational-object:actor:warehouse-manager.v1",
            "operational-object:actor:warehouse-worker.v1",
            "operational-object:actor:mart-operator.v1",
            "operational-object:facility:restaurant.v1",
            "operational-object:facility:warehouse.v1",
            "operational-object:facility:ssalddel-mart.v1",
            "operational-object:vehicle:cargo.v1",
            "operational-object:vehicle:food-delivery.v1",
            "operational-object:work:cargo-load.v1",
            "operational-object:work:food-order.v1",
            "operational-object:work:warehouse-handling-unit.v1"
        ];

        Assert.All(requiredIds, id => Assert.True(candidates.ContainsKey(id), id));
        Assert.Contains(candidates.Values, candidate => candidate.GetProperty("objectKindCode").GetString() == "Actor");
        Assert.Contains(candidates.Values, candidate => candidate.GetProperty("objectKindCode").GetString() == "Facility");
        Assert.Contains(candidates.Values, candidate => candidate.GetProperty("objectKindCode").GetString() == "Vehicle");
        Assert.Contains(candidates.Values, candidate => candidate.GetProperty("objectKindCode").GetString() == "WorkObject");
    }

    [Fact]
    public void 생성대장은_개인정보와_실행권위를_포함하지_않고_Scene생성을_열지_않는다()
    {
        using var document = ReadCatalog();
        var candidates = document.RootElement.GetProperty("roleObjectCandidates")
            .EnumerateArray()
            .ToArray();
        var forbiddenProperties = new HashSet<string>(
            ["userId", "phone", "address", "accountNumber", "exactLocation", "rating", "acceptanceRate"],
            StringComparer.OrdinalIgnoreCase);

        Assert.All(candidates, candidate =>
        {
            Assert.False(candidate.GetProperty("isExecutionAuthority").GetBoolean());
            Assert.False(candidate.GetProperty("containsPrivateData").GetBoolean());
            Assert.False(candidate.GetProperty("prefabReady").GetBoolean());
            Assert.False(candidate.GetProperty("sceneReady").GetBoolean());
            Assert.DoesNotContain(candidate.EnumerateObject(), property => forbiddenProperties.Contains(property.Name));
        });

        var platformOperator = Assert.Single(candidates, candidate =>
            candidate.GetProperty("objectArchetypeId").GetString() == "operational-object:actor:platform-operator.v1");
        Assert.Equal("NoUnityRepresentation", platformOperator.GetProperty("representationDecisionCode").GetString());
        Assert.Equal("NotSpawnable", platformOperator.GetProperty("spawnModeCode").GetString());
    }

    private static JsonDocument ReadCatalog()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "docs",
            "AI",
            "generated",
            "operational-unity-transfer-catalog.json")));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }
}
