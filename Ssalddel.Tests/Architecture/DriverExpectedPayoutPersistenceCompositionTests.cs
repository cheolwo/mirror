namespace Ssalddel.Tests.Architecture;

public sealed class DriverExpectedPayoutPersistenceCompositionTests
{
    [Fact]
    public void 화주가_제시한_기사지급예정운임은_운임구성에_별도_저장된다()
    {
        var source = File.ReadAllText(ProjectFile(
            "Ssalddel",
            "Application",
            "Shipper",
            "Request",
            "Handlers",
            "의뢰생성CommandHandler.cs"));

        Assert.Contains(
            "기사지급예정운임 = request.기사지급예정운임",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "|| request.기사지급예정운임.HasValue",
            source,
            StringComparison.Ordinal);
    }

    private static string ProjectFile(params string[] paths)
    {
        var root = TestRepositoryRoot.Find();
        return Path.Combine(new[] { root }.Concat(paths).ToArray());
    }
}
