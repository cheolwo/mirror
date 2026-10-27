namespace Ssalddel.Contracts.Common.Versioning;

public sealed record SsalddelProductRoadmapStage(
    string Version,
    string ProductName,
    string DisplayName,
    int SortOrder,
    string PrerequisiteVersion,
    string ExitOutcome,
    bool IsCurrent = false)
{
    public string FullDisplayName => $"{ProductName} {Version} · {DisplayName}";

    public bool IsCultureTransport
        => string.Equals(
            ProductName,
            SsalddelProductRoadmapCatalog.CultureTransportName,
            StringComparison.Ordinal);
}

/// <summary>
/// 통합 앱 1.0 안에서 완성해야 할 업무 분야입니다. LegacyVersion은 기존 API·화면 분류의
/// 호환 값이며 별도 제품 출시 순서나 완료 상태가 아닙니다.
/// </summary>
public sealed record SsalddelUnifiedReleaseWorkstream(
    string WorkstreamKey,
    string DisplayName,
    string LegacyVersion,
    string CompletionCriteria,
    IReadOnlyList<string> AppCodes)
{
    public bool RequiresIndependentCompletionEvidence => true;
}

/// <summary>단일 제품 완성 목표의 정의입니다. 선언 자체는 완료·공개·업무 실행 승인이 아닙니다.</summary>
public sealed record SsalddelUnifiedReleaseTarget(
    string ReleaseKey,
    string Version,
    string DisplayName,
    string BuildSolution,
    IReadOnlyList<SsalddelUnifiedReleaseWorkstream> Workstreams)
{
    public bool IsCompletionEvidence => false;
    public bool GrantsOperationalActivation => false;
}

public static class SsalddelProductRoadmapCatalog
{
    public const string CultureTransportName = "문화교통";
    public const string DefaultProductName = "살뜰";
    public const string FoundationVersion = "0.0";
    public const string IndividualOrderVersion = "0.5";
    public const string LegacyGroupPurchaseVersion = "1.0";
    public const string GroupPurchaseVersion = LegacyGroupPurchaseVersion;
    public const string TradeReadinessVersion = "1.5";
    public const string TransportVersion = "2.0";
    public const string FulfillmentVersion = "2.5";
    public const string FoodDeliveryVersion = "3.0";
    public const string MartVersion = "3.5";
    public const string UnifiedReleaseVersion = "1.0";
    public const string UnifiedReleaseName = "통합 앱 1.0";
    public const string UnifiedReleaseKey = "unified-app-1.0";

    // 이미 배포된 capability·권한 계약의 현재 기본 공개 기준입니다. 단일 완성 목표와 별개이며
    // 목표를 1.0으로 바꿨다는 이유로 기존 기능 플래그나 실행 관문을 승격하지 않습니다.
    public const string CurrentDeliveryVersion = FoundationVersion;
    [Obsolete("단일 목표는 UnifiedReleaseTarget을 사용하세요. 숫자만 Find에 넘기면 기존 공동주문 분류로 해석됩니다.")]
    public const string DeploymentTargetVersion = UnifiedReleaseVersion;

    // 기존 호출부의 호환 별칭입니다. 현재 기본 공개와 완성할 통합 제품을 구분합니다.
    public const string CurrentVersion = CurrentDeliveryVersion;

