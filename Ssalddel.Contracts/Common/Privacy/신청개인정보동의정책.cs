namespace Ssalddel.Contracts.Common.Privacy;

public static class 신청개인정보업무Codes
{
    public const string 물류대행 = "logistics-proxy";
    public const string 운송대행 = "transport-proxy";
    public const string 개별주문 = "individual-order";
}

public static class 신청개인정보출처Codes
{
    public const string 커뮤니티지도 = "community-map";
    public const string 생활교류 = "neighborhood-exchange";

    public static bool 지원하는출처인가(string? code)
        => string.Equals(code, 커뮤니티지도, StringComparison.Ordinal)
           || string.Equals(code, 생활교류, StringComparison.Ordinal);
}

public static class 신청개인정보동의상태Codes
{
    public const string 유효 = "Active";
    public const string 철회 = "Withdrawn";
}

public sealed class 신청개인정보동의기록Request
{
    public Guid 증적Id { get; set; }
    public string 업무Code { get; set; } = string.Empty;
    public string 출처Code { get; set; } = string.Empty;
    public string 동의문버전 { get; set; } = string.Empty;
    public bool 수집이용동의 { get; set; }
    public bool 연령요건확인 { get; set; }
}

public sealed class 신청개인정보동의철회Request
{
    public string 철회사유 { get; set; } = string.Empty;
}

public sealed class 신청개인정보동의증적Response
{
    public Guid 증적Id { get; set; }
    public string 업무Code { get; set; } = string.Empty;
    public string 출처Code { get; set; } = string.Empty;
    public string 동의문버전 { get; set; } = string.Empty;
    public string 수집이용목적 { get; set; } = string.Empty;
    public IReadOnlyList<string> 수집항목 { get; set; } = [];
    public string 보유이용기간 { get; set; } = string.Empty;
    public string 동의문Hash { get; set; } = string.Empty;
    public string 상태Code { get; set; } = 신청개인정보동의상태Codes.유효;
    public DateTime 동의일시Utc { get; set; }
    public DateTime? 철회일시Utc { get; set; }
}

public sealed record 신청개인정보동의안내(
    string 업무Code,
    string 업무명,
    string 수집이용목적,
    IReadOnlyList<string> 수집항목,
    string 보유이용기간,
    string 동의거부안내,
    string 제3자제공안내,
    string 국외이전안내);

public sealed record ApplicationPrivacyConsentNotice(
    string WorkCode,
    string WorkName,
    string CollectionUsePurpose,
    IReadOnlyList<string> CollectionItems,
    string RetentionPeriod,
    string RefusalNotice,
    string ThirdPartyDisclosureNotice,
    string CrossBorderTransferNotice);

/// <summary>
/// 지도에서 시작한 신청 화면의 개인정보 동의 안내 초안입니다.
/// 운영 전 실제 처리방침, 수탁자/제공받는 자, 보유·파기 구현과 법률 검토를 완료해야 합니다.
/// </summary>
public static class 신청개인정보동의정책
{
    public const string 현재버전 = "application-privacy-consent-draft-2026-08-04";

    public const string 보유이용기간 =
        "신청 철회 또는 처리 목적 달성 시까지 보유합니다. 관계 법령에 따라 보존할 필요가 있는 기록은 해당 법정 기간 동안 다른 개인정보와 분리 보관한 뒤 파기합니다.";

    public const string 동의거부안내 =
        "동의를 거부할 권리가 있습니다. 다만 신청 처리에 필요한 최소 정보의 수집·이용에 동의하지 않으면 해당 신청서를 작성하거나 제출할 수 없습니다. 지도와 공개정보 조회는 계속 이용할 수 있습니다.";

    public const string 제3자제공안내 =
        "이 단계에서는 창고·운송사·판매자에게 개인정보를 제공하지 않습니다. 실제 상대가 정해지면 제공받는 자, 제공 목적, 제공 항목과 보유 기간을 표시하고 별도 동의를 받아야 합니다.";

    public const string 국외이전안내 =
        "이 단계에서는 개인정보를 국외로 이전하지 않습니다. 해외 사업자 또는 국외 저장·처리 서비스로 이전할 때에는 이전 국가, 이전받는 자, 목적, 항목, 시기·방법과 보유 기간을 별도로 알리고 적법한 근거를 확인해야 합니다.";

