using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Hubs;
using Ssalddel.Services.Community;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Request;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver;

/// <summary>격리된 메모리 원장으로 실제 기사 조회의 생활 배송 정보 제공 경계를 검증합니다.</summary>
public sealed class NeighborhoodDeliveryDriverDisclosureTests
{
    [Theory]
    [InlineData("unrelated")]
    [InlineData("expired")]
    [InlineData("reassigned")]
    public async Task 관계없는기사는_생활배송의뢰상세를_조회할수없다(string relation)
    {
        await using var db = Context();
        var queue = await Seed(db);
        if (relation == "expired")
        {
            queue.확정기사Id = null;
            queue.현재추천대상기사Id = "driver-1";
            queue.배차노출상태 = 상태값.배차노출상태.추천중;
            queue.추천만료시각 = DateTime.UtcNow.AddMinutes(-1);
        }
        else if (relation == "reassigned") queue.확정기사Id = "driver-2";
        else queue.확정기사Id = null;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => DetailReader(db, new Disclosure(true))
            .Handle(new 운송의뢰상세조회Query("driver-1", "request-1"), CancellationToken.None));
    }

    [Fact]
    public async Task 추천대상도_별도동의서비스결과와무관하게_주소좌표를볼수없다()
    {
        await using var db = Context();
        var queue = await Seed(db);
        queue.확정기사Id = null;
        queue.현재추천대상기사Id = "driver-1";
        queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.추천만료시각 = DateTime.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();
        var disclosure = new Disclosure(true);

        var response = await DetailReader(db, disclosure)
            .Handle(new 운송의뢰상세조회Query("driver-1", "request-1"), CancellationToken.None);

        AssertRequestMasked(response);
        Assert.Empty(disclosure.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 확정기사의_의뢰상세는_별도제공동의가있어야_주소를반환한다(bool consented)
    {
        await using var db = Context();
        await Seed(db);
        var disclosure = new Disclosure(consented);

        var response = await DetailReader(db, disclosure)
            .Handle(new 운송의뢰상세조회Query("driver-1", "request-1"), CancellationToken.None);

        if (consented)
        {
            Assert.Equal("픽업 비공개 주소", response.픽업지);
            Assert.Equal("101호", response.픽업상세지);
            Assert.Equal(37.5m, response.픽업위도);
        }
        else AssertRequestMasked(response);
        Assert.Equal(("request-1", "driver-1", "owner-1"), Assert.Single(disclosure.Calls));
    }

    [Fact]
    public async Task 동의서비스가없으면_확정기사에게도_생활배송정보를공개하지않는다()
    {
        await using var db = Context();
        await Seed(db);
        AssertRequestMasked(await DetailReader(db)
            .Handle(new 운송의뢰상세조회Query("driver-1", "request-1"), CancellationToken.None));
    }

    [Theory]
    [InlineData("current", false)]
    [InlineData("list", false)]
    [InlineData("detail", false)]
    [InlineData("current", true)]
    [InlineData("list", true)]
    [InlineData("detail", true)]
    public async Task 현재목록운송상세에도_동일한_별도동의가필요하다(string query, bool consented)
    {
        await using var db = Context();
        await Seed(db);
        var disclosure = new Disclosure(consented);
        var response = await TransportReader(db, query, disclosure);

        Assert.Equal(!consented, response.개인정보제공보류);
        if (consented)
        {
            Assert.Equal("픽업 비공개 주소", response.출발지);
            Assert.Equal("010-1111-2222", response.상차연락처);
            Assert.Equal("010-3333-4444", response.수령자연락처);
            Assert.Equal("비공개 전달 요청", response.전달요청);
        }
        else
        {
            Assert.Equal(생활배송기사정보공개Policy.PickupPending, response.출발지);
            Assert.Equal(생활배송기사정보공개Policy.DropoffPending, response.도착지);
            Assert.Empty(response.상차담당자명); Assert.Empty(response.상차연락처);
            Assert.Empty(response.수령자명); Assert.Empty(response.수령자연락처); Assert.Empty(response.전달요청);
            if (response is 기사운송상세응답 detail) { Assert.Empty(detail.첨부Json); Assert.Empty(detail.메모); }
        }
        Assert.Equal(5000m, response.운임);
        Assert.Equal(("request-1", "driver-1", "owner-1"), Assert.Single(disclosure.Calls));
    }

    [Theory]
    [InlineData("current")]
    [InlineData("list")]
    [InlineData("detail")]
    public async Task 기존화물의_조회와연락처는_새생활배송정책으로변경하지않는다(string query)
    {
        await using var db = Context();
        await Seed(db, "legacy-cargo");
        var disclosure = new Disclosure(false);
        var response = await TransportReader(db, query, disclosure);
        Assert.False(response.개인정보제공보류);
        Assert.Equal("픽업 비공개 주소", response.출발지);
        Assert.Equal("010-1111-2222", response.상차연락처);
        Assert.Empty(disclosure.Calls);
    }

    [Fact]
    public async Task 기존화물의_의뢰상세접근정책은_이번패치가변경하지않는다()
    {
        await using var db = Context();
        await Seed(db, "legacy-cargo");
        var response = await DetailReader(db)
            .Handle(new 운송의뢰상세조회Query("other-driver", "request-1"), CancellationToken.None);
        Assert.Equal("픽업 비공개 주소", response.픽업지);
    }

    [Fact]
    public async Task 생활배송은_정보제공동의후에도_내부메모와첨부를반환하지않고_기존화물은유지한다()
    {
        await using var db = Context();
        await Seed(db);
        var disclosure = new Disclosure(true);
        var request = await DetailReader(db, disclosure)
            .Handle(new 운송의뢰상세조회Query("driver-1", "request-1"), CancellationToken.None);
        var transport = Assert.IsType<기사운송상세응답>(await TransportReader(db, "detail", disclosure));
        Assert.Null(request.정산메모); Assert.Empty(transport.첨부Json); Assert.Empty(transport.메모);
        Assert.Equal("픽업 비공개 주소", request.픽업지);
        Assert.Equal("010-3333-4444", transport.수령자연락처);

        var entity = await db.화주운송의뢰.SingleAsync();
        entity.클라이언트요청Id = "legacy-cargo";
        await db.SaveChangesAsync();
        var legacy = Assert.IsType<기사운송상세응답>(await TransportReader(db, "detail", disclosure));
        Assert.Equal("비공개 첨부", legacy.첨부Json); Assert.Equal("비공개 메모", legacy.메모);
    }

    [Fact]
    public void 추천주소가림은_정확좌표를제거하고_기사운임은유지한다()
    {
        var response = new DispatchRecommendationDto
        {
            픽업지 = "픽업 비공개 주소", 하차지 = "전달 비공개 주소",
            픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.6m, 하차_경도 = 127.1m,
            예상수익 = 5000m
        };
        생활배송기사정보공개Policy.추천정보가림(response);
        Assert.Equal(생활배송기사정보공개Policy.PickupPending, response.픽업지);
        Assert.Equal(생활배송기사정보공개Policy.DropoffPending, response.하차지);
        Assert.Null(response.픽업_위도); Assert.Null(response.픽업_경도);
        Assert.Null(response.하차_위도); Assert.Null(response.하차_경도);
        Assert.Equal(5000m, response.예상수익);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("nationwide")]
    public async Task 공개전국콜도_생활배송의_주소와좌표를_반환하지않는다(string scope)
    {
        await using var db = Context();
        var queue = await Seed(db);
        queue.확정기사Id = null;
        queue.배차큐단계 = 상태값.배차큐단계.공개배차;
        queue.배차노출상태 = 상태값.배차노출상태.공개중;
        queue.상태 = 상태값.배차대기상태.대기;
        queue.픽업_도로명주소 = "픽업 비공개 주소"; queue.하차_도로명주소 = "전달 비공개 주소";
        queue.픽업_위도 = 37.5m; queue.픽업_경도 = 127m;
        queue.하차_위도 = 37.6m; queue.하차_경도 = 127.1m;
        await db.SaveChangesAsync();

        var items = scope == "public"
            ? await new 공개배차Service(db).GetPublicDispatchesAsync("driver-2")
            : await new NationalDispatchRequestService(db, new Rejections()).GetNationwideRequestsAsync("driver-2");
        var response = Assert.Single(items);
        Assert.Equal(생활배송기사정보공개Policy.PickupPending, response.픽업지);
        Assert.Equal(생활배송기사정보공개Policy.DropoffPending, response.하차지);
        Assert.Null(response.픽업_위도); Assert.Null(response.픽업_경도);
        Assert.Null(response.하차_위도); Assert.Null(response.하차_경도);
    }

    private static 운송의뢰상세조회QueryHandler DetailReader(SsalddelContext db, I생활배송기사정보제공동의Service? disclosure = null)
        => new(db, new 차량화물적합성Service(new 차량적재추천Engine()), disclosure);

    private static async Task<기사운송요약응답> TransportReader(SsalddelContext db, string query, Disclosure disclosure)
        => query switch
        {
            "current" => (await new 운송현재조회QueryHandler(db, disclosure).Handle(new 운송현재조회Query("driver-1"), CancellationToken.None))!,
            "list" => Assert.Single(await new 운송목록조회QueryHandler(db, disclosure).Handle(new 운송목록조회Query("driver-1"), CancellationToken.None)),
            _ => (await new 운송상세조회QueryHandler(db, disclosure).Handle(new 운송상세조회Query("driver-1", 1), CancellationToken.None))!
        };

    private static void AssertRequestMasked(기사운송의뢰상세응답 response)
    {
        Assert.Equal(생활배송기사정보공개Policy.PickupPending, response.픽업지);
        Assert.Equal(생활배송기사정보공개Policy.DropoffPending, response.하차지);
        Assert.Empty(response.픽업상세지); Assert.Empty(response.하차상세지);
        Assert.Null(response.픽업위도); Assert.Null(response.픽업경도);
        Assert.Null(response.하차위도); Assert.Null(response.하차경도);
        Assert.Null(response.정산메모); Assert.Empty(response.화물설명);
        Assert.Equal(5000, response.결제예정금액);
    }

    private static async Task<운송원장> Seed(SsalddelContext db, string? source = null)
    {
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-1", 주문자UserId = "owner-1", 화주Id = "owner-1",
            클라이언트요청Id = source ?? NeighborhoodDeliveryRoutes.ClientRequestPrefix + "one",
            픽업_도로명주소 = "픽업 비공개 주소", 픽업_상세주소 = "101호", 픽업_위도 = 37.5m, 픽업_경도 = 127m,
            하차_도로명주소 = "전달 비공개 주소", 하차_상세주소 = "202호", 하차_위도 = 37.6m, 하차_경도 = 127.1m,
            픽업_연락처_이름 = "픽업인", 픽업_연락처_전화번호 = "010-1111-2222",
            하차_연락처_이름 = "수령인", 하차_연락처_전화번호 = "010-3333-4444",
            요청사항 = "비공개 전달 요청", 정산메모 = "비공개 메모", 화물설명 = "주소가 들어갈 수도 있는 자유 입력",
            화물종류 = "상자", 결제예정금액 = 5000
        });
        var queue = new 운송원장
        {
            Id = 1, 의뢰Id = "request-1", 운송번호 = "request-1", 기사_운송자 = "driver-1", 확정기사Id = "driver-1",
            상태 = "배차확정", 배차업무유형 = 상태값.배차업무유형.용달운송,
            배차큐단계 = 상태값.배차큐단계.확정, 출발지 = "픽업 비공개 주소", 도착지 = "전달 비공개 주소",
            첨부_json = "비공개 첨부", 메모 = "비공개 메모", 운임 = 5000m
        };
        db.운송원장.Add(queue); await db.SaveChangesAsync(); return queue;
    }

    private static SsalddelContext Context()
        => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"neighborhood-driver-disclosure-{Guid.NewGuid():N}").Options, new Encryption());

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class Disclosure(bool consented) : I생활배송기사정보제공동의Service
    {
        public List<(string RequestId, string DriverId, string OwnerId)> Calls { get; } = [];
        public Task<bool> 유효한기사제공동의인가Async(string requestId, string driverId, string ownerUserId, CancellationToken cancellationToken = default)
        {
            Calls.Add((requestId, driverId, ownerUserId)); return Task.FromResult(consented);
        }
        public Task<Result<NeighborhoodDeliveryDisclosureResponse>> 기록Async(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<NeighborhoodDeliveryDisclosureResponse?> 내상태Async(string requestId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class Rejections : IDriverRejectedRequestStore
    {
        public Task RejectAsync(string driverId, string requestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsRejectedAsync(string driverId, string requestId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string driverId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string requestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
}
