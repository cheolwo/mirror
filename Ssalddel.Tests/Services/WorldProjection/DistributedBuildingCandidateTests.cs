using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Ssalddel.Services.WorldProjection;
using Ssalddel.Application.Warehouse;

namespace Ssalddel.Tests.Services.WorldProjection;

public sealed class DistributedBuildingCandidateTests
{
    [Fact]
    public async Task 주소후보는_같은_지역의_정확_도로명만_대조하고_복수건물을_임의_하나로_줄이지_않는다()
    {
        using var fixture = new Bundle();
        var provider = fixture.Provider();
        var candidates = await provider.FindAsync(1, "서울특별시 중랑구 사가정로49길 56, 1층 101호", CancellationToken.None);
        Assert.Equal(2, candidates.Count);
        Assert.Equal(new[] { "osm:way:11", "osm:way:12" }, candidates.Select(item => item.PlaceId));
        Assert.All(candidates, item => Assert.StartsWith("osm-map:" + fixture.Sha.ToLowerInvariant(), item.PublicCandidateRef));
        Assert.All(candidates, item => Assert.DoesNotContain("101호", item.PublicLabel));
        Assert.Empty(await provider.FindAsync(1, "서울특별시 광진구 사가정로49길 56", CancellationToken.None));
        Assert.Empty(await provider.FindAsync(1, "서울특별시 중랑구 사가정로49길 560", CancellationToken.None));
    }

    [Fact]
    public async Task 기본비활성화는_파일을_읽지_않고_원본변조나_판본불일치는_후보를_만들지_않는다()
    {
        var disabled = new DistributedBuildingCandidateProvider(Options.Create(new DistributedBuildingCandidateOptions()));
        Assert.Empty(await disabled.FindAsync(1, "주소", CancellationToken.None));
        using var fixture = new Bundle();
        var wrongRevision = fixture.Provider(mapRevision: "wrong");
        await Assert.ThrowsAsync<DistributedFulfillmentException>(() => wrongRevision.FindAsync(1, "서울특별시 중랑구 사가정로49길 56", CancellationToken.None));
        await File.AppendAllTextAsync(fixture.Path, " ");
        await Assert.ThrowsAsync<DistributedFulfillmentException>(() => fixture.Provider().FindAsync(1, "서울특별시 중랑구 사가정로49길 56", CancellationToken.None));
    }

    [Fact]
    public async Task 취소된_자료조회와_중복건물ID는_거절한다()
    {
        using var fixture = new Bundle(duplicate: true);
        await Assert.ThrowsAsync<DistributedFulfillmentException>(() => fixture.Provider().FindAsync(1, "서울특별시 중랑구 사가정로49길 56", CancellationToken.None));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Provider().FindAsync(1, "주소", cancelled.Token));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"revision\":42,\"attribution\":\"fixture\",\"buildings\":[]}")]
    [InlineData("{\"revision\":\"sagajeong-reference.r3\",\"attribution\":\"fixture\",\"buildings\":[null]}")]
    public async Task 손상된_자료도_파일경로_노출_없이_구조화된_조회불가로_응답한다(string content)
    {
        using var fixture = new Bundle(content: content);
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(() => fixture.Provider().FindAsync(1, "서울특별시 중랑구 사가정로49길 56", CancellationToken.None));
        Assert.Equal("DistributedBuildingCandidatesUnavailable", error.Code);
        Assert.Equal(503, error.Status);
        Assert.DoesNotContain(fixture.Path, error.Message);
    }

    private sealed class Bundle : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "distributed-building-test-" + Guid.NewGuid().ToString("N") + ".json");
        public string Sha { get; }
        public Bundle(bool duplicate = false, string? content = null)
        {
            var second = duplicate ? "11" : "12";
            var json = content ?? "{\"revision\":\"sagajeong-reference.r3\",\"attribution\":\"OSM fixture\",\"buildings\":[{\"id\":\"osm:way:11\",\"street\":\"사가정로49길\",\"houseNumber\":\"56\"},{\"id\":\"osm:way:" + second + "\",\"street\":\"사가정로49길\",\"houseNumber\":\"56\"}]}";
            File.WriteAllText(Path, json, new UTF8Encoding(false)); Sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path)));
        }
        public DistributedBuildingCandidateProvider Provider(string mapRevision = "sagajeong-reference.r3")
            => new(Options.Create(new DistributedBuildingCandidateOptions { Enabled = true, BundlePath = Path, Sha256 = Sha, MapRevision = mapRevision }));
        public void Dispose() => File.Delete(Path);
    }
}
