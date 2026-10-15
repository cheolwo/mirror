using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Domain.Pricing;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed partial class 배차수락CommandHandlerTests
{
    [Fact]
    public async Task 소형직선정책OFF는기존기사금액과고객요금을변경하지않는다()
    {
        await using var db = CreateContext(); var (queue, _, fare) = await SeedQuickPricing(db);
        var service = new 소형배송기사제안요금Service(db, Options.Create(new StraightLineDriverPricingOptions()));
        Assert.True((await service.준비Async(queue)).IsSuccess);
        Assert.True((await service.수락검증Async(queue, "driver-1")).IsSuccess);
        Assert.Null(queue.기사제안요금계산근거Json); Assert.Equal(7777m, fare.기사지급예정운임);
        Assert.Equal(34000m, fare.최종운임); Assert.Equal(15m, fare.예상거리Km);
    }

    [Theory]
    [InlineData("1톤")]
    [InlineData("다마스")]
    [InlineData("라보")]
    [InlineData("자전거")]
    public async Task 일반화물과확인되지않은소형차종은새직선기사요금을적용하지않는다(string vehicle)
    {
        await using var db = CreateContext(); var (queue, request, fare) = await SeedQuickPricing(db);
        request.차량종류 = vehicle; await db.SaveChangesAsync();
        var service = QuickPricing(db, QuickOptions());
        Assert.True((await service.준비Async(queue)).IsSuccess);
        Assert.Null(queue.기사제안요금계산근거Json); Assert.Equal(7777m, fare.기사지급예정운임);
    }

    [Fact]
    public async Task 정책OFF후기존소형제안근거가손상되면추천준비에서도차단한다()
    {
        await using var db = CreateContext(); var (queue, _, fare) = await SeedQuickPricing(db);
        var policy = QuickOptions(); var service = QuickPricing(db, policy);
        Assert.True((await service.준비Async(queue)).IsSuccess);
        var payout = queue.기사지급예정액;
        policy.Enabled = false; queue.기사제안요금계산근거Json = "{}";
        var result = await service.준비Async(queue);
        Assert.Equal("SmallGoodsDriverPricingEvidenceInvalid", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Equal(payout, queue.기사지급예정액); Assert.Equal(7777m, fare.기사지급예정운임);
        Assert.Null(queue.확정기사Id);
    }

    [Fact]
    public async Task 새소형제안의기사기본비근거가없으면고객요금으로대체하지않는다()
    {
        await using var db = CreateContext(); var (queue, _, fare) = await SeedQuickPricing(db);
        var policy = QuickOptions(); policy.SmallGoodsBaseFeeKrw = null;
        var result = await QuickPricing(db, policy).준비Async(queue);
        Assert.Equal("SmallGoodsDriverPricingPolicyUnavailable", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.기사지급예정액); Assert.Null(queue.기사제안요금계산근거Json);
        Assert.Equal(7777m, fare.기사지급예정운임);
    }

    [Fact]
    public async Task 직선요금은원거리에비례하고원단위반올림하며고객청구는보존한다()
    {
        await using var db = CreateContext(); var (queue, _, fare) = await SeedQuickPricing(db);
        Assert.True((await QuickPricing(db, QuickOptions()).준비Async(queue)).IsSuccess);
        var pricing = Assert.IsType<Ssalddel.Contracts.Common.Drivers.DriverOfferPricingDto>(소형배송기사제안요금Service.TryReadPricing(queue));
        var distance = StraightLineDeliveryPricingPolicy.CalculateDistanceKm(37.5m, 127m, 37.5234567m, 127.0123456m);
        Assert.Equal(distance * 1000m, pricing.RawDistanceMeters);
        Assert.Equal(decimal.Round(distance * 1000m, 0, MidpointRounding.AwayFromZero), pricing.DistanceFeeKrw);
        Assert.Equal(1000m, pricing.RatePerKmKrw); Assert.Equal(0m, pricing.IncludedDistanceMeters);
        Assert.Equal(1000m + pricing.DistanceFeeKrw + 200m + 300m, pricing.TotalPayoutKrw);
        Assert.Equal(7777m, fare.기사지급예정운임); // 제안은 아직 수락이 아닙니다.
        Assert.Equal(34000m, fare.최종운임); Assert.Equal(15m, fare.예상거리Km);
    }

    [Fact]
    public async Task SQL좌표소수자릿수재표현은동일좌표의동결근거를바꾸지않는다()
    {
        await using var db = CreateContext(); var (queue, _, _) = await SeedQuickPricing(db);
        var service = QuickPricing(db, QuickOptions()); Assert.True((await service.준비Async(queue)).IsSuccess);
        var json = queue.기사제안요금계산근거Json;
        queue.픽업_위도 = 37.500000000m; queue.픽업_경도 = 127.000000000m;
        Assert.Equal(0, queue.기사제안요금판정시각Utc!.Value.Ticks % 10);
        Assert.NotNull(소형배송기사제안요금Service.TryReadPricing(queue));
        Assert.True((await service.조회검증Async(queue)).IsSuccess);
        Assert.Equal(json, queue.기사제안요금계산근거Json);
    }

    [Fact]
    public async Task 최소지급액과영거리도명시기사정책만사용한다()
    {
        await using var db = CreateContext(); var (queue, request, _) = await SeedQuickPricing(db);
        queue.하차_위도 = request.하차_위도 = queue.픽업_위도; queue.하차_경도 = request.하차_경도 = queue.픽업_경도;
        await db.SaveChangesAsync(); var policy = QuickOptions(); policy.SmallGoodsMinimumPayoutKrw = 5000m;
        Assert.True((await QuickPricing(db, policy).준비Async(queue)).IsSuccess);
        var pricing = 소형배송기사제안요금Service.TryReadPricing(queue)!;
        Assert.Equal(0m, pricing.RawDistanceMeters); Assert.Equal(0m, pricing.DistanceFeeKrw); Assert.Equal(5000m, pricing.TotalPayoutKrw);
    }

    [Fact]
    public async Task 원의뢰와좌표가다르면가격동결없이보류한다()
    {
        await using var db = CreateContext(); var (queue, _, _) = await SeedQuickPricing(db);
        queue.하차_경도 = 128m;
        var result = await QuickPricing(db, QuickOptions()).준비Async(queue);
        Assert.Equal("SmallGoodsPricingCoordinatesInvalid", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.기사제안요금계산근거Json);
    }

    [Fact]
    public async Task 정책시작전노출제안은보존하고시작후근거누락은조회와수락을보류한다()
    {
        await using var db = CreateContext(); var (queue, _, fare) = await SeedQuickPricing(db);
        var policy = QuickOptions(); var service = QuickPricing(db, policy);
        queue.추천시작시각 = policy.SmallGoodsEffectiveFromUtc!.Value.AddMinutes(-1);
        Assert.True((await service.조회검증Async(queue)).IsSuccess);
        Assert.True((await service.준비Async(queue)).IsSuccess); Assert.Null(queue.기사제안요금계산근거Json);
        queue.추천시작시각 = DateTime.UtcNow;
        Assert.Equal("SmallGoodsDriverPricingNotPrepared", Assert.Single((await service.조회검증Async(queue)).Errors).Metadata["ErrorCode"]);
        Assert.True((await service.수락검증Async(queue, "driver-1")).IsFailed);
        Assert.Equal(7777m, fare.기사지급예정운임);
    }

    [Fact]
    public async Task 제안부터공개조회수락까지같은기사금액을쓰고현재요율변경과재수락은보존한다()
    {
        await using var db = CreateContext(); var (queue, request, fare) = await SeedQuickPricing(db);
        var policy = QuickOptions(); var service = QuickPricing(db, policy);
        Assert.True((await service.준비Async(queue)).IsSuccess);
        var beforeJson = queue.기사제안요금계산근거Json; var expected = queue.기사지급예정액;
        queue.배차큐단계 = 상태값.배차큐단계.공개배차; queue.배차노출상태 = 상태값.배차노출상태.공개중;
        queue.생활배송배차방식 = NeighborhoodDispatchModes.PublicCall; queue.추천라운드 = 4; queue.공개전환시각 = DateTime.UtcNow;
        await db.SaveChangesAsync();
        policy.SmallGoodsBaseFeeKrw = 9000m; policy.SmallGoodsPolicyRevision = "changed-current-policy";
        var offers = await new 공개배차Service(db, smallGoodsPricing: service).GetPublicDispatchesAsync("driver-1");
        Assert.Equal(expected, Assert.Single(offers).Pricing!.TotalPayoutKrw);
        Assert.Equal(beforeJson, queue.기사제안요금계산근거Json);
        var handler = QuickHandler(db, service);
        Assert.True((await handler.Handle(new 배차수락Command("driver-1", request.의뢰Id, 4), default)).IsSuccess);
        Assert.Equal(expected, fare.기사지급예정운임); Assert.Equal(34000m, fare.최종운임); Assert.Equal(15m, fare.예상거리Km);
        Assert.Equal(34000m, queue.운임); Assert.Equal(beforeJson, queue.기사제안요금계산근거Json);
        queue.기사제안요금계산근거Json = "bad-json"; await db.SaveChangesAsync();
        Assert.True((await handler.Handle(new 배차수락Command("driver-1", request.의뢰Id, 4), default)).IsSuccess);
        Assert.Equal(expected, fare.기사지급예정운임);
    }

    [Fact]
    public async Task 제안좌표변경과손상근거는조회수락에서차단하고원장을확정하지않는다()
    {
        await using var db = CreateContext(); var (queue, request, fare) = await SeedQuickPricing(db);
        var service = QuickPricing(db, QuickOptions()); Assert.True((await service.준비Async(queue)).IsSuccess);
        queue.배차큐단계 = 상태값.배차큐단계.공개배차; queue.배차노출상태 = 상태값.배차노출상태.공개중;
        queue.생활배송배차방식 = NeighborhoodDispatchModes.PublicCall; queue.추천라운드 = 1;
        request.하차_경도 = 128m; await db.SaveChangesAsync();
        Assert.Empty(await new 공개배차Service(db, smallGoodsPricing: service).GetPublicDispatchesAsync("driver-1"));
        var result = await QuickHandler(db, service).Handle(new 배차수락Command("driver-1", request.의뢰Id, 1), default);
        Assert.Equal("SmallGoodsDriverPricingEvidenceInvalid", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.확정기사Id); Assert.Equal(7777m, fare.기사지급예정운임);
        request.하차_경도 = queue.하차_경도; queue.기사제안요금계산근거Json = "{}"; await db.SaveChangesAsync();
        Assert.True((await service.조회검증Async(queue)).IsFailed);
    }

    [Fact]
    public async Task 기존기사약정선택은직선정책으로재계산하지않고원래금액과스냅샷을보존한다()
    {
        await using var db = CreateContext(); var (queue, request, fare) = await SeedQuickPricing(db);
        var now = DateTime.UtcNow; var terms = new AgreedCargoPricingDto(6500m, 200m, 6500m);
        var snapshot = new AgreedRateTableSnapshotDto("agreement-quick", 1, AgreedRateBusinessKinds.CargoTransport, request.차량종류,
            new(AgreedRatePartyKinds.Shipper, request.화주Id), new(AgreedRatePartyKinds.CargoDriver, "driver-1"), null, terms,
            now.AddHours(-1), now.AddHours(1), now, AgreedRateTableEvidencePolicy.TermsDigest(null, terms));
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        fare.기사지급예정운임 = 7777m; fare.합의요금표SnapshotJson = json;
        db.Set<AgreedRateTableRecord>().Add(new()
        {
            AgreementId = snapshot.AgreementId, BusinessKind = snapshot.BusinessKind, VehicleCode = snapshot.VehicleCode,
            ProposerKind = snapshot.Proposer.PartyKind, ProposerId = snapshot.Proposer.PartyId,
            CounterpartyKind = snapshot.Counterparty.PartyKind, CounterpartyId = snapshot.Counterparty.PartyId,
            TermsDigest = snapshot.TermsDigest, AcceptedAtUtc = snapshot.AcceptedAtUtc,
            EffectiveFromUtc = snapshot.EffectiveFromUtc, EffectiveUntilUtc = snapshot.EffectiveUntilUtc, StatusCode = "Accepted"
        });
        db.Set<AgreedRateTableSelectionRecord>().Add(new()
        {
            BusinessKind = snapshot.BusinessKind, WorkId = request.의뢰Id, AgreementId = snapshot.AgreementId,
            AgreementRevision = 1, SnapshotJson = json, AgreedAmountKrw = 7777m, SynchronizationStatusCode = "Synchronized"
        });
        await db.SaveChangesAsync(); var service = QuickPricing(db, QuickOptions());
        Assert.True((await service.준비Async(queue)).IsSuccess); Assert.True((await service.조회검증Async(queue)).IsSuccess);
        Assert.True((await service.수락검증Async(queue, "driver-1")).IsSuccess);
        Assert.Null(queue.기사제안요금계산근거Json); Assert.Equal(json, fare.합의요금표SnapshotJson); Assert.Equal(7777m, fare.기사지급예정운임);
    }

    private static StraightLineDriverPricingOptions QuickOptions() => new()
    {
        Enabled = true, SmallGoodsPolicyRevision = "test-r72", SmallGoodsEffectiveFromUtc = DateTime.UtcNow.AddHours(-1),
        SmallGoodsEffectiveUntilUtc = DateTime.UtcNow.AddHours(1), SmallGoodsBaseFeeKrw = 1000m,
        SmallGoodsMinimumPayoutKrw = 1000m, SmallGoodsWorkFeeKrw = 200m, SmallGoodsSurchargeKrw = 300m
    };
    private static 소형배송기사제안요금Service QuickPricing(SsalddelContext db, StraightLineDriverPricingOptions policy) => new(db, Options.Create(policy));
    private static 배차수락CommandHandler QuickHandler(SsalddelContext db, I소형배송기사제안요금Service service) => new(db,
        new ThrowingPublisher(), new TestCurrentUserAccessor("driver-1", "기사"), new 참여자실행권한검사(),
        new WorkRelationshipSnapshotCollector(), new StubEligibilityService(), new StubContinuityUseCase(),
        NullLogger<배차수락CommandHandler>.Instance, smallGoodsPricing: service);
    private static async Task<(운송원장 Queue, 화주운송의뢰 Work, 운임구성 Fare)> SeedQuickPricing(SsalddelContext db)
    {
        var fare = new 운임구성 { 의뢰Id = "quick-r72", 최종운임 = 34000m, 기사지급예정운임 = 7777m, 예상거리Km = 15m, Km당단가 = 500m };
        db.운임구성.Add(fare); await db.SaveChangesAsync();
        var request = new 화주운송의뢰
        {
            의뢰Id = fare.의뢰Id, 화주Id = "shipper-1", 주문자UserId = "shipper-1", 운임구성Id = fare.Id, 차량종류 = "오토바이",
            최종운임 = fare.최종운임, 결제상태 = 상태값.결제상태.결제대기, 배차상태 = 상태값.배차상태.매칭중,
            정산시점 = "운송완료후정산", 정산상태 = "후불승인완료",
            픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.5234567m, 하차_경도 = 127.0123456m
        };
        var queue = new 운송원장
        {
            의뢰Id = request.의뢰Id, 배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = 상태값.배차대기상태.대기,
            생활배송배차방식 = NeighborhoodDispatchModes.Automatic,
            픽업_위도 = request.픽업_위도, 픽업_경도 = request.픽업_경도, 하차_위도 = request.하차_위도, 하차_경도 = request.하차_경도
        };
        db.화주운송의뢰.Add(request); db.운송원장.Add(queue); await db.SaveChangesAsync();
        return (queue, request, fare);
    }
}
