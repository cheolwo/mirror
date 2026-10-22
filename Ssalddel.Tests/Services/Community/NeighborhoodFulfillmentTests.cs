using System.Reflection;
using System.Data.Common;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

/// <summary>실제 SQLite의 영속 선택·판본·중복 제약과 모의 공급자 계약을 확인합니다. 실제 업체 API·수금·지급 증거는 아닙니다.</summary>
public sealed class NeighborhoodFulfillmentTests
{
    [Fact]
    public async Task 조건을충족한견적중최저비용을추천하고_희망시간을넘는후보를제외한다()
    {
        await using var f = await Fixture.CreateAsync();
        var options = await f.Service.견적Async(f.Input, f.Fare, null, default);
        Assert.Equal("mock-local", Assert.Single(options.Where(x => x.IsRecommended)).ProviderId);
        f.Input.DesiredCompletionUtc = f.Clock.Now.AddMinutes(30);
        options = await f.Service.견적Async(f.Input, f.Fare, null, default);
        Assert.Equal("mock-quick", Assert.Single(options.Where(x => x.IsRecommended)).ProviderId);
        Assert.False(options.Single(x => x.ProviderId == "mock-local").IsAvailable);
        f.Input.WeightKg = 50;
        Assert.DoesNotContain(await f.Service.견적Async(f.Input, f.Fare, null, default), x => x.IsAvailable);
    }

    [Theory]
    [InlineData(false, SsalddelExecutionMode.Simulation)]
    [InlineData(true, SsalddelExecutionMode.Operational)]
    public async Task 기본비활성과운영모드는새테이블을읽지않고모의견적을반환하지않는다(bool enabled, SsalddelExecutionMode mode)
    {
        await using var f = await Fixture.CreateAsync(createSchema: false);
        var service = f.NewService(f.Db, enabled, mode);
        Assert.Empty(await service.견적Async(f.Input, f.Fare, null, default));
        Assert.Null(await service.내상태Async("request-1", default));
        Assert.Equal("FulfillmentDisabled", Code(await service.최초확정Async("request-1", f.Input, default)));
    }