    public const string EnglishRetentionPeriod =
        "We retain the information until the request is withdrawn or its processing purpose is fulfilled. Records that must be retained under applicable law will be separated from other personal information and deleted after the required period.";

    public const string EnglishRefusalNotice =
        "You may decline. If you do not agree to the collection and use of the minimum information needed to process the request, you cannot open or submit this request form. You may still browse the map and public information.";

    public const string EnglishThirdPartyDisclosureNotice =
        "At this stage, the workflow is designed not to disclose personal information to a warehouse, carrier, or seller, and not to sell or share it for cross-context behavioral advertising. Before launch, actual hosting, analytics, and service-provider flows must be verified. If a recipient is later selected, the recipient, purpose, categories, and retention period must be disclosed and the applicable legal basis or separate consent must be confirmed.";

    public const string EnglishCrossBorderTransferNotice =
        "This draft does not represent that a U.S. or other cross-border transfer occurs at this stage. Before any overseas storage or processing is enabled, the destination, recipient or processor, purpose, categories, transfer method, retention period, safeguards, and applicable law must be confirmed.";

    public static 신청개인정보동의안내 For(string 업무Code)
        => 업무Code switch
        {
            신청개인정보업무Codes.물류대행 => Build(
                업무Code,
                "물류대행 신청",
                "입고 요청 작성, 본인 신청 확인, 창고 후보 검토와 신청 관련 문의 처리",
                ["계정 식별자", "신청자 이름·연락처", "선택한 창고·공급처", "도착지 주소·예정일", "신청 메모"]),
            신청개인정보업무Codes.운송대행 => Build(
                업무Code,
                "운송대행 신청",
                "운송 의뢰 작성, 본인 신청 확인, 운송 조건 검토와 신청 관련 문의 처리",
                ["계정 식별자", "신청자 이름·연락처", "상차·하차 주소와 담당 연락처", "화물·차량·일정 조건", "신청 메모"]),
            신청개인정보업무Codes.개별주문 => Build(
                업무Code,
                "개별 주문 신청",
                "비구속 주문 의향 작성, 본인 신청 확인, 상품·수량·수령 조건 검토와 신청 관련 문의 처리",
                ["계정 식별자", "신청자·수령인 이름과 연락처", "수령 주소", "상품·수량·수령 조건", "신청 메모"]),
            _ => throw new ArgumentOutOfRangeException(nameof(업무Code), 업무Code, "지원하지 않는 신청 개인정보 업무입니다.")
        };

    public static ApplicationPrivacyConsentNotice ForEnglish(string workCode)
        => workCode switch
        {
            신청개인정보업무Codes.물류대행 => BuildEnglish(
                workCode,
                "Logistics assistance request",
                "Create an inbound request, verify the requester, review warehouse candidates, and respond to request-related inquiries",
                ["Account identifier", "Requester name and contact details", "Selected warehouse or supplier", "Destination address and expected date", "Request notes"]),
            신청개인정보업무Codes.운송대행 => BuildEnglish(
                workCode,
                "Transportation assistance request",
                "Create a transportation request, verify the requester, review transportation conditions, and respond to request-related inquiries",
                ["Account identifier", "Requester name and contact details", "Pickup and delivery addresses and contacts", "Cargo, vehicle, and schedule conditions", "Request notes"]),
            신청개인정보업무Codes.개별주문 => BuildEnglish(
                workCode,
                "Individual order request",
                "Create a non-binding order request, verify the requester, review product, quantity, and delivery conditions, and respond to request-related inquiries",
                ["Account identifier", "Requester and recipient names and contact details", "Delivery address", "Product, quantity, and delivery conditions", "Request notes"]),
            _ => throw new ArgumentOutOfRangeException(nameof(workCode), workCode, "Unsupported application privacy work code.")
        };

    private static 신청개인정보동의안내 Build(
        string 업무Code,
        string 업무명,
        string 목적,
        IReadOnlyList<string> 항목)
        => new(
            업무Code,
            업무명,
            목적,
            항목,
            보유이용기간,
            동의거부안내,
            제3자제공안내,
            국외이전안내);

    private static ApplicationPrivacyConsentNotice BuildEnglish(
        string workCode,
        string workName,
        string purpose,
        IReadOnlyList<string> items)
        => new(
            workCode,
            workName,
            purpose,
            items,
            EnglishRetentionPeriod,
            EnglishRefusalNotice,
            EnglishThirdPartyDisclosureNotice,
            EnglishCrossBorderTransferNotice);
}
