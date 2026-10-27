using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Hubs;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Tests.Support;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Request;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch;

public sealed class CargoCallOfferProjectionTests
{
    private static readonly DateTime PickupStart = new(2026, 10, 24, 1, 15, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true, "general")]
    [InlineData(true, "group-direct-picking")]
    [InlineData(true, "masked")]
    [InlineData(true, "missing-request")]
    [InlineData(true, "null-cargo-kind")]
    [InlineData(true, "empty-cargo-kind")]
    [InlineData(true, "pricing-path")]
    [InlineData(true, "filled-conditions")]
    [InlineData(true, "null-conditions")]
    [InlineData(true, "zero-conditions")]
    [InlineData(false, "general")]
    [InlineData(false, "group-direct-picking")]
    [InlineData(false, "masked")]
    [InlineData(false, "missing-request")]
    [InlineData(false, "null-cargo-kind")]
    [InlineData(false, "empty-cargo-kind")]
    [InlineData(false, "pricing-path")]
    [InlineData(false, "filled-conditions")]
    [InlineData(false, "null-conditions")]
    [InlineData(false, "zero-conditions")]
    public async Task 공개전국콜의전체DTO는_기존flag와추가된운송조건의값및null차이를유지한다(bool isPublic, string scenario)
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"cargo-offer-projection-{Guid.NewGuid():N}", options => options.EnableNullChecks(false))
            .Options, new Encryption());
        var queue = new 운송원장
        {
            Id = 101, 의뢰Id = "request-1", 운송번호 = "transport-1",
            배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.공개배차, 배차노출상태 = 상태값.배차노출상태.공개중,
            픽업_도로명주소 = "상차 원문", 하차_도로명주소 = "하차 원문",
            픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.6m, 하차_경도 = 127.1m,
            추천라운드 = 7, CreatedAt = new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc)
        };
        var request = scenario == "missing-request" ? null : new 화주운송의뢰
        {
            의뢰Id = queue.의뢰Id,
            화물종류 = scenario == "null-cargo-kind" ? null! : scenario == "empty-cargo-kind" ? "" : "원천 상자",
            클라이언트요청Id = scenario == "masked" ? NeighborhoodDeliveryRoutes.ClientRequestPrefix + "fixture" : string.Empty
        };
        var cargo = new 화물요구조건 { 의뢰Id = queue.의뢰Id, 주의사항 = "화물조건 원문" };
        if (request is not null)
        {
            if (scenario is "filled-conditions" or "masked")
            {
                request.운송방식 = "단독";
                request.차량종류 = "1톤 카고";
                request.화물중량Kg = 12.5m;
                request.화물수량 = 3;
                request.픽업_시간창_시작일시 = DateTime.SpecifyKind(PickupStart, DateTimeKind.Unspecified);
                request.픽업_시간창_종료일시 = PickupStart.AddHours(1);
                request.하차_시간창_시작일시 = DateTime.SpecifyKind(PickupStart.AddHours(2), DateTimeKind.Unspecified);
                request.하차_시간창_종료일시 = PickupStart.AddHours(3);
            }
            if (scenario == "null-conditions")
            {
                request.운송방식 = null!;
                request.차량종류 = null!;
            }
            if (scenario == "zero-conditions")
            {
                request.화물중량Kg = 0m;
                request.화물수량 = 0;
            }
        }
        var directPicking = scenario == "group-direct-picking";
        if (directPicking)
        {
            queue.원본의뢰유형 = 운송의뢰배차원천유형.공동주문국내운송;
            queue.공동구매도착지유형코드 = 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution;
            queue.공동구매기사세대배송여부 = true;
            queue.공동구매세대배송건수 = 7;
            db.Set<분산배송주문Record>().Add(new() { OrderId = "distributed-order", CargoRequestId = queue.의뢰Id });
        }
        if (scenario == "pricing-path")
            queue.기사제안요금계산근거Json = """
                {"Pricing":{"distanceBasisCode":"StraightLine","rawDistanceMeters":1234,"ratePerKmKrw":1000,"includedDistanceMeters":0,"roundingCode":"ProportionalAwayFromZero","distanceFeeKrw":1234,"totalPayoutKrw":5555,"policyRevision":"fixture"}}
                """;
        db.운송원장.Add(queue);
        if (request is not null) db.화주운송의뢰.Add(request);
        db.화물요구조건.Add(cargo);
        await db.SaveChangesAsync();
        var queueBefore = JsonSerializer.Serialize(queue);
        var requestBefore = JsonSerializer.Serialize(request);
        var expected = isPublic ? LegacyPublic(queue, request, cargo, directPicking) : LegacyNational(queue, request, directPicking);

        var actual = Assert.Single(isPublic
            ? await new 공개배차Service(db, 신규업무자격: Simulation운송자격Guard.Instance).GetPublicDispatchesAsync("driver")
            : await new NationalDispatchRequestService(db, new Rejections(), 신규업무자격: Simulation운송자격Guard.Instance).GetNationwideRequestsAsync("driver"));

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(queueBefore, JsonSerializer.Serialize(queue));
        Assert.Equal(requestBefore, JsonSerializer.Serialize(request));
        Assert.False(db.ChangeTracker.HasChanges());
        AssertConditionFields(actual, scenario);
        Assert.Equal(isPublic ? 7 : 0, actual.추천라운드);
        Assert.Equal(isPublic ? "public" : "", actual.추천유형);
        Assert.Equal(isPublic ? "공개배차" : "", actual.추천사유);
        Assert.Equal(isPublic ? 0m : (decimal?)null, actual.추천점수);
        Assert.Equal(isPublic ? new[] { "공개배차" } : [], actual.배지);
        Assert.Empty(actual.경고);
        Assert.True(actual.차량적합여부);
        Assert.Null(actual.추천시작시각);
        Assert.Null(actual.추천만료시각);
        Assert.Null(actual.기사지급예정운임);
        Assert.Null(actual.예상수익);
        if (scenario == "pricing-path")
        {
            if (isPublic) Assert.Null(actual.Pricing);
            else Assert.Equal(5555m, actual.Pricing!.TotalPayoutKrw);
        }
        if (scenario == "masked")
        {
            Assert.Equal(생활배송기사정보공개Policy.PickupPending, actual.픽업지);
            Assert.Equal(생활배송기사정보공개Policy.DropoffPending, actual.하차지);
            Assert.Null(actual.픽업_위도); Assert.Null(actual.픽업_경도);
            Assert.Null(actual.하차_위도); Assert.Null(actual.하차_경도);
        }
        if (directPicking)
        {
            Assert.True(actual.공동주문운송여부);
            Assert.True(actual.세대배송포함여부);
            Assert.Equal(7, actual.세대배송건수);
            Assert.Equal(new[] { "Pick", "Deliver" }, actual.AssignedActivities);
        }
        if (scenario == "null-cargo-kind") Assert.Equal(isPublic ? cargo.주의사항 : null, actual.화물종류);
        if (scenario == "empty-cargo-kind") Assert.Equal("", actual.화물종류);
        if (scenario == "missing-request") Assert.Equal(isPublic ? cargo.주의사항 : queue.픽업_도로명주소, actual.화물종류);
    }

    // 기존 전체 DTO와 인증 콜의 운송 조건 확장 계약을 독립 fixture로 유지합니다.
    // 제품의 두 Projector를 호출하지 않아 필드 누락·원장값 변경을 전체 JSON 대조에서 찾습니다.
    private static DispatchRecommendationDto LegacyPublic(운송원장 item, 화주운송의뢰? request, 화물요구조건? cargo, bool directPicking)
    {
        var recommendation = new DispatchRecommendationDto
        {
            WorkKind = SharedDriverWorkAdmissionService.ResolveWorkKind(item),
            AssignedActivities = 기사제안표시근거Service.운송작업(directPicking),
            Pricing = 소형배송기사제안요금Service.TryReadPricing(item),
            의뢰Id = item.의뢰Id,
            화물종류 = request?.화물종류 ?? cargo?.주의사항 ?? item.픽업_도로명주소,
            운송방식 = request?.운송방식,
            차량종류 = request?.차량종류,
            화물중량Kg = request?.화물중량Kg,
            화물수량 = request?.화물수량,
            상차시간창시작일시 = ExpectedUtc(request?.픽업_시간창_시작일시),
            상차시간창종료일시 = ExpectedUtc(request?.픽업_시간창_종료일시),
            하차시간창시작일시 = ExpectedUtc(request?.하차_시간창_시작일시),
            하차시간창종료일시 = ExpectedUtc(request?.하차_시간창_종료일시),
            픽업지 = item.픽업_도로명주소,
            하차지 = item.하차_도로명주소,
            픽업_위도 = item.픽업_위도,
            픽업_경도 = item.픽업_경도,
            하차_위도 = item.하차_위도,
            하차_경도 = item.하차_경도,
            추천라운드 = item.추천라운드,
            추천유형 = "public",
            추천사유 = "공개배차",
            추천점수 = 0m,
            상태 = 상태값.배차큐단계.공개배차.ToString(),
            배차상태 = 상태값.배차노출상태.공개중.ToString(),
            배지 = ["공개배차"],
            경고 = Array.Empty<string>(),
            차량적합여부 = true,
            차량부적합사유 = Array.Empty<string>(),
            차량경고 = Array.Empty<string>()
        };

        DispatchRecommendationRequestTypeClassifier.ApplyTo(
            recommendation,
            item.원본의뢰유형,
            item.공동구매도착지유형코드,
            item.공동구매기사세대배송여부,
            item.공동구매세대배송건수);
        if (생활배송기사정보공개Policy.생활배송인가(request?.클라이언트요청Id))
            생활배송기사정보공개Policy.추천정보가림(recommendation);
        return recommendation;
    }

    private static DispatchRecommendationDto LegacyNational(운송원장 q, 화주운송의뢰? request, bool directPicking)
    {
        var recommendation = new DispatchRecommendationDto
        {
            WorkKind = Ssalddel.Services.Dispatch.Participation.SharedDriverWorkAdmissionService.ResolveWorkKind(q),
            AssignedActivities = 기사제안표시근거Service.운송작업(directPicking),
            Pricing = 기사제안표시근거Service.요금근거(q),
            의뢰Id = q.의뢰Id,
            화물종류 = request is not null ? request.화물종류 : q.픽업_도로명주소,
            운송방식 = request?.운송방식,
            차량종류 = request?.차량종류,
            화물중량Kg = request?.화물중량Kg,
            화물수량 = request?.화물수량,
            상차시간창시작일시 = ExpectedUtc(request?.픽업_시간창_시작일시),
            상차시간창종료일시 = ExpectedUtc(request?.픽업_시간창_종료일시),
            하차시간창시작일시 = ExpectedUtc(request?.하차_시간창_시작일시),
            하차시간창종료일시 = ExpectedUtc(request?.하차_시간창_종료일시),
            픽업지 = q.픽업_도로명주소,
            하차지 = q.하차_도로명주소,
            픽업_위도 = q.픽업_위도,
            픽업_경도 = q.픽업_경도,
            하차_위도 = q.하차_위도,
            하차_경도 = q.하차_경도,
            직선거리Km = null,
            주행거리Km = null,
            상태 = q.상태,
            배차상태 = 상태값.배차상태.대기
        };

        DispatchRecommendationRequestTypeClassifier.ApplyTo(
            recommendation,
            q.원본의뢰유형,
            q.공동구매도착지유형코드,
            q.공동구매기사세대배송여부,
            q.공동구매세대배송건수);
        if (생활배송기사정보공개Policy.생활배송인가(request?.클라이언트요청Id))
            생활배송기사정보공개Policy.추천정보가림(recommendation);
        return recommendation;
    }

    // 이 fixture의 시각은 UTC 또는 동일한 UTC ticks를 가진 EF Unspecified 값입니다.
    private static DateTime? ExpectedUtc(DateTime? value)
        => value is { } instant && instant != default ? new DateTime(instant.Ticks, DateTimeKind.Utc) : null;

    private static void AssertConditionFields(DispatchRecommendationDto actual, string scenario)
    {
        var filled = scenario is "filled-conditions" or "masked";
        var absent = scenario is "missing-request" or "null-conditions";
        Assert.Equal(filled ? "단독" : absent ? null : "혼적", actual.운송방식);
        Assert.Equal(filled ? "1톤 카고" : absent ? null : string.Empty, actual.차량종류);
        Assert.Equal(filled ? 12.5m : scenario == "zero-conditions" ? 0m : (decimal?)null, actual.화물중량Kg);
        Assert.Equal(filled ? 3 : scenario == "zero-conditions" ? 0 : (int?)null, actual.화물수량);
        var showWindow = scenario == "filled-conditions";
        Assert.Equal(showWindow ? PickupStart : (DateTime?)null, actual.상차시간창시작일시);
        Assert.Equal(showWindow ? PickupStart.AddHours(1) : (DateTime?)null, actual.상차시간창종료일시);
        Assert.Equal(showWindow ? PickupStart.AddHours(2) : (DateTime?)null, actual.하차시간창시작일시);
        Assert.Equal(showWindow ? PickupStart.AddHours(3) : (DateTime?)null, actual.하차시간창종료일시);
        if (showWindow)
        {
            Assert.Equal(DateTimeKind.Utc, actual.상차시간창시작일시!.Value.Kind);
            Assert.Equal(DateTimeKind.Utc, actual.하차시간창시작일시!.Value.Kind);
        }
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Rejections : IDriverRejectedRequestStore
    {
        public Task RejectAsync(string driverId, string requestId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsRejectedAsync(string driverId, string requestId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string driverId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string requestId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
}
