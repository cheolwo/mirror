using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Services.Commerce;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Operations;

/// <summary>실제 확인한 비공개 근거의 참조만 운영 설정에 기록합니다. 문서 원본·인증정보를 넣지 않습니다.</summary>
public sealed class 화물플랫폼등록준비Options
{
    public const string SectionName = "FreightPlatformReadiness";
    public bool FreeDirectConnectionMode { get; set; } = true;
    public Dictionary<string, 화물플랫폼준비근거Options> Requirements { get; set; } = new(StringComparer.Ordinal);
    public 화물플랫폼준비근거Options PlatformRegistration { get; set; } = new();
    public 화물플랫폼준비근거Options BrokeragePermit { get; set; } = new();
    public 화물플랫폼준비근거Options FeeFiling { get; set; } = new();
    public 화물플랫폼준비근거Options TermsFiling { get; set; } = new();
}

public sealed class 화물플랫폼준비근거Options
{
    public string StatusCode { get; set; } = FreightPlatformReadinessStatusCodes.Unknown;
    public string EvidenceReference { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public DateTimeOffset? RecheckAtUtc { get; set; }
    public string AuthorityReceiptReference { get; set; } = string.Empty;
}

public interface I화물플랫폼등록준비Service
{
    FreightPlatformReadinessDto 조회();
}

/// <summary>입력된 근거를 보수적으로 평가합니다. 기관 전송·허가 발급·실제 영업 활성화 기능은 없습니다.</summary>
public sealed class 화물플랫폼등록준비Service(
    IOptions<화물플랫폼등록준비Options> options,
    IOptions<기사이용료정책Options> usageOptions,
    IOptions<통신판매운영Options> commerceOptions,
    TimeProvider? clock = null) : I화물플랫폼등록준비Service
{
    private static readonly (string Code, string Title, string NextAction)[] Dossier =
    [
        ("subordinate-rules-review", "확정 등록 세부기준 대조", "시행령·시행규칙의 확정 기준과 실제 신청 요건을 대조해 주세요."),
        ("business-plan", "사업계획·이용요금·부가서비스", "무료 0원 요금과 실제 부가서비스를 포함한 사업계획 검토 근거를 기록해 주세요."),
        ("office", "사무실 등 등록 시설", "확정 기준에 따른 실제 사무실·시설 자료와 검토 근거를 확인해 주세요."),
        ("disqualification-review", "사업자 결격사유 확인", "신청 주체와 적용 대상자의 결격사유를 적법한 범위에서 확인해 주세요."),
        ("information-security", "화물정보 처리·보안 체계", "실제 접근권한·변경 이력·보안·장애 복구 검증 근거를 확인해 주세요."),
        ("direct-contract-payment", "무료 연결·직접계약·직접지급", "실제 운송계약 당사자와 수납·정산 경로를 확인해 주세요. 무료 자체가 허가 면제 판정은 아닙니다."),
        ("fee-filing-dossier", "이용요금 신고 준비", "0원 요금을 포함한 신고 서류·절차와 제출 근거를 준비해 주세요."),
        ("terms-filing-dossier", "이용약관 신고 준비", "실제 계약·취소·분쟁 기준과 적용 약관의 신고 준비 근거를 확인해 주세요."),
        ("violation-prior-notice", "금지행위 사전 고지", "무허가 운송·무자격 운전·과적·불법 재위탁의 적용 기준과 사전 고지를 확인해 주세요."),
        ("violation-review-report", "위반 확인·기관 신고 절차", "의심·확인·미전송·전송 증거·기관 확인을 구분한 담당자와 신고 절차를 검토해 주세요."),
        ("member-restriction-appeal", "위반 회원 제한·이의·복구", "확인된 위반의 이용 제한·신규 배차 차단·이의·복구와 기존 업무 안전 인계를 검증해 주세요."),
        ("cargo-required-information", "화물 필수정보·포장 포함 중량", "종류·포장/깔판 포함 중량·길이/너비/높이·출도착지가 실제 등록과 배차에 보존되는지 검증해 주세요.")
    ];

    public FreightPlatformReadinessDto 조회()
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var value = options.Value;
        var usage = usageOptions.Value;
        var free = value.FreeDirectConnectionMode && usage.무료배차
            && usage.기본이용료 == 0m && usage.월상한이용료 == 0m;
        var items = Dossier.Select(definition => Evaluate(definition.Code, definition.Title,
            definition.NextAction, value.Requirements.GetValueOrDefault(definition.Code), now)).ToList();
        items.Add(new()
        {
            Code = "free-fee-settings", Title = "무료 배차·이용료 0원 설정",
            StatusCode = free ? FreightPlatformReadinessStatusCodes.Prepared : FreightPlatformReadinessStatusCodes.Pending,
            IsSatisfied = free,
            NextAction = free ? "현재 서버 설정은 무료 배차·기본 및 월 상한 0원입니다. 실제 운임·당사자 직접지급은 별도입니다."
                : "이번 무료 연결 모드와 기사 기본이용료·월상한 0원 설정을 일치시켜 주세요."
        });
        var commerce = commerceOptions.Value;
        var commerceReady = !string.IsNullOrWhiteSpace(commerce.TermsApprovalReference)
            && !string.IsNullOrWhiteSpace(commerce.PrivacyApprovalReference)
            && !string.IsNullOrWhiteSpace(commerce.ExternalProcessingReference)
            && !string.IsNullOrWhiteSpace(commerce.DisputeOperationsReference);
        // 통신판매의 기존 검토 참조를 재사용하되 해당 기능의 활성화/적합 판정을 대신하지 않습니다.
        items.Add(new()
        {
            Code = "commerce-privacy-review", Title = "약관·개인정보·분쟁 기존 검토 연결",
            StatusCode = commerceReady ? FreightPlatformReadinessStatusCodes.Prepared : FreightPlatformReadinessStatusCodes.Unknown,
            IsSatisfied = commerceReady, HasEvidenceReference = commerceReady,
            NextAction = "기존 통신판매 운영 검토 참조를 재사용합니다. 개인정보·보존·외부 처리의 실제 적합 검증은 해당 전문 체계에서 확인해 주세요."
        });
        var registration = Evaluate("platform-registration", "플랫폼 실제 기관 등록",
            "관할기관의 실제 등록 확인 근거를 기록해 주세요. 접수·준비만으로 등록 완료가 되지 않습니다.", value.PlatformRegistration, now, authority: true);
        var brokerage = Evaluate("brokerage-permit", "유상 운송주선사업 허가",
            "유상 주선 여부와 실제 허가를 별도로 확인해 주세요. 플랫폼 등록은 주선 허가를 대체하지 않습니다.", value.BrokeragePermit, now, authority: true);
        var feeFiling = Evaluate("fee-filing", "이용요금 실제 신고 확인",
            "신고 준비·접수 주장·전송 근거와 실제 기관 확인을 구분해 기록해 주세요. 이 서버는 외부 신고를 전송하지 않습니다.", value.FeeFiling, now, authority: true);
        var termsFiling = Evaluate("terms-filing", "이용약관 실제 신고 확인",
            "약관 준비·접수 주장·전송 근거와 실제 기관 확인을 구분해 기록해 주세요. 이 서버는 외부 신고를 전송하지 않습니다.", value.TermsFiling, now, authority: true);
        return new()
        {
            EvaluatedAtUtc = now, IsPreparationComplete = items.All(x => x.IsSatisfied),
            ActualRegistrationConfirmed = registration.IsSatisfied,
            RequiredAuthorityProceduresConfirmed = registration.IsSatisfied && feeFiling.IsSatisfied && termsFiling.IsSatisfied,
            PlatformRegistrationStatusCode = registration.StatusCode,
            BrokeragePermitStatusCode = brokerage.StatusCode,
            FreeConnectionConfigured = free, CanActivateOperations = false,
            MissingRequirementCodes = items.Where(x => !x.IsSatisfied).Select(x => x.Code).ToArray(),
            Requirements = [.. items, registration, brokerage, feeFiling, termsFiling]
        };
    }

