using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Commerce;

namespace Ssalddel.Services.Commerce;

public sealed class 통신판매운영Options
{
    public const string SectionName = "CommerceProtection";
    public bool EnableOperationalTransactions { get; set; }
    public 운영주체정보 Operator { get; set; } = new();
    public string TermsApprovalReference { get; set; } = string.Empty;
    public string PrivacyApprovalReference { get; set; } = string.Empty;
    public string DisputeOperationsReference { get; set; } = string.Empty;
    public string RetentionOperationsReference { get; set; } = string.Empty;
    public string ExternalProcessingReference { get; set; } = string.Empty;
    public string RightsExecutionReviewReference { get; set; } = string.Empty;
    public string SupportRetentionReviewReference { get; set; } = string.Empty;
    public string PersonalIdentityDisclosureReviewReference { get; set; } = string.Empty;
    public Dictionary<string, string> RestaurantSellerUserIds { get; set; } = new();
}

public sealed class 통신판매안내Service(IOptions<통신판매운영Options> options,
    IOptions<Ssalddel.Services.PrivacyRetention.개인정보보존Options>? retention = null,
    Ssalddel.Services.PrivacySupport.한국영업일Calendar? calendar = null, TimeProvider? clock = null,
    Ssalddel.Services.PrivacySupport.I보호지원권리실행확인? rightsExecution = null,
    IOptions<Ssalddel.Services.PrivacySupport.보호지원Options>? support = null)
{
    public const string Version = "commerce-privacy-2026-10-06-r25";
    public const string IntermediaryNotice = "미러는 판매자와 이용자의 거래를 연결합니다. 상품 판매·공급의 계약 당사자는 거래 화면의 판매자입니다. 미러는 법령과 이용약관에 따른 정보 확인·제공 및 분쟁 처리 책임을 이행합니다.";

    public 통신판매안내Response 조회()
    {
        var missing = 운영부족();
        return new()
        {
            Version = Version, IntermediaryNotice = IntermediaryNotice, OperatorInfo = options.Value.Operator,
            MissingRequirements = missing, IsOperationalReady = missing.Count == 0,
            Documents =
            [
                new("terms", "이용약관", [
                    new("거래 당사자와 비용", "판매자 유형과 계약 당사자, 물품 가격, 구매자·판매자의 배송비 부담 및 취소 조건을 거래 전에 확인합니다. 플랫폼 이용과 상품·배송의 무상 여부는 서로 다릅니다. 당사자 직접 지급의 지급 주장과 실제 수령 확인을 구분합니다."),
                    new("중개와 책임", IntermediaryNotice),
                    new("거래 자격", "초기 주문·개인 거래·생활 배송 신청은 서버에서 성년 여부가 확인된 만 19세 이상 이용자에게 제공합니다. 판매자 신원 확인이 일반 게시판 회원의 실명 공개를 뜻하지 않습니다."),
                    new("구매안전과 취소", "선입금 거래의 구매안전 요건은 거래 유형에 따라 확인합니다. 이용 가능한 결제대금예치가 있으면 이를 안내하고 이용을 권고합니다. 실제 제공하지 않는 결제·환불·에스크로 서비스를 제공한다고 표시하지 않습니다.")]),
                new("privacy", "개인정보 처리방침", [
                    new("목적과 최소 수집", "계정 관리, 신원 확인, 주문·배송 수행, 분쟁 및 정보주체의 권리 요청 처리에 필요한 항목만 처리합니다. 주민등록번호·신분증 원본을 일반 가입 정보로 수집하지 않습니다. 성년 확인은 검증 결과만 보존합니다."),
                    new("정보 제공", "주소·연락처는 공개 지도에 표시하지 않습니다. 해당 거래 당사자와 현재 배정 기사에게 목적과 수행 단계에 필요한 항목만 제공합니다. 동의에 근거한 제3자 제공은 제공받는 자·목적·항목·기간·거부 영향을 특정하여 별도로 확인합니다."),
                    new("보존과 파기", "목적이 끝난 정보는 파기합니다. 적용되는 거래기록은 표시·광고 6개월, 계약·청약철회 및 결제·공급 5년, 불만·분쟁 3년 동안 법정 범위만 분리 보존합니다. 기사 상세 열람 3일 제한은 DB 파기 기간과 별개입니다."),
                    new("권리 행사", "내 정보 관리에서 열람·정정·삭제·처리정지·탈퇴를 요청할 수 있습니다. 법정 보존 또는 분쟁 때문에 제한되는 경우 해당 항목·사유·근거·종료 또는 재검토 시점을 안내합니다."),
                    new("위탁·국외이전", "위탁자·수탁자·처리지역·항목·목적·기간 및 적법한 이전 근거를 실제 처리경로에 맞게 공개한 뒤 서비스를 연결합니다. 설정되지 않은 외부 저장·지도 제공처는 운영 개인정보 전송을 허용하지 않습니다."),
                    new("운영 준비 상태", "운영주체·담당 연락처·실제 처리항목·수탁자 등 운영 설정이 완료되기 전의 문안은 공개 거래 운영을 승인하는 처리방침이 아닙니다. 운영 준비 부족 항목은 아래 상태에서 확인합니다.")]),
                new("disputes", "분쟁 처리 기준", [
                    new("접수와 안내", "거래 상세의 문제 신고에서 접수번호로 처리 상태를 확인합니다. 접수 확인과 조사 경과 안내는 구분하며, 적용되는 분쟁에는 3영업일 이내 진행 경과와 10영업일 이내 조사 결과 또는 처리방안을 안내합니다."),
                    new("신원정보 제공", "개인 판매자의 신원은 공개하지 않습니다. 적법한 법원·분쟁조정기구 요청을 검토하며, 이 조항에 따라 소비자에게 직접 제공할 때에는 해당 판매자의 동의를 확인합니다. 처리 기록과 제공 범위를 남깁니다."),
                    new("이의와 증거", "담당자의 조사·처리방안과 이의신청을 별도 기록합니다. 첨부 원본과 상대방 개인정보는 일반 공개 응답에서 제외합니다. 파기 유예는 대상·사유·재검토일을 정합니다.")])
            ]
        };
    }

    public IReadOnlyList<string> 운영부족()
    {
        var value = options.Value; var missing = new List<string>();
        if (!value.EnableOperationalTransactions) missing.Add("공개 거래 활성화 검토");
        var owner = value.Operator;
        if (new[] { owner.상호, owner.대표자, owner.주소, owner.전화번호, owner.이메일, owner.사업자등록번호, owner.개인정보담당연락처 }.Any(string.IsNullOrWhiteSpace))
            missing.Add("운영주체·개인정보 담당 연락처");
        if (string.IsNullOrWhiteSpace(value.TermsApprovalReference)) missing.Add("약관·거래 유형 적용 검토 근거");
        if (string.IsNullOrWhiteSpace(value.PrivacyApprovalReference)) missing.Add("실제 처리경로에 맞는 처리방침 검토 근거");
        if (string.IsNullOrWhiteSpace(value.DisputeOperationsReference)) missing.Add("분쟁·권리 요청 담당 및 처리 체계");
        if (string.IsNullOrWhiteSpace(value.RetentionOperationsReference)) missing.Add("보존·파기·백업 복원 절차 검증");
        if (string.IsNullOrWhiteSpace(value.ExternalProcessingReference)) missing.Add("외부 처리·위탁·위치정보 요건 검토");
        if (retention?.Value.Ready != true) missing.Add("실제 보존 작업·백업 복원 차단 검증");
        if (calendar?.기한((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime, 10).Verified != true) missing.Add("분쟁 처리 영업일 달력 검증");
        if (string.IsNullOrWhiteSpace(value.RightsExecutionReviewReference)) missing.Add("권리 요청 실제 처리·완료 증명 절차 검토");
        if (string.IsNullOrWhiteSpace(value.SupportRetentionReviewReference)) missing.Add("분쟁·권리 요청·사고 증거 보존 정책 검토");
        if (string.IsNullOrWhiteSpace(value.PersonalIdentityDisclosureReviewReference)) missing.Add("개인 판매자 신원 제공의 기관 요청·당사자 동의 검토 절차");
        if (rightsExecution is null) missing.Add("개인정보 권리 실행 원장 확인 연결");
        if (support?.Value is not { ClosedDisputePurgeEnabled: true, ClosedDisputeRetentionPolicyConfirmed: true,
                ClosedDisputeRetentionPolicyVersion: "internal-closed-dispute-three-years.r1" }) missing.Add("종료 분쟁 보존·파기 정책 검증");
        return missing;
    }
}
