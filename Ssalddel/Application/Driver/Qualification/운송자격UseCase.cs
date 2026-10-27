using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.Options;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Contracts.Driver.Settings;
using 살뜰.Services.Options;

namespace Ssalddel.Application.Driver.Qualification;

[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase("기사·실차 운송 자격 검토", Summary = "수단별 준비 근거를 비공개로 검토하고 현재 기사·선택 실차의 신규 업무 가능 여부를 확인합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Driver)]
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalLogisticsOs, SsalddelCodeLayer.Application,
    "기사·운송사업자 관계·실차·보험의 검토와 유효성을 분리해 신규 업무 전에 재확인한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 5, StepKey = "application.driver-transport-qualification",
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "내부 서류 검토만 지원. 공식기관 조회·법정 등록 적격 확정·신규 권한·운영 활성화 없음. 개인정보는 persistence 암호화.")]
public sealed class 운송자격UseCase(SsalddelContext db, IOptions<운송자격Options> options,
    ISsalddelExecutionModePolicy execution, TimeProvider clock) : I운송자격UseCase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<운송자격평가결과> 평가Async(string driverId, string? vehicleKind, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(driverId)) return Denied("AuthenticationRequired", "기사 인증이 필요합니다.");
        if (execution.IsSimulation && options.Value.AllowSimulationBypass)
            return new(true, "Qualification.SimulationBypass", "내부 Simulation 시험으로 자격 관문을 우회했습니다. 실제 자격 확인이 아닙니다.", "SimulationBypass", IsSimulationBypass: true);
        var row = await db.운송자격.AsNoTracking().SingleOrDefaultAsync(x => x.DriverId == driverId, ct);
        var current = await CurrentVehicleAsync(driverId, ct);
        var participation = await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == driverId, ct);
        var actualKind = CanonicalKind(participation?.VehicleKind);
        if (current is null) return Denied("DriverProfileMissing", "기사 프로필을 먼저 등록해 주세요.");
        if (!current.Value.Active) return Denied("DriverInactive", "기사 프로필의 활동 상태를 먼저 확인해 주세요.", row);
        if (row is null) return Denied("NotSubmitted", "운송 자격 검토 자료를 제출해 주세요.");
        if (actualKind is null || vehicleKind is not null && CanonicalKind(vehicleKind) != actualKind)
            return Denied("VehicleKindUnknown", "현재 업무 설정의 운송수단을 확인해 주세요.", row);
        var details = Read(row);
        if (details is null) return Denied("EvidenceUnreadable", "자격 근거를 확인할 수 없어 재검토가 필요합니다.", row);
        if (row.PolicyVersion != 운송자격Codes.PolicyVersion) return Denied("PolicyChanged", "새 준비 기준에 따른 재검토가 필요합니다.", row);
        if (row.VehicleKind != actualKind || row.VehicleProfileValue != current.Value.Vehicle
            || details.ActualVehicleReference != current.Value.ActualVehicleReference)
            return Denied("VehicleChanged", "선택 차량이 검토 당시와 달라 다시 검토해야 합니다.", row);
        if (row.Status != 운송자격Codes.Valid) return Denied(row.Status, StatusMessage(row.Status), row);
        if (row.ReviewerId is null || row.ReviewerId == driverId || row.ReviewedAtUtc is null)
            return Denied("ReviewMissing", "관리자의 독립적인 서류 확인이 필요합니다.", row);
        if (row.RecheckAtUtc is null || row.RecheckAtUtc <= Now || details.Evidence.Any(x => x.ValidUntilUtc <= Now))
            return Denied("Expired", "유효기간 또는 재확인일이 지나 다시 검토해야 합니다.", row, 운송자격Codes.Expired);
        if (!ReviewedEvidenceValid(row.VehicleKind, details.Evidence, Now))
            return Denied("EvidenceIncomplete", "수단별 검토 근거가 부족합니다.", row);
        return new(true, "Qualification.Valid", "관리자가 검토한 현재 기사·실차의 준비 자료가 유효합니다. 공식기관 조회 결과는 아닙니다.", row.Status, row.Revision);
    }

    public async Task<Result<운송자격상세Dto>> 본인조회Async(string? actorId, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(actorId) ? Failure("AuthenticationRequired", "로그인이 필요합니다.", 401)
            : await ReadDtoAsync(actorId, ct);

    public Task<Result<운송자격상세Dto>> 관리자조회Async(string driverId, CancellationToken ct = default)
        => ReadDtoAsync(driverId, ct);

    public async Task<Result<운송자격상세Dto>> 제출Async(string? actorId, 운송자격제출요청 request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return Failure("AuthenticationRequired", "로그인이 필요합니다.", 401);
        if (request is null || request.ExpectedRevision < 0 || request.PolicyVersion != 운송자격Codes.PolicyVersion
            || request.VehicleKind is not (운송자격Codes.Motorcycle or 운송자격Codes.FreightVehicle)
            || !Reference(request.ActualVehicleReference, "vehicle")
            || string.IsNullOrWhiteSpace(request.VehicleProfileValue) || request.VehicleProfileValue.Length > 150
            || request.VehicleKind == 운송자격Codes.FreightVehicle && !Reference(request.TransportOperatorReference, "operator")
            || !SubmittedEvidenceValid(request.VehicleKind, request.Evidence))
            return Failure("InvalidSubmission", "수단별 준비 근거와 비공개 내부 참조를 확인해 주세요. 원본 번호·사진·주소를 입력하지 마세요.", 400);
        var current = await CurrentVehicleAsync(actorId, ct);
        if (current is null) return Failure("DriverProfileMissing", "기사 프로필을 먼저 등록해 주세요.", 404);
        var participation = await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == actorId, ct);
        if (request.VehicleProfileValue != current.Value.Vehicle || CanonicalKind(participation?.VehicleKind) != request.VehicleKind)
            return Failure("VehicleMismatch", "현재 기사 프로필과 업무 설정의 차량에 맞춰 제출해 주세요.", 409);
        var row = await db.운송자격.SingleOrDefaultAsync(x => x.DriverId == actorId, ct);
        if ((row?.Revision ?? 0) != request.ExpectedRevision) return Conflict();
        // 정지 중 본인의 재제출로 관리자의 이용 제한을 해제할 수 없다.
        if (row?.Status == 운송자격Codes.Suspended) return Failure("Suspended", "정지 사유를 관리자와 확인한 뒤 재제출해 주세요.", 409);
        var now = Now;
        if (row is null) { row = new 운송자격Record { DriverId = actorId }; db.운송자격.Add(row); }
        AppendHistory(row, actorId, "Submitted", now);
        row.Revision++; row.Status = 운송자격Codes.Pending; row.PolicyVersion = request.PolicyVersion;
        row.VehicleKind = request.VehicleKind; row.VehicleProfileValue = request.VehicleProfileValue;
        row.PrivateDetailsJson = JsonSerializer.Serialize(new Details(request.ActualVehicleReference,
            request.TransportOperatorReference, "", request.Evidence.Select(x => x with
            { VerificationLevel = "Submitted", ReviewReason = "" }).ToArray()), Json);
        row.SubmittedAtUtc = now; row.ReviewedAtUtc = null; row.ReviewerId = null; row.RecheckAtUtc = null;
        if (current.Value.Cargo)
        {
            var driver = await db.용달기사.SingleAsync(x => x.기사Id == actorId, ct);
            driver.현재실차참조 = request.ActualVehicleReference;
        }
        else
        {
            var driver = await db.배달기사.SingleAsync(x => x.기사Id == actorId, ct);
            driver.현재실차참조 = request.ActualVehicleReference;
        }
        var save = await SaveAsync(row, ct); if (save is not null) return save;
        return await ReadDtoAsync(actorId, ct);
    }

    public async Task<Result<운송자격상세Dto>> 검토Async(string? reviewerId, string driverId, 운송자격검토요청 request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reviewerId)) return Failure("AuthenticationRequired", "로그인이 필요합니다.", 401);
        if (reviewerId == driverId) return Failure("SelfReviewForbidden", "본인의 자격을 직접 승인하거나 검토할 수 없습니다.", 403);
        if (request is null || request.Decision is not (운송자격Codes.Valid or 운송자격Codes.Rejected or 운송자격Codes.Suspended)
            || !SafeSummary(request.ReviewReason, 10, 600))
            return Failure("ReviewReasonRequired", "검토 결과와 구체적인 판단 근거를 입력해 주세요.", 400);
        var row = await db.운송자격.SingleOrDefaultAsync(x => x.DriverId == driverId, ct);
        if (row is null) return Failure("NotSubmitted", "검토할 제출 자료가 없습니다.", 404);
        if (row.Revision != request.ExpectedRevision) return Conflict();
        var details = Read(row);
        if (details is null) return Failure("EvidenceUnreadable", "제출 근거를 읽을 수 없어 승인할 수 없습니다.", 409);
        if (request.Decision == 운송자격Codes.Valid)
        {
            if (row.Status != 운송자격Codes.Pending) return Failure("SubmissionRequired", "승인은 새 검토 요청에 대해서만 가능합니다. 재제출부터 진행해 주세요.", 409);
            if (request.RecheckAtUtc is null || request.RecheckAtUtc <= Now || request.RecheckAtUtc.Value.Kind != DateTimeKind.Utc
                || request.ActualVehicleReference != details.ActualVehicleReference
                || !ReviewedEvidenceValid(row.VehicleKind, request.Evidence, Now)
                || request.Evidence.Any(x => !details.Evidence.Any(y => y.RequirementCode == x.RequirementCode
                    && y.PrivateEvidenceReference == x.PrivateEvidenceReference)))
                return Failure("ReviewEvidenceRequired", "제출된 각 근거의 서류 확인·기간·실차 일치와 다음 재확인일을 기록해 주세요. 공식기관 조회는 아직 지원하지 않습니다.", 400);
            var current = await CurrentVehicleAsync(driverId, ct);
            var participation = await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == driverId, ct);
            if (current is null || !current.Value.Active || current.Value.Vehicle != row.VehicleProfileValue
                || current.Value.ActualVehicleReference != details.ActualVehicleReference
                || CanonicalKind(participation?.VehicleKind) != row.VehicleKind)
                return Failure("VehicleChanged", "검토 중 선택 차량이 바뀌었습니다. 기사에게 재제출을 요청해 주세요.", 409);
        }
        AppendHistory(row, reviewerId, request.Decision, Now);
        row.Revision++; row.Status = request.Decision; row.ReviewerId = reviewerId; row.ReviewedAtUtc = Now;
        row.RecheckAtUtc = request.Decision == 운송자격Codes.Valid ? request.RecheckAtUtc : null;
        row.PrivateDetailsJson = JsonSerializer.Serialize(details with { ReviewReason = request.ReviewReason,
            Evidence = request.Decision == 운송자격Codes.Valid ? request.Evidence : details.Evidence }, Json);
        var save = await SaveAsync(row, ct); if (save is not null) return save;
        return await ReadDtoAsync(driverId, ct);
    }

    private async Task<Result<운송자격상세Dto>> ReadDtoAsync(string driverId, CancellationToken ct)
    {
        var current = await CurrentVehicleAsync(driverId, ct);
        if (current is null) return Failure("DriverProfileMissing", "기사 프로필을 찾을 수 없습니다.", 404);
        var row = await db.운송자격.AsNoTracking().SingleOrDefaultAsync(x => x.DriverId == driverId, ct);
        var participation = await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == driverId, ct);
        var details = row is null ? null : Read(row);
        var eligibility = await 평가Async(driverId, null, ct);
        return Result.Ok(new 운송자격상세Dto
        {
            DriverId = driverId, Status = eligibility.Status == 운송자격Codes.Expired ? 운송자격Codes.Expired : row?.Status ?? 운송자격Codes.NotSubmitted,
            Revision = row?.Revision ?? 0, PolicyVersion = row?.PolicyVersion ?? 운송자격Codes.PolicyVersion,
            VehicleKind = row?.VehicleKind ?? CanonicalKind(participation?.VehicleKind) ?? "",
            VehicleProfileValue = current.Value.Vehicle,
            ActualVehicleReference = details?.ActualVehicleReference ?? current.Value.ActualVehicleReference ?? "", TransportOperatorReference = details?.TransportOperatorReference ?? "",
            SubmittedAtUtc = row?.SubmittedAtUtc, ReviewedAtUtc = row?.ReviewedAtUtc, ReviewerId = row?.ReviewerId,
            RecheckAtUtc = row?.RecheckAtUtc, ReviewReason = details?.ReviewReason ?? "",
            RequiredEvidenceCodes = 운송자격Codes.RequiredFor(row?.VehicleKind ?? CanonicalKind(participation?.VehicleKind) ?? ""),
            Evidence = details?.Evidence ?? [], OfficialAuthorityVerified = false, Eligibility = eligibility
        });
    }

    private async Task<(string Vehicle, string? ActualVehicleReference, bool Cargo, bool Active)?> CurrentVehicleAsync(string id, CancellationToken ct)
    {
        var cargo = await db.용달기사.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == id, ct);
        if (cargo is not null) return (cargo.차량, cargo.현재실차참조, true, cargo.상태 == "활동중");
        var food = await db.배달기사.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == id, ct);
        return food is null ? null : (food.차량, food.현재실차참조, false, food.상태 == "활동중");
    }

    private static string? CanonicalKind(string? kind) => kind switch
    {
        운송자격Codes.Motorcycle => 운송자격Codes.Motorcycle,
        운송자격Codes.FreightVehicle or DriverVehicleKinds.Truck or DriverVehicleKinds.Van => 운송자격Codes.FreightVehicle,
        _ => null
    };

    private static bool SubmittedEvidenceValid(string kind, IReadOnlyList<운송자격근거Dto>? evidence)
    {
        var required = 운송자격Codes.RequiredFor(kind);
        return evidence is not null && evidence.All(x => x is not null) && evidence.Count == required.Count && evidence.Select(x => x.RequirementCode).Distinct().Count() == evidence.Count
            && required.All(code => evidence.Any(x => x.RequirementCode == code))
            && evidence.All(x => Reference(x.PrivateEvidenceReference, "private-evidence") && SafeSummary(x.Summary, 3, 300)
                && x.VerificationLevel == "Submitted" && string.IsNullOrWhiteSpace(x.ReviewReason));
    }

    private static bool ReviewedEvidenceValid(string kind, IReadOnlyList<운송자격근거Dto>? evidence, DateTime now)
    {
        var required = 운송자격Codes.RequiredFor(kind);
        return required.Count > 0 && evidence is not null && evidence.All(x => x is not null) && evidence.Count == required.Count
            && evidence.Select(x => x.RequirementCode).Distinct().Count() == evidence.Count
            && required.All(code => evidence.Any(x => x.RequirementCode == code))
            && evidence.All(x => Reference(x.PrivateEvidenceReference, "private-evidence") && SafeSummary(x.Summary, 3, 300)
                && x.VerificationLevel == 운송자격Codes.DocumentReview && SafeSummary(x.ReviewReason, 10, 600)
                && (x.ValidFromUtc is null || x.ValidFromUtc.Value.Kind == DateTimeKind.Utc && x.ValidFromUtc <= now)
                && (x.ValidUntilUtc is null ? x.NoStatedExpiry && x.RequirementCode is not (운송자격Codes.CompulsoryInsurance or 운송자격Codes.DeliveryCoverage)
                    : !x.NoStatedExpiry && x.ValidUntilUtc.Value.Kind == DateTimeKind.Utc && x.ValidUntilUtc > now
                        && (x.ValidFromUtc is null || x.ValidUntilUtc > x.ValidFromUtc)));
    }

    private static bool Reference(string? value, string prefix)
        => value is not null && value.StartsWith(prefix + ":", StringComparison.Ordinal)
            && Guid.TryParseExact(value[(prefix.Length + 1)..], "D", out var id) && id != Guid.Empty;

    private static bool SafeSummary(string? value, int min, int max)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= min && value.Length <= max
            && !System.Text.RegularExpressions.Regex.IsMatch(value, @"\d{6}[- ]?\d{7}|\d{2}-\d{2}-\d{6}-\d{2}|01[016789][ -]?\d{3,4}[ -]?\d{4}|\d{2,3}[가-힣]\s?\d{4}|https?://|[A-Za-z]:\\");

    private static Details? Read(운송자격Record row)
    {
        try
        {
            var details = JsonSerializer.Deserialize<Details>(row.PrivateDetailsJson, Json);
            return details is not null && details.Evidence is not null && details.Evidence.All(x => x is not null)
                && Reference(details.ActualVehicleReference, "vehicle")
                && (row.VehicleKind != 운송자격Codes.FreightVehicle || Reference(details.TransportOperatorReference, "operator")) ? details : null;
        }
        catch (JsonException) { return null; }
    }

    private static void AppendHistory(운송자격Record row, string actorId, string action, DateTime now)
    {
        var history = JsonSerializer.Deserialize<List<History>>(row.HistoryJson, Json) ?? [];
        history.Add(new(row.Revision, row.Status, row.PrivateDetailsJson, actorId, action, now,
            row.ReviewerId, row.ReviewedAtUtc, row.RecheckAtUtc, row.PolicyVersion, row.VehicleKind, row.VehicleProfileValue));
        row.HistoryJson = JsonSerializer.Serialize(history, Json);
    }

    private async Task<Result<운송자격상세Dto>?> SaveAsync(운송자격Record row, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return null; }
        catch (DbUpdateConcurrencyException) { db.Entry(row).State = EntityState.Detached; return Conflict(); }
        catch (DbUpdateException ex) when (IsUnique(ex)) { db.Entry(row).State = EntityState.Detached; return Conflict(); }
    }

    private static bool IsUnique(DbUpdateException ex)
    {
        var e = ex.GetBaseException();
        return e.GetType().FullName == "MySqlConnector.MySqlException" && Equals(e.GetType().GetProperty("Number")?.GetValue(e), 1062)
            || e.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException" && e.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(e) is int c && c is 1555 or 2067;
    }
    private static Result<운송자격상세Dto> Conflict() => Failure("RevisionConflict", "다른 화면에서 변경했습니다. 현재 상태를 다시 확인해 주세요.", 409);
    private static Result<운송자격상세Dto> Failure(string code, string text, int status)
        => Result.Fail<운송자격상세Dto>(new Error(text).WithMetadata("Code", "Qualification." + code).WithMetadata("StatusCode", status));
    private static 운송자격평가결과 Denied(string code, string message, 운송자격Record? row = null, string? status = null)
        => new(false, "Qualification." + code, message, status ?? row?.Status ?? 운송자격Codes.NotSubmitted, row?.Revision ?? 0);
    private static string StatusMessage(string status) => status switch
    { 운송자격Codes.Pending => "관리자의 자격 자료 검토를 기다리고 있습니다.", 운송자격Codes.Rejected => "반려 사유를 확인하고 자료를 다시 제출해 주세요.",
        운송자격Codes.Suspended => "자격이 정지되어 신규 업무를 받을 수 없습니다. 기존 업무는 담당자와 안전하게 마무리해 주세요.", _ => "유효한 자격 검토가 필요합니다." };
    private sealed record Details(string ActualVehicleReference, string TransportOperatorReference, string ReviewReason, IReadOnlyList<운송자격근거Dto> Evidence);
    private sealed record History(int Revision, string Status, string PreviousDetailsJson, string ActorId, string Action, DateTime AtUtc,
        string? PreviousReviewerId, DateTime? PreviousReviewedAtUtc, DateTime? PreviousRecheckAtUtc,
        string PreviousPolicyVersion, string PreviousVehicleKind, string PreviousVehicleProfileValue);
}
