using Hongdal.Contracts.Common.AgriculturalFisheries;
using 홍달.Services.Options;

namespace Hongdal.Services.AgriculturalFisheries.Information;

internal static class AgriculturalFisheriesInformationOverviewFactory
{
    public static AgriculturalFisheriesInformationOverviewResponse Create(PublicDataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dataGoKrConfigured = !string.IsNullOrWhiteSpace(options.DataGoKrServiceKey)
            || !string.IsNullOrWhiteSpace(options.ServiceKey);
        var atConfigured = dataGoKrConfigured || !string.IsNullOrWhiteSpace(options.AtFoodPrices.ServiceKey);
        var customsConfigured = dataGoKrConfigured
            || !string.IsNullOrWhiteSpace(options.CustomsTradeStatistics.ServiceKey);
        var nassConfigured = !string.IsNullOrWhiteSpace(options.UsdaNassQuickStats.ApiKey);
        var australiaCatalog = 호주농수산식품가격Catalog.Build();

        return new AgriculturalFisheriesInformationOverviewResponse
        {
            SupportedMarketCodes = ["KR", "US", "AU"],
            Positioning = "공공데이터를 읽고 비교하고 수입 준비 절차의 확인 기록을 함께 관리하는 정보 기반입니다. 주문·계약·배차를 만들지 않습니다.",
            AllowsReadinessRecordWrites = true,
            AllowsTransactionExecution = false,
            BrokerageBoundaryNote = "현재 단계에서는 화물 주선, 운송계약 체결, 운임 중개, 기사 배정과 수수료 정산을 제공하지 않습니다.",
            ReadinessRecordBoundaryNote = "읽기 전용 공공정보와 별도로 참여자는 육류 수입 준비 상태·증빙 메타데이터·질문·양측 확인을 기록할 수 있지만, 이 기록은 거래 실행이나 정부기관의 공식 결정을 의미하지 않습니다.",
            DataSources =
            [
                Source(
                    "at-daily-wholesale-retail-food-price",
                    "한국농수산식품유통공사(aT)",
                    "일별 도·소매 가격정보",
                    "농축수산물의 국내 중도매·소매 가격",
                    "일별 조사",
                    atConfigured,
                    "https://www.data.go.kr/data/15156057/openapi.do",
                    "가격은 kg 기준으로 정규화하며 품질·등급·포장 차이를 함께 안내합니다."),
                Source(
                    "customs-hs-country-import-statistics",
                    "관세청",
                    "품목별 국가별 수출입실적",
                    "HS 코드·국가·월별 수입금액과 순중량",
                    "월별 통계",
                    customsConfigured,
                    "https://www.data.go.kr/data/15100475/openapi.do",
                    "국내 가격의 비교 맥락으로만 사용하며 실제 매입가나 운송 견적으로 보지 않습니다."),
                Source(
                    미국농수산가격출처Keys.UsdaNassQuickStats,
                    "미국 농무부 농업통계청(USDA NASS)",
                    "Quick Stats 농수산물 가격·판매 통계",
                    "미국 농작물·축산물·양식 수산물의 공식 집계 가격과 판매 통계",
                    "품목·조사 프로그램별 상이",
                    nassConfigured,
                    "https://quickstats.nass.usda.gov/api",
                    "미국 공식 품목명과 조사 단위를 유지하며 국내 aT 가격과 직접 같은 값으로 보지 않습니다."),
                .. australiaCatalog.Sources.Select(AustraliaSource)
            ],
            Capabilities =
            [
                Capability(
                    "SupportedItemCatalog",
                    "지원 품목 찾기",
                    "검토된 HS-aT 연결표에서 농축수산물과 매칭 품질을 검색합니다.",
                    "GET /api/v1/agricultural-fisheries/items"),
                Capability(
                    "DomesticPriceInformation",
                    "국내 가격 정보",
                    "기준일 주변의 aT 중도매·소매 가격과 최신 조사일을 제공합니다.",
                    "GET /api/v1/agricultural-fisheries/items/{hsCode}/domestic-price"),
                Capability(
                    "ImportPriceContext",
                    "수입 통계 비교",
                    "기존 HS 가격 비교 기능에서 관세청 CIF 통계단가와 국내가격을 나란히 봅니다.",
                    "GET /api/v1/customs/hs-codes/{hsCode}/food-price-comparison"),
                Capability(
                    "MeatImportReadinessCollaboration",
                    "육류 수입 준비도 협업",
                    "한국 수입업자와 해외 작업장이 같은 절차도에서 상태, 증빙 메타데이터, 질문·이의와 양측 확인을 관리합니다.",
                    "GET /api/v1/agricultural-fisheries/import-readiness/diagram"),
                Capability(
                    "UnitedStatesPriceInformation",
                    "미국 농수산물 가격 정보",
                    "USDA NASS의 농산물과 양식 수산물 가격·판매 집계 통계를 조회합니다.",
                    "GET /api/v1/agricultural-fisheries/us-prices"),
                Capability(
                    "UnitedStatesOperatorInformationSources",
                    "미국 농어업경영체 정보 원천",
                    "개별 기록의 비공개 경계와 인증·검사·자발적 등재·지역 허가 목적별 공개 명부를 구분해 제공합니다.",
                    "GET /api/v1/agricultural-fisheries/us-operator-information-sources"),
                Capability(
                    "AustraliaFoodPriceIndexes",
                    "호주 식품 가격지수",
                    "ABS의 8개 주도시 가중평균과 도시별 월별 식품·육류·수산물·유제품·과일·채소 소비자 가격지수를 조회합니다.",
                    "GET /api/v1/agricultural-fisheries/au-food-price-indexes"),
                Capability(
                    "AustraliaFoodPriceSourceCatalog",
                    "호주 농수산물 가격 원천 카탈로그",
                    "ABS 자동 조회와 ABARES 농축산·원예·수산물 파일·참고 원천의 수집 경계를 구분합니다.",
                    "GET /api/v1/agricultural-fisheries/au-food-price-indexes/catalog"),
                new AgriculturalFisheriesCapabilityResponse
                {
                    Code = "FreightBrokerage",
                    Label = "화물 주선·중개",
                    Description = "업계 이해와 운영 요건이 충분히 축적된 뒤 별도 모듈로 검토합니다.",
                    AvailableNow = false
                }
            ],
            NextDataPriorities =
            [
                "ABARES 수산·양식 통계 XLSX를 원본 해시·회계연도·어종·단위와 함께 적재하는 연간 수집기 구현",
                "ABARES 주간 농축산·원예 가격의 민간 원자료 이용조건과 안정적인 기계 판독 계약 확인",
                "미국 농어업경영체 공개 원천 중 CSV·API 제공 명부를 개인정보 최소화 규칙과 함께 순차 연동",
                "미국 NOAA 수산물 양륙·생산 자료의 안정적인 공식 제공 방식과 NASS 품목 코드 연결 검증",
                "축산물 등급·도매 유통가격과 aT 가격의 역할 구분",
                "소비자 체감가격·온라인 가격의 조사 기준과 수집 허용 범위 정리",
                "지역·시장·품질·등급·포장단위별 시계열 품질지표 축적"
            ],
            BrokerageReadinessRequirements =
            [
                "데이터 누락률·갱신 지연·품목 매칭 정확도를 기간별로 측정할 것",
                "화주·기사·주선사·시장 운영자 인터뷰로 실제 업무와 책임 경계를 확인할 것",
                "화물자동차 운수사업 관련 등록·허가·약관·보험·정산 요건을 전문가와 검토할 것",
                "분쟁·사고·취소·과적·품질 훼손의 책임과 증빙 절차를 먼저 설계할 것",
                "정보 제공과 주선 거래를 별도 모듈·권한·감사기록으로 분리할 것"
            ]
        };
    }

