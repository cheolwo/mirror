using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.PrivacySupport;

namespace Ssalddel.Services.PrivacySupport;

public sealed class 보호지원Exception(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class 보호지원UseCase(I보호지원Store store, I보호지원SourceResolver source,
    한국영업일Calendar calendar, IOptions<보호지원Options> options, TimeProvider clock,
    IHttpContextAccessor http, IAuthorizationService authorization,
    I보호지원권리실행확인? rightsExecution = null, I보호지원보존연결? retention = null)
{
    public const string AdminPolicy = "서버관리자전용";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] RightsKinds = ["access", "correction", "deletion", "suspend", "withdrawal"];

    public async Task<보호지원CaseResponse> 분쟁접수Async(거래분쟁접수Request request, CancellationToken ct = default)
    {
        var actor = Actor();
        RequireCreate(request.ClientRequestId, request.Summary);
        RequireBounded(request.PrivateEvidence, 8000);
        RequireBounded(request.SourceId, 160, required: true);
        var id = StableId(actor, 보호지원종류Codes.분쟁, request.ClientRequestId);
        var fingerprint = Fingerprint(request);
        if (await store.조회Async(id, ct) is { } existing) return ReplayCreate(existing, actor, fingerprint);
        var context = await source.조회Async(request.SourceKind, request.SourceId, actor, ct)
                      ?? throw Error("SourceNotFound", "본인이 참여한 거래를 찾을 수 없습니다.", 404);
        var record = New(id, actor, 보호지원종류Codes.분쟁, request.ClientRequestId, fingerprint, request.Summary);
        record.SourceKind = request.SourceKind;
        record.SourceId = request.SourceId;
        record.PartyUserIds = context.PartyUserIds;
        var progress = calendar.기한(record.CreatedAtUtc, 3);
        var result = calendar.기한(record.CreatedAtUtc, 10);
        record.ProgressDueAtUtc = progress.DueAtUtc;
        record.ResultDueAtUtc = result.DueAtUtc;
        record.BusinessCalendarVerified = progress.Verified && result.Verified;
        record.BusinessCalendarVersion = progress.Version;
        AddEvidence(record, actor, request.PrivateEvidence);
        return await InsertAsync(record, actor, fingerprint, ct);
    }

    public async Task<보호지원CaseResponse> 권리접수Async(개인정보권리접수Request request, CancellationToken ct = default)
    {
        var actor = Actor();
        RequireCreate(request.ClientRequestId, request.Summary);
        if (!RightsKinds.Contains(request.RequestKind, StringComparer.Ordinal)) throw Error("InvalidRightsKind", "개인정보 요청 종류를 확인해 주세요.");
        var fingerprint = Fingerprint(request);
        var id = StableId(actor, 보호지원종류Codes.권리, request.ClientRequestId);
        if (await store.조회Async(id, ct) is { } existing) return ReplayCreate(existing, actor, fingerprint);
        var record = New(id, actor, 보호지원종류Codes.권리, request.ClientRequestId, fingerprint, request.Summary);
        record.RequestKind = request.RequestKind;
        record.ResultDueAtUtc = record.CreatedAtUtc.AddDays(Math.Clamp(options.Value.RightsResponseCalendarDays, 1, 10));
        return await InsertAsync(record, actor, fingerprint, ct);
    }

    public async Task<보호지원CaseResponse> 사고접수Async(개인정보사고접수Request request, CancellationToken ct = default)
    {
        var actor = await AdminAsync();
        RequireCreate(request.ClientRequestId, request.Summary);
        RequireBounded(request.PrivateEvidence, 8000);
        if (request.KnownAtUtc.Kind != DateTimeKind.Utc || request.KnownAtUtc == default || request.KnownAtUtc > Now)
            throw Error("InvalidKnownAt", "인지한 UTC 시각을 확인해 주세요.");
        var fingerprint = Fingerprint(request);
        var id = StableId(actor, 보호지원종류Codes.사고, request.ClientRequestId);
        if (await store.조회Async(id, ct) is { } existing) return ReplayCreate(existing, actor, fingerprint, admin: true);
        var record = New(id, actor, 보호지원종류Codes.사고, request.ClientRequestId, fingerprint, request.Summary);
        record.AssignedAdminUserId = actor;
        record.KnownAtUtc = request.KnownAtUtc;
        record.IncidentDeadlineAtUtc = request.KnownAtUtc.AddHours(72);
        AddEvidence(record, actor, request.PrivateEvidence);
        return await InsertAsync(record, actor, fingerprint, ct, admin: true);
    }

    public async Task<보호지원ListResponse> 목록Async(string? kind, int page = 1, bool admin = false, CancellationToken ct = default)
    {
        var actor = admin ? await AdminAsync() : Actor();
        page = Math.Clamp(page, 1, 10000);
        if (!admin && kind == 보호지원종류Codes.사고) throw Error("CaseNotFound", "요청을 찾을 수 없습니다.", 404);
        var rows = await store.목록Async(admin ? null : actor, kind, (page - 1) * 20, 21, ct);
        var visible = new List<보호지원CaseResponse>();
        foreach (var row in rows.Take(20))
        {
            if (!admin && (row.Kind == 보호지원종류Codes.사고 || !await 현재당사자인가(row, actor, ct))) continue;
            visible.Add(ToResponse(row, actor, admin));
        }
        return new() { Page = page, HasMore = rows.Count > 20, Items = visible };
    }

    public async Task<보호지원CaseResponse> 상세Async(string id, bool admin = false, CancellationToken ct = default)
    {
        var actor = admin ? await AdminAsync() : Actor();
        var row = await LoadAsync(id, actor, admin, ct);
        return ToResponse(row, actor, admin);
    }

    public async Task<보호지원증거Response> 증거조회Async(string id, string reasonCode, bool admin = false, CancellationToken ct = default)
    {
        var actor = admin ? await AdminAsync() : Actor();
        if (reasonCode is not ("case-review" or "own-request")) throw Error("ReadReasonRequired", "증거 열람 사유를 선택해 주세요.");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var row = await LoadAsync(id, actor, admin, ct);
            if (admin && row.AssignedAdminUserId != actor)
                throw Error("EvidenceAccessDenied", "본인의 증거 또는 본인이 담당하는 사건만 열람할 수 있습니다.", 403);
            var evidence = row.Evidence.Where(x => admin || x.ActorUserId == actor).Select(x => new 보호지원증거Item
                { Text = x.Text, RecordedAtUtc = x.RecordedAtUtc }).ToList();
            if (admin || row.OwnerUserId == actor) evidence.Insert(0, new() { Text = row.Summary, RecordedAtUtc = row.CreatedAtUtc });
            if (admin && row.PreparedProgress is { } progress) evidence.Add(new() { Text = progress, RecordedAtUtc = row.UpdatedAtUtc });
            if (admin && row.PreparedResult is { } result) evidence.Add(new() { Text = result, RecordedAtUtc = row.UpdatedAtUtc });
            if (admin) evidence.AddRange(row.IncidentAssessments.Select(x => new 보호지원증거Item
                { Text = JsonSerializer.Serialize(x.Assessment, Json), RecordedAtUtc = x.RecordedAtUtc }));
            var revision = row.Revision++;
            AddHistory(row, "read-evidence:" + reasonCode, actor, admin);
            if (await store.교체Async(row, revision, ct)) return new()
            {
                CaseId = id, Items = evidence,
                AuthorizedRecipientIds = admin ? row.PartyUserIds : []
            };
        }
        throw Error("RevisionConflict", "증거 열람 기록을 저장하지 못했습니다. 다시 조회해 주세요.", 409);
    }

    public async Task<보호지원CaseResponse> 변경Async(string id, 보호지원CommandRequest request, bool admin = false, CancellationToken ct = default)
    {
        var actor = admin ? await AdminAsync() : Actor();
        if (request.ClientRequestId == Guid.Empty || request.ExpectedRevision < 1) throw Error("InvalidCommand", "요청 번호와 현재 판본을 확인해 주세요.");
        RequireBounded(request.Summary, 2000);
        RequireBounded(request.PrivateEvidence, 8000);
        RequireBounded(request.LegalBasis, 1000);
        RequireBounded(request.DeliveryEvidenceRef, 250);
        var fingerprint = Fingerprint(request);
        var row = await LoadAsync(id, actor, admin, ct);
        var receipt = row.Receipts.SingleOrDefault(x => x.RequestId == request.ClientRequestId);
        if (receipt is not null)
        {
            if (receipt.ActorUserId != actor || receipt.Fingerprint != fingerprint) throw Error("IdempotencyConflict", "같은 요청 번호로 다른 결정을 보낼 수 없습니다.", 409);
            return ToResponse(row, actor, admin, replay: true);
        }
        if (row.RetentionIntent is { } pending)
        {
            if (!admin || pending.RequestId != request.ClientRequestId || pending.ActorUserId != actor || pending.Fingerprint != fingerprint)
                throw Error("RetentionPending", "보존 상태를 먼저 확인하고 있습니다. 같은 요청으로 다시 확인해 주세요.", 409);
            return ToResponse(await ReconcileRetentionAsync(row.CaseId, ct), actor, admin);
        }
        if (request.ClientRequestId == row.CreateRequestId) throw Error("IdempotencyConflict", "접수 번호는 변경 요청에 재사용할 수 없습니다.", 409);
        if (row.Revision != request.ExpectedRevision) throw Error("RevisionConflict", "최신 상태를 다시 확인해 주세요.", 409);
        if (row.Receipts.Count >= 500) throw Error("CommandLimitReached", "이 사건의 변경 한도에 도달했습니다.", 409);
        if (!Actions(row, actor, admin).Contains(request.Action, StringComparer.Ordinal)) throw Error("ActionNotAllowed", "현재 상태 또는 담당 권한에서는 이 처리를 할 수 없습니다.", 409);
        if (request.Action is "hold-record" or "release-hold")
        {
            if (retention is null) throw Error("RetentionUnavailable", "보존 작업 연결을 확인할 수 없습니다.", 409);
            RequireBounded(request.LegalBasis, 1000, required: true);
            if (request.Action == "hold-record" && (request.HoldReviewAtUtc is not { Kind: DateTimeKind.Utc } review
                || review <= Now || review > Now.AddDays(90)))
                throw Error("InvalidHoldReview", "90일 이내의 미래 UTC 재검토일을 입력해 주세요.");
            // 외부 원장에 쓰기 전에 사건 자체에 의도를 먼저 결속합니다. 불명확한 동안 사건 파기를 차단합니다.
            row.RetentionIntent = new(request.ClientRequestId, fingerprint, actor, request.Action, request.LegalBasis!,
                request.Action == "hold-record" ? request.HoldReviewAtUtc : null, Now, row.Revision + 1);
            row.UpdatedAtUtc = Now;
            var intentRevision = row.Revision++;
            if (!await store.교체Async(row, intentRevision, ct)) throw Error("RevisionConflict", "보존 요청 판본이 먼저 변경되었습니다.", 409);
            return ToResponse(await ReconcileRetentionAsync(row.CaseId, ct), actor, admin);
        }
        await ApplyAsync(row, request, actor, admin, ct);
        AddEvidence(row, actor, request.PrivateEvidence);
        if (admin && !string.IsNullOrWhiteSpace(request.Summary)) AddEvidence(row, actor, "[" + request.Action + "] " + request.Summary);
        if (admin && !string.IsNullOrWhiteSpace(request.LegalBasis)) AddEvidence(row, actor, "[" + request.Action + ":legal-basis] " + request.LegalBasis);
        AddHistory(row, request.Action, actor, admin);
        row.Receipts.Add(new(request.ClientRequestId, actor, fingerprint));
        row.UpdatedAtUtc = Now;
        var expected = row.Revision++;
        if (!await store.교체Async(row, expected, ct)) throw Error("RevisionConflict", "상태가 먼저 변경되었습니다. 같은 요청 번호로 다시 확인해 주세요.", 409);
        return ToResponse(row, actor, admin);
    }

    private async Task ApplyAsync(보호지원Record row, 보호지원CommandRequest request, string actor, bool admin, CancellationToken ct)
    {
        switch (request.Action)
        {
            case "assign-self": row.AssignedAdminUserId = actor; break;
            case "start-review": row.StatusCode = "reviewing"; break;
            case "add-evidence":
                if (string.IsNullOrWhiteSpace(request.PrivateEvidence)) throw Error("EvidenceRequired", "추가할 비공개 내용을 입력해 주세요.");
                break;
            case "appeal":
                if (string.IsNullOrWhiteSpace(request.PrivateEvidence)) throw Error("EvidenceRequired", "이의제기 사유를 입력해 주세요.");
                row.StatusCode = "reopened";
                row.ReviewCycle++;
                row.PreparedProgress = null; row.PreparedResult = null;
                row.RightsExecutionConfirmedAtUtc = null; row.RightsExecutionEvidenceRef = null;
                row.ClosedAtUtc = null; row.PurgeAfterAtUtc = null;
                // 원 접수 기한과 과거 통지를 보존합니다. 재개일을 법정 접수일로 덮지 않습니다.
                break;
            case "prepare-progress":
                RequireBounded(request.Summary, 2000, required: true);
                row.PreparedProgress = request.Summary; break;
            case "prepare-result":
                RequireBounded(request.Summary, 2000, required: true);
                row.PreparedResult = request.Summary; row.StatusCode = "result-prepared"; break;
            case "record-progress-notified": RecordNotice(row, request, "progress"); break;
            case "record-result-notified": RecordNotice(row, request, "result"); break;
            case "deny":
                RequireBounded(request.LegalBasis, 1000, required: true);
                RequireBounded(request.Summary, 2000, required: true);
                if (request.LegalReasonCode is not ("statutory-retention" or "ongoing-dispute" or "rights-exemption"))
                    throw Error("LegalReasonRequired", "거절의 법적 사유 종류를 선택해 주세요.");
                if (request.LegalReasonCode is "statutory-retention" or "ongoing-dispute"
                    && (request.RetainUntilUtc is not { Kind: DateTimeKind.Utc } until || until <= Now))
                    throw Error("RetentionEndRequired", "보존 종료 또는 재검토할 미래 UTC 시각을 기록해 주세요.");
                row.LegalReasonCode = request.LegalReasonCode; row.RetainUntilUtc = request.RetainUntilUtc;
                row.LegalBasis = request.LegalBasis; row.PreparedResult = request.Summary; row.StatusCode = "denial-prepared";
                break;
            case "complete-rights":
                if (rightsExecution is null || string.IsNullOrWhiteSpace(request.DeliveryEvidenceRef)
                    || !await rightsExecution.완료확인Async(row.CaseId, row.OwnerUserId, row.RequestKind, request.DeliveryEvidenceRef, ct))
                    throw Error("ExecutionEvidenceUnavailable", "실제 개인정보 처리 결과가 확인되지 않아 완료할 수 없습니다.", 409);
                row.RightsExecutionEvidenceRef = request.DeliveryEvidenceRef;
                row.RightsExecutionConfirmedAtUtc = Now;
                row.StatusCode = "execution-completed"; break;
            case "close":
                if (!AllNotified(row, "result")) throw Error("ResultNoticePending", "당사자별 결과 안내 증빙을 먼저 기록해 주세요.", 409);
                if (row.Kind == 보호지원종류Codes.권리 && !row.RightsExecutionConfirmedAtUtc.HasValue && row.StatusCode != "denial-prepared")
                    throw Error("ExecutionEvidenceUnavailable", "권리 처리 실행 또는 법적 거절 근거가 필요합니다.", 409);
                row.StatusCode = row.StatusCode == "denial-prepared" ? "denied" : "closed";
                row.ClosedAtUtc = Now;
                if (row.Kind == 보호지원종류Codes.분쟁)
                {
                    row.PurgeAfterAtUtc = row.ClosedAtUtc.Value.AddYears(3);
                    row.RetentionPolicyVersion = 보호지원파기Service.DisputeRetentionPolicyVersion;
                }
                break;
            case "assess-incident":
                var assessment = request.IncidentAssessment ?? throw Error("IncidentAssessmentRequired", "사고 판단 내용이 필요합니다.");
                if (assessment.OccurrenceCode is not ("unknown" or "possible" or "actual" or "false-alarm") || assessment.AffectedPersonCount < 0)
                    throw Error("InvalidIncidentAssessment", "유출 상태와 영향 인원을 확인해 주세요.");
                RequireBounded(assessment.LegalBasis, 1000, required: true);
                // 실제 유출은 정보주체 통지 검토를 빠뜨릴 수 없습니다. 신고 요건 후보는 거절 사유를 반드시 남깁니다.
                if (assessment.OccurrenceCode == "actual" && assessment.IndividualNoticeRequired != true)
                    throw Error("IndividualNoticeRequired", "실제 유출은 정보주체 통지 필요 상태로 기록해야 합니다.");
                if (assessment.OccurrenceCode == "actual" && (assessment.AffectedPersonCount >= 1000
                    || assessment.SensitiveOrUniqueIdentifierAffected || assessment.ExternalIllegalAccess)
                    && assessment.AuthorityReportRequired != true)
                    throw Error("AuthorityReportCriteriaMet", "기관 신고 요건에 해당하는 실제 유출은 신고 필요 상태로 기록해야 합니다.");
                if (assessment.OccurrenceCode is "actual" or "possible" && assessment.AffectedPersonCount is null
                    && assessment.AuthorityReportRequired == false)
                    throw Error("IncidentImpactUnknown", "영향 범위가 미확인인 동안 기관 신고 불필요로 확정할 수 없습니다.");
                if (assessment.OccurrenceCode == "false-alarm" && (assessment.IndividualNoticeRequired == true || assessment.AuthorityReportRequired == true))
                    throw Error("FalseAlarmCorrectionRequired", "오탐 정정과 남은 통지 필요 여부를 별도 판단해 주세요.");
                row.IncidentAssessment = Clone(assessment); row.LegalBasis = assessment.LegalBasis;
                row.IncidentAssessments.Add(new(actor, Clone(assessment), Now));
                row.StatusCode = assessment.OccurrenceCode == "false-alarm" ? "false-alarm" : "reviewing"; break;
            case "prepare-incident-notice":
                RequireBounded(request.Summary, 2000, required: true);
                row.PreparedProgress = request.Summary; break;
            case "record-individual-notified": RecordNotice(row, request, "incident-individual"); break;
            case "record-authority-reported": RecordNotice(row, request, "incident-authority"); break;
            case "record-followup":
                RequireBounded(request.PrivateEvidence, 8000, required: true); break;
            default: throw Error("ActionNotAllowed", "지원하지 않는 처리입니다.", 409);
        }
    }

    private async Task<보호지원Record> ReconcileRetentionAsync(string id, CancellationToken ct)
    {
        if (retention is null) throw Error("RetentionUnavailable", "보존 작업 연결을 확인할 수 없습니다.", 409);
        try { return await new 보호지원보존Service(store, retention, clock).조정Async(id, ct); }
        catch (InvalidOperationException) { throw Error("RetentionPending", "보존 결과가 미확인입니다. 같은 요청으로 다시 확인해 주세요.", 409); }
        catch (KeyNotFoundException) { throw Error("RetentionSourceNotFound", "보존 대상 원장을 확인하지 못했습니다. 보존 요청은 검토 대기 중입니다.", 409); }
    }

    private void RecordNotice(보호지원Record row, 보호지원CommandRequest request, string kind)
    {
        RequireBounded(request.DeliveryEvidenceRef, 250, required: true);
        if (request.OccurredAtUtc is not { Kind: DateTimeKind.Utc } occurred || occurred < (row.KnownAtUtc ?? row.CreatedAtUtc) || occurred > Now)
            throw Error("InvalidNoticeTime", "실제 전달 시각과 UTC 형식을 확인해 주세요.");
        var incident = row.Kind == 보호지원종류Codes.사고;
        // 수동 통지 증빙은 한 발송 기록입니다. 전체 영향 대상의 전달 완료 범위로 확대하지 않습니다.
        var recipient = incident ? kind == "incident-authority" ? "authority" : "individual-notice-record" : request.RecipientUserId;
        if (!incident && (recipient is null || !row.PartyUserIds.Contains(recipient, StringComparer.Ordinal)))
            throw Error("InvalidRecipient", "이 사건의 당사자에게 전달한 근거가 필요합니다.");
        if (kind == "progress" && string.IsNullOrWhiteSpace(row.PreparedProgress)
            || kind == "result" && string.IsNullOrWhiteSpace(row.PreparedResult)
            || incident && string.IsNullOrWhiteSpace(row.PreparedProgress))
            throw Error("NoticeNotPrepared", "안내 내용을 먼저 준비해 주세요.", 409);
        row.Notifications.Add(new(kind, recipient!, incident ? recipient! : recipient == row.OwnerUserId ? "requester" : "counterparty",
            request.DeliveryEvidenceRef!, occurred, Now, row.ReviewCycle, Fingerprint(NoticeContent(row, kind)),
            incident ? Fingerprint(row.IncidentAssessment) : ""));
    }

    private string[] Actions(보호지원Record row, string actor, bool admin)
    {
        if (row.PurgeStateCode == "claimed" || row.RetentionIntent is not null) return [];
        if (admin)
        {
            if (row.AssignedAdminUserId != actor) return row.AssignedAdminUserId is null ? ["assign-self"] : [];
            if (row.Kind == 보호지원종류Codes.사고)
                return ["assess-incident", "prepare-incident-notice", "record-individual-notified", "record-authority-reported", "record-followup", "add-evidence"];
            if (row.StatusCode is "closed" or "denied") return row.Kind == 보호지원종류Codes.분쟁 && retention is not null
                && row.SourceKind is 보호지원출처Codes.음식주문 or 보호지원출처Codes.생활배송
                ? row.SourceHoldActive ? ["hold-record", "release-hold"] : ["hold-record"] : [];
            var result = new List<string> { "start-review", "add-evidence", "prepare-progress", "record-progress-notified", "prepare-result", "record-result-notified", "close" };
            if (row.Kind == 보호지원종류Codes.권리) { result.Add("deny"); if (rightsExecution is not null) result.Add("complete-rights"); }
            if (row.Kind == 보호지원종류Codes.분쟁 && retention is not null
                && row.SourceKind is 보호지원출처Codes.음식주문 or 보호지원출처Codes.생활배송)
            {
                result.Add("hold-record");
                if (row.SourceHoldActive) result.Add("release-hold");
            }
            return result.ToArray();
        }
        if (row.Kind == 보호지원종류Codes.사고) return [];
        return row.StatusCode is "closed" or "denied" ? ["appeal"] : ["add-evidence"];
    }

    private 보호지원CaseResponse ToResponse(보호지원Record row, string actor, bool admin, bool replay = false)
    {
        // 자유 입력과 당사자 ID는 공개 응답에 투영하지 않습니다. 원문은 사유를 기록한 별도 증거 조회만 허용합니다.
        return new()
        {
            CaseId = row.CaseId, Kind = row.Kind, RequestKind = row.RequestKind, StatusCode = row.StatusCode, RetentionPending = row.RetentionIntent is not null,
            SourceKind = row.SourceKind, SourceId = row.SourceId, Summary = row.Kind == 보호지원종류Codes.분쟁 ? "거래 문제 접수" : row.Kind == 보호지원종류Codes.권리 ? "개인정보 요청 접수" : "개인정보 사고 검토",
            PreparedProgress = row.PreparedProgress is null ? null : "진행 안내 준비 완료",
            PreparedResult = row.PreparedResult is null ? null : "처리방안 안내 준비 완료",
            LegalBasis = row.LegalBasis is null ? null : "법적 근거 기록됨",
            LegalReasonCode = row.LegalReasonCode, RetainUntilUtc = row.RetainUntilUtc,
            Revision = row.Revision, CreatedAtUtc = row.CreatedAtUtc, UpdatedAtUtc = row.UpdatedAtUtc, ServerNowUtc = Now,
            ProgressDueAtUtc = row.ProgressDueAtUtc, ResultDueAtUtc = row.ResultDueAtUtc,
            ProgressNotifiedAtUtc = NoticeCompletedAt(row, "progress"), ResultNotifiedAtUtc = NoticeCompletedAt(row, "result"),
            BusinessCalendarVerified = row.BusinessCalendarVerified, BusinessCalendarVersion = row.BusinessCalendarVersion,
            ProgressOverdue = row.ProgressDueAtUtc < Now && !AllNotified(row, "progress"),
            ResultOverdue = row.ResultDueAtUtc < Now && !AllNotified(row, "result"),
            KnownAtUtc = row.KnownAtUtc, IncidentDeadlineAtUtc = row.IncidentDeadlineAtUtc,
            IncidentAssessment = admin && row.IncidentAssessment is { } a ? new()
            {
                OccurrenceCode = a.OccurrenceCode, AffectedPersonCount = a.AffectedPersonCount,
                SensitiveOrUniqueIdentifierAffected = a.SensitiveOrUniqueIdentifierAffected, ExternalIllegalAccess = a.ExternalIllegalAccess,
                IndividualNoticeRequired = a.IndividualNoticeRequired, AuthorityReportRequired = a.AuthorityReportRequired,
                LegalBasis = "판단 근거 비공개 증거에 기록됨"
            } : null,
            IncidentOverdue = row.IncidentDeadlineAtUtc < Now && IncidentPending(row),
            IncidentNoticeCoverageVerified = false, // 전체 대상 명부·실제 전달 결과의 결속 adapter는 아직 없습니다.
            AllowedActions = Actions(row, actor, admin),
            History = row.History.Select(x => new 보호지원HistoryResponse { Action = x.Action, ActorRoleCode = x.ActorRoleCode, RecordedAtUtc = x.RecordedAtUtc }).ToArray(),
            Notifications = row.Notifications.Select(x => new 보호지원통지Response { Kind = x.Kind, RecipientRoleCode = x.RecipientRoleCode, RecordedDeliveredAtUtc = x.DeliveredAtUtc }).ToArray(),
            Replay = replay
        };
    }

    private static bool IncidentPending(보호지원Record row) => row.IncidentAssessment is null
        || row.IncidentAssessment.OccurrenceCode == "unknown"
        || row.IncidentAssessment.IndividualNoticeRequired is null || row.IncidentAssessment.AuthorityReportRequired is null
        || row.IncidentAssessment.IndividualNoticeRequired == true // 일부 수동 통지 기록만으로 전체 범위를 완료 처리하지 않습니다.
        || row.IncidentAssessment.AuthorityReportRequired == true && !row.Notifications.Any(x => x.Kind == "incident-authority" && CurrentNotice(row, x));
    private static bool AllNotified(보호지원Record row, string kind) => row.PartyUserIds.Length > 0
        && row.PartyUserIds.All(id => row.Notifications.Any(x => x.Kind == kind && x.RecipientUserId == id && CurrentNotice(row, x)));
    private static DateTime? NoticeCompletedAt(보호지원Record row, string kind) => AllNotified(row, kind)
        ? row.PartyUserIds.Select(id => row.Notifications.Where(x => x.Kind == kind && x.RecipientUserId == id && CurrentNotice(row, x)).Min(x => x.DeliveredAtUtc)).Max() : null;
    private static string? NoticeContent(보호지원Record row, string kind) => kind == "result" ? row.PreparedResult : row.PreparedProgress;
    private static bool CurrentNotice(보호지원Record row, 보호지원Notification notice) => notice.ReviewCycle == row.ReviewCycle
        && NoticeContent(row, notice.Kind) is not null && notice.ContentFingerprint == Fingerprint(NoticeContent(row, notice.Kind))
        && (row.Kind != 보호지원종류Codes.사고 || notice.AssessmentFingerprint == Fingerprint(row.IncidentAssessment));
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private string Actor()
    {
        var user = http.HttpContext?.User;
        var id = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id)) throw Error("AuthenticationRequired", "로그인이 필요합니다.", 401);
        return id;
    }
    private async Task<string> AdminAsync()
    {
        var actor = Actor();
        if (!(await authorization.AuthorizeAsync(http.HttpContext!.User, null, AdminPolicy)).Succeeded)
            throw Error("AdminRequired", "운영자 권한이 필요합니다.", 403);
        return actor;
    }
    private async Task<보호지원Record> LoadAsync(string id, string actor, bool admin, CancellationToken ct)
    {
        var row = await store.조회Async(id, ct);
        if (row is null || !admin && (row.Kind == 보호지원종류Codes.사고 || !await 현재당사자인가(row, actor, ct)))
            throw Error("CaseNotFound", "본인의 요청을 찾을 수 없습니다.", 404);
        if (row.PurgeStateCode == "claimed") throw Error("CaseRetentionEnded", "보존 기간이 끝나 파기를 처리하고 있습니다.", 410);
        return row;
    }
    private async Task<bool> 현재당사자인가(보호지원Record row, string actor, CancellationToken ct)
        => row.PartyUserIds.Contains(actor, StringComparer.Ordinal) && (row.OwnerUserId == actor
            || row.Kind == 보호지원종류Codes.분쟁 && await source.조회Async(row.SourceKind, row.SourceId, actor, ct) is not null);
    private 보호지원Record New(string id, string actor, string kind, Guid requestId, string fingerprint, string summary)
    {
        var row = new 보호지원Record { CaseId = id, Kind = kind, OwnerUserId = actor, PartyUserIds = [actor], Summary = summary,
            CreateRequestId = requestId, CreateFingerprint = fingerprint, CreatedAtUtc = Now, UpdatedAtUtc = Now };
        AddHistory(row, "create", actor, kind == 보호지원종류Codes.사고);
        return row;
    }
    private async Task<보호지원CaseResponse> InsertAsync(보호지원Record row, string actor, string fingerprint, CancellationToken ct, bool admin = false)
    {
        if (await store.생성Async(row, ct)) return ToResponse(row, actor, admin);
        var prior = await store.조회Async(row.CaseId, ct) ?? throw Error("StorageUnavailable", "접수 결과를 확인하지 못했습니다.", 503);
        return ReplayCreate(prior, actor, fingerprint, admin);
    }
    private 보호지원CaseResponse ReplayCreate(보호지원Record row, string actor, string fingerprint, bool admin = false)
    {
        if (row.OwnerUserId != actor || row.CreateFingerprint != fingerprint) throw Error("IdempotencyConflict", "같은 요청 번호로 접수 내용을 변경할 수 없습니다.", 409);
        return ToResponse(row, actor, admin, replay: true);
    }
    private void AddEvidence(보호지원Record row, string actor, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) row.Evidence.Add(new(actor, text.Trim(), Now));
    }
    private void AddHistory(보호지원Record row, string action, string actor, bool admin)
        => row.History.Add(new(action, admin ? "assigned-admin" : actor == row.OwnerUserId ? "requester" : "counterparty", actor, Now));
    private static void RequireCreate(Guid id, string summary)
    {
        if (id == Guid.Empty) throw Error("RequestIdRequired", "접수 요청 번호가 필요합니다.");
        RequireBounded(summary, 2000, true);
    }
    private static void RequireBounded(string? value, int max, bool required = false)
    {
        if (value?.Length > max || required && string.IsNullOrWhiteSpace(value)) throw Error("InvalidInput", "입력 내용과 길이를 확인해 주세요.");
    }
    private static string StableId(string actor, string kind, Guid id) => "support:" + Fingerprint(new { actor, kind, id });
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json)))).ToLowerInvariant();
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    private static 보호지원Exception Error(string code, string message, int status = 400) => new(code, message, status);
}
