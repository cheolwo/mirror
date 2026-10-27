using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Progress;
using Ssalddel.Application.Common.Transport;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Controllers.Driver.Recommendation02;
using Ssalddel.Hubs;
using Ssalddel.Services.Dispatch.Compliance;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Request;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver;

/// <summary>메모리 원장의 실제 조회와 JSON 응답 경계에서 화물 조건·시간·정보 보호를 검증합니다.</summary>
public sealed class CargoRecommendationConditionsProjectionTests
{
    private static readonly DateTime Start = new(2026, 10, 24, 1, 15, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("public", "혼적")]
    [InlineData("public", "단독")]
    [InlineData("national", "혼적")]
    [InlineData("national", "단독")]
    public async Task 공개콜과전국콜은_실제화주의_운송방식과상하차시간을반환한다(string scope, string mode)
    {
        await using var db = Context();
        await Seed(db, mode);
        var response = Assert.Single(await ReadOffers(db, scope));
        AssertConditions(response, mode);
        // 원장/wire 값은 단독 그대로이며 표시용 독차로 덮어쓰지 않습니다.
        Assert.Equal(mode, (await db.화주운송의뢰.SingleAsync()).운송방식);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("national")]
    public async Task 과거의뢰의_미입력시간창은_현재시각이나기본날짜로대체하지않는다(string scope)
    {
        await using var db = Context();
        await Seed(db);
        var request = await db.화주운송의뢰.SingleAsync();
        request.픽업_시간창_시작일시 = default;
        request.픽업_시간창_종료일시 = default;
        request.하차_시간창_시작일시 = default(DateTime);
        request.하차_시간창_종료일시 = null;
        await db.SaveChangesAsync();
        AssertNoWindows(Assert.Single(await ReadOffers(db, scope)));
    }

    [Theory]
    [InlineData("public")]
    [InlineData("national")]
    public async Task 생활배송의_공개추천은_정확주소좌표와함께시간창도보류한다(string scope)
    {
        await using var db = Context();
        await Seed(db);
        (await db.화주운송의뢰.SingleAsync()).클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + "test";
        await db.SaveChangesAsync();
        var response = Assert.Single(await ReadOffers(db, scope));
        AssertNoWindows(response);
        Assert.Equal(생활배송기사정보공개Policy.PickupPending, response.픽업지);
        Assert.Null(response.픽업_위도);
        Assert.Null(response.하차_경도);
    }

    [Theory]
    [InlineData("혼적")]
    [InlineData("단독")]
    public async Task 의뢰상세도_같은원장의_조건을읽는다(string mode)
    {
        await using var db = Context();
        await Seed(db, mode);
        var response = await DetailReader(db).Handle(new 운송의뢰상세조회Query("driver", "request"), default);
        AssertConditions(response, mode);
    }