    private static AgriculturalFisheriesDataSourceResponse Source(
        string key,
        string provider,
        string displayName,
        string coverage,
        string updateCycle,
        bool isConfigured,
        string documentationUrl,
        string usageNote)
        => new()
        {
            Key = key,
            Provider = provider,
            DisplayName = displayName,
            Coverage = coverage,
            UpdateCycle = updateCycle,
            StatusCode = isConfigured ? "Ready" : "NeedsServiceKey",
            StatusLabel = isConfigured ? "조회 준비됨" : "공공데이터 인증키 필요",
            IsConfigured = isConfigured,
            DocumentationUrl = documentationUrl,
            UsageNote = usageNote
        };

    private static AgriculturalFisheriesDataSourceResponse AustraliaSource(
        호주농수산식품가격원천응답 source)
        => new()
        {
            Key = source.Key,
            Provider = source.Provider,
            DisplayName = source.DisplayName,
            Coverage = source.Coverage,
            UpdateCycle = source.UpdateCycle,
            StatusCode = source.IntegrationStatusCode,
            StatusLabel = source.IntegrationStatusCode switch
            {
                "IntegratedApi" => "자동 조회 가능",
                "DownloadAvailable" => "공식 파일 수집 준비",
                _ => "참고 원천 확인됨"
            },
            IsConfigured = source.AutomatedQueryAvailable,
            DocumentationUrl = source.DocumentationUrl,
            UsageNote = source.UsageNote
        };

    private static AgriculturalFisheriesCapabilityResponse Capability(
        string code,
        string label,
        string description,
        string endpoint)
        => new()
        {
            Code = code,
            Label = label,
            Description = description,
            AvailableNow = true,
            Endpoint = endpoint
        };
}
