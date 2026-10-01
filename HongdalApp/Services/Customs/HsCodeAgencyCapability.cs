namespace HongdalApp.Services.Customs;

public enum HsCodeBusinessCategory
{
    All = 0,
    Food = 10,
    GeneralCargo = 20,
    Mixed = 30,
    Unknown = 90
}

public sealed record HsCodeAgencyCapabilityResult(
    string HsCode,
    HsCodeBusinessCategory BusinessCategory,
    bool HasPlatformCustomsAgency,
    bool HasPlatformImportAgency,
    int CustomsAgencyCaseCount,
    int ImportAgencyCaseCount,
    string RiskLevel,
    string Summary,
    bool HasContributorConsentedData,
    bool PaidAccessRequired,
    decimal PaidAccessPrice,
    decimal ContributorRewardRate,
    string DisclosurePolicy,
    IReadOnlyList<HsCodeAgencyRiskTag> RiskTags,
    IReadOnlyList<HsCodeAgencyCapabilityCase> RecentCases,
    IReadOnlyList<string> RequiredDocuments);

public sealed record HsCodeAgencyRiskTag(
    string Label,
    string Reason,
    string Severity);

public sealed record HsCodeAgencyCapabilityCase(
    string AgencyType,
    string CountryRoute,
    string CaseStatus,
    DateTime CompletedAt,
    string Note,
    bool ContributorConsented,
    bool IsPaidDetail);

public interface IHsCodeAgencyCapabilityService
{
    Task<HsCodeAgencyCapabilityResult> LookupAsync(
        string hsCode,
        string shipperUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HsCodeAgencyCapabilityResult>> BrowseAsync(
        HsCodeBusinessCategory businessCategory,
        string shipperUserId,
        CancellationToken cancellationToken = default);
}

public sealed class SampleHsCodeAgencyCapabilityService : IHsCodeAgencyCapabilityService
{
    private static readonly IReadOnlyDictionary<string, HsCodeAgencyCapabilityResult> SampleResults =
        new Dictionary<string, HsCodeAgencyCapabilityResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["9401.69"] = new(
                HsCode: "9401.69",
                BusinessCategory: HsCodeBusinessCategory.GeneralCargo,
                HasPlatformCustomsAgency: true,
                HasPlatformImportAgency: true,
                CustomsAgencyCaseCount: 12,
                ImportAgencyCaseCount: 5,
                RiskLevel: "보통",
                Summary: "가구/의자류로 플랫폼 내 통관 대행과 일부 수입 대행 이력이 있습니다. 기본 화면에서는 집계와 준비 서류만 제공하고, 개별 사례는 제공 동의된 건만 유료 열람 대상으로 둡니다.",
                HasContributorConsentedData: true,
                PaidAccessRequired: true,
                PaidAccessPrice: 3900m,
                ContributorRewardRate: 0.7m,
                DisclosurePolicy: "화주가 공개에 동의한 사례만 익명화해 제공합니다. 열람 결제 금액의 일부는 데이터 제공 화주에게 정산될 수 있습니다.",
                RiskTags:
                [
                    new("가구/생활용품", "재질, 구성품, 원산지 정보를 확인해야 합니다.", "normal"),
                    new("관세사 검토 권장", "목재/복합재 여부에 따라 추가 서류가 필요할 수 있습니다.", "normal")
                ],
                RecentCases:
                [
                    new("통관대행", "KR -> US", "완료", DateTime.UtcNow.AddDays(-18), "목재 프레임 의자류 수출 통관 검토 완료", true, true),
                    new("수입대행", "JP -> KR", "완료", DateTime.UtcNow.AddDays(-42), "생활가구 샘플 수입 대행 처리", true, true),
                    new("통관대행", "CN -> KR", "완료", DateTime.UtcNow.AddDays(-60), "동종 품목 처리 이력 있음", false, false)
                ],
                RequiredDocuments:
                [
                    "상업송장",
                    "패킹리스트",
                    "원산지 정보",
                    "재질/구성품 명세"
                ]),
            ["2106.90"] = new(
                HsCode: "2106.90",
                BusinessCategory: HsCodeBusinessCategory.Food,
                HasPlatformCustomsAgency: true,
                HasPlatformImportAgency: false,
                CustomsAgencyCaseCount: 7,
                ImportAgencyCaseCount: 0,
                RiskLevel: "높음",
                Summary: "기타 조제식료품 계열로 통관 검토 이력은 있으나 수입 대행은 검역/식품신고 변수 때문에 사전 상담 권장입니다. 공개 동의된 세부 사례가 제한적입니다.",
                HasContributorConsentedData: true,
                PaidAccessRequired: true,
                PaidAccessPrice: 5900m,
                ContributorRewardRate: 0.7m,
                DisclosurePolicy: "식품 관련 사례는 민감도가 높아 제공 화주가 공개 범위를 허용한 항목만 요약 제공합니다.",
                RiskTags:
                [
                    new("식품 관련", "HS chapter 01-24 계열로 식품 또는 식품 인접 화물입니다.", "high"),
                    new("검역/식품신고 확인", "성분표, 제조공정서, 표시사항, 수입신고 대상 여부 확인이 필요합니다.", "high"),
                    new("조제식품/보충제 검토", "제품 효능 표현과 성분에 따라 추가 검토가 필요할 수 있습니다.", "high"),
                    new("관세사 검토 권장", "식품 관련 통관 변수 때문에 사전 검토가 권장됩니다.", "high")
                ],
                RecentCases:
                [
                    new("통관대행", "KR -> US", "완료", DateTime.UtcNow.AddDays(-11), "성분표 확인 후 수출 통관 검토", true, true),
                    new("통관대행", "CN -> KR", "보류", DateTime.UtcNow.AddDays(-33), "식품검역 서류 미비로 보완 요청", false, false)
                ],
                RequiredDocuments:
                [
                    "성분표",
                    "제조공정서",
                    "식품표시사항",
                    "검역/수입신고 대상 여부 확인자료"
                ]),
            ["8543.70"] = new(
                HsCode: "8543.70",
                BusinessCategory: HsCodeBusinessCategory.GeneralCargo,
                HasPlatformCustomsAgency: true,
                HasPlatformImportAgency: true,
                CustomsAgencyCaseCount: 9,
                ImportAgencyCaseCount: 3,
                RiskLevel: "보통",
                Summary: "기타 전기기기 계열로 플랫폼 처리 이력이 있습니다. 배터리 포함 여부와 인증 대상 여부를 먼저 확인해야 합니다.",
                HasContributorConsentedData: true,
                PaidAccessRequired: true,
                PaidAccessPrice: 4900m,
                ContributorRewardRate: 0.7m,
                DisclosurePolicy: "제품명, 거래처, 금액 등 식별 가능한 정보는 숨기고 공개 동의된 실무 포인트만 제공합니다.",
                RiskTags:
                [
                    new("전기/인증 확인", "전기용품, 전파, 제품안전 인증 대상 여부를 확인해야 합니다.", "normal"),
                    new("배터리 포함 가능", "배터리 포함 여부에 따라 운송 서류와 안전 확인이 달라집니다.", "normal"),
                    new("관세사 검토 권장", "제품 사양서 기준으로 인증/배터리 여부를 먼저 확인해야 합니다.", "normal")
                ],
                RecentCases:
                [
                    new("통관대행", "KR -> US", "완료", DateTime.UtcNow.AddDays(-9), "전자 디바이스 수출 통관 검토", true, true),
                    new("수입대행", "CN -> KR", "완료", DateTime.UtcNow.AddDays(-28), "소형 전자부품 수입 대행", true, true)
                ],
                RequiredDocuments:
                [
                    "제품 사양서",
                    "전기/배터리 포함 여부",
                    "인증 대상 확인자료",
                    "상업송장"
                ])
        };

