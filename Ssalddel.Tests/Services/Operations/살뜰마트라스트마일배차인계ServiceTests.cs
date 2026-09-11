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
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.운영;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 살뜰마트라스트마일배차인계ServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 픽업준비완료는_마트자식업무를음식배달Os에인계한뒤배차를수락한다()
    {
        await using var db = CreateContext();
        var queue = new RecordingDispatchQueue();
        var coordinator = new 운영체제업무인계Coordinator(
            db,
            new FixedTimeProvider(new DateTimeOffset(Now)));
        var service = new 살뜰마트라스트마일배차인계Service(
            coordinator,
            queue,
            new 살뜰마트라스트마일배차Policy());

        var request = Request(
            살뜰마트라스트마일준비단계Codes.픽업준비완료,
            Now);
        var result = await service.인계Async(request);
        var replay = await service.인계Async(request);

        Assert.NotNull(result.배차대기);
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
    public async Task 한가한피킹시작은_인계후보만기록하고기사에게아직제안하지않는다()
    {
        await using var db = CreateContext();
        var queue = new RecordingDispatchQueue();
        var coordinator = new 운영체제업무인계Coordinator(
            db,
            new FixedTimeProvider(new DateTimeOffset(Now)));
        var service = new 살뜰마트라스트마일배차인계Service(
            coordinator,
            queue,
            new 살뜰마트라스트마일배차Policy());

        var result = await service.인계Async(Request(
            살뜰마트라스트마일준비단계Codes.피킹시작,
            Now.AddMinutes(20)));

        Assert.Null(result.배차대기);
        Assert.Equal(운영체제업무인계상태Codes.요청됨, result.인계.상태Code);
        Assert.Equal(살뜰마트라스트마일배차전략Codes.균형, result.정책판정.전략Code);
        Assert.Null(queue.Target);
        Assert.Single(await db.운영체제업무인계Outbox.ToListAsync());
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
        public 출고예정운송대상? Target { get; private set; }
        public 운송의뢰배차대기생성옵션? Options { get; private set; }

        public Task<운송원장> 생성또는조회Async(
            출고예정운송대상 target,
            운송의뢰배차대기생성옵션? options = null,
            CancellationToken cancellationToken = default)
        {
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
