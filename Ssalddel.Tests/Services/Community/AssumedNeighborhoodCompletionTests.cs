using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using Ssalddel.Services.Fulfillment;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

/// <summary>실제 SQLite·Coordinator·원천 Adapter의 모의 완료 연결입니다. 실업체·Mongo·지급 검증이 아닙니다.</summary>
public sealed class AssumedNeighborhoodCompletionTests
{
    [Fact]
    public async Task 명시배정픽업전달증거만완료가능성을열고재시작에도내부큐와지급은만들지않는다()
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        await SourceAsync(f); var service = Service(f.Db, f.Clock); var reader = Reader(f.Db);
        var current = await StartAsync(service, f.Key);
        Assert.False((await reader.조회Async(f.Key.SourceId))!.Completed);
        var assigned = await EvidenceAsync(service, current, FulfillmentEvidenceKinds.DriverAssigned);
        Assert.False((await reader.조회Async(f.Key.SourceId))!.Completed);
        var picked = await EvidenceAsync(service, assigned, FulfillmentEvidenceKinds.PickedUp);
        Assert.False((await reader.조회Async(f.Key.SourceId))!.Completed);
        var deliveredRequest = Evidence(picked, FulfillmentEvidenceKinds.Delivered);
        var delivered = await service.모의증거기록Async(picked.AttemptId!, deliveredRequest);
        Assert.True(delivered.IsSuccess); Assert.True((await reader.조회Async(f.Key.SourceId))!.Completed);
        Assert.True((await service.모의증거기록Async(picked.AttemptId!, deliveredRequest)).IsSuccess);
        await using var restarted = f.OpenDb();
        Assert.True((await Reader(restarted).조회Async(f.Key.SourceId))!.Completed);
        Assert.Equal(1, await restarted.NeighborhoodFulfillmentAttempts.CountAsync());
        Assert.Equal(1, await restarted.FulfillmentMockJobs.CountAsync());
        Assert.Equal(0, await restarted.운송원장.CountAsync());
        var source = await restarted.화주운송의뢰.SingleAsync();
        Assert.Equal(상태값.배차상태.미시작, source.배차상태);
        Assert.Equal("미결제", source.결제상태); Assert.Equal("미정산", source.정산상태);
        Assert.False((await new Ef생활협업배송Source(restarted).조회Async(f.Key.SourceId))!.Completed);
        Assert.False((await Reader(restarted, SsalddelExecutionMode.Operational).조회Async(f.Key.SourceId))!.Completed);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("cancelled")]
    [InlineData("consent")]
    [InlineData("current")]
    [InlineData("job")]
    [InlineData("owner")]
    public async Task 완료표시가있어도현재원천동의시도증거가다르면협업완료근거가되지않는다(string changed)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync(); await SourceAsync(f);
        var service = Service(f.Db, f.Clock); var current = await CompleteAsync(service, f.Key);
        Assert.True((await Reader(f.Db).조회Async(f.Key.SourceId))!.Completed);
        switch (changed)
        {
            case "source": (await f.Db.화주운송의뢰.SingleAsync()).생활배송포장크기Code = "Large"; break;
            case "cancelled": (await f.Db.화주운송의뢰.SingleAsync()).상태 = 상태값.의뢰상태.취소; break;
            case "consent": (await f.Db.AutoFulfillmentConsents.SingleAsync()).Revision++; break;
            case "current": (await f.Db.NeighborhoodFulfillments.SingleAsync()).AttemptId = "different"; break;
            case "job": (await f.Db.FulfillmentMockJobs.SingleAsync()).ExternalRequestId = "different"; break;
            case "owner": (await f.Db.AutoFulfillmentConsents.SingleAsync()).OwnerId = "different"; break;
        }
        await f.Db.SaveChangesAsync();
        Assert.False((await Reader(f.Db).조회Async(f.Key.SourceId))!.Completed);
    }

    [Fact]
    public async Task 픽업생략과원천변경진행증거는거절되고모의작업도앞서변하지않는다()
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync(); await SourceAsync(f);
        var service = Service(f.Db, f.Clock); var current = await StartAsync(service, f.Key);
        Assert.True((await service.모의증거기록Async(current.AttemptId!, Evidence(current, FulfillmentEvidenceKinds.Delivered))).IsFailed);
        current = await EvidenceAsync(service, current, FulfillmentEvidenceKinds.DriverAssigned);
        (await f.Db.화주운송의뢰.SingleAsync()).생활배송포장크기Code = "Large";
        await f.Db.SaveChangesAsync();
        var rejected = await service.모의증거기록Async(current.AttemptId!, Evidence(current, FulfillmentEvidenceKinds.PickedUp));
        Assert.True(rejected.IsFailed); Assert.Equal("MockEvidenceSourceChanged", rejected.Errors[0].Metadata["ErrorCode"]);
        Assert.Equal(AutoFulfillmentStates.DriverAssigned, (await f.Db.FulfillmentMockJobs.SingleAsync()).StateCode);
        Assert.Equal(0, await f.Db.운송원장.CountAsync());
    }

    internal static Ef생활협업배송Source Reader(SsalddelContext db, SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation)
        => new(db, Policy(mode));
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public async Task 내부인수완료도모의표식은Simulation에서만읽으며기존실운송은보존한다(bool mock, bool automatic, bool operationalCompletion)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync(); await SourceAsync(f);
        var source = await f.Db.화주운송의뢰.SingleAsync(); source.배차상태 = 상태값.배차상태.인수완료;
        source.모의처리처수행 = mock; source.자동처리처수행 = automatic; await f.Db.SaveChangesAsync();
        Assert.Equal(operationalCompletion, (await Reader(f.Db, SsalddelExecutionMode.Operational).조회Async(f.Key.SourceId))!.Completed);
        Assert.Equal(operationalCompletion, (await new Ef생활협업배송Source(f.Db).조회Async(f.Key.SourceId))!.Completed);
        Assert.True((await Reader(f.Db).조회Async(f.Key.SourceId))!.Completed);
    }

    [Fact]
    public async Task 동의철회와원천크기변경뒤에도같은원접수취소확인은가능하다()
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync(); await SourceAsync(f);
        var service = Service(f.Db, f.Clock); var current = await StartAsync(service, f.Key);
        (await f.Db.AutoFulfillmentConsents.SingleAsync()).Revoked = true;
        (await f.Db.화주운송의뢰.SingleAsync()).생활배송포장크기Code = NeighborhoodPackageSizes.Large;
        await f.Db.SaveChangesAsync();
        var cancelled = await service.취소Async(f.Key, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision });
        Assert.True(cancelled.IsSuccess, string.Join(";", cancelled.Errors.Select(e => e.Message)));
        Assert.Equal(AutoFulfillmentStates.Cancelled, (await f.Db.FulfillmentMockJobs.SingleAsync()).StateCode);
        Assert.False((await Reader(f.Db).조회Async(f.Key.SourceId))!.Completed);
        Assert.Equal(1, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync()); Assert.Equal(0, await f.Db.운송원장.CountAsync());
    }
    internal static 자동수행Coordinator Service(SsalddelContext db, TimeProvider clock)
    {
        var options = Options.Create(new FulfillmentRoutingOptions { Enabled = true, AssumedProvidersEnabled = true });
        var policy = Policy(SsalddelExecutionMode.Simulation);
        var source = new 생활배송수행원천Adapter(db, new UnexpectedQueue(), new UnexpectedLedger(), clock, policy);
        return new(db, [source], AssumedFulfillmentProviderCatalog.Providers.Select(p => (I자동수행Provider)new 가정자동수행Provider(db, clock, p, options, policy)), options, policy, clock);
    }
    private static ISsalddelExecutionModePolicy Policy(SsalddelExecutionMode mode)
        => new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode }));
    internal static async Task SourceAsync(AutoFulfillmentCoordinatorTests.Fixture f, long sourcePostId = 10)
    {
        f.Db.화주운송의뢰.Add(new()
        {
            의뢰Id = f.Key.SourceId, 주문자UserId = "owner", 클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + Guid.NewGuid().ToString("N"),
            자동처리처수행 = true, 모의처리처수행 = true, 생활배송포장크기Code = NeighborhoodPackageSizes.Medium,
            정산메모 = "neighborhood-delivery:v1:test:source-post:" + sourcePostId,
            화물종류 = "물건", 화물수량 = 1, 화물중량Kg = 2, 차량종류 = "오토바이", 최종운임 = 10000,
            픽업_도로명주소 = "픽업 장소", 하차_도로명주소 = "전달 장소", 픽업_시간창_시작일시 = f.Clock.Now,
            픽업_시간창_종료일시 = f.Clock.Now.AddHours(1), UpdatedAt = f.Clock.Now,
            상태 = 상태값.의뢰상태.생성됨, 배차상태 = 상태값.배차상태.미시작, 결제상태 = "미결제", 정산상태 = "미정산"
        });
        f.Db.운임구성.Add(new() { 의뢰Id = f.Key.SourceId, 예상거리Km = 3, 최종운임 = 10000 });
        await f.Db.SaveChangesAsync();
    }
    internal static async Task<AutoFulfillmentSnapshot> StartAsync(자동수행Coordinator service, FulfillmentSourceKey key)
    {
        var saved = await service.사전동의저장Async(key, "owner", new() { ClientRequestId = Guid.NewGuid(), MaxUserChargeKrw = 20000,
            AllowedProviderIds = [AssumedFulfillmentProviderCatalog.KakaoTQuick], AutomaticSubmissionAgreed = true, ProviderInformationSharingAgreed = true });
        Assert.True(saved.IsSuccess, string.Join(";", saved.Errors.Select(e => e.Message)));
        var started = await service.선정실행Async(key, Guid.NewGuid());
        Assert.True(started.IsSuccess); Assert.Equal(AutoFulfillmentStates.Accepted, started.Value.StateCode); return started.Value;
    }
    internal static async Task<AutoFulfillmentSnapshot> CompleteAsync(자동수행Coordinator service, FulfillmentSourceKey key)
    {
        var current = await StartAsync(service, key);
        foreach (var kind in new[] { FulfillmentEvidenceKinds.DriverAssigned, FulfillmentEvidenceKinds.PickedUp, FulfillmentEvidenceKinds.Delivered })
            current = await EvidenceAsync(service, current, kind);
        return current;
    }
    private static FulfillmentMockEvidenceRequest Evidence(AutoFulfillmentSnapshot current, string kind) => new()
    { ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision, EvidenceKind = kind,
        ExternalDriverReference = kind == FulfillmentEvidenceKinds.DriverAssigned ? "mock-driver" : null };
    private static async Task<AutoFulfillmentSnapshot> EvidenceAsync(자동수행Coordinator service, AutoFulfillmentSnapshot current, string kind)
    {
        var result = await service.모의증거기록Async(current.AttemptId!, Evidence(current, kind));
        Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(e => e.Message))); return result.Value;
    }
    private sealed class UnexpectedQueue : I운송의뢰배차대기Service
    {
        public Task<운송원장> 생성또는조회Async(출고예정운송대상 target, 운송의뢰배차대기생성옵션? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("외부 모의 흐름이 내부 큐를 만들 수 없습니다.");
    }
    private sealed class UnexpectedLedger : I운송원장Mongo동기화Service
    {
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("외부 모의 증거는 내부 업무 상태를 바꾸지 않습니다.");
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("외부 모의 증거는 내부 운송을 생성하지 않습니다.");
        public Task<운송원장Mongo동기화상태> 상태조회Async(string requestId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("불필요한 내부 동기화 조회입니다.");
    }
}

public sealed partial class NeighborhoodCollaborationTests
{
    [Fact]
    public async Task 가정배송완료이후에도양측완료확인과공개동의는별도다()
    {
        await using var f = await Fixture.Create(); var work = await f.Agreed("transport");
        await using var sql = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        await AssumedNeighborhoodCompletionTests.SourceAsync(sql, f.Post.Id);
        var source = AssumedNeighborhoodCompletionTests.Reader(sql.Db);
        생활협업UseCase App(string actor) => new(f.Store, f.Db, new Current(actor), f.Clock, f.Space, f.Storage, source);
        async Task<NeighborhoodCollaborationResponse> Command(string actor, string action) => Success(await App(actor).변경Async(work.StableId,
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision, Action = action, LinkedDeliveryRequestId = sql.Key.SourceId }));
        work = await Command("owner", "link-delivery"); work = await Command("owner", "start"); work = await Command("owner", "propose-completion");
        AssertCode("DeliveryCompletionRequired", await App("requester").변경Async(work.StableId,
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision, Action = "confirm-completion" }));
        await AssumedNeighborhoodCompletionTests.CompleteAsync(AssumedNeighborhoodCompletionTests.Service(sql.Db, sql.Clock), sql.Key);
        Assert.Equal("completion-proposed", (await App("requester").상세Async(work.StableId))!.StatusCode);
        work = await Command("requester", "confirm-completion"); Assert.Equal("completed", work.StatusCode);
        Assert.Empty(await App("owner").공개이력Async(f.Post.Id));
        Assert.Equal(0, await sql.Db.운송원장.CountAsync());
    }
}
