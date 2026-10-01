using Hongdal.Contracts.Common.Customs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using 홍달.Services.External.PublicData;

namespace Hongdal.Tests.Services.External.PublicData;

public sealed class Hs공공데이터수집ServiceTests
{
    [Fact]
    public async Task 수집_한출처가실패해도다른출처결과를유지한다()
    {
        var successCollector = new StubCollector(
            Hs공공데이터출처Keys.세관장확인대상물품,
            new Hs공공데이터출처응답
            {
                SourceKey = Hs공공데이터출처Keys.세관장확인대상물품,
                StatusCode = Hs공공데이터수집상태Codes.성공,
                Items =
                [
                    new Hs공공데이터정보항목
                    {
                        ItemKey = "requirement",
                        AttentionRequired = true
                    }
                ]
            });
        var failingCollector = new StubCollector(
            Hs공공데이터출처Keys.관세환율,
            new InvalidOperationException("temporary failure"));
        var service = new Hs공공데이터수집Service(
            [successCollector, failingCollector],
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<Hs공공데이터수집Service>.Instance);

        var result = await service.수집Async(new Hs공공데이터수집요청
        {
            HsCode = "0901.21-0000",
            CountryCode = "cn",
            ReferenceMonth = "2026-07",
            ReferenceDate = "2026-07-15",
            SourceKeys =
            [
                Hs공공데이터출처Keys.세관장확인대상물품,
                Hs공공데이터출처Keys.관세환율
            ]
        });

        Assert.Equal("0901210000", result.HsCode);
        Assert.Equal("CN", result.CountryCode);
        Assert.Equal("202607", result.ReferenceMonth);
        Assert.Equal("20260715", result.ReferenceDate);
        Assert.Equal(1, result.SuccessSourceCount);
        Assert.True(result.RequiresProfessionalReview);
        Assert.Equal(2, result.Sources.Count);
        Assert.Contains(result.Sources, source =>
            source.SourceKey == Hs공공데이터출처Keys.관세환율
            && source.StatusCode == Hs공공데이터수집상태Codes.오류);
    }

    [Fact]
    public async Task 수집_성공한동일출처결과를30분캐시한다()
    {
        var collector = new CountingCollector(Hs공공데이터출처Keys.관세환율);
        var service = new Hs공공데이터수집Service(
            [collector],
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<Hs공공데이터수집Service>.Instance);
        var request = new Hs공공데이터수집요청
        {
            HsCode = "0901210000",
            CountryCode = "CN",
            ReferenceMonth = "202607",
            ReferenceDate = "20260715",
            SourceKeys = [Hs공공데이터출처Keys.관세환율]
        };

        await service.수집Async(request);
        await service.수집Async(request);

        Assert.Equal(1, collector.CallCount);
    }

    private sealed class StubCollector : IHs공공데이터수집기
    {
        private readonly Hs공공데이터출처응답? _response;
        private readonly Exception? _exception;

        public StubCollector(string sourceKey, Hs공공데이터출처응답 response)
        {
            SourceKey = sourceKey;
            _response = response;
        }

        public StubCollector(string sourceKey, Exception exception)
        {
            SourceKey = sourceKey;
            _exception = exception;
        }

        public string SourceKey { get; }

        public Task<Hs공공데이터출처응답> 수집Async(
            Hs공공데이터수집요청 request,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                return Task.FromException<Hs공공데이터출처응답>(_exception);
            }

            return Task.FromResult(_response!);
        }
    }

    private sealed class CountingCollector(string sourceKey) : IHs공공데이터수집기
    {
        public string SourceKey { get; } = sourceKey;

        public int CallCount { get; private set; }

        public Task<Hs공공데이터출처응답> 수집Async(
            Hs공공데이터수집요청 request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new Hs공공데이터출처응답
            {
                SourceKey = SourceKey,
                StatusCode = Hs공공데이터수집상태Codes.성공
            });
        }
    }
}
