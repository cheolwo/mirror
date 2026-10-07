using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacySupport;

namespace Ssalddel.Tests.Services.PrivacySupport;

public sealed class 보호지원UseCaseTests
{
    [Fact]
    public async Task 본인원장으로확인되지않은ID는분쟁접수에쓸수없다()
    {
        var f = new Fixture();
        f.Source.Available = false;
        var ex = await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.분쟁접수Async(Dispute()));
        Assert.Equal("SourceNotFound", ex.Code);
        Assert.Empty(f.Store.Rows);
    }

    [Fact]
    public async Task 역할을고르거나사용자ID를알아도운영자권한없이담당자배정할수없다()
    {
        var f = new Fixture();
        var row = await f.Service.분쟁접수Async(Dispute());
        f.SetActor("not-admin");
        var ex = await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.변경Async(row.CaseId, Command(row, "assign-self"), admin: true));
        Assert.Equal("AdminRequired", ex.Code);
        Assert.Null(f.Store.Rows[row.CaseId].AssignedAdminUserId);
    }

    [Fact]
    public async Task 다른사람의사건은존재여부와비공개증거를보여주지않는다()
    {
        var f = new Fixture();
        var row = await f.Service.분쟁접수Async(Dispute());
        f.SetActor("outsider");
        Assert.Equal("CaseNotFound", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.상세Async(row.CaseId))).Code);
        Assert.Empty((await f.Service.목록Async(보호지원종류Codes.분쟁)).Items);
    }

    [Fact]
    public async Task 상대방에는원문과접수자증거를반환하지않는다()
    {
        var f = new Fixture();
        var row = await f.Service.분쟁접수Async(Dispute());
        f.SetActor("seller");
        var response = await f.Service.상세Async(row.CaseId);
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("010-1234-5678", json);
        Assert.DoesNotContain("우리집101호", json);
        Assert.Empty((await f.Service.증거조회Async(row.CaseId, "own-request")).Items);
    }

    [Fact]
    public async Task 증거열람은본인증거만반환하고감사저장실패시반환하지않는다()
    {
        var f = new Fixture();
        var row = await f.Service.분쟁접수Async(Dispute());
        var evidence = await f.Service.증거조회Async(row.CaseId, "own-request");
        Assert.Contains(evidence.Items, x => x.Text.Contains("010-1234-5678"));
        Assert.Contains(f.Store.Rows[row.CaseId].History, x => x.Action == "read-evidence:own-request");
        f.Store.AlwaysConflict = true;
        Assert.Equal("RevisionConflict", (await Assert.ThrowsAsync<보호지원Exception>(
            () => f.Service.증거조회Async(row.CaseId, "own-request"))).Code);
    }

    [Fact]
    public async Task 원문은배정된운영자만사유와함께조회한다()
    {
        var f = new Fixture();
        var row = await f.Service.분쟁접수Async(Dispute());
        f.SetActor("admin", true);
        Assert.Equal("EvidenceAccessDenied", (await Assert.ThrowsAsync<보호지원Exception>(
            () => f.Service.증거조회Async(row.CaseId, "case-review", true))).Code);
        row = await f.Service.변경Async(row.CaseId, Command(row, "assign-self"), true);
        var evidence = await f.Service.증거조회Async(row.CaseId, "case-review", true);
        Assert.Equal(["buyer", "seller"], evidence.AuthorizedRecipientIds);
        f.SetActor("another-admin", true);
        row = await f.Service.상세Async(row.CaseId, true);
        Assert.Equal("ActionNotAllowed", (await Assert.ThrowsAsync<보호지원Exception>(
            () => f.Service.변경Async(row.CaseId, Command(row, "start-review"), true))).Code);
    }

    [Fact]
    public async Task 접수와명령재시도는동일내용일때만멱등이고다른내용은충돌한다()
    {
        var f = new Fixture();
        var request = Dispute();
        var row = await f.Service.분쟁접수Async(request);
        Assert.True((await f.Service.분쟁접수Async(request)).Replay);
        request.Summary = "다른내용";
        Assert.Equal("IdempotencyConflict", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.분쟁접수Async(request))).Code);
        var command = Command(row, "add-evidence", "추가증거");
        var changed = await f.Service.변경Async(row.CaseId, command);
        Assert.True((await f.Service.변경Async(row.CaseId, command)).Replay);
        Assert.Equal(2, changed.Revision);
        command.PrivateEvidence = "새 증거";
        Assert.Equal("IdempotencyConflict", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.변경Async(row.CaseId, command))).Code);
    }

    [Fact]
    public async Task 저장CAS충돌은상태성공으로숨기지않고동일명령재시도를허용한다()
    {
        var f = new Fixture();
        var row = await f.Service.분쟁접수Async(Dispute());
        var command = Command(row, "add-evidence", "증거");
        f.Store.AlwaysConflict = true;
        Assert.Equal("RevisionConflict", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.변경Async(row.CaseId, command))).Code);
        Assert.Equal(1, f.Store.Rows[row.CaseId].Revision);
        f.Store.AlwaysConflict = false;
        Assert.Equal(2, (await f.Service.변경Async(row.CaseId, command)).Revision);
    }

    [Fact]
    public async Task 안내문을작성해도전달증빙없이통지완료나종결로표시하지않는다()
    {
        var f = new Fixture();
        var row = await f.Service.분쟁접수Async(Dispute());
        f.SetActor("admin", true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "assign-self"), true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "prepare-progress", summary: "접수 내용 조사 중"), true);
        Assert.Null(row.ProgressNotifiedAtUtc);
        Assert.Equal("InvalidInput", (await Assert.ThrowsAsync<보호지원Exception>(
            () => f.Service.변경Async(row.CaseId, Command(row, "record-progress-notified"), true))).Code);
        row = await f.Service.변경Async(row.CaseId, Command(row, "prepare-result", summary: "처리방안"), true);
        Assert.Equal("ResultNoticePending", (await Assert.ThrowsAsync<보호지원Exception>(
            () => f.Service.변경Async(row.CaseId, Command(row, "close"), true))).Code);
    }

    [Fact]
    public async Task 모든당사자안내증빙이있어야종결되고이의제기해도원기한은보존한다()
    {
        var f = new Fixture();
        var row = await f.Service.분쟁접수Async(Dispute());
        var originalDue = row.ResultDueAtUtc;
        f.SetActor("admin", true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "assign-self"), true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "prepare-result", summary: "당사자간 해결방안 안내"), true);
        foreach (var party in new[] { "buyer", "seller" })
        {
            var command = Command(row, "record-result-notified");
            command.RecipientUserId = party; command.DeliveryEvidenceRef = "delivered:" + party; command.OccurredAtUtc = f.Clock.Now;
            row = await f.Service.변경Async(row.CaseId, command, true);
        }
        Assert.NotNull(row.ResultNotifiedAtUtc);
        row = await f.Service.변경Async(row.CaseId, Command(row, "close"), true);
        Assert.Equal("closed", row.StatusCode);
        f.SetActor("buyer");
        f.Clock.Now = f.Clock.Now.AddDays(20);
        row = await f.Service.변경Async(row.CaseId, Command(row, "appeal", "추가 확인 필요"));
        Assert.Equal("reopened", row.StatusCode);
        Assert.Equal(originalDue, row.ResultDueAtUtc);
    }

    [Fact]
    public async Task 개인정보삭제요청은실행근거없는완료표시를차단한다()
    {
        var f = new Fixture();
        var row = await f.Service.권리접수Async(new() { ClientRequestId = Guid.NewGuid(), RequestKind = "deletion", Summary = "내정보삭제" });
        f.SetActor("admin", true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "assign-self"), true);
        Assert.DoesNotContain("complete-rights", row.AllowedActions);
        row = await f.Service.변경Async(row.CaseId, Command(row, "prepare-result", summary: "접수됨"), true);
        var notification = Command(row, "record-result-notified");
        notification.RecipientUserId = "buyer"; notification.DeliveryEvidenceRef = "manual:delivery-confirmation"; notification.OccurredAtUtc = f.Clock.Now;
        row = await f.Service.변경Async(row.CaseId, notification, true);
        Assert.Equal("ExecutionEvidenceUnavailable", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.변경Async(row.CaseId, Command(row, "close"), true))).Code);
    }

    [Fact]
    public async Task 안내문이바뀌면예전전달증빙으로새결과를종결할수없다()
    {
        var f = new Fixture(); var row = await f.Service.분쟁접수Async(Dispute()); f.SetActor("admin", true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "assign-self"), true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "prepare-result", summary: "첫 결과"), true);
        foreach (var party in new[] { "buyer", "seller" })
        {
            var notice = Command(row, "record-result-notified"); notice.RecipientUserId = party;
            notice.DeliveryEvidenceRef = "external-ack:" + party; notice.OccurredAtUtc = f.Clock.Now;
            row = await f.Service.변경Async(row.CaseId, notice, true);
        }
        Assert.NotNull(row.ResultNotifiedAtUtc);
        row = await f.Service.변경Async(row.CaseId, Command(row, "prepare-result", summary: "수정된 해결 방안"), true);
        Assert.Null(row.ResultNotifiedAtUtc);
        Assert.Equal("ResultNoticePending", (await Assert.ThrowsAsync<보호지원Exception>(
            () => f.Service.변경Async(row.CaseId, Command(row, "close"), true))).Code);
        Assert.Equal(2, row.Notifications.Count);
    }

    [Fact]
    public async Task 거절은법적근거와안내증빙을남겨야하며본인은이의제기를할수있다()
    {
        var f = new Fixture();
        var row = await f.Service.권리접수Async(new() { ClientRequestId = Guid.NewGuid(), RequestKind = "deletion", Summary = "내정보삭제" });
        f.SetActor("admin", true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "assign-self"), true);
        Assert.Equal("InvalidInput", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.변경Async(row.CaseId, Command(row, "deny", summary: "보존필요"), true))).Code);
        var denial = Command(row, "deny", summary: "거래기록 별도 보존"); denial.LegalBasis = "법정 보존 근거 및 종료일";
        denial.LegalReasonCode = "statutory-retention"; denial.RetainUntilUtc = f.Clock.Now.AddYears(3);
        row = await f.Service.변경Async(row.CaseId, denial, true);
        Assert.Equal("denial-prepared", row.StatusCode);
        Assert.DoesNotContain("법정 보존 근거", JsonSerializer.Serialize(row));
        var notice = Command(row, "record-result-notified"); notice.RecipientUserId = "buyer";
        notice.DeliveryEvidenceRef = "actual:receipt:1"; notice.OccurredAtUtc = f.Clock.Now;
        row = await f.Service.변경Async(row.CaseId, notice, true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "close"), true);
        f.SetActor("buyer");
        Assert.Contains("appeal", (await f.Service.상세Async(row.CaseId)).AllowedActions);
    }

    [Fact]
    public async Task 사고는실제가능성기관신고를별도로판정하고원인지72시간기한을보존한다()
    {
        var f = new Fixture(); f.SetActor("admin", true);
        var known = f.Clock.Now.AddHours(-20);
        var row = await f.Service.사고접수Async(new() { ClientRequestId = Guid.NewGuid(), KnownAtUtc = known, Summary = "접근이상" });
        Assert.Equal(known.AddHours(72), row.IncidentDeadlineAtUtc);
        var command = Command(row, "assess-incident");
        command.IncidentAssessment = new() { OccurrenceCode = "possible", IndividualNoticeRequired = true, AuthorityReportRequired = false,
            AffectedPersonCount = 1, LegalBasis = "유출 가능성 요건 검토" };
        row = await f.Service.변경Async(row.CaseId, command, true);
        Assert.True(row.IncidentAssessment!.IndividualNoticeRequired);
        Assert.False(row.IncidentAssessment.AuthorityReportRequired);
        f.Clock.Now = known.AddHours(73);
        Assert.True((await f.Service.상세Async(row.CaseId, true)).IncidentOverdue);
        row = await f.Service.변경Async(row.CaseId, Command(await f.Service.상세Async(row.CaseId, true), "record-followup", "추가 조사"), true);
        Assert.Equal(known.AddHours(72), row.IncidentDeadlineAtUtc);
    }

    [Fact]
    public async Task 오탐정정은과거판단을삭제하지않고인지시각기한도재설정하지않는다()
    {
        var f = new Fixture(); f.SetActor("admin", true);
        var row = await f.Service.사고접수Async(new() { ClientRequestId = Guid.NewGuid(), KnownAtUtc = f.Clock.Now, Summary = "이상 접속 조사" });
        var due = row.IncidentDeadlineAtUtc;
        var command = Command(row, "assess-incident");
        command.IncidentAssessment = new() { OccurrenceCode = "false-alarm", IndividualNoticeRequired = false, AuthorityReportRequired = false, LegalBasis = "검토 결과 개인자료 노출 없음" };
        row = await f.Service.변경Async(row.CaseId, command, true);
        Assert.Equal("false-alarm", row.StatusCode);
        Assert.Contains(row.History, x => x.Action == "assess-incident");
        Assert.Equal(due, row.IncidentDeadlineAtUtc);
    }

    [Fact]
    public async Task 일부정보주체통지증빙은전체영향대상완료나기한충족을의미하지않는다()
    {
        var f = new Fixture(); f.SetActor("admin", true);
        var row = await f.Service.사고접수Async(new() { ClientRequestId = Guid.NewGuid(), KnownAtUtc = f.Clock.Now.AddHours(-73), Summary = "실제 유출 검토" });
        var assessment = Command(row, "assess-incident");
        assessment.IncidentAssessment = new() { OccurrenceCode = "actual", AffectedPersonCount = 1500,
            IndividualNoticeRequired = true, AuthorityReportRequired = true, LegalBasis = "요건 확인" };
        row = await f.Service.변경Async(row.CaseId, assessment, true);
        row = await f.Service.변경Async(row.CaseId, Command(row, "prepare-incident-notice", summary: "통지 문안"), true);
        foreach (var action in new[] { "record-individual-notified", "record-authority-reported" })
        {
            var notice = Command(row, action); notice.DeliveryEvidenceRef = "manual-batch-record"; notice.OccurredAtUtc = f.Clock.Now;
            row = await f.Service.변경Async(row.CaseId, notice, true);
        }
        Assert.True(row.IncidentOverdue);
        Assert.False(row.IncidentNoticeCoverageVerified);
        Assert.DoesNotContain("close", row.AllowedActions);
        Assert.Contains(row.Notifications, x => x.RecipientRoleCode == "individual-notice-record");
    }

    [Fact]
    public async Task 개인사용자는운영사고접수와목록을열수없다()
    {
        var f = new Fixture();
        Assert.Equal("AdminRequired", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.사고접수Async(new()
        { ClientRequestId = Guid.NewGuid(), KnownAtUtc = f.Clock.Now, Summary = "임의사고" }))).Code);
        Assert.Equal("CaseNotFound", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.목록Async(보호지원종류Codes.사고))).Code);
    }

    [Fact]
    public void 한국시간주말공휴일을제외하며검증되지않은달력은잠정기한으로남긴다()
    {
        var options = new 보호지원Options { Holidays = ["2026-10-09"], CalendarVersion = "kr-2026-verified",
            CalendarVerifiedFrom = new(2026, 1, 1), CalendarVerifiedThrough = new(2026, 12, 31) };
        var calendar = new 한국영업일Calendar(Options.Create(options));
        var received = new DateTime(2026, 10, 8, 14, 30, 0, DateTimeKind.Utc); // 한국 10/8 목요일
        var due = calendar.기한(received, 3); // 12월요일,13화요일,14수요일
        Assert.Equal(new DateTime(2026, 10, 14, 14, 59, 59, DateTimeKind.Utc).AddTicks(9999999), due.DueAtUtc);
        Assert.True(due.Verified);
        options.CalendarVerifiedThrough = new(2026, 10, 10);
        Assert.False(calendar.기한(received, 3).Verified);
    }

    [Fact]
    public async Task 비로그인상태는권리요청과분쟁접수를저장하지않는다()
    {
        var f = new Fixture(); f.Http.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Equal("AuthenticationRequired", (await Assert.ThrowsAsync<보호지원Exception>(() => f.Service.분쟁접수Async(Dispute()))).Code);
        Assert.Empty(f.Store.Rows);
    }

    private static 거래분쟁접수Request Dispute() => new() { ClientRequestId = Guid.NewGuid(), SourceKind = 보호지원출처Codes.음식주문,
        SourceId = "order-verified", Summary = "우리집101호 거래 문제", PrivateEvidence = "010-1234-5678 비공개" };
    private static 보호지원CommandRequest Command(보호지원CaseResponse row, string action, string evidence = "", string summary = "")
        => new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = row.Revision, Action = action, PrivateEvidence = evidence, Summary = summary };

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public Source Source { get; } = new();
        public Clock Clock { get; } = new();
        public HttpContextAccessor Http { get; } = new() { HttpContext = new DefaultHttpContext() };
        public 보호지원UseCase Service { get; }
        public Fixture()
        {
            SetActor("buyer");
            var options = Options.Create(new 보호지원Options());
            Service = new(Store, Source, new(options), options, Clock, Http, new Authorization());
        }
        public void SetActor(string id, bool admin = false) => Http.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, id), new(ClaimTypes.Role, admin ? "actual-test-admin" : "normal-user")], "test"));
    }
    private sealed class Source : I보호지원SourceResolver
    {
        public bool Available { get; set; } = true;
        public Task<보호지원Source?> 조회Async(string kind, string id, string actor, CancellationToken cancellationToken = default)
            => Task.FromResult<보호지원Source?>(Available && actor is "buyer" or "seller" ? new(["buyer", "seller"]) : null);
    }
    private sealed class Authorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(user.IsInRole("actual-test-admin") ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => AuthorizeAsync(user, resource, Array.Empty<IAuthorizationRequirement>());
    }
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Store : I보호지원Store
    {
        public Dictionary<string, 보호지원Record> Rows { get; } = new(StringComparer.Ordinal);
        public bool AlwaysConflict { get; set; }
        public Task<보호지원Record?> 조회Async(string id, CancellationToken ct = default)
            => Task.FromResult(Rows.TryGetValue(id, out var row) ? Clone(row) : null);
        public Task<bool> 생성Async(보호지원Record row, CancellationToken ct = default)
            => Task.FromResult(Rows.TryAdd(row.CaseId, Clone(row)));
        public Task<bool> 교체Async(보호지원Record row, long expected, CancellationToken ct = default)
        {
            if (AlwaysConflict || !Rows.TryGetValue(row.CaseId, out var prior) || prior.Revision != expected) return Task.FromResult(false);
            Rows[row.CaseId] = Clone(row); return Task.FromResult(true);
        }
        public Task<IReadOnlyList<보호지원Record>> 목록Async(string? userId, string? kind, int skip, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<보호지원Record>>(Rows.Values.Where(x => (userId is null || x.PartyUserIds.Contains(userId))
                && (kind is null || x.Kind == kind)).Skip(skip).Take(take).Select(Clone).ToArray());
        public Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime now, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<보호지원Record>>(Rows.Values.Where(x => x.PurgeAfterAtUtc <= now && !x.SourceHoldActive).Take(take).Select(Clone).ToArray());
        public Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<보호지원Record>>(Rows.Values.Where(x => x.RetentionIntent is not null).Take(take).Select(Clone).ToArray());
        public Task<bool> 파기Async(보호지원Record row, DateTime now, CancellationToken ct = default)
            => Task.FromResult(Rows.TryGetValue(row.CaseId, out var prior) && prior.Revision == row.Revision && Rows.Remove(row.CaseId));
        private static 보호지원Record Clone(보호지원Record row) => JsonSerializer.Deserialize<보호지원Record>(JsonSerializer.Serialize(row))!;
    }
}
