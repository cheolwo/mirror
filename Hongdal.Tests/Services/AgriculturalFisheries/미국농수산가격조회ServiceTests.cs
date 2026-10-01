using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class 미국농수산가격조회ServiceTests
{
    [Fact]
    public async Task 같은조회조건의_완료결과는_공급자를다시호출하지않는다()
    {
        var provider = new StubProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new 미국농수산가격조회Service(
            [provider],
            cache,
            NullLogger<미국농수산가격조회Service>.Instance);
        var request = new 미국농수산가격조회요청
        {
            Commodity = "CORN",
            YearFrom = 2025,
            YearTo = 2026
        };

        var first = await service.조회Async(request);
        var second = await service.조회Async(request);

        Assert.True(first.Success);
        Assert.Same(first, second);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task 등록되지않은출처는_외부호출없이_잘못된요청으로분류한다()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new 미국농수산가격조회Service(
            [],
            cache,
            NullLogger<미국농수산가격조회Service>.Instance);

        var result = await service.조회Async(new 미국농수산가격조회요청
        {
            SourceKey = "unknown-source",
            Commodity = "CORN",
            YearFrom = 2025,
            YearTo = 2026
        });

        Assert.False(result.Success);
        Assert.Equal(미국농수산가격조회상태Codes.지원하지않는출처, result.StatusCode);
    }

    private sealed class StubProvider : I미국농수산가격공급자
    {
        public string SourceKey => 미국농수산가격출처Keys.UsdaNassQuickStats;

        public string ProviderName => "stub";

        public string DocumentationUrl => "https://example.test";

        public bool IsConfigured => true;

        public int CallCount { get; private set; }

        public Task<미국농수산가격조회응답> 조회Async(
            미국농수산가격조회요청 request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new 미국농수산가격조회응답
            {
                Success = true,
                StatusCode = 미국농수산가격조회상태Codes.완료,
                SourceKey = SourceKey,
                Provider = ProviderName,
                DocumentationUrl = DocumentationUrl,
                Query = request,
                Summary = "complete"
            });
        }
    }
}
