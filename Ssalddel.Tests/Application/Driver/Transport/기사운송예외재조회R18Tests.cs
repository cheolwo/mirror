using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Services.Operations;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.사용자;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class 기사운송예외재조회R18Tests
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
    [Fact]
    public void 생활배송_정보제공동의가_없으면_신규예외원장과행동을_노출하지않는다()
    {
        var source = new 기사운송상세응답 { Id = 1, 상태 = "상차지도착", 최근예외메시지 = "사적 현장 사유",
            예외검토목록 = [new("private-case", "상차", "화물훼손", "OperationsReviewPending", "PlatformOperationsReview", "FullyHeld", "EntireTransport", true, 2, DateTime.UtcNow)],
            가능한행동 = ["pickup", "issue"] };
        생활배송기사정보공개Policy.운송정보가림(source);
        Assert.Empty(source.예외검토목록); Assert.Empty(source.가능한행동!);
        Assert.Empty(source.최근예외메시지);
        Assert.Null(CargoDriverWorkspaceAdapter.Map(source).Actions);
    }

    [Theory]
    [InlineData("current", "일반문제")]
    [InlineData("detail", "일반문제")]
    [InlineData("list", "일반문제")]
    [InlineData("current", "수량불일치")]
    [InlineData("detail", "수량불일치")]
    [InlineData("list", "수량불일치")]
    public async Task 신고를저장하고_다른조회수명에서도_예외안내와현재행동을복원한다(string query, string code)
    {
        var options = Options();
        long id;
        await using (var db = new SsalddelContext(options, new Encryption()))
        {
            id = await SeedAsync(db);
            var result = await Reporter(db).Handle(new("driver-1", id, "상차", code, "현장 수량 확인", null,
                null, null, true), default);
            Assert.True(result.IsSuccess);
            Assert.True(result.Value.예외신고됨);
        }
        await using var other = new SsalddelContext(options, new Encryption());
        var response = await ReadAsync(other, query, id);
        Assert.NotNull(response);
        Assert.True(response.예외신고됨);
        Assert.True(response.관리자확인필요);
        Assert.Equal("현장 수량 확인", response.최근예외메시지);
        Assert.NotEmpty(response.다음행동안내);
        Assert.Contains("pickup", response.가능한행동!);
        Assert.False(response.운송진행보류);
        Assert.Equal(code == "수량불일치", response.정산보류);
        Assert.Equal(code == "수량불일치" ? 1 : 0, response.예외검토목록.Count);
        Assert.True(Assert.Single(CargoDriverWorkspaceAdapter.Map(response).Actions!, item => item.IsPrimary).Enabled);
    }

    [Theory]
    [InlineData("TransportResumed")]
    [InlineData("IncidentClosed")]
    public async Task 운영검토후_재개또는종료하면_이전신고의관리자차단을다시만들지않는다(string decision)
    {
        await using var db = new SsalddelContext(Options(), new Encryption());
        var id = await SeedAsync(db);
        await Reporter(db).Handle(new("driver-1", id, "상차", "화물훼손", "현장 확인", null, null, null, true,
            현장진행불가: true), default);
        var incident = await db.비정상운송사건.SingleAsync();
        var review = await new 비정상운송사건Service(db).검토Async(new(incident.사건StableId, Guid.NewGuid(),
            incident.Revision, decision, null, null, false, "현장 조건 확인 완료", "operator-1"));
        Assert.True(review.찾음);
        var updated = review.사건!.UpdatedAt;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var response = await ReadAsync(db, "detail", id);
        Assert.NotNull(response);
        Assert.False(response.관리자확인필요);
        Assert.False(response.운송진행보류);
        Assert.False(response.정산보류);
        Assert.Contains("pickup", response.가능한행동!);
        Assert.Equal(updated, Assert.Single(response.예외검토목록).UpdatedAt);
        Assert.DoesNotContain("관리자 지시를 기다려", response.다음행동안내);
    }

    [Fact]
    public async Task 최근사건이종료되어도_다른사건의전체보류는_조회와직접명령을함께차단한다()
    {
        await using var db = new SsalddelContext(Options(), new Encryption());
        var id = await SeedAsync(db);
        db.비정상운송사건.AddRange(
            Incident(id, "old-hold", "FullyHeld", "ActionDecided", true, DateTime.UtcNow.AddMinutes(-5)),
            Incident(id, "new-closed", "Closed", "Closed", false, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var response = await ReadAsync(db, "detail", id);
        Assert.NotNull(response);
        Assert.True(response.운송진행보류);
        Assert.Equal(new[] { "issue" }, response.가능한행동);
        Assert.False(Assert.Single(CargoDriverWorkspaceAdapter.Map(response).Actions!, item => item.IsPrimary).Enabled);
        var result = await Executor(db).실행Async(new("driver-1", id, "driver-1", 살뜰역할유형.기사,
            "상차완료", "Test", context => new TestEvent(context.운송.Id)), default);
        Assert.True(result.IsFailed);
        Assert.Equal("상차지도착", (await db.운송원장.SingleAsync()).상태);
        Assert.Empty(await db.운송이벤트.ToListAsync());
    }

    [Theory]
    [InlineData("ContinueWithCaution", "OperationsReviewPending")]
    [InlineData("PartiallyHeld", "ActionDecided")]
    public async Task 주의진행또는영향수량보류를_전체운송중단으로해석하지않는다(string control, string status)
    {
        await using var db = new SsalddelContext(Options(), new Encryption());
        var id = await SeedAsync(db);
        db.비정상운송사건.Add(Incident(id, "current-review", control, status, true, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var response = await ReadAsync(db, "detail", id);
        Assert.NotNull(response);
        Assert.True(response.정산보류);
        Assert.False(response.운송진행보류);
        Assert.Contains("pickup", response.가능한행동!);
        var result = await Executor(db).실행Async(new("driver-1", id, "driver-1", 살뜰역할유형.기사,
            "상차완료", "Test", context => new TestEvent(context.운송.Id)), default);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task 일반신고의_명시적진행불가는보존하지만_사건을꾸미지않고_새진행가능신고로해제한다()
    {
        await using var db = new SsalddelContext(Options(), new Encryption());
        var id = await SeedAsync(db);
        await Reporter(db).Handle(new("driver-1", id, "상차", "일반문제", "진행 불가", null, null, null, true,
            현장진행불가: true), default);
        var response = await ReadAsync(db, "list", id);
        Assert.NotNull(response);
        Assert.True(response.운송진행보류);
        Assert.Empty(response.예외검토목록);
        await Reporter(db).Handle(new("driver-1", id, "상차", "일반문제", "현장 조건 확인됨", null, null, null, true), default);
        response = await ReadAsync(db, "detail", id);
        Assert.NotNull(response);
        Assert.False(response.운송진행보류);
        Assert.Contains("pickup", response.가능한행동!);
    }

    [Theory]
    [InlineData("배차대기", "arrive-pickup")]
    [InlineData("이동중", "arrive-pickup")]
    [InlineData("상차지도착", "pickup")]
    [InlineData("운송중", "arrive-dropoff")]
    [InlineData("하차지도착", "dropoff")]
    public async Task 기존전이정책의단계가_서버행동과카드에서일치한다(string state, string action)
    {
        await using var db = new SsalddelContext(Options(), new Encryption());
        var id = await SeedAsync(db);
        (await db.운송원장.SingleAsync()).상태 = state;
        await db.SaveChangesAsync();
        var response = await ReadAsync(db, "detail", id);
        Assert.NotNull(response);
        Assert.Contains(action, response.가능한행동!);
        Assert.Equal(action, Assert.Single(CargoDriverWorkspaceAdapter.Map(response).Actions!, item => item.IsPrimary).Key);
    }

    [Fact]
    public async Task 다른기사와다른의뢰사건을_현재운송에노출하지않는다()
    {
        await using var db = new SsalddelContext(Options(), new Encryption());
        var id = await SeedAsync(db);
        var wrong = Incident(id, "foreign", "FullyHeld", "OperationsReviewPending", true, DateTime.UtcNow);
        wrong.운송의뢰Id = "other-request";
        db.비정상운송사건.Add(wrong);
        await db.SaveChangesAsync();
        var response = await ReadAsync(db, "detail", id);
        Assert.NotNull(response);
        Assert.Empty(response.예외검토목록);
        Assert.False(response.운송진행보류);
        Assert.Null(await new 운송상세조회QueryHandler(db).Handle(new("other-driver", id), default));
    }

    private static DbContextOptions<SsalddelContext> Options() => new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("cargo-exception-" + Guid.NewGuid(), DatabaseRoot).Options;
    private static async Task<long> SeedAsync(SsalddelContext db)
    {
        var transport = new 운송원장 { 의뢰Id = "request-1", 운송번호 = "transport-1", 기사_운송자 = "driver-1", 확정기사Id = "driver-1", 상태 = "상차지도착" };
        db.운송원장.Add(transport);
        db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = "request-1", 화주Id = "shipper-1", 주문자UserId = "shipper-1", 화물수량 = 3 });
        await db.SaveChangesAsync(); return transport.Id;
    }
    private static 비정상운송사건 Incident(long id, string name, string control, string status, bool hold, DateTime time)
        => new() { 사건StableId = name, 운송Id = id, 운송의뢰Id = "request-1", 원본예외Code = "수량불일치", 단계Code = "상차",
            업무통제상태Code = control, 상태Code = status, 보류범위Code = control == "FullyHeld" ? "EntireTransport" : control == "PartiallyHeld" ? "AffectedQuantity" : "None",
            정산보류적용여부 = hold, UpdatedAt = time };
    private static 운송문제신고CommandHandler Reporter(SsalddelContext db) => new(db, new Publisher(), new User(), new 참여자실행권한검사(),
        new 운송증빙첨부JsonWriter(), new 비정상운송사건Service(db), NullLogger<운송문제신고CommandHandler>.Instance);
    private static 기사운송상태변경CommandExecutor Executor(SsalddelContext db) => new(db, new 기사운송상태전이Service(),
        new Publisher(), new User(), new 참여자실행권한검사(), NullLogger<기사운송상태변경CommandExecutor>.Instance);
    private static async Task<기사운송요약응답?> ReadAsync(SsalddelContext db, string kind, long id) => kind switch
    {
        "current" => await new 운송현재조회QueryHandler(db).Handle(new("driver-1"), default),
        "list" => (await new 운송목록조회QueryHandler(db).Handle(new("driver-1"), default)).SingleOrDefault(),
        _ => await new 운송상세조회QueryHandler(db).Handle(new("driver-1", id), default)
    };
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class User : ICurrentUserAccessor { public string? UserId => "driver-1"; public string? Role => "기사"; }
    private sealed record TestEvent(long Id) : INotification;
    private sealed class Publisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