    public Task<HsCodeAgencyCapabilityResult> LookupAsync(
        string hsCode,
        string shipperUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = Normalize(hsCode);
        if (SampleResults.TryGetValue(normalized, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(new HsCodeAgencyCapabilityResult(
            HsCode: normalized,
            BusinessCategory: HsCodeBusinessCategory.Unknown,
            HasPlatformCustomsAgency: false,
            HasPlatformImportAgency: false,
            CustomsAgencyCaseCount: 0,
            ImportAgencyCaseCount: 0,
            RiskLevel: "검토필요",
            Summary: "플랫폼 내 확인 가능한 통관 대행/수입 대행 이력이 아직 없습니다. 관세사 검토 요청으로 전환하는 흐름이 필요합니다.",
            HasContributorConsentedData: false,
            PaidAccessRequired: false,
            PaidAccessPrice: 0m,
            ContributorRewardRate: 0m,
            DisclosurePolicy: "제공 동의된 사례가 없으므로 개별 화주 데이터는 노출하지 않습니다.",
            RiskTags:
            [
                new("미분류", "플랫폼 내 분류 기준에 아직 매칭되지 않았습니다.", "normal"),
                new("관세사 검토 권장", "상품명과 상세 설명을 기준으로 HS 코드 검토 요청이 필요합니다.", "normal")
            ],
            RecentCases: [],
            RequiredDocuments:
            [
                "상품명",
                "제품 상세 설명",
                "원산지",
                "거래 국가",
                "예상 수량/금액"
            ]));
    }

    public Task<IReadOnlyList<HsCodeAgencyCapabilityResult>> BrowseAsync(
        HsCodeBusinessCategory businessCategory,
        string shipperUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = SampleResults.Values
            .Where(x => businessCategory == HsCodeBusinessCategory.All || x.BusinessCategory == businessCategory)
            .OrderBy(x => x.BusinessCategory)
            .ThenBy(x => x.HsCode)
            .ToList();

        return Task.FromResult<IReadOnlyList<HsCodeAgencyCapabilityResult>>(items);
    }

    private static string Normalize(string hsCode)
    {
        var value = (hsCode ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? "검토필요" : value;
    }
}