    /// <summary>기존 0.0~3.5 분야를 모두 단일 1.0의 병행 완성 범위로 묶습니다.</summary>
    public static IReadOnlyList<SsalddelUnifiedReleaseWorkstream> UnifiedReleaseWorkstreams { get; } =
        Array.AsReadOnly<SsalddelUnifiedReleaseWorkstream>(
        [
            new("community-foundation", "커뮤니티·공공데이터", FoundationVersion,
                "공개 탐색·본인 작성·동의·철회·신고와 권한에 맞는 자료 조회를 같은 앱에서 확인합니다.",
                [SsalddelPageAppCodes.IntegratedWeb, SsalddelPageAppCodes.Shipper, SsalddelPageAppCodes.Admin]),
            new("individual-order", "개별주문·개별 원장", IndividualOrderVersion,
                "본인 요청 작성·조회·변경·철회와 다음 전문 업무로의 명시적 인계를 확인합니다.",
                [SsalddelPageAppCodes.Orderer, SsalddelPageAppCodes.Shipper, SsalddelPageAppCodes.IntegratedWeb]),
            new("group-purchase", "공동주문·참여 합의", LegacyGroupPurchaseVersion,
                "모집 조건과 참여·수락·철회·변경을 연결하고 각 당사자의 원장과 합의를 구분해 확인합니다.",
                [SsalddelPageAppCodes.Orderer, SsalddelPageAppCodes.Shipper, SsalddelPageAppCodes.IntegratedWeb]),
            new("supply-readiness", "공급·가격·무역 준비", TradeReadinessVersion,
                "공급자·견적·원가·품목분류 후보와 필요한 확인 근거를 연결하고 준비와 실제 계약·신고를 구분합니다.",
                [SsalddelPageAppCodes.Orderer, SsalddelPageAppCodes.Admin, SsalddelPageAppCodes.IntegratedWeb]),
            new("cargo-transport", "퀵·화물 운송", TransportVersion,
                "의뢰·조건 확인·기사 수락·상하차 증빙·인수·정산 준비를 같은 운송으로 재조회하고 재실행 복구를 확인합니다.",
                [SsalddelPageAppCodes.Shipper, SsalddelPageAppCodes.Driver, SsalddelPageAppCodes.Admin]),
            new("warehouse-sales", "창고·판매", FulfillmentVersion,
                "입고·검수·적재·재고·주문별 피킹·포장·인계를 기존 원장으로 연결하고 수량과 중복 처리를 확인합니다.",
                [SsalddelPageAppCodes.Warehouse, "SellerApp", SsalddelPageAppCodes.HumanResources, SsalddelPageAppCodes.Shipper, SsalddelPageAppCodes.Driver, SsalddelPageAppCodes.Admin]),
            new("food-delivery", "음식점 배달", FoodDeliveryVersion,
                "주문·음식점 수락·유효 기사 배정·조리·픽업·전달·주문자 수령을 각 역할의 현재 상태로 확인합니다.",
                [SsalddelPageAppCodes.Orderer, SsalddelPageAppCodes.RestaurantDesk, SsalddelPageAppCodes.FoodDeliveryDriver, SsalddelPageAppCodes.Admin]),
            new("mart-logistics", "마트·도심 물류", MartVersion,
                "판매 가능한 재고·주문·피킹·포장·배송과 작업자 인계를 연결하고 완료·수납·정산·실지급을 따로 확인합니다.",
                [SsalddelPageAppCodes.Orderer, SsalddelPageAppCodes.Warehouse, SsalddelPageAppCodes.Driver, SsalddelPageAppCodes.Shipper, SsalddelPageAppCodes.Admin])
        ]);

    public static SsalddelUnifiedReleaseTarget UnifiedReleaseTarget { get; } = new(
        UnifiedReleaseKey,
        UnifiedReleaseVersion,
        UnifiedReleaseName,
        "Ssalddel.slnx",
        UnifiedReleaseWorkstreams);

    /// <summary>기존 API·화면·Command 분류의 호환 목록입니다. 단일 제품 목표는 UnifiedReleaseTarget입니다.</summary>
    public static IReadOnlyList<SsalddelProductRoadmapStage> All { get; } =
    [
        new(
            FoundationVersion,
            CultureTransportName,
            "커뮤니티·공공데이터 기반",
            0,
            string.Empty,
            "공개 음식·재료 데이터와 참여 의사를 안전하게 기록할 수 있습니다.",
            IsCurrent: true),
        new(
            IndividualOrderVersion,
            CultureTransportName,
            "개별주문·개별 원장",
            50,
            FoundationVersion,
            "한 사람이 선택한 상품·수량·수령 조건과 철회 가능한 주문 의향을 자신의 개별 원장으로 관리합니다."),
        new(
            GroupPurchaseVersion,
            CultureTransportName,
            "같이 주문·주문자 집단화",
            100,
            IndividualOrderVersion,
            "동의한 개별주문을 품목·지역·수령 조건별로 집단화하고 같이 주문 모집 원장으로 보여 줍니다."),
        new(
            TradeReadinessVersion,
            CultureTransportName,
            "공급·가격·무역 준비",
            150,
            GroupPurchaseVersion,
            "공급자 근거, 견적, 원가, HS·HTS 후보와 수입 준비 체크포인트를 연결합니다."),
        new(
            TransportVersion,
            DefaultProductName,
            "국내 화물·운송 이행",
            200,
            TradeReadinessVersion,
            "확정된 구매·출고 필요를 운송 의뢰, 배차, 증빙과 정산 흐름에 인계합니다."),
        new(
            FulfillmentVersion,
            DefaultProductName,
            "창고·판매 이행",
            250,
            TransportVersion,
            "입고, 재고, 피킹, 포장, 판매채널과 최종 배분을 연결합니다."),
        new(
            FoodDeliveryVersion,
            DefaultProductName,
            "음식점 배달",
            300,
            TransportVersion,
            "음식 주문을 조리, 픽업과 고객 배송으로 연결합니다."),
        new(
            MartVersion,
            DefaultProductName,
            "마트·도심 물류",
            350,
            FulfillmentVersion,
            "도심 재고, 피킹, 포장과 즉시배송을 연결합니다.")
    ];

    /// <summary>기존 분류를 조회합니다. "1.0"은 공동주문이며 통합 릴리스 목표를 반환하지 않습니다.</summary>
    public static SsalddelProductRoadmapStage Find(string? version)
        => All.FirstOrDefault(stage => string.Equals(
               stage.Version,
               version?.Trim(),
               StringComparison.OrdinalIgnoreCase))
           ?? All.Single(stage => stage.IsCurrent);

    public static bool IsCultureTransportVersion(string? version)
        => All.Any(stage =>
            stage.IsCultureTransport
            && string.Equals(
                stage.Version,
                version?.Trim(),
                StringComparison.OrdinalIgnoreCase));
}
