using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class 호주농수산식품가격조회ServiceTests
{
    [Fact]
    public async Task 같은조회조건의_완료결과는_공급자를다시호출하지않는다()
    {
        var provider = new StubProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new 호주농수산식품가격조회Service(
            [provider],
            cache,
            NullLogger<호주농수산식품가격조회Service>.Instance);
        var request = ValidRequest();

        var first = await service.조회Async(request);
        var second = await service.조회Async(request);

        Assert.True(first.Success);
        Assert.Same(first, second);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task Abares참고원천은_자동조회로오인하지않는다()
    {
        var provider = new StubProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new 호주농수산식품가격조회Service(
            [provider],
            cache,
            NullLogger<호주농수산식품가격조회Service>.Instance);

        var result = await service.조회Async(new 호주농수산식품가격조회요청
        {
            SourceKey = 호주농수산식품가격출처Keys.AbaresWeeklyAgriculturalPrices,
            StartPeriod = "2026-04",
            EndPeriod = "2026-05"
        });

        Assert.False(result.Success);
        Assert.Equal(호주농수산식품가격조회상태Codes.지원하지않는출처, result.StatusCode);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void Catalog는_Abs자동조회와_Abares파일및참고원천을_구분한다()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new 호주농수산식품가격조회Service(
            [new StubProvider()],
            cache,
            NullLogger<호주농수산식품가격조회Service>.Instance);

        var catalog = service.GetCatalog();

        Assert.Contains(catalog.Sources, source =>
            source.Key == 호주농수산식품가격출처Keys.AbsConsumerPriceIndex
            && source.AutomatedQueryAvailable
            && source.IntegrationStatusCode == "IntegratedApi");
        Assert.Contains(catalog.Sources, source =>
            source.Key == 호주농수산식품가격출처Keys.AbaresWeeklyHorticulturePrices
            && source.ContainsThirdPartyInputs
            && source.IntegrationStatusCode == "ReferenceOnly");
        Assert.Contains(catalog.Sources, source =>
            source.Key == 호주농수산식품가격출처Keys.AbaresFisheriesAquacultureStatistics
            && !source.AutomatedQueryAvailable
            && source.IntegrationStatusCode == "DownloadAvailable");
        Assert.Contains(catalog.Indexes, item =>
            item.Code == 호주식품가격지수Codes.FishAndOtherSeafood);
        Assert.Contains(catalog.Regions, item =>
            item.Code == 호주식품가격지수지역Codes.Melbourne);
    }

    private static 호주농수산식품가격조회요청 ValidRequest()
        => new()
        {
            IndexCode = 호주식품가격지수Codes.BeefAndVeal,
            StartPeriod = "2026-04",
            EndPeriod = "2026-05"
        };

    private sealed class StubProvider : I호주농수산식품가격공급자
    {
        public string SourceKey => 호주농수산식품가격출처Keys.AbsConsumerPriceIndex;

        public string ProviderName => "stub";

        public string DocumentationUrl => "https://example.test";

        public int CallCount { get; private set; }

        public Task<호주농수산식품가격조회응답> 조회Async(
            호주농수산식품가격조회요청 request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new 호주농수산식품가격조회응답
            {
                Success = true,
                StatusCode = 호주농수산식품가격조회상태Codes.완료,
                SourceKey = SourceKey,
                Provider = ProviderName,
                DocumentationUrl = DocumentationUrl,
                Query = request,
                Summary = "complete"
            });
        }
    }
}