    [Fact]
    public async Task 확인비용_동의_만료_변경된내용_다른소유자의견적을검사한다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SelectInputAsync("mock-local");
        f.Input.FulfillmentSelection!.ProviderInformationSharingAgreed = false;
        Assert.Equal("ProviderSharingAgreementRequired", Code(await f.Service.선택검사Async(f.Input, default)));
        f.Input.FulfillmentSelection.ProviderInformationSharingAgreed = true;
        f.Input.FulfillmentSelection.ConfirmedCostKrw++;
        Assert.Equal("FulfillmentCostChanged", Code(await f.Service.선택검사Async(f.Input, default)));
        f.Input.FulfillmentSelection.ConfirmedCostKrw--;
        f.Input.CargoName = "다른 물품";
        Assert.Equal("FulfillmentQuoteMismatch", Code(await f.Service.선택검사Async(f.Input, default)));
        f.Input.CargoName = "생활 물품";
        f.Current.UserId = "other";
        Assert.Equal("FulfillmentQuoteMismatch", Code(await f.Service.선택검사Async(f.Input, default)));
        f.Current.UserId = "owner"; f.Clock.Now = f.Clock.Now.AddMinutes(6);
        Assert.Equal("FulfillmentQuoteExpired", Code(await f.Service.선택검사Async(f.Input, default)));
    }

    [Fact]
    public async Task 외부선택은한시도만영속화하고재시작후재송신해도재접수나자체큐를만들지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SelectInputAsync("mock-local");
        var first = await f.Service.최초확정Async("request-1", f.Input, default);
        Assert.True(first.IsSuccess, string.Join(";", first.Errors));
        Assert.Equal(NeighborhoodFulfillmentStates.Accepted, first.Value.StateCode);
        Assert.False(first.Value.IsOperationalSettlementEligible);
        await using var restarted = f.OpenDb();
        var replay = await f.NewService(restarted).최초확정Async("request-1", f.Input, default);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.AttemptId, replay.Value.AttemptId);
        Assert.Single(await restarted.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Empty(await restarted.운송원장.ToListAsync());
        Assert.Equal(1, f.Local.Submits);
        var raw = (await restarted.NeighborhoodFulfillmentAttempts.SingleAsync()).HistoryJson;
        Assert.DoesNotContain("010-", raw); Assert.DoesNotContain("동일로", raw);
    }

    [Fact]
    public async Task 접수응답유실은재조회로회복하고확인전다른처리처접수를차단한다()
    {
        await using var f = await Fixture.CreateAsync(); f.Local.LoseSubmitResponse = true;
        await f.SelectInputAsync("mock-local");
        var unknown = await f.Service.최초확정Async("request-1", f.Input, default);
        Assert.Equal(NeighborhoodFulfillmentStates.ResultUnknown, unknown.Value.StateCode);
        Assert.False(unknown.Value.CanChangeProvider);
        var denied = await f.Service.선택Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = unknown.Value.Revision,
            Selection = f.Input.FulfillmentSelection! }, default);
        Assert.Equal("FulfillmentLocked", Code(denied));
        await using var restarted = f.OpenDb();
        var recovered = await f.NewService(restarted).새로고침Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = unknown.Value.Revision }, default);
        Assert.Equal(NeighborhoodFulfillmentStates.Accepted, recovered.Value.StateCode);
        Assert.Equal(1, f.Local.Submits); Assert.Equal(1, f.Local.Reads);
    }

    [Fact]
    public async Task 두SQL연결의동시선택은한현재행과한수행만허용한다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SelectInputAsync("mock-local"); f.Local.SubmitEntered = new(); f.Local.ReleaseSubmit = new();
        var firstTask = f.Service.최초확정Async("request-1", f.Input, default);
        await f.Local.SubmitEntered.Task;
        await using var contenderDb = f.OpenDb();
        var contender = await f.NewService(contenderDb).최초확정Async("request-1", f.Input, default);
        Assert.True(contender.IsSuccess); // 동일 요청은 영속 시도의 현재 상태를 반환합니다.
        Assert.Equal(NeighborhoodFulfillmentStates.Submitting, contender.Value.StateCode);
        var secondInput = Clone(f.Input); secondInput.ClientRequestId = Guid.NewGuid();
        Assert.Equal("FulfillmentLocked", Code(await f.NewService(contenderDb).최초확정Async("request-1", secondInput, default)));
        f.Local.ReleaseSubmit.SetResult();
        Assert.True((await firstTask).IsSuccess);
        Assert.Single(await contenderDb.NeighborhoodFulfillments.AsNoTracking().ToListAsync());
        Assert.Single(await contenderDb.NeighborhoodFulfillmentAttempts.AsNoTracking().ToListAsync());
        Assert.Equal(1, f.Local.Submits);
    }

    [Fact]
    public async Task SQLite선택판본은다른연결의오래된수정이새선택을덮지못하게한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.SelectInputAsync("mock-local");
        Assert.True((await f.Service.최초확정Async("request-1", f.Input, default)).IsSuccess);
        await using var a = f.OpenDb(); await using var b = f.OpenDb();
        var rowA = await a.NeighborhoodFulfillments.SingleAsync(); var rowB = await b.NeighborhoodFulfillments.SingleAsync();
        rowA.Revision++; await a.SaveChangesAsync(); rowB.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task 두SQL연결의동시확정은Serializable로하나의수행만허용한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.SelectInputAsync("mock-local");
        var barrier = new InitialChoiceBarrier();
        await using var a = f.OpenDb(barrier); await using var b = f.OpenDb(barrier);
        var other = Clone(f.Input); other.ClientRequestId = Guid.NewGuid();
        var results = await Task.WhenAll(f.NewService(a).최초확정Async("request-1", f.Input, default),
            f.NewService(b).최초확정Async("request-1", other, default));
        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Contains(Code(Assert.Single(results.Where(x => x.IsFailed))), new[] { "FulfillmentRevisionConflict", "FulfillmentLocked" });
        Assert.Equal(2, barrier.Arrivals);
        Assert.Single(await f.Db.NeighborhoodFulfillments.AsNoTracking().ToListAsync());
        Assert.Single(await f.Db.NeighborhoodFulfillmentAttempts.AsNoTracking().ToListAsync());
        Assert.Equal(1, f.Local.Submits);
    }

    [Fact]
    public async Task 취소응답이불명일때원시도를조회하고확인된후새견적과새동의로처리처를바꾼다()
    {
        await using var f = await Fixture.CreateAsync(); await f.SelectInputAsync("mock-local");
        var accepted = (await f.Service.최초확정Async("request-1", f.Input, default)).Value;
        f.Local.LoseCancelResponse = true;
        var action = new NeighborhoodFulfillmentActionRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision };
        var pending = (await f.Service.취소Async("request-1", action, default)).Value;
        Assert.Equal(NeighborhoodFulfillmentStates.CancelPending, pending.StateCode);
        Assert.False(pending.CanChangeProvider);
        Assert.True((await f.Service.내견적Async("request-1", default)).IsFailed);
        var cancelled = (await f.Service.새로고침Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = pending.Revision }, default)).Value;
        Assert.Equal(NeighborhoodFulfillmentStates.Cancelled, cancelled.StateCode);
        var quote = await f.Service.내견적Async("request-1", default); Assert.True(quote.IsSuccess);
        var quick = quote.Value.FulfillmentOptions.Single(x => x.ProviderId == "mock-quick");
        var changed = await f.Service.선택Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = cancelled.Revision,
            Selection = Selection(quick) }, default);
        Assert.True(changed.IsSuccess, string.Join(";", changed.Errors));
        Assert.Equal("mock-quick", changed.Value.ProviderId);
        Assert.Equal(2, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
        Assert.Equal(1, f.Local.Cancels); Assert.Equal(1, f.Quick.Submits);
    }

    [Fact]
    public async Task 다른소유자는조회와취소를할수없고취소된원장의처리처를다시열지않는다()
    {
        await using var f = await Fixture.CreateAsync(); await f.SelectInputAsync("mock-local");
        var accepted = (await f.Service.최초확정Async("request-1", f.Input, default)).Value;
        f.Current.UserId = "other"; Assert.Null(await f.Service.내상태Async("request-1", default));
        Assert.Equal("RequestNotFound", Code(await f.Service.취소Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision }, default)));
        f.Current.UserId = "owner";
        var cancelled = (await f.Service.취소Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision }, default)).Value;
        var source = await f.Db.화주운송의뢰.SingleAsync(); source.상태 = 상태값.의뢰상태.취소; await f.Db.SaveChangesAsync();
        Assert.False((await f.Service.내상태Async("request-1", default))!.CanChangeProvider);
        Assert.Equal("FulfillmentRequestClosed", Code(await f.Service.내견적Async("request-1", default)));
        Assert.Equal("FulfillmentRequestClosed", Code(await f.Service.선택Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = cancelled.Revision,
            Selection = f.Input.FulfillmentSelection! }, default)));
    }

    [Fact]
    public async Task 취소후새처리처확정뒤늦게돌아온이전조회가새시도를덮지못한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.SelectInputAsync("mock-local");
        var accepted = (await f.Service.최초확정Async("request-1", f.Input, default)).Value;
        f.Local.ReadEntered = new(); f.Local.ReleaseRead = new();
        await using var readDb = f.OpenDb();
        var oldRead = f.NewService(readDb).새로고침Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = accepted.Revision }, default);
        await f.Local.ReadEntered.Task;
        var claimed = (await f.Service.내상태Async("request-1", default))!;
        var cancelled = (await f.Service.취소Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = claimed.Revision }, default)).Value;
        var quote = (await f.Service.내견적Async("request-1", default)).Value;
        var quick = quote.FulfillmentOptions.Single(x => x.ProviderId == "mock-quick");
        var changed = (await f.Service.선택Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = cancelled.Revision,
            Selection = Selection(quick) }, default)).Value;
        f.Local.ReleaseRead.SetResult();
        var stale = (await oldRead).Value;
        Assert.Equal(changed.AttemptId, stale.AttemptId); Assert.Equal("mock-quick", stale.ProviderId);
        var current = (await f.Service.내상태Async("request-1", default))!;
        Assert.Equal(changed.AttemptId, current.AttemptId); Assert.Equal(changed.Revision, current.Revision);
    }

    [Fact]
    public async Task 모의진행은예정픽업이전에는완료되지않고저장된완료시각을사용한다()
    {
        var clock = new Clock(); var provider = new MockNeighborhoodFulfillmentProvider("mock", "모의", 10, 10, 1, 60, clock);
        var eta = clock.Now.AddHours(5);
        var operation = new FulfillmentProviderOperation("a", "r", clock.Now, null, NeighborhoodFulfillmentStates.Accepted, "mock:a", eta);
        clock.Now = clock.Now.AddHours(2);
        Assert.Equal(NeighborhoodFulfillmentStates.Accepted, (await provider.ReadAsync(operation, default)).StateCode);
        clock.Now = eta.AddMinutes(-30);
        Assert.Equal(NeighborhoodFulfillmentStates.InTransit, (await provider.ReadAsync(operation, default)).StateCode);
        clock.Now = eta;
        Assert.Equal(NeighborhoodFulfillmentStates.Completed, (await provider.ReadAsync(operation, default)).StateCode);
    }

    [Fact]
    public async Task 내부직접지급조건을명시적으로확인해야선택할수있다()
    {
        await using var f = await Fixture.CreateAsync(); await f.SelectInputAsync("internal");
        f.Input.FulfillmentSelection!.DirectPaymentAgreed = false;
        Assert.Equal("DirectPaymentAgreementRequired", Code(await f.Service.선택검사Async(f.Input, default)));
    }

    [Fact]
    public async Task 원의뢰저장후선택저장전중단은만료견적을재접수하지않고새견적재확정으로회복한다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Input.DesiredCompletionUtc = f.Clock.Now.AddMinutes(90);
        var source = await f.Db.화주운송의뢰.SingleAsync(); source.모의처리희망완료Utc = f.Input.DesiredCompletionUtc;
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await f.SelectInputAsync("mock-local"); f.Clock.Now = f.Clock.Now.AddMinutes(6);
        var recovered = await f.Service.최초확정Async("request-1", f.Input, default);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(NeighborhoodFulfillmentStates.AwaitingSelection, recovered.Value.StateCode);
        Assert.Equal(0, recovered.Value.Revision); Assert.True(recovered.Value.CanChangeProvider);
        Assert.Equal(0, f.Local.Submits); Assert.Empty(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        var refreshed = await f.Service.내견적Async("request-1", default); Assert.True(refreshed.IsSuccess);
        Assert.All(refreshed.Value.FulfillmentOptions.Where(x => x.IsAvailable), option => Assert.True(option.EstimatedCompletionUtc <= source.모의처리희망완료Utc));
        var choice = refreshed.Value.FulfillmentOptions.Single(x => x.ProviderId == "mock-local");
        var confirmed = await f.Service.선택Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0, Selection = Selection(choice) }, default);
        Assert.True(confirmed.IsSuccess, string.Join(";", confirmed.Errors)); Assert.Equal(1, f.Local.Submits);
        Assert.Single(await f.Db.화주운송의뢰.ToListAsync());
    }

    [Fact]
    public async Task 아직전송하지않은저장의뢰는자체큐나공급자호출없이취소하고새선택을차단한다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SelectInputAsync("mock-local"); // 취소 전에 확인했던 견적도 취소된 의뢰를 다시 열 수 없습니다.
        var pending = (await f.Service.내상태Async("request-1", default))!;
        Assert.Equal(NeighborhoodFulfillmentStates.AwaitingSelection, pending.StateCode); Assert.True(pending.CanCancel);
        var action = new NeighborhoodFulfillmentActionRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0 };
        var cancelled = await f.Service.취소Async("request-1", action, default);
        Assert.True(cancelled.IsSuccess); Assert.Equal(NeighborhoodFulfillmentStates.Cancelled, cancelled.Value.StateCode);
        Assert.False(cancelled.Value.CanChangeProvider); Assert.False(cancelled.Value.CanCancel);
        Assert.True((await f.Service.취소Async("request-1", action, default)).IsSuccess);
        Assert.Empty(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync()); Assert.Empty(await f.Db.운송원장.ToListAsync());
        Assert.Equal(0, f.Local.Submits); Assert.Equal(0, f.Local.Cancels);
        Assert.Equal("FulfillmentRequestClosed", Code(await f.Service.내견적Async("request-1", default)));
        Assert.Single(await f.Db.운송이벤트.ToListAsync());

        var source = await f.Db.화주운송의뢰.SingleAsync();
        운송원장업무투영Handler.ApplyShipperRequest(source, new()
        { RequestId = source.의뢰Id, RequestState = 상태값.의뢰상태.생성됨, SimulationFulfillment = false });
        await f.Db.SaveChangesAsync();
        await using var restarted = f.OpenDb();
        var service = f.NewService(restarted);
        var afterReplay = (await service.내상태Async("request-1", default))!;
        Assert.Equal(NeighborhoodFulfillmentStates.Cancelled, afterReplay.StateCode);
        Assert.False(afterReplay.CanCancel); Assert.False(afterReplay.CanChangeProvider);
        Assert.Equal("FulfillmentRequestClosed", Code(await service.내견적Async("request-1", default)));
        Assert.Equal("FulfillmentRequestClosed", Code(await service.선택Async("request-1", new()
        { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0, Selection = f.Input.FulfillmentSelection! }, default)));
        Assert.Empty(await restarted.NeighborhoodFulfillmentAttempts.ToListAsync()); Assert.Equal(0, f.Local.Submits);
    }

    [Fact]
    public void 모의처리처가아닌기존의뢰의원장상태투영규칙은유지한다()
    {
        var source = new 화주운송의뢰 { 의뢰Id = "legacy", 상태 = 상태값.의뢰상태.취소 };
        Assert.True(운송원장업무투영Handler.ApplyShipperRequest(source, new() { RequestState = 상태값.의뢰상태.생성됨 }));
        Assert.Equal(상태값.의뢰상태.생성됨, source.상태);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 실제MongoFirst원장투영은모의의뢰만저장하고기존의뢰의자체큐생성은유지한다(bool simulation)
    {
        await using var f = await Fixture.CreateAsync();
        f.Input.DesiredCompletionUtc = f.Clock.Now.AddMinutes(90);
        await f.SelectInputAsync("mock-local");
        var canonical = await f.Db.화주운송의뢰.SingleAsync();
        canonical.모의처리처수행 = simulation; canonical.모의처리희망완료Utc = simulation ? f.Input.DesiredCompletionUtc : null;
        if (!simulation) { canonical.정산시점 = "현장지급"; canonical.정산상태 = "현장수금예정"; }
        var mongoFirst = ProjectionLedger(canonical, persistedMirror: false);
        f.Db.화주운송의뢰.Remove(canonical); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var projection = new 운송원장업무투영Handler(f.Db, NullLogger<운송원장업무투영Handler>.Instance);

        await projection.동기화Async(mongoFirst);

        var source = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(simulation, source.모의처리처수행);
        Assert.Equal(simulation ? f.Input.DesiredCompletionUtc : null, source.모의처리희망완료Utc);
        Assert.Equal("owner", source.주문자UserId); Assert.Equal(f.Input.CargoName, source.화물종류);
        Assert.Equal(simulation ? 0 : 1, await f.Db.운송원장.CountAsync());
        if (simulation)
        {
            var submitted = await f.Service.최초확정Async("request-1", f.Input, default);
            Assert.True(submitted.IsSuccess, string.Join(";", submitted.Errors));
            Assert.Equal(NeighborhoodFulfillmentStates.Accepted, submitted.Value.StateCode);
            Assert.Equal(1, f.Local.Submits);
            await projection.동기화Async(ProjectionLedger(source, persistedMirror: true));
            Assert.Empty(await f.Db.운송원장.ToListAsync());
        }
    }

    [Fact]
    public async Task 오래된Rdb사본이모의표식을누락해도현재모의의뢰의자체큐를새로만들지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var oldSource = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync(); oldSource.모의처리처수행 = false;
        var staleMirror = ProjectionLedger(oldSource, persistedMirror: true);
        Assert.True(운송원장업무투영Snapshot.생성(staleMirror)!.IsPersistedRdbSnapshot);
        await new 운송원장업무투영Handler(f.Db, NullLogger<운송원장업무투영Handler>.Instance).동기화Async(staleMirror);
        Assert.True((await f.Db.화주운송의뢰.AsNoTracking().SingleAsync()).모의처리처수행);
        Assert.Empty(await f.Db.운송원장.ToListAsync()); Assert.Equal(0, f.Local.Submits);
        Assert.Equal(NeighborhoodFulfillmentStates.AwaitingSelection, (await f.Service.내상태Async("request-1", default))!.StateCode);
        Assert.True((await f.Service.취소Async("request-1", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0 }, default)).IsSuccess);
    }

    private static 커뮤니티원장Dto ProjectionLedger(화주운송의뢰 source, bool persistedMirror)
    {
        var save = 운송원장Mongo동기화Builder.저장요청생성(source, null);
        if (persistedMirror) 운송원장RdbSnapshotMarker.Attach(save, source.의뢰Id);
        return new() { 원장Id = save.원장Id!, 원장템플릿Key = save.원장템플릿Key,
            상태 = save.상태!, 현재단계Key = save.현재단계Key,
            블록목록 = save.블록목록, 참여자목록 = save.참여자목록, 외부참조 = save.외부참조, 확장속성 = save.확장속성 };
    }

    private static string Code(ResultBase result) => (string)Assert.Single(result.Errors).Metadata["ErrorCode"];
    private static NeighborhoodDeliveryRequest Clone(NeighborhoodDeliveryRequest source)
        => System.Text.Json.JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(System.Text.Json.JsonSerializer.Serialize(source))!;
    private static NeighborhoodFulfillmentSelection Selection(NeighborhoodFulfillmentOption option) => new()
    { ProviderId = option.ProviderId, QuoteId = option.QuoteId, ConfirmedCostKrw = option.TotalCostKrw!.Value,
        ProviderInformationSharingAgreed = true, DirectPaymentAgreed = option.ProviderId == "internal" };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection keeper;
        private readonly string connectionString;
        public SsalddelContext Db { get; }
        public Current Current { get; } = new();
        public Clock Clock { get; } = new();
        public RecordingProvider Local { get; }
        public RecordingProvider Quick { get; }
        public RecordingProvider Internal { get; }
        public NeighborhoodDeliveryRequest Input { get; }
        public 화주운송기준운임견적응답 Fare { get; } = new() { 예상거리Km = 3, 최종운임 = 10000 };
        public 생활배송처리처Service Service => NewService(Db);
        private Fixture(SqliteConnection connection, SsalddelContext db, string connectionString)
        {
            keeper = connection; Db = db; this.connectionString = connectionString;
            Local = new(new MockNeighborhoodFulfillmentProvider("mock-local", "모의 생활 배송", 10, 10, .85m, 60, Clock));
            Quick = new(new MockNeighborhoodFulfillmentProvider("mock-quick", "모의 퀵 배송", 15, 20, 1.15m, 25, Clock));
            Internal = new(new MockNeighborhoodFulfillmentProvider("internal", "자체 배차", 20, 30, 1, 45, Clock));
            Input = new() { ClientRequestId = Guid.NewGuid(), CargoName = "생활 물품", WeightKg = 2, VehicleType = "오토바이",
                Pickup = new() { 주소 = new() { 도로명주소 = "동일로 촬영용 출발지", 상세주소 = "", 위도 = 37.58m, 경도 = 127.08m },
                    연락처 = new() { 이름 = "촬영용 담당자", 전화번호 = "010-0000-0000" }, 시간창 = new() { 시작일시 = Clock.Now, 종료일시 = Clock.Now.AddHours(1) } },
                Dropoff = new() { 주소 = new() { 도로명주소 = "동일로 촬영용 도착지", 상세주소 = "", 위도 = 37.59m, 경도 = 127.08m },
                    연락처 = new() { 이름 = "촬영용 담당자", 전화번호 = "010-0000-0000" } }, Notes = "" };
        }
        public static async Task<Fixture> CreateAsync(bool createSchema = true)
        {
            var cs = "Data Source=fulfillment-" + Guid.NewGuid().ToString("N") + ";Mode=Memory;Cache=Shared";
            var connection = new SqliteConnection(cs); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(cs).Options, new Encryption());
            var f = new Fixture(connection, db, cs);
            if (createSchema)
            {
                await db.Database.EnsureCreatedAsync();
                db.화주운송의뢰.Add(new() { 의뢰Id = "request-1", 화주Id = "owner", 주문자UserId = "owner", 모의처리처수행 = true,
                    클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + f.Input.ClientRequestId.ToString("N"),
                    화물종류 = f.Input.CargoName, 화물수량 = 1, 화물중량Kg = 2, 차량종류 = "오토바이", 최종운임 = 10000,
                    픽업_도로명주소 = f.Input.Pickup.주소.도로명주소, 픽업_위도 = 37.58m, 픽업_경도 = 127.08m,
                    픽업_연락처_이름 = "촬영용 담당자", 픽업_연락처_전화번호 = "010-0000-0000",
                    픽업_시간창_시작일시 = f.Clock.Now, 픽업_시간창_종료일시 = f.Clock.Now.AddHours(1),
                    하차_도로명주소 = f.Input.Dropoff.주소.도로명주소, 하차_위도 = 37.59m, 하차_경도 = 127.08m,
                    하차_연락처_이름 = "촬영용 담당자", 하차_연락처_전화번호 = "010-0000-0000" });
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            }
            return f;
        }
        public SsalddelContext OpenDb(IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connectionString);
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return new(builder.Options, new Encryption());
        }
        public 생활배송처리처Service NewService(SsalddelContext db, bool enabled = true, SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation)
        {
            var transport = DispatchProxy.Create<I화주운송의뢰UseCase, QuoteTransport>();
            return new(db, Current, [Internal, Local, Quick], Options.Create(new NeighborhoodFulfillmentOptions { Enabled = enabled }),
                new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })), Clock, transport);
        }
        public async Task SelectInputAsync(string provider)
        {
            var option = (await Service.견적Async(Input, Fare, null, default)).Single(x => x.ProviderId == provider);
            Input.FulfillmentSelection = Selection(option);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await keeper.DisposeAsync(); }
    }
    private sealed class Current : ICurrentUserAccessor
    {
        public string? UserId { get; set; } = "owner";
        public string? Role => null;
    }
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2080, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class InitialChoiceBarrier : DbTransactionInterceptor
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HashSet<DbConnection> connections = [];
        public int Arrivals;
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            // transaction 진입 전에 양 연결을 출발시킵니다. SQLite의 BEGIN IMMEDIATE 뒤에 barrier를 두면 교착됩니다.
            lock (connections)
            {
                // 공급자 응답 저장의 암시적 transaction을 새로운 참가자로 세지 않습니다.
                if (!connections.Add(connection)) return result;
                if (++Arrivals == 2) release.TrySetResult();
            }
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
    public class QuoteTransport : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == nameof(I화주운송의뢰UseCase.기준운임견적Async)
                ? Task.FromResult(Result.Ok(new 화주운송기준운임견적응답 { 최종운임 = 10000, 예상거리Km = 3 }))
                : throw new NotSupportedException(method?.Name);
    }
    private sealed class RecordingProvider(IFulfillmentProvider inner) : IFulfillmentProvider
    {
        public string Id => inner.Id; public string Name => inner.Name; public bool IsSimulation => inner.IsSimulation;
        public FulfillmentProviderCapabilities Capabilities => inner.Capabilities;
        public int Submits; public int Reads; public int Cancels;
        public bool LoseSubmitResponse; public bool LoseCancelResponse;
        public TaskCompletionSource? SubmitEntered; public TaskCompletionSource? ReleaseSubmit;
        public TaskCompletionSource? ReadEntered; public TaskCompletionSource? ReleaseRead;
        public Task<FulfillmentProviderQuote> QuoteAsync(FulfillmentQuoteInput input, CancellationToken ct) => inner.QuoteAsync(input, ct);
        public async Task<FulfillmentProviderResult> SubmitAsync(FulfillmentProviderOperation operation, CancellationToken ct)
        {
            Interlocked.Increment(ref Submits); SubmitEntered?.TrySetResult();
            if (ReleaseSubmit is not null) await ReleaseSubmit.Task;
            if (LoseSubmitResponse) throw new HttpRequestException("response lost");
            return await inner.SubmitAsync(operation, ct);
        }
        public async Task<FulfillmentProviderResult> ReadAsync(FulfillmentProviderOperation operation, CancellationToken ct)
        {
            Interlocked.Increment(ref Reads);
            var result = await inner.ReadAsync(operation, ct); ReadEntered?.TrySetResult();
            if (ReleaseRead is not null) await ReleaseRead.Task;
            return result;
        }
        public Task<FulfillmentProviderResult> CancelAsync(FulfillmentProviderOperation operation, CancellationToken ct)
        { Cancels++; if (LoseCancelResponse) throw new HttpRequestException("response lost"); return inner.CancelAsync(operation, ct); }
    }
}
