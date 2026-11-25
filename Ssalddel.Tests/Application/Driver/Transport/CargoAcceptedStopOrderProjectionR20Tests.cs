using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class CargoAcceptedStopOrderProjectionR20Tests
{
    [Theory]
    [InlineData("pickup", "확정", "상차지로 이동")]
    [InlineData("pickup", "상차지도착", "상차 확인·인수증 증빙")]
    [InlineData("dropoff", "운송중", "하차지로 이동")]
    [InlineData("dropoff", "하차지도착", "하차 완료 증빙")]
    public async Task 현재운송과작업공간은_수락순서의첫미완료정차를같이선택한다(string stage, string status, string nextAction)
    {
        await using var db = CreateContext();
        var a = Transport(1, "A", "상차완료");
        var b = Transport(2, "B", status);
        db.운송원장.AddRange(a, b); await db.SaveChangesAsync();
        var plan = Plan(Stop("B", stage, 2, 0), Stop("A", "dropoff", 1, 1));
        var schedules = new Schedule(plan);
        var current = await new 운송현재조회QueryHandler(db, null, schedules).Handle(new("driver-1"), default);
        var workspace = await Workspace(schedules, Summary(a), Summary(b));
        Assert.Equal(2, current!.Id);
        Assert.Equal(2, workspace.다음행동운송!.Id);
        Assert.Equal(nextAction, workspace.다음행동);
        Assert.Equal("B", workspace.권장경로정차목록[0].의뢰Id);
        Assert.Empty(workspace.BlockCodes);
        Assert.Equal("상차완료", (await db.운송원장.FindAsync(1L))!.상태);
        Assert.Equal(status, (await db.운송원장.FindAsync(2L))!.상태);
    }

    [Fact]
    public async Task B완료뒤에는_남은A정차를선택하고_구판은기존선택을보존한다()
    {
        var a = Summary(Transport(1, "A", "상차완료"));
        var b = Summary(Transport(2, "B", "인수완료"));
        var result = await Workspace(new(Plan(Stop("A", "dropoff", 1, 0))), a, b);
        Assert.Equal(1, result.다음행동운송!.Id);
        Assert.Single(result.활성운송목록);
        var legacy = Plan(Stop("B", "pickup", 2, 0), Stop("A", "dropoff", 1, 1)) with { 수락정차순서반영됨 = false };
        result = await Workspace(new(legacy), a, Summary(Transport(2, "B", "확정")));
        Assert.Equal(1, result.다음행동운송!.Id);
        Assert.Empty(result.BlockCodes);
    }

    [Theory]
    [InlineData("SourceChanged")]
    [InlineData("OwnershipChanged")]
    [InlineData("ActiveWorkChanged")]
    public async Task 순서근거가보류되면_확정된순서로오인하지않고추가수락을차단한다(string reason)
    {
        var code = "FreightAcceptedStopOrder." + reason;
        var plan = Plan(Stop("B", "pickup", 2, 0), Stop("A", "dropoff", 1, 1))
            with { 수락정차순서반영됨 = false, 수락정차순서보류Code = code };
        var result = await Workspace(new(plan), Summary(Transport(1, "A", "상차완료")), Summary(Transport(2, "B", "확정")));
        Assert.Contains(code, result.WarningCodes);
        Assert.Contains(code, result.BlockCodes);
        Assert.Equal("수락정차순서보류·재확인필요", result.일정검증상태);
        Assert.Contains("다시 확인", result.다음행동);
    }

    [Theory]
    [InlineData("pickup", "상차완료", 2, "B")]
    [InlineData("dropoff", "확정", 2, "B")]
    [InlineData("dropoff", "운송중", 999, "B")]
    [InlineData("unknown", "운송중", 2, "B")]
    public async Task 첫정차와현재원장의연결이나단계가다르면_오래된순서를채택하지않는다(string stage, string status, long id, string requestId)
    {
        var plan = Plan(Stop(requestId, stage, id, 0), Stop("A", "dropoff", 1, 1));
        var result = await Workspace(new(plan), Summary(Transport(1, "A", "상차완료")), Summary(Transport(2, "B", status)));
        Assert.Contains("FreightAcceptedStopOrder.NextActionMismatch", result.BlockCodes);
        Assert.Contains("다시 확인", result.다음행동);
    }

    [Fact]
    public async Task 요약의별도운송번호를_의뢰Id로혼동하지않고_원장조회에서는실제의뢰참조를대조한다()
    {
        await using var db = CreateContext();
        var a = Transport(1, "A", "상차완료");
        var b = Transport(2, "B", "운송중");
        a.운송번호 = "WAYBILL-A"; b.운송번호 = "WAYBILL-B";
        db.운송원장.AddRange(a, b); await db.SaveChangesAsync();
        var plan = Plan(Stop("B", "dropoff", 2, 0), Stop("A", "dropoff", 1, 1));
        var summaryB = Summary(b); summaryB.운송번호 = b.운송번호;
        var result = await Workspace(new(plan), Summary(a), summaryB);
        Assert.Equal(2, result.다음행동운송!.Id); Assert.Empty(result.BlockCodes);
        var current = await new 운송현재조회QueryHandler(db, null, new Schedule(plan)).Handle(new("driver-1"), default);
        Assert.Equal(2, current!.Id);
        var wrongReference = Plan(Stop("other-request", "dropoff", 2, 0));
        current = await new 운송현재조회QueryHandler(db, null, new Schedule(wrongReference)).Handle(new("driver-1"), default);
        Assert.Contains("다시 확인", current!.다음행동안내);
    }

    [Fact]
    public async Task 개인정보보류정차는_별도표시운송번호여도_typed원장Id로가리고_허용된A는유지한다()
    {
        var a = Summary(Transport(1, "A", "상차완료"));
        var b = Summary(Transport(2, "B", "확정"));
        b.운송번호 = "display-B"; b.개인정보제공보류 = true;
        var plan = Plan(Stop("B", "pickup", 2, 0) with { 주소 = "B의 비공개 상차 주소" },
            Stop("B", "dropoff", 2, 1) with { 주소 = "B의 비공개 하차 주소" },
            Stop("A", "dropoff", 1, 2) with { 주소 = "허용된 A의 하차 주소" });
        var result = await Workspace(new(plan), a, b);
        var pickup = result.권장경로정차목록[0]; var dropoff = result.권장경로정차목록[1];
        Assert.Equal(생활배송기사정보공개Policy.PickupPending, pickup.주소);
        Assert.Equal(생활배송기사정보공개Policy.DropoffPending, dropoff.주소);
        Assert.False(pickup.좌표근거있음); Assert.False(dropoff.좌표근거있음);
        Assert.Equal("허용된 A의 하차 주소", result.권장경로정차목록[2].주소);
        Assert.True(result.권장경로정차목록[2].좌표근거있음);
        Assert.DoesNotContain("B의 비공개", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Contains("FreightScheduleEvidenceMissing", result.BlockCodes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(999L)]
    public async Task typed연결Id가없거나현재목록에없으면_문자열일치로주소를공개하지않는다(long? transportId)
    {
        var plan = Plan(Stop("B", "pickup", 2, 0) with { 운송Id = transportId, 주소 = "연결 불명 비공개 주소" },
            Stop("A", "dropoff", 1, 1) with { 주소 = "허용된 A의 하차 주소" });
        var result = await Workspace(new(plan), Summary(Transport(1, "A", "상차완료")), Summary(Transport(2, "B", "확정")));
        Assert.Equal(생활배송기사정보공개Policy.PickupPending, result.권장경로정차목록[0].주소);
        Assert.False(result.권장경로정차목록[0].좌표근거있음);
        Assert.Equal("허용된 A의 하차 주소", result.권장경로정차목록[1].주소);
        Assert.DoesNotContain("연결 불명 비공개", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task 같은원장Id의보류와허용응답이충돌하면_허용된것만골라공개하지않는다()
    {
        var allowed = Summary(Transport(2, "B", "확정"));
        var held = Summary(Transport(2, "B", "확정")); held.개인정보제공보류 = true;
        var result = await Workspace(new(Plan(Stop("B", "pickup", 2, 0) with { 주소 = "충돌된 비공개 주소" })), allowed, held);
        Assert.Equal(생활배송기사정보공개Policy.PickupPending, Assert.Single(result.권장경로정차목록).주소);
        Assert.False(result.권장경로정차목록[0].좌표근거있음);
        Assert.DoesNotContain("충돌된 비공개", System.Text.Json.JsonSerializer.Serialize(result));
    }

    private static async Task<기사화물운송작업공간응답> Workspace(Schedule plan, params 기사운송요약응답[] items)
        => await new 화물운송작업공간조회QueryHandler(new Sender(items), plan, new Continuity())
            .Handle(new("driver-1"), default);

    private static 기사운송일정계획 Plan(params 기사운송일정항목[] stops)
        => new("driver-1", DateTime.UtcNow, null, stops) { 수락정차순서반영됨 = true };
    private static 기사운송일정항목 Stop(string requestId, string stage, long transportId, int order)
        => new(requestId, stage, "합성 장소", new(37.5m, 127m), DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2), order, transportId, true, false) { 작업시간분 = 2m };
    private static 운송원장 Transport(long id, string requestId, string status) => new()
    {
        Id = id, 의뢰Id = requestId, 운송번호 = requestId, 기사_운송자 = "driver-1", 확정기사Id = "driver-1",
        상태 = status, UpdatedAt = DateTime.UtcNow, 출발_픽업 = DateTime.UtcNow.AddHours(-id)
    };
    private static 기사운송요약응답 Summary(운송원장 value) => new()
        { Id = value.Id, 운송번호 = value.의뢰Id, 기사_운송자 = value.기사_운송자, 상태 = value.상태 };
    private static SsalddelContext CreateContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("accepted-stop-order-projection-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Schedule(기사운송일정계획 plan) : I기사운송일정구성Service
    {
        public Task<기사운송일정계획> 구성Async(string 기사Id, 배차경로좌표? 시작좌표, CancellationToken cancellationToken = default)
            => Task.FromResult(plan);
    }
    private sealed class Sender(IReadOnlyList<기사운송요약응답> items) : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse)(object)items);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Continuity : I화물연속배차UseCase
    {
        public Task<화물연속배차상태Dto> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물연속배차상태Dto());
        public Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string id, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<화물운송시간약속Dto>>([]);
        public Task<화물경로위험Dto> 현재위험조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물경로위험Dto());
        public Task<화물연속배차상태Dto> 의사변경Async(string id, 화물연속배차의사변경요청 request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<화물다음콜예약Dto?> 추천탐색Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<화물예약검증결과> 수락예약검증Async(string id, string requestId, string? reservationId, long? revision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 수락완료Async(string id, string requestId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 시간약속잠금Async(string id, 화주운송의뢰 request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 완료기록Async(string id, DateTime at, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
