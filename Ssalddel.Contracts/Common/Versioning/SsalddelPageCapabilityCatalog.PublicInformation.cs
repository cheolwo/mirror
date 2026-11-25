using Ssalddel.Contracts.Common.Content;
using Ssalddel.Contracts.Common.PublicData;

namespace Ssalddel.Contracts.Common.Versioning;

public static partial class SsalddelPageCapabilityCatalog
{
    private static IReadOnlyList<SsalddelPageCapabilityRule> CreatePublicInformationItems()
        =>
        [
            Prefix("public-data", SsalddelPageAppCodes.IntegratedWeb, "/information/public-data", PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, "0.0", "출처와 기준 시각을 함께 표시하는 공개 정보 조회 화면입니다."),
            Exact("food-delivery-replay", SsalddelPageAppCodes.IntegratedWeb, "/information/food-delivery-replay", PageCapabilityStage.Experience,
                PageInteractionBoundary.Simulation, false, SsalddelProductRoadmapCatalog.FoundationVersion, "가상 음식배달 검증 기록의 주문 등록부터 수령 확인까지 재생합니다."),
            Prefix("public-price-comparison", SsalddelPageAppCodes.IntegratedWeb, "/information/agricultural-fisheries-price-comparison", PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, "0.0", "공개 가격 자료의 출처·단위·기준 시각을 비교합니다."),
            Exact("kamis-domestic-price-comparison", SsalddelPageAppCodes.IntegratedWeb, "/information/kamis-domestic-price-comparison", PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, "0.0", "KAMIS 국내 중도매·소매 조사값을 g·kg·대표 개수 기준으로 환산하고 경락가 API 제공 경계를 함께 표시합니다."),
            Exact("usda-us-price-comparison", SsalddelPageAppCodes.IntegratedWeb, "/information/usda-us-price-comparison", PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, "0.0", "USDA NASS 농가 수취가격을 oz·lb·대표 개수 기준으로 환산하고 소매가격과의 자료 경계를 표시합니다."),
            Exact("official-food-ingredients", SsalddelPageAppCodes.IntegratedWeb, "/information/food-ingredients", PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, "0.0", "공식 레시피의 표준 재료, 출처가 확인된 공공가격과 실제 관련 레시피를 조회합니다."),
            Exact("regional-culture-specialties", SsalddelPageAppCodes.IntegratedWeb, RegionalCultureSpecialtyRoutes.Browse, PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, SsalddelProductRoadmapCatalog.FoundationVersion, "미국의 주와 중국의 현재 행정구역·역사문화권을 구분해 문화와 대표 특산물을 탐색합니다."),
            Exact("regional-culture-specialty-detail", SsalddelPageAppCodes.IntegratedWeb, RegionalCultureSpecialtyRoutes.DetailTemplate, PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, SsalddelProductRoadmapCatalog.FoundationVersion, "선택한 지역의 문화 질문, 대표 특산물과 공식 근거 경계를 읽기 전용으로 확인합니다."),
            Exact("regional-product-candidates", SsalddelPageAppCodes.IntegratedWeb, RegionalCultureSpecialtyRoutes.RegionalProducts, PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, SsalddelProductRoadmapCatalog.FoundationVersion, "선택한 지역과 연결된 특산물 후보를 공개 정보 근거와 함께 읽기 전용으로 조회합니다."),
            Exact("regional-produce-price-comparison", SsalddelPageAppCodes.IntegratedWeb, RegionalCultureSpecialtyRoutes.ProducePriceComparison, PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, SsalddelProductRoadmapCatalog.FoundationVersion, "선택한 지역과 농산물의 공개 가격 관측을 출처·단위·기준 시각과 함께 비교합니다."),
            Exact("regional-apple-price-comparison", SsalddelPageAppCodes.IntegratedWeb, RegionalCultureSpecialtyRoutes.ApplePriceComparison, PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, SsalddelProductRoadmapCatalog.FoundationVersion, "기존 사과 가격 비교 주소에서 같은 지역별 공개 가격 비교 화면을 읽기 전용으로 제공합니다."),
            Exact("regional-agricultural-map", SsalddelPageAppCodes.IntegratedWeb, RegionalAgriculturalMapRoutes.RegionalMap, PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, SsalddelProductRoadmapCatalog.FoundationVersion, "검증된 행정구역 기준점에 연결된 한국·미국 농수산물 가격 관측을 국가·관계 레이어별로 읽기 전용 확인합니다."),
            Exact("korea-regional-agricultural-map-legacy", SsalddelPageAppCodes.IntegratedWeb, RegionalAgriculturalMapRoutes.KoreaMap, PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, SsalddelProductRoadmapCatalog.FoundationVersion, "기존 한국 지도 주소에서 같은 공개 지역 지도 셸을 한국 기본값으로 제공합니다.")
        ];
}