    private static FreightPlatformReadinessRequirementDto Evaluate(string code, string title,
        string nextAction, 화물플랫폼준비근거Options? evidence, DateTimeOffset now, bool authority = false)
    {
        var status = evidence?.StatusCode switch
        {
            FreightPlatformReadinessStatusCodes.Pending => FreightPlatformReadinessStatusCodes.Pending,
            FreightPlatformReadinessStatusCodes.Prepared => FreightPlatformReadinessStatusCodes.Prepared,
            FreightPlatformReadinessStatusCodes.Submitted => FreightPlatformReadinessStatusCodes.Submitted,
            FreightPlatformReadinessStatusCodes.Confirmed => FreightPlatformReadinessStatusCodes.Confirmed,
            FreightPlatformReadinessStatusCodes.Rejected => FreightPlatformReadinessStatusCodes.Rejected,
            _ => FreightPlatformReadinessStatusCodes.Unknown
        };
        var reviewed = evidence is not null && !string.IsNullOrWhiteSpace(evidence.EvidenceReference)
            && !string.IsNullOrWhiteSpace(evidence.ReviewedBy)
            && evidence.ReviewedAtUtc.HasValue && evidence.ReviewedAtUtc <= now;
        var expired = evidence?.RecheckAtUtc is { } expiry && expiry <= now;
        if (expired) status = FreightPlatformReadinessStatusCodes.Expired;
        var satisfied = reviewed && !expired
            && (authority ? status == FreightPlatformReadinessStatusCodes.Confirmed
                    && !string.IsNullOrWhiteSpace(evidence!.AuthorityReceiptReference)
                : status is FreightPlatformReadinessStatusCodes.Prepared or FreightPlatformReadinessStatusCodes.Confirmed);
        if (!satisfied && status is FreightPlatformReadinessStatusCodes.Prepared or FreightPlatformReadinessStatusCodes.Confirmed)
            status = FreightPlatformReadinessStatusCodes.Pending;
        return new()
        {
            Code = code, Title = title, StatusCode = status, IsSatisfied = satisfied,
            HasEvidenceReference = !string.IsNullOrWhiteSpace(evidence?.EvidenceReference),
            HasAuthorityReceiptReference = !string.IsNullOrWhiteSpace(evidence?.AuthorityReceiptReference),
            ReviewedAtUtc = evidence?.ReviewedAtUtc, RecheckAtUtc = evidence?.RecheckAtUtc,
            NextAction = satisfied ? "확인 근거의 범위·효력과 확정 세부기준 변경을 재확인해 주세요." : nextAction
        };
    }
}
