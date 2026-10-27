using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch;

public sealed class CargoDriverPayoutProjectionR70Tests
{
    [Fact]
    public async Task 실제추천의_기사운임조회가_배열식별자도_EF에서평가하고_다른운임을가져오지않는다()
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("cargo-fare-query-r70-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
        db.운임구성.AddRange(new 운임구성 { Id = 7, 의뢰Id = "quick-r70", 기사지급예정운임 = 6200m },
            new 운임구성 { Id = 8, 의뢰Id = "other", 기사지급예정운임 = 9900m });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        // 실제 추천 서비스의 조회를 호출합니다. 별도 모사 LINQ는 Span 바인딩 회귀를 잡지 못합니다.
        var service = new 화물배차추천Service(db, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null!);
        var method = typeof(화물배차추천Service).GetMethod("LoadDriverFareMapAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var task = (Task<Dictionary<long, 운임구성>>)method.Invoke(service,
            [new[] { new 화주운송의뢰 { 의뢰Id = "quick-r70", 운임구성Id = 7 } }])!;
        var result = Assert.Single(await task);
        Assert.Equal(7, result.Key); Assert.Equal(6200m, result.Value.기사지급예정운임);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("agreed", "6200")]
    [InlineData("missing", null)]
    [InlineData("negative", null)]
    [InlineData("fraction", null)]
    [InlineData("request-mismatch", null)]
    [InlineData("fare-id-mismatch", null)]
    [InlineData("fare-request-mismatch", null)]
    [InlineData("frozen-conflict", null)]
    [InlineData("broken-small-goods", null)]
    public void 원의뢰와운임연결과동결액이일치할때만_기사지급약정을읽는다(string scenario, string? expected)
    {
        var queue = new 운송원장 { 의뢰Id = "quick-r70", 운임 = 18000m };
        var work = new 화주운송의뢰 { 의뢰Id = queue.의뢰Id, 운임구성Id = 7, 최종운임 = 18000m, 결제예정금액 = 18000 };
        var fare = new 운임구성 { Id = 7, 의뢰Id = work.의뢰Id, 최종운임 = 18000m, 기사지급예정운임 = 6200m };
        switch (scenario)
        {
            case "missing": fare.기사지급예정운임 = null; break;
            case "negative": fare.기사지급예정운임 = -1; break;
            case "fraction": fare.기사지급예정운임 = 6200.5m; break;
            case "request-mismatch": work.의뢰Id = "other"; break;
            case "fare-id-mismatch": fare.Id = 8; break;
            case "fare-request-mismatch": fare.의뢰Id = "other"; break;
            case "frozen-conflict": queue.기사지급예정액 = 6400; break;
            case "broken-small-goods": queue.기사제안요금계산근거Json = "not-json"; break;
        }
        var result = 기사제안표시근거Service.화물보수(queue, work, fare, null);
        Assert.Equal(expected is null ? null : (decimal?)decimal.Parse(expected), result.Amount);
        Assert.Equal(expected is null ? null : "운임구성.기사지급예정운임", result.Source);
        Assert.Equal(18000m, fare.최종운임); Assert.Equal(18000, work.결제예정금액);
    }

    [Fact]
    public void 기존소형제안은_기존고객운임이나이전약정으로재계산하지않는다()
    {
        var queue = new 운송원장 { 의뢰Id = "quick-r70", 기사지급예정액 = 4200m, 기사제안요금계산근거Json = "existing-frozen-evidence" };
        var work = new 화주운송의뢰 { 의뢰Id = queue.의뢰Id, 운임구성Id = 7 };
        var fare = new 운임구성 { Id = 7, 의뢰Id = work.의뢰Id, 최종운임 = 18000m, 기사지급예정운임 = 6200m };
        var price = new DriverOfferPricingDto { TotalPayoutKrw = 4200m };
        var result = 기사제안표시근거Service.화물보수(queue, work, fare, price);
        Assert.Equal(4200m, result.Amount); Assert.Equal("운송원장.기사제안요금계산근거Json", result.Source);
        Assert.Equal(6200m, fare.기사지급예정운임);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("6200.5")]
    public void 완료후에도_유효한동결액없이는_고객운임을기사지급예정액으로보이지않는다(string? raw)
    {
        var queue = new 운송원장 { 운임 = 18000m, 기사지급예정액 = raw is null ? null : decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture) };
        var response = new 기사운송요약응답 { 운임 = queue.운임 };
        기사운송보수Projector.투영(response, queue);
        Assert.Null(response.기사지급예정액); Assert.Null(response.기사지급예정액근거);
        Assert.Equal(18000m, response.운임);
    }

    [Fact]
    public async Task 기사현재_상세_목록은_저장된동결액을같이읽고_다른기사에게공개하지않는다()
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("cargo-pay-r70-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
        var queue = new 운송원장 { 의뢰Id = "quick-r70", 운송번호 = "quick-r70", 기사_운송자 = "owner", 확정기사Id = "owner",
            배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = "운송중", 운임 = 18000m, 기사지급예정액 = 6200m };
        db.운송원장.Add(queue); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var current = await new 운송현재조회QueryHandler(db).Handle(new("owner"), default);
        var detail = await new 운송상세조회QueryHandler(db).Handle(new("owner", queue.Id), default);
        var list = await new 운송목록조회QueryHandler(db).Handle(new("owner"), default);
        foreach (var response in new[] { current, detail, Assert.Single(list) })
        {
            Assert.NotNull(response); Assert.Equal(18000m, response.운임); Assert.Equal(6200m, response.기사지급예정액);
            Assert.Equal("운송원장.기사지급예정액", response.기사지급예정액근거);
        }
        Assert.Null(await new 운송상세조회QueryHandler(db).Handle(new("other", queue.Id), default));
        Assert.Empty(await new 운송목록조회QueryHandler(db).Handle(new("other"), default));
        Assert.Empty(db.ChangeTracker.Entries());
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
