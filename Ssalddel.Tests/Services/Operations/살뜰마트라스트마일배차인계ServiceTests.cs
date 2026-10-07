using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Operations;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.도메인.운영;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 살뜰마트라스트마일배차인계ServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 소비자연결이준비된인계Fixture는_픽업준비완료후배차를수락한다()
    {
        await using var db = CreateContext();
        var queue = new RecordingDispatchQueue();
        var coordinator = new 운영체제업무인계Coordinator(
            db,
            new FixedTimeProvider(new DateTimeOffset(Now)));
        var service = new 살뜰마트라스트마일배차인계Service(
            coordinator,
            queue,
            new 살뜰마트라스트마일배차Policy(),
            new ReadyReadinessPolicy());

        var request = Request(
            살뜰마트라스트마일준비단계Codes.픽업준비완료,
            Now);
        var result = await service.인계Async(request);
        var replay = await service.인계Async(request);

        Assert.NotNull(result.배차대기);
        Assert.NotNull(result.인계);
        Assert.NotNull(replay.인계);
        Assert.Equal(운영체제업무인계상태Codes.수락됨, result.인계.상태Code);
        Assert.Equal(OperatingSystemIds.SsalddelMartUrbanLogistics, result.인계.출발운영체제Id);
        Assert.Equal(OperatingSystemIds.FoodDelivery, result.인계.도착운영체제Id);
        Assert.Equal(OperatingSystemIds.FoodDelivery, result.인계.현재책임운영체제Id);
        Assert.Equal("mart-last-mile:MART-101", result.인계.출발업무StableId);
        Assert.Equal("food-delivery-dispatch-request:MART-101", result.인계.도착업무StableId);
        Assert.Equal(result.인계.인계StableId, replay.인계.인계StableId);
        Assert.Equal(result.인계.Revision, replay.인계.Revision);
        Assert.Equal(운송의뢰배차원천유형.살뜰마트포장완료주문, queue.Options?.원본의뢰유형);
        Assert.Equal(2, await db.운영체제업무인계Outbox.CountAsync());
    }

    [Fact]
    public async Task 소비자연결이준비된인계Fixture는_한가한피킹시작에후보만기록한다()
    {
        await using var db = CreateContext();
        var queue = new RecordingDispatchQueue();
        var coordinator = new 운영체제업무인계Coordinator(
            db,
            new FixedTimeProvider(new DateTimeOffset(Now)));
        var service = new 살뜰마트라스트마일배차인계Service(
            coordinator,
            queue,
            new 살뜰마트라스트마일배차Policy(),
            new ReadyReadinessPolicy());

        var result = await service.인계Async(Request(
            살뜰마트라스트마일준비단계Codes.피킹시작,
            Now.AddMinutes(20)));

        Assert.Null(result.배차대기);
        Assert.NotNull(result.인계);
        Assert.Equal(운영체제업무인계상태Codes.요청됨, result.인계.상태Code);
        Assert.Equal(살뜰마트라스트마일배차전략Codes.균형, result.정책판정.전략Code);
        Assert.Null(queue.Target);
        Assert.Single(await db.운영체제업무인계Outbox.ToListAsync());
    }

    [Theory]
    [InlineData("mart-disabled", MartLastMileDispatchReadinessCodes.FeatureDisabled)]
    [InlineData("food-disabled", MartLastMileDispatchReadinessCodes.FeatureDisabled)]
    [InlineData("warehouse-disabled", MartLastMileDispatchReadinessCodes.FeatureDisabled)]
    [InlineData("source-absent", MartLastMileDispatchReadinessCodes.SourceNotBound)]
    [InlineData("source-ambiguous", MartLastMileDispatchReadinessCodes.SourceNotBound)]
    [InlineData("source-owner-empty", MartLastMileDispatchReadinessCodes.SourceNotBound)]
    [InlineData("outbound-absent", MartLastMileDispatchReadinessCodes.SourceNotBound)]
    [InlineData("seller-mismatch", MartLastMileDispatchReadinessCodes.SourceNotBound)]
    [InlineData("orderer-mismatch", MartLastMileDispatchReadinessCodes.SourceNotBound)]
    [InlineData("source-bound", MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable)]
    [InlineData("same-food-number", MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable)]
    public async Task 실제원천과소비자연결이준비되지않으면_반복요청도인계및큐를만들지않는다(
        string scenario,
        string expectedCode)
    {
        await using var db = CreateContext();
        await SeedReadinessScenarioAsync(db, scenario);
        var sourceBefore = await db.마트주문.AsNoTracking().Select(order => new
        {
            order.Id, order.상태, order.현재단계, order.UpdatedAt
        }).ToListAsync();
        var outboundBefore = await db.출고예정.AsNoTracking().Select(plan => new
        {
            plan.Id, plan.상태, plan.운송의뢰Id, plan.UpdatedAt
        }).ToListAsync();
        var foodBefore = await db.음식주문.AsNoTracking().Select(order => new
        {
            order.Id, order.상태, order.배차상태, order.배차대기Id, order.UpdatedAt
        }).ToListAsync();
        var queue = new RecordingDispatchQueue();
        var service = new 살뜰마트라스트마일배차인계Service(
            new 운영체제업무인계Coordinator(db, new FixedTimeProvider(new DateTimeOffset(Now))),
            queue,
            new 살뜰마트라스트마일배차Policy(),
            new MartLastMileDispatchReadinessPolicy(db, new ReadinessFeatures(scenario)));

        var result = await service.인계Async(Request(살뜰마트라스트마일준비단계Codes.픽업준비완료, Now));
        var replay = await service.인계Async(Request(살뜰마트라스트마일준비단계Codes.픽업준비완료, Now));

        Assert.Equal(expectedCode, result.보류사유Code);
        Assert.Equal(expectedCode, replay.보류사유Code);
        Assert.False(string.IsNullOrWhiteSpace(result.보류메시지));
        Assert.Null(result.인계);
        Assert.Null(replay.인계);
        Assert.Null(result.배차대기);
        Assert.Null(replay.배차대기);
        Assert.Equal(0, queue.CallCount);
        Assert.Empty(await db.운송원장.ToListAsync());
        Assert.Empty(await db.운영체제업무인계.ToListAsync());
        Assert.Empty(await db.운영체제업무인계Outbox.ToListAsync());
        Assert.Equal(sourceBefore, await db.마트주문.AsNoTracking().Select(order => new
        {
            order.Id, order.상태, order.현재단계, order.UpdatedAt
        }).ToListAsync());
        Assert.Equal(outboundBefore, await db.출고예정.AsNoTracking().Select(plan => new
        {
            plan.Id, plan.상태, plan.운송의뢰Id, plan.UpdatedAt
        }).ToListAsync());
        Assert.Equal(foodBefore, await db.음식주문.AsNoTracking().Select(order => new
        {
            order.Id, order.상태, order.배차상태, order.배차대기Id, order.UpdatedAt
        }).ToListAsync());
    }

    private static async Task SeedReadinessScenarioAsync(SsalddelContext db, string scenario)
    {
        if (scenario != "source-absent")
        {
            db.마트주문.Add(new 마트주문
            {
                주문참조번호 = "MART-101",
                판매자UserId = scenario == "source-owner-empty" ? string.Empty : "mart-a",
                주문자UserId = "orderer-a",
                상태 = "포장 완료",
                현재단계 = "포장",
                CreatedAt = Now.AddMinutes(-10),
                UpdatedAt = Now
            });
        }

        if (scenario == "source-ambiguous")
            db.마트주문.Add(new 마트주문 { 주문참조번호 = "MART-101", 판매자UserId = "other", 주문자UserId = "other" });

        if (scenario != "outbound-absent")
            db.출고예정.Add(new 출고예정
            {
                주문참조번호 = "MART-101",
                판매자UserId = scenario == "seller-mismatch" ? "other-seller" : "mart-a",
                주문자UserId = scenario == "orderer-mismatch" ? "other-orderer" : "orderer-a",
                상태 = 출고상태.출고완료,
                상품명 = "쌀",
                SKU = "RICE",
                수량 = 3,
                CreatedAt = Now.AddMinutes(-10),
                UpdatedAt = Now
            });

        if (scenario == "same-food-number")
            db.음식주문.Add(new 음식주문
            {
                주문번호 = "MART-101",
                주문자UserId = "orderer-a",
                상태 = "조리완료",
                CreatedAt = Now.AddMinutes(-10),
                UpdatedAt = Now
            });

        await db.SaveChangesAsync();
    }

    private static 살뜰마트라스트마일배차인계요청 Request(string stage, DateTime readyAt)
        => new(
            "MART-101",
            Now.Ticks,
            라인수: 2,
            총수량: 3,
            new 출고예정운송대상
            {
                원천유형 = 출고예정운송대상원천유형.살뜰마트주문,
                원천참조번호 = "MART-101",
                운송의뢰Id = "MART-101",
                판매자UserId = "mart-a",
                주문자UserId = "orderer-a",
                상차주소 = "서울 중랑구 마트로 1",
                하차주소 = "서울 중랑구 고객로 2"
            },
            new 살뜰마트라스트마일배차입력(
                Now.AddMinutes(-10),
                Now,
                stage,
                readyAt,
                배차대기주문수: 1,
                배차가능기사수: 3,
                함께묶을준비주문수: 1,
                수락된기사배정있음: false));

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options,
            new PassThroughEncryption());

    private sealed class RecordingDispatchQueue : I운송의뢰배차대기Service
    {
        public int CallCount { get; private set; }
        public 출고예정운송대상? Target { get; private set; }
        public 운송의뢰배차대기생성옵션? Options { get; private set; }

        public Task<운송원장> 생성또는조회Async(
            출고예정운송대상 target,
            운송의뢰배차대기생성옵션? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Target = target;
            Options = options;
            return Task.FromResult(new 운송원장
            {
                Id = 301,
                의뢰Id = options?.의뢰Id ?? target.원천참조번호,
                운송번호 = options?.의뢰Id ?? target.원천참조번호,
                화주Id = options?.화주Id ?? target.판매자UserId,
                원본의뢰유형 = options?.원본의뢰유형 ?? target.원천유형,
                원본의뢰Id = options?.원본의뢰Id ?? target.원천참조번호,
                배차업무유형 = options?.배차업무유형 ?? 상태값.배차업무유형.음식배달
            });
        }
    }

    // 큐/인계 자체의 기존 정책 시험용 fixture입니다. 제품의 consumer 연결 준비 증거가 아닙니다.
    private sealed class ReadyReadinessPolicy : IMartLastMileDispatchReadinessPolicy
    {
        public Task<MartLastMileDispatchReadiness> EvaluateAsync(string orderReference, CancellationToken cancellationToken)
            => Task.FromResult(new MartLastMileDispatchReadiness(true, "TestConsumerReady", string.Empty));
    }

    private sealed class ReadinessFeatures(string scenario) : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey)
            => !(scenario == "mart-disabled" && featureKey == VersionFeatureFlagKeys.SsalddelMartWorkflow)
               && !(scenario == "food-disabled" && featureKey == VersionFeatureFlagKeys.FoodDeliveryWorkflow)
               && !(scenario == "warehouse-disabled" && featureKey == VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow);

        public IReadOnlyDictionary<string, bool> GetAll()
            => new Dictionary<string, bool>
            {
                [VersionFeatureFlagKeys.SsalddelMartWorkflow] = IsEnabled(VersionFeatureFlagKeys.SsalddelMartWorkflow),
                [VersionFeatureFlagKeys.FoodDeliveryWorkflow] = IsEnabled(VersionFeatureFlagKeys.FoodDeliveryWorkflow),
                [VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow] = IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)
            };
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