    [Fact]
    public async Task 관계없는기사는_새시간창이추가돼도_생활배송의뢰를조회하지못한다()
    {
        await using var db = Context();
        await Seed(db);
        (await db.화주운송의뢰.SingleAsync()).클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + "test";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => DetailReader(db)
            .Handle(new 운송의뢰상세조회Query("other-driver", "request"), default));
    }

    [Fact]
    public async Task 배정기사도_생활배송정보동의가없으면_상세시간창을볼수없다()
    {
        await using var db = Context();
        await Seed(db);
        (await db.화주운송의뢰.SingleAsync()).클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + "test";
        (await db.운송원장.SingleAsync()).확정기사Id = "driver";
        await db.SaveChangesAsync();
        var response = await DetailReader(db).Handle(new 운송의뢰상세조회Query("driver", "request"), default);
        AssertNoWindows(response);
        Assert.Equal(생활배송기사정보공개Policy.PickupPending, response.픽업지);
    }

    [Theory]
    [InlineData("current")]
    [InlineData("detail")]
    [InlineData("list")]
    public async Task 진행운송의_세조회도_같은의뢰의조건을보존한다(string scope)
    {
        await using var db = Context();
        var transport = await SeedAssigned(db);
        AssertConditions(Assert.IsAssignableFrom<기사운송요약응답>(await ReadAssigned(db, scope, "driver", transport.Id)), "단독");
        Assert.Null(await ReadAssigned(db, scope, "other-driver", transport.Id));
    }

    [Theory]
    [InlineData("current")]
    [InlineData("detail")]
    [InlineData("list")]
    public async Task 진행운송은_연결원장이없으면_조건을만들지않는다(string scope)
    {
        await using var db = Context();
        var transport = await SeedAssigned(db);
        db.화주운송의뢰.Remove(await db.화주운송의뢰.SingleAsync());
        await db.SaveChangesAsync();
        AssertNoConditions(Assert.IsAssignableFrom<기사운송요약응답>(await ReadAssigned(db, scope, "driver", transport.Id)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 운영자목록은_명시의뢰관계와_빈의뢰Id의기존운송번호만조회한다(bool legacy)
    {
        await using var db = Context();
        var transport = await SeedAssigned(db);
        transport.운송번호 = legacy ? "request" : "display-number";
        if (legacy) transport.의뢰Id = string.Empty;
        db.화주운송의뢰.Add(Request("display-number", "혼적"));
        await db.SaveChangesAsync();
        var response = Assert.Single(await new 관리자운송목록조회QueryHandler(db)
            .Handle(new 관리자운송목록조회Query(null, "freight-delivery"), default));
        AssertConditions(response, "단독");
    }

    [Fact]
    public async Task 운영자의_음식배달목록에는_같은Id의화물조건을붙이지않는다()
    {
        await using var db = Context();
        var transport = await SeedAssigned(db);
        transport.배차업무유형 = 상태값.배차업무유형.음식배달;
        await db.SaveChangesAsync();
        var response = Assert.Single(await new 관리자운송목록조회QueryHandler(db)
            .Handle(new 관리자운송목록조회Query(null, "food-delivery"), default));
        AssertNoConditions(response);
    }

    [Fact]
    public void 추천응답의_새필드는_네이티브와웹수신계약에서손실되지않는다()
    {
        var wire = new DispatchRecommendationDto { 의뢰Id = "request" };
        화물운송조건Projector.투영(wire, Request("request", "단독"));
        var json = JsonSerializer.Serialize(wire, Json);
        var native = JsonSerializer.Deserialize<기사배차추천항목응답>(json, Json)!;
        var web = JsonSerializer.Deserialize<기사추천수신항목>(json, Json)!;
        AssertConditions(native, "단독");
        AssertConditions(web, "단독");
    }

    [Theory]
    [InlineData("recommendation")]
    [InlineData("detail")]
    [InlineData("admin")]
    public void 다른의뢰의원장을_잘못연결해도_조건을노출하지않는다(string kind)
    {
        var foreign = Request("foreign", "단독");
        object actual;
        if (kind == "recommendation")
        {
            var item = new DispatchRecommendationDto { 의뢰Id = "request", 운송방식 = "stale", 상차시간창시작일시 = Start };
            화물운송조건Projector.투영(item, foreign); actual = item;
        }
        else if (kind == "detail")
        {
            var item = new 기사운송의뢰상세응답 { 의뢰Id = "request", 운송방식 = "stale", 상차시간창시작일시 = Start };
            화물운송조건Projector.투영(item, foreign); actual = item;
        }
        else
        {
            var item = new 운송진행응답 { 운송방식 = "stale", 상차시간창시작일시 = Start };
            화물운송조건Projector.투영(item, foreign, "request"); actual = item;
        }
        AssertNoConditions(actual);
    }

    [Fact]
    public void EF의Kind없는UTC값은_동일시각Z로전송하고_레거시기본값은null이다()
    {
        var unspecified = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified);
        Assert.Equal(Start, 화물운송조건Projector.시각(unspecified));
        Assert.Equal(DateTimeKind.Utc, 화물운송조건Projector.시각(unspecified)!.Value.Kind);
        Assert.Equal(Start, 화물운송조건Projector.시각(Start.ToLocalTime()));
        Assert.Null(화물운송조건Projector.시각(default(DateTime)));
        Assert.Null(화물운송조건Projector.시각(null));
    }

    [Fact]
    public void 익명공개요약에는_정확시간창과연락처를추가하지않고_기사콜권한도유지한다()
    {
        var source = Request("request", "혼적");
        var json = JsonSerializer.Serialize(화주운송의뢰매퍼.To공개화물요약응답(source), Json);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("상차시간창시작일시", out _));
        Assert.False(document.RootElement.TryGetProperty("하차시간창종료일시", out _));
        Assert.False(document.RootElement.TryGetProperty("픽업", out _));
        foreach (var type in new[] { typeof(기사배차추천Controller), typeof(기사공개배차Controller) })
        {
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Roles == 역할명.기사);
            Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>());
        }
    }

    private static 기사배차추천항목응답 Conditions(object actual)
        => JsonSerializer.Deserialize<기사배차추천항목응답>(JsonSerializer.Serialize(actual, actual.GetType(), Json), Json)!;

    private static void AssertConditions(object actual, string mode)
    {
        var value = Conditions(actual);
        Assert.Equal(mode, value.운송방식);
        Assert.Equal("1톤 탑차", value.차량종류);
        Assert.Equal(250m, value.화물중량Kg);
        Assert.Equal(7, value.화물수량);
        Assert.Equal(Start, value.상차시간창시작일시);
        Assert.Equal(Start.AddMinutes(45), value.상차시간창종료일시);
        Assert.Equal(Start.AddHours(2), value.하차시간창시작일시);
        Assert.Equal(Start.AddHours(3), value.하차시간창종료일시);
        Assert.Equal(DateTimeKind.Utc, value.상차시간창시작일시!.Value.Kind);
    }

    private static void AssertNoWindows(object actual)
    {
        var value = Conditions(actual);
        Assert.Null(value.상차시간창시작일시); Assert.Null(value.상차시간창종료일시);
        Assert.Null(value.하차시간창시작일시); Assert.Null(value.하차시간창종료일시);
    }

    private static void AssertNoConditions(object actual)
    {
        var value = Conditions(actual);
        Assert.Null(value.운송방식); Assert.Null(value.차량종류);
        Assert.Null(value.화물중량Kg); Assert.Null(value.화물수량);
        AssertNoWindows(actual);
    }

    private static 운송의뢰상세조회QueryHandler DetailReader(SsalddelContext db)
        => new(db, new 차량화물적합성Service(new 차량적재추천Engine()));

    private static Task<IReadOnlyList<DispatchRecommendationDto>> ReadOffers(SsalddelContext db, string scope)
        => scope == "public" ? new 공개배차Service(db, 신규업무자격: new Guard()).GetPublicDispatchesAsync("driver")
            : new NationalDispatchRequestService(db, new Rejections(), 신규업무자격: new Guard()).GetNationwideRequestsAsync("driver");

    private static async Task<기사운송요약응답?> ReadAssigned(SsalddelContext db, string scope, string driver, long id)
        => scope switch
        {
            "current" => await new 운송현재조회QueryHandler(db).Handle(new 운송현재조회Query(driver), default),
            "detail" => await new 운송상세조회QueryHandler(db).Handle(new 운송상세조회Query(driver, id), default),
            "list" => (await new 운송목록조회QueryHandler(db).Handle(new 운송목록조회Query(driver), default)).SingleOrDefault(),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("cargo-condition-projection-" + Guid.NewGuid().ToString("N")).Options, new Encryption());

    private static async Task<운송원장> Seed(SsalddelContext db, string mode = "단독")
    {
        db.화주운송의뢰.Add(Request("request", mode));
        var transport = new 운송원장
        {
            의뢰Id = "request", 운송번호 = "request", 배차업무유형 = 상태값.배차업무유형.용달운송,
            상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.공개배차,
            배차노출상태 = 상태값.배차노출상태.공개중,
            픽업_도로명주소 = "상차지역", 하차_도로명주소 = "하차지역"
        };
        db.운송원장.Add(transport);
        await db.SaveChangesAsync();
        return transport;
    }

    private static async Task<운송원장> SeedAssigned(SsalddelContext db)
    {
        var transport = await Seed(db);
        transport.상태 = "상차지도착"; transport.기사_운송자 = "driver"; transport.확정기사Id = "driver";
        await db.SaveChangesAsync();
        return transport;
    }

    private static 화주운송의뢰 Request(string id, string mode) => new()
    {
        의뢰Id = id, 화주Id = "owner", 주문자UserId = "owner", 운송방식 = mode, 차량종류 = "1톤 탑차",
        화물종류 = "상자", 화물중량Kg = 250m, 화물수량 = 7,
        화물길이Mm = 100, 화물폭Mm = 200, 화물높이Mm = 300,
        픽업_도로명주소 = "상차지역", 하차_도로명주소 = "하차지역",
        픽업_시간창_시작일시 = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified),
        픽업_시간창_종료일시 = Start.AddMinutes(45), 하차_시간창_시작일시 = Start.AddHours(2),
        하차_시간창_종료일시 = Start.AddHours(3)
    };

    private sealed class Guard : I신규화물업무자격Guard
    {
        public Task<운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default)
            => Task.FromResult(new 운송자격평가결과(true, "Qualification.Valid", "명시적 시험 결과", 운송자격Codes.Valid));
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Rejections : IDriverRejectedRequestStore
    {
        public Task RejectAsync(string driverId, string requestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsRejectedAsync(string driverId, string requestId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string driverId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string requestId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
}
