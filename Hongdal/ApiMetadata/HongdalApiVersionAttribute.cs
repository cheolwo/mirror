using Hongdal.Contracts.Common.Versioning;

namespace Hongdal.ApiMetadata;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HongdalApiVersionAttribute : Attribute
{
    public HongdalApiVersionAttribute(HongdalProductVersion version)
    {
        Version = version;
    }

    public HongdalProductVersion Version { get; }

    public string VersionLabel => HongdalProductVersionLabels.GetLabel(Version);

    public string? FeatureKey { get; set; }

    public string? WorkflowKey { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HongdalApiWorkflowAttribute : Attribute
{
    public HongdalApiWorkflowAttribute(HongdalWorkflow workflow)
    {
        Workflow = workflow;
    }

    public HongdalWorkflow Workflow { get; }

    public string WorkflowLabel => HongdalWorkflowLabels.GetLabel(Workflow);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HongdalApiGrowthTrackAttribute : Attribute
{
    public HongdalApiGrowthTrackAttribute(HongdalApiGrowthTrack track)
    {
        Track = track;
    }

    public HongdalApiGrowthTrack Track { get; }

    public string TrackLabel => HongdalApiGrowthTrackLabels.GetLabel(Track);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class HongdalUseCaseAttribute : Attribute
{
    public HongdalUseCaseAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string Summary { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HongdalUseCaseActorAttribute : Attribute
{
    public HongdalUseCaseActorAttribute(HongdalActor actor, HongdalUseCaseActorRole role = HongdalUseCaseActorRole.Primary)
    {
        Actor = actor;
        Role = role;
    }

    public HongdalActor Actor { get; }

    public HongdalUseCaseActorRole Role { get; }

    public string ActorCode => Actor.ToString();

    public string ActorLabel => HongdalActorLabels.GetLabel(Actor);

    public string RoleLabel => HongdalUseCaseActorRoleLabels.GetLabel(Role);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class HongdalUseCaseRelationAttribute : Attribute
{
    public HongdalUseCaseRelationAttribute(HongdalUseCaseRelationKind kind, string targetUseCaseCode)
    {
        Kind = kind;
        TargetUseCaseCode = targetUseCaseCode;
    }

    public HongdalUseCaseRelationKind Kind { get; }

    public string TargetUseCaseCode { get; }

    public string KindLabel => HongdalUseCaseRelationKindLabels.GetLabel(Kind);

    public string Condition { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}

public enum HongdalProductVersion
{
    V0_0 = 0,
    V1_0 = 100,
    V1_5 = 150,
    V2_0 = 200,
    V2_5 = 250,
    V3_0 = 300,
    V3_5 = 350
}

public enum HongdalApiGrowthTrack
{
    CoreLogistics = 100,
    Community = 200,
    Warehouse = 300,
    Customs = 400,
    OrdererGroupCommerce = 500,
    FoodDelivery = 600,
    HongdalMart = 700,
    PlatformOperations = 800
}

public enum HongdalOperatingSystem
{
    DomesticCargoTransport = 100,
    WarehouseCommerceFulfillment = 200,
    GroupPurchaseImport = 300,
    FoodDelivery = 400,
    HongdalMartUrbanLogistics = 500,
    CommunityTrust = 600,
    PlatformOperations = 700
}

public enum HongdalSchedulingPolicyKind
{
    Fcfs = 100,
    Sjf = 200,
    Priority = 300,
    Edf = 400,
    Mlfq = 500,
    Aging = 600,
    Batching = 700,
    Affinity = 800,
    GeoNearest = 900,
    FitFirst = 1000
}

public enum HongdalWorkflow
{
    DomesticTransport = 100,
    WarehouseFulfillment = 200,
    CustomsAndTradeData = 300,
    GroupPurchaseImport = 400,
    SalesChannelFulfillment = 500,
    CommunityTrust = 600,
    HrParticipation = 700,
    FoodDelivery = 800,
    HongdalMart = 900
}

public enum HongdalWorkflowRelationKind
{
    References = 100,
    Calls = 200,
    HandsOffTo = 300,
    Feeds = 400,
    PublishesSignalTo = 500,
    OperatesWith = 600
}

public enum HongdalActor
{
    Shipper = 100,
    Driver = 200,
    Recipient = 300,
    PlatformOperator = 400,
    WarehouseManager = 500,
    ShipperOrSeller = 600,
    CustomsBroker = 700,
    OrdererGroupLeader = 800,
    Orderer = 900,
    OverseasSellerOrForwarder = 1000,
    Seller = 1100,
    CommunityMember = 1200,
    Worker = 1300,
    EmployerOrOperatingEntity = 1400,
    Restaurant = 1500,
    FoodDeliveryDriver = 1600,
    MartOperator = 1700
}

public enum HongdalUseCaseActorRole
{
    Primary = 100,
    Supporting = 200
}

public enum HongdalUseCaseRelationKind
{
    Include = 100,
    Extend = 200
}

public sealed record HongdalWorkflowRelation(
    HongdalWorkflow Source,
    HongdalWorkflow Target,
    HongdalWorkflowRelationKind Kind,
    string Summary);

public sealed record HongdalWorkflowParticipant(
    HongdalWorkflow Workflow,
    string ActorCode,
    string ActorName,
    bool IsPrimary,
    string Responsibility);

public sealed record HongdalWorkflowScreen(
    HongdalWorkflow Workflow,
    string ActorCode,
    string AppCode,
    string AppName,
    string ScreenName,
    string Route,
    string Purpose);

public sealed record HongdalOperatingSystemEngine(
    string EngineCode,
    string EngineName,
    string AdjustmentPolicy);

public sealed record HongdalSchedulingPolicy(
    HongdalSchedulingPolicyKind Kind,
    string PolicyCode,
    string PolicyName,
    string TargetQueue,
    string AppliedEngineCode,
    string Rule,
    string StarvationGuard);

public sealed record HongdalOperatingSystemDefinition(
    HongdalOperatingSystem OperatingSystem,
    string Name,
    string Purpose,
    IReadOnlyList<HongdalWorkflow> Workflows,
    IReadOnlyList<HongdalOperatingSystemEngine> Engines,
    IReadOnlyList<HongdalSchedulingPolicy> SchedulingPolicies);

public static class HongdalProductVersionLabels
{
    public static string GetLabel(HongdalProductVersion version)
    {
        return version switch
        {
            HongdalProductVersion.V0_0 => "0.0",
            HongdalProductVersion.V1_0 => "1.0",
            HongdalProductVersion.V1_5 => "1.5",
            HongdalProductVersion.V2_0 => "2.0",
            HongdalProductVersion.V2_5 => "2.5",
            HongdalProductVersion.V3_0 => "3.0",
            HongdalProductVersion.V3_5 => "3.5",
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown Hongdal product version.")
        };
    }
}

public static class HongdalOperatingSystemLabels
{
    public static string GetLabel(HongdalOperatingSystem operatingSystem)
    {
        return operatingSystem switch
        {
            HongdalOperatingSystem.DomesticCargoTransport => "국내 화물 운송 OS",
            HongdalOperatingSystem.WarehouseCommerceFulfillment => "창고·커머스 이행 OS",
            HongdalOperatingSystem.GroupPurchaseImport => "공동주문 수입 OS",
            HongdalOperatingSystem.FoodDelivery => "음식 배달 OS",
            HongdalOperatingSystem.HongdalMartUrbanLogistics => "알뜰살뜰 마트 도심 물류 OS",
            HongdalOperatingSystem.CommunityTrust => "커뮤니티 신뢰 OS",
            HongdalOperatingSystem.PlatformOperations => "플랫폼 운영 OS",
            _ => throw new ArgumentOutOfRangeException(nameof(operatingSystem), operatingSystem, "Unknown Hongdal operating system.")
        };
    }
}

public static class HongdalSchedulingPolicyKindLabels
{
    public static string GetLabel(HongdalSchedulingPolicyKind kind)
    {
        return kind switch
        {
            HongdalSchedulingPolicyKind.Fcfs => "FCFS",
            HongdalSchedulingPolicyKind.Sjf => "SJF",
            HongdalSchedulingPolicyKind.Priority => "Priority",
            HongdalSchedulingPolicyKind.Edf => "EDF",
            HongdalSchedulingPolicyKind.Mlfq => "MLFQ",
            HongdalSchedulingPolicyKind.Aging => "Aging",
            HongdalSchedulingPolicyKind.Batching => "Batching",
            HongdalSchedulingPolicyKind.Affinity => "Affinity",
            HongdalSchedulingPolicyKind.GeoNearest => "Geo Nearest",
            HongdalSchedulingPolicyKind.FitFirst => "Fit First",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Hongdal scheduling policy kind.")
        };
    }
}

public static class HongdalWorkflowLabels
{
    public static string GetLabel(HongdalWorkflow workflow)
    {
        return workflow switch
        {
            HongdalWorkflow.DomesticTransport => "국내 화물 운송",
            HongdalWorkflow.WarehouseFulfillment => "창고 입출고",
            HongdalWorkflow.CustomsAndTradeData => "통관·무역 데이터",
            HongdalWorkflow.GroupPurchaseImport => "공동주문 수입",
            HongdalWorkflow.SalesChannelFulfillment => "판매채널 출고",
            HongdalWorkflow.CommunityTrust => "커뮤니티 신뢰",
            HongdalWorkflow.HrParticipation => "참여 인력 관리",
            HongdalWorkflow.FoodDelivery => "음식 배달",
            HongdalWorkflow.HongdalMart => "알뜰살뜰 마트",
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unknown Hongdal workflow.")
        };
    }
}

public static class HongdalOperatingSystems
{
    private static readonly IReadOnlyList<HongdalOperatingSystemDefinition> Items =
    [
        new(
            HongdalOperatingSystem.DomesticCargoTransport,
            HongdalOperatingSystemLabels.GetLabel(HongdalOperatingSystem.DomesticCargoTransport),
            "화주 의뢰, 창고 출고품, 공동주문 국내 운송, 음식/마트 배송처럼 실제 이동이 필요한 대상을 기사 추천, 상차, 하차, 증빙, 정산 후보 흐름으로 실행합니다.",
            [HongdalWorkflow.DomesticTransport, HongdalWorkflow.WarehouseFulfillment, HongdalWorkflow.GroupPurchaseImport, HongdalWorkflow.SalesChannelFulfillment, HongdalWorkflow.FoodDelivery, HongdalWorkflow.HongdalMart],
            [
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "운송 의뢰 원천을 분류한 뒤 차량 적합성, 거리, 일정 삽입 가능성, 기사 상태를 기준으로 화물 기사 또는 음식 배달 기사 후보를 조정합니다.")
            ],
            [
                new(HongdalSchedulingPolicyKind.FitFirst, "CargoFitFirst", "차량·화물 적합성 우선", "배차대기", EngineFamilyIds.TransportRequestDispatch, "차량 제원, 온도 조건, 파손 주의, FCL/LCL, 상하차 장비 조건을 먼저 통과한 기사만 후보로 둡니다.", "적합 기사 없음 상태가 반복되면 공개배차 또는 운영자 보류 큐로 승격합니다."),
                new(HongdalSchedulingPolicyKind.Edf, "PickupDeadlineEdf", "상차 마감 임박 우선", "배차대기", EngineFamilyIds.TransportRequestDispatch, "상차 시간창 종료가 가까운 운송 의뢰에 우선 점수를 부여합니다.", "마감이 여유로운 의뢰도 대기 시간이 길어지면 Aging 점수를 더합니다."),
                new(HongdalSchedulingPolicyKind.GeoNearest, "NearestDriver", "상차지 근접 기사 우선", "배차추천", EngineFamilyIds.TransportRequestDispatch, "상차지까지의 거리와 현재 기사 위치를 기준으로 추천 점수를 보정합니다.", "가까운 기사에게만 반복 노출되지 않도록 추천 라운드와 거절 이력을 반영합니다."),
                new(HongdalSchedulingPolicyKind.Mlfq, "DispatchQueueMlfq", "계획배차·추천배차·공개배차 단계 큐", "배차대기", EngineFamilyIds.TransportRequestDispatch, "계획배차에서 후보가 실패하면 추천배차, 공개배차 단계로 큐를 승격합니다.", "최대 추천 라운드를 넘기면 공개배차로 전환해 기아 상태를 막습니다."),
                new(HongdalSchedulingPolicyKind.Aging, "DispatchAging", "장기 대기 보정", "배차대기", EngineFamilyIds.TransportRequestDispatch, "오래 대기한 운송 의뢰에는 추천 점수와 노출 우선순위를 점진적으로 보정합니다.", "대기 시간이 임계값을 넘으면 운영자 확인 또는 공개배차 전환 대상으로 표시합니다.")
            ]),
        new(
            HongdalOperatingSystem.WarehouseCommerceFulfillment,
            HongdalOperatingSystemLabels.GetLabel(HongdalOperatingSystem.WarehouseCommerceFulfillment),
            "입고상품을 재고화하고 판매채널 주문이나 화주 출고 요청을 출고, 피킹, 포장, 운송 인계로 연결합니다.",
            [HongdalWorkflow.WarehouseFulfillment, HongdalWorkflow.SalesChannelFulfillment, HongdalWorkflow.DomesticTransport],
            [
                new(EngineFamilyIds.OutboundBatch, "출고 배치 엔진", "주문/출고 요청을 어느 창고와 재고로 처리할지 조정합니다."),
                new(EngineFamilyIds.PickingBatch, "피킹 배치 엔진", "출고 라인을 적재대, 피킹 작업자, 포장 작업자 단위로 조정합니다."),
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "포장 완료 또는 출고 예정 화물을 운송 의뢰로 넘깁니다.")
            ],
            [
                new(HongdalSchedulingPolicyKind.Sjf, "SimpleOutboundSjf", "단순 출고 우선", "출고예정", EngineFamilyIds.OutboundBatch, "단일 상품, 단일 창고, 재고 충분 주문처럼 처리 시간이 짧은 출고 요청을 먼저 계획합니다.", "마감 임박 또는 장기 대기 주문은 Priority/Aging 점수로 SJF 뒤에 밀리지 않게 합니다."),
                new(HongdalSchedulingPolicyKind.Batching, "WarehouseZoneBatching", "창고·배송권 묶음 처리", "출고예정", EngineFamilyIds.OutboundBatch, "같은 창고, 같은 배송권, 같은 상품군을 묶어 출고 계획과 운송 인계를 줄입니다.", "묶음 형성 대기 시간이 길어지면 단독 출고로 풀어줍니다."),
                new(HongdalSchedulingPolicyKind.Affinity, "PickerZoneAffinity", "작업자·구역 친화도", "피킹대기", EngineFamilyIds.PickingBatch, "작업자가 익숙한 구역, 현재 위치와 가까운 적재대, 같은 로트 작업을 우선 배정합니다.", "특정 작업자에게 몰리지 않도록 작업자 부하와 장기 대기 작업을 같이 반영합니다."),
                new(HongdalSchedulingPolicyKind.Priority, "ColdChainPriority", "냉장·냉동 우선", "출고예정", EngineFamilyIds.OutboundBatch, "온도 민감 상품과 보관 시간 제한이 있는 상품에 우선순위를 부여합니다.", "냉장·냉동 작업이 일반 작업을 계속 밀어내지 않도록 일반 작업 Aging 점수를 유지합니다.")
            ]),
        new(
            HongdalOperatingSystem.GroupPurchaseImport,
            HongdalOperatingSystemLabels.GetLabel(HongdalOperatingSystem.GroupPurchaseImport),
            "주문자 수요를 모으고 해외 선적, 통관, 보세구역 반출, 국내 3PL 입고 또는 세대 배송까지 이어지는 공동주문 실행을 조정합니다.",
            [HongdalWorkflow.GroupPurchaseImport, HongdalWorkflow.CustomsAndTradeData, HongdalWorkflow.WarehouseFulfillment, HongdalWorkflow.DomesticTransport, HongdalWorkflow.CommunityTrust],
            [
                new(EngineFamilyIds.GroupPurchaseClustering, "집단화 엔진", "같은 상품과 배송권 안의 수요를 자동으로 묶습니다."),
                new(EngineFamilyIds.OutboundBatch, "출고 배치 엔진", "국내 3PL 입고 뒤 판매나 재출고가 필요한 물량을 창고 기준으로 조정합니다."),
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "보세구역 반출 뒤 3PL 입고 운송 또는 세대 직배송을 조정합니다.")
            ],
            [
                new(HongdalSchedulingPolicyKind.Batching, "DemandClusterBatching", "수요 집단화 묶음", "구매의사대기", EngineFamilyIds.GroupPurchaseClustering, "같은 상품, 같은 배송권, 비슷한 수령 조건을 가진 주문자 수요를 묶습니다.", "모집 기간이 끝났거나 최소 수량에 못 미치면 보류·환불·단독 구매 후보로 전환합니다."),
                new(HongdalSchedulingPolicyKind.Priority, "CustomsReadyPriority", "통관·반출 가능 우선", "수입반출대기", EngineFamilyIds.TransportRequestDispatch, "통관 완료, 반출 가능 시각 확정, BL/AWB 확인 완료 건을 국내 운송 후보로 우선 올립니다.", "통관 지연 건은 장기 보류 알림과 운영자 확인 큐로 분리합니다."),
                new(HongdalSchedulingPolicyKind.Edf, "BondedReleaseEdf", "보세구역 반출 마감 우선", "수입반출대기", EngineFamilyIds.TransportRequestDispatch, "보세구역 반출 가능 시간창과 보관 비용 증가 시점을 기준으로 우선순위를 계산합니다.", "마감 임박 건만 계속 선점하지 않도록 집단별 비용 부담과 Aging을 같이 반영합니다."),
                new(HongdalSchedulingPolicyKind.Aging, "GroupPurchaseAging", "공동주문 장기 대기 보정", "공동주문원장", EngineFamilyIds.GroupPurchaseClustering, "모집, 통관, 반출, 분배 단계에서 오래 머문 원장의 운영 우선순위를 높입니다.", "장기 정체 원장은 투표, 운영자 승인, 환불 후보로 전환합니다.")
            ]),
        new(
            HongdalOperatingSystem.FoodDelivery,
            HongdalOperatingSystemLabels.GetLabel(HongdalOperatingSystem.FoodDelivery),
            "음식 주문의 조리 상태, 픽업 가능 시각, 고객 전달 시간창을 기준으로 배달 기사 배차를 조정합니다.",
            [HongdalWorkflow.FoodDelivery, HongdalWorkflow.DomesticTransport],
            [
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "음식점 주문은 조리/픽업 시간과 짧은 반경 기사 위치를 중심으로 조정합니다.")
            ],
            [
                new(HongdalSchedulingPolicyKind.Edf, "FoodPickupEdf", "픽업·전달 마감 우선", "음식배달대기", EngineFamilyIds.TransportRequestDispatch, "조리 완료 예상 시각, 픽업 마감, 고객 전달 마감이 가까운 주문을 우선 배차합니다.", "짧은 마감 주문이 몰리면 오래 대기한 일반 주문에 Aging 점수를 부여합니다."),
                new(HongdalSchedulingPolicyKind.GeoNearest, "FoodNearestRider", "근접 배달 기사 우선", "음식배달대기", EngineFamilyIds.TransportRequestDispatch, "음식점과 가까운 배달 기사, 픽업 후 고객까지의 이동거리를 기준으로 후보를 보정합니다.", "같은 기사에게 연속 배차가 몰리지 않도록 휴식/부하 상태를 반영합니다."),
                new(HongdalSchedulingPolicyKind.Batching, "FoodRouteBatching", "동선 묶음 배달", "음식배달대기", EngineFamilyIds.TransportRequestDispatch, "같은 생활권, 유사 도착 방향, 품질 저하 허용 범위 안의 주문을 묶음 후보로 둡니다.", "품질 저하 예상 시간이 임계값을 넘으면 묶음을 해제합니다.")
            ]),
        new(
            HongdalOperatingSystem.HongdalMartUrbanLogistics,
            HongdalOperatingSystemLabels.GetLabel(HongdalOperatingSystem.HongdalMartUrbanLogistics),
            "도심 내 협소한 창고 재고를 피킹/포장 통합 방식으로 처리하고 포장 완료 뒤 배달 기사 인계로 연결합니다.",
            [HongdalWorkflow.HongdalMart, HongdalWorkflow.WarehouseFulfillment, HongdalWorkflow.FoodDelivery, HongdalWorkflow.DomesticTransport],
            [
                new(EngineFamilyIds.OutboundBatch, "출고 배치 엔진", "도심 재고와 가까운 배송권을 우선해 출고 물량을 조정합니다."),
                new(EngineFamilyIds.PickingBatch, "피킹 배치 엔진", "알뜰살뜰 마트 도심 창고는 피킹·포장 통합 옵션을 우선 적용합니다."),
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "포장 완료 시점과 묶음 배송 가능성을 기준으로 기사 후보를 조정합니다.")
            ],
            [
                new(HongdalSchedulingPolicyKind.Sjf, "MartSmallPickSjf", "소량 피킹 우선", "마트피킹대기", EngineFamilyIds.PickingBatch, "도심 창고의 협소한 공간을 고려해 소량·단순 위치 피킹을 빠르게 처리합니다.", "대량 주문은 마감 시간과 Aging 점수로 별도 보정합니다."),
                new(HongdalSchedulingPolicyKind.Affinity, "MartPickPackAffinity", "피킹·포장 통합 작업자 우선", "마트피킹대기", EngineFamilyIds.PickingBatch, "알뜰살뜰 마트 도심 창고는 같은 작업자가 피킹과 포장을 함께 처리하는 옵션을 우선 적용합니다.", "특정 작업자에게 몰리면 포장 분리 모드로 전환할 수 있게 합니다."),
                new(HongdalSchedulingPolicyKind.Edf, "MartPromiseEdf", "즉시배송 약속 시간 우선", "마트배송대기", EngineFamilyIds.TransportRequestDispatch, "고객 약속 시간과 포장 완료 예상 시각이 가까운 주문을 먼저 기사 인계합니다.", "묶음 배송 대기 시간이 길어지면 단독 배송으로 전환합니다."),
                new(HongdalSchedulingPolicyKind.Batching, "ApartmentDropBatching", "단지·동선 묶음 배송", "마트배송대기", EngineFamilyIds.TransportRequestDispatch, "같은 아파트 단지, 같은 동선, 유사 도착 시간대의 주문을 묶습니다.", "묶음 때문에 약속 시간이 깨지는 주문은 EDF 정책으로 분리합니다.")
            ]),
        new(
            HongdalOperatingSystem.CommunityTrust,
            HongdalOperatingSystemLabels.GetLabel(HongdalOperatingSystem.CommunityTrust),
            "각 운영 체제에서 발생한 공개 가능한 활동 신호를 후기, 투표, 문서, 관계 기록으로 전환합니다.",
            [HongdalWorkflow.CommunityTrust, HongdalWorkflow.DomesticTransport, HongdalWorkflow.GroupPurchaseImport, HongdalWorkflow.SalesChannelFulfillment],
            [
                new(EngineFamilyIds.CommunitySignal, "커뮤니티 활동 신호 엔진", "개인정보 보호 범위 안에서 공개 가능한 업무 행동을 커뮤니티 신호로 조정합니다.")
            ],
            [
                new(HongdalSchedulingPolicyKind.Priority, "SafetyModerationPriority", "신고·안전 이슈 우선", "커뮤니티운영대기", EngineFamilyIds.CommunitySignal, "신고, 개인정보 노출 가능성, 분쟁 관련 글을 운영자 확인 큐에서 우선 처리합니다.", "일반 게시판 개설 요청은 FCFS와 Aging으로 장기 대기를 막습니다."),
                new(HongdalSchedulingPolicyKind.Fcfs, "BoardRequestFcfs", "게시판 개설 신청 순서 처리", "게시판개설대기", EngineFamilyIds.CommunitySignal, "사용자 게시판 개설 신청은 접수 순서를 기본으로 처리합니다.", "오래 대기한 신청은 운영자 알림 우선순위를 높입니다."),
                new(HongdalSchedulingPolicyKind.Aging, "CommunitySignalAging", "활동 신호 장기 미처리 보정", "활동신호대기", EngineFamilyIds.CommunitySignal, "공개 가능 여부 판단이 오래 걸린 활동 신호를 운영자 검토 대상으로 올립니다.", "민감도가 높은 신호는 자동 공개하지 않고 보류합니다.")
            ]),
        new(
            HongdalOperatingSystem.PlatformOperations,
            HongdalOperatingSystemLabels.GetLabel(HongdalOperatingSystem.PlatformOperations),
            "운영자 승인, 기능 플래그, 참여 인력, 정산, 예외 처리를 여러 운영 체제 위에 공통 정책으로 적용합니다.",
            [HongdalWorkflow.HrParticipation, HongdalWorkflow.CommunityTrust, HongdalWorkflow.DomesticTransport, HongdalWorkflow.WarehouseFulfillment],
            [
                new(EngineFamilyIds.WorkflowPolicy, "워크플로우 정책 엔진", "기능 노출, 권한, 보조 기능, 예외 처리를 운영 목적에 맞게 조정합니다.")
            ],
            [
                new(HongdalSchedulingPolicyKind.Priority, "IncidentPriority", "운영 사고 우선", "운영예외대기", EngineFamilyIds.WorkflowPolicy, "결제, 정산, 개인정보, 운송 지연, 냉장/냉동 사고처럼 손실 위험이 큰 예외를 먼저 처리합니다.", "낮은 심각도 예외도 Aging으로 장기 미처리를 막습니다."),
                new(HongdalSchedulingPolicyKind.Fcfs, "ApprovalFcfs", "운영 승인 접수 순서 처리", "운영승인대기", EngineFamilyIds.WorkflowPolicy, "일반 승인 요청은 접수 순서를 기본으로 처리합니다.", "업무 마감이나 법정 신고 기한이 있으면 Priority/EDF로 승격합니다."),
                new(HongdalSchedulingPolicyKind.Edf, "LegalDeadlineEdf", "신고·정산 기한 우선", "기한업무대기", EngineFamilyIds.WorkflowPolicy, "4대보험 신고 준비, 정산 지급, 문서 제출처럼 기한이 있는 업무는 마감이 가까운 순서로 처리합니다.", "기한 없는 운영 업무도 Aging 점수를 부여합니다.")
            ])
    ];

    public static IReadOnlyList<HongdalOperatingSystemDefinition> GetAll() => Items;

    public static IReadOnlyList<HongdalOperatingSystemDefinition> GetByWorkflow(HongdalWorkflow workflow)
        => Items.Where(item => item.Workflows.Contains(workflow)).ToArray();

    public static string GetCanonicalId(HongdalOperatingSystem operatingSystem)
        => operatingSystem switch
        {
            HongdalOperatingSystem.DomesticCargoTransport => OperatingSystemIds.DomesticCargoTransport,
            HongdalOperatingSystem.WarehouseCommerceFulfillment => OperatingSystemIds.WarehouseCommerceFulfillment,
            HongdalOperatingSystem.GroupPurchaseImport => OperatingSystemIds.GroupPurchaseImport,
            HongdalOperatingSystem.FoodDelivery => OperatingSystemIds.FoodDelivery,
            HongdalOperatingSystem.HongdalMartUrbanLogistics => OperatingSystemIds.HongdalMartUrbanLogistics,
            HongdalOperatingSystem.CommunityTrust => OperatingSystemIds.CommunityTrust,
            HongdalOperatingSystem.PlatformOperations => OperatingSystemIds.PlatformOperations,
            _ => throw new ArgumentOutOfRangeException(nameof(operatingSystem), operatingSystem, "Unknown Hongdal operating system.")
        };
}

public static class HongdalWorkflowRelationKindLabels
{
    public static string GetLabel(HongdalWorkflowRelationKind kind)
    {
        return kind switch
        {
            HongdalWorkflowRelationKind.References => "참조",
            HongdalWorkflowRelationKind.Calls => "호출",
            HongdalWorkflowRelationKind.HandsOffTo => "인계",
            HongdalWorkflowRelationKind.Feeds => "공급",
            HongdalWorkflowRelationKind.PublishesSignalTo => "신호 공개",
            HongdalWorkflowRelationKind.OperatesWith => "공동 운영",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Hongdal workflow relation kind.")
        };
    }
}

public static class HongdalActorLabels
{
    public static string GetLabel(HongdalActor actor)
    {
        return actor switch
        {
            HongdalActor.Shipper => "화주",
            HongdalActor.Driver => "기사",
            HongdalActor.Recipient => "수령자",
            HongdalActor.PlatformOperator => "플랫폼 운영자",
            HongdalActor.WarehouseManager => "창고 관리자",
            HongdalActor.ShipperOrSeller => "화주·판매자",
            HongdalActor.CustomsBroker => "관세사",
            HongdalActor.OrdererGroupLeader => "주문자 집단 대표",
            HongdalActor.Orderer => "주문자",
            HongdalActor.OverseasSellerOrForwarder => "해외 판매자·배송대행지",
            HongdalActor.Seller => "판매자",
            HongdalActor.CommunityMember => "커뮤니티 참여자",
            HongdalActor.Worker => "참여 인력",
            HongdalActor.EmployerOrOperatingEntity => "고용·운영 주체",
            HongdalActor.Restaurant => "음식점",
            HongdalActor.FoodDeliveryDriver => "배달 기사",
            HongdalActor.MartOperator => "알뜰살뜰 마트 운영자",
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, "Unknown Hongdal actor.")
        };
    }
}

public static class HongdalUseCaseActorRoleLabels
{
    public static string GetLabel(HongdalUseCaseActorRole role)
    {
        return role switch
        {
            HongdalUseCaseActorRole.Primary => "주 액터",
            HongdalUseCaseActorRole.Supporting => "보조 액터",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Hongdal use case actor role.")
        };
    }
}

public static class HongdalUseCaseRelationKindLabels
{
    public static string GetLabel(HongdalUseCaseRelationKind kind)
    {
        return kind switch
        {
            HongdalUseCaseRelationKind.Include => "포함",
            HongdalUseCaseRelationKind.Extend => "확장",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Hongdal use case relation kind.")
        };
    }
}

public static class HongdalWorkflowRelations
{
    private static readonly IReadOnlyList<HongdalWorkflowRelation> Items =
    [
        new(
            HongdalWorkflow.GroupPurchaseImport,
            HongdalWorkflow.CustomsAndTradeData,
            HongdalWorkflowRelationKind.References,
            "공동주문 수입은 HS 코드, BL/AWB, 문서관리번호, 통관 단계, 수출입 단가 데이터를 참조합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            HongdalWorkflow.DomesticTransport,
            HongdalWorkflowRelationKind.HandsOffTo,
            "보세구역 반출 뒤 아파트 직행 배송이나 국내 3PL 이동이 필요하면 국내 화물 운송으로 인계합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            HongdalWorkflow.WarehouseFulfillment,
            HongdalWorkflowRelationKind.HandsOffTo,
            "국내 3PL 입고를 선택하면 공동수입 물품을 창고 입고, 재고, 출고 가능 상태로 넘깁니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            HongdalWorkflow.SalesChannelFulfillment,
            HongdalWorkflowRelationKind.Feeds,
            "공동수입 재고를 스마트스토어, 쿠팡, Amazon 같은 판매채널 출품 후보로 공급합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            HongdalWorkflow.HrParticipation,
            HongdalWorkflowRelationKind.OperatesWith,
            "공동주문 분류, 배분, 단지 내부 보조 업무가 필요하면 참여 인력 관리와 함께 운영합니다."),
        new(
            HongdalWorkflow.SalesChannelFulfillment,
            HongdalWorkflow.WarehouseFulfillment,
            HongdalWorkflowRelationKind.Calls,
            "판매채널 주문이 들어오면 재고 확인과 출고 배치를 창고 입출고 워크플로우에 요청합니다."),
        new(
            HongdalWorkflow.SalesChannelFulfillment,
            HongdalWorkflow.DomesticTransport,
            HongdalWorkflowRelationKind.HandsOffTo,
            "출고 뒤 화물 배송이나 재위탁 운송이 필요하면 국내 화물 운송으로 인계합니다."),
        new(
            HongdalWorkflow.HongdalMart,
            HongdalWorkflow.WarehouseFulfillment,
            HongdalWorkflowRelationKind.Calls,
            "알뜰살뜰 마트 주문은 도심 재고, 피킹, 포장 처리를 창고 입출고 흐름과 연결합니다."),
        new(
            HongdalWorkflow.HongdalMart,
            HongdalWorkflow.DomesticTransport,
            HongdalWorkflowRelationKind.HandsOffTo,
            "포장 완료 뒤 기사 인계와 배송 증빙이 필요하면 국내 화물 운송으로 인계합니다."),
        new(
            HongdalWorkflow.FoodDelivery,
            HongdalWorkflow.DomesticTransport,
            HongdalWorkflowRelationKind.HandsOffTo,
            "음식점 픽업과 고객 전달은 운송 실행, 위치, 완료 증빙 흐름으로 합류합니다."),
        new(
            HongdalWorkflow.DomesticTransport,
            HongdalWorkflow.CommunityTrust,
            HongdalWorkflowRelationKind.PublishesSignalTo,
            "상하차 완료, 감사, 후기 같은 공개 가능한 운송 활동 신호를 커뮤니티 신뢰 흐름으로 보냅니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            HongdalWorkflow.CommunityTrust,
            HongdalWorkflowRelationKind.PublishesSignalTo,
            "공동주문 모집, 투표, 진행 상태, 분배 후기를 개인정보 보호 범위 안에서 커뮤니티 신호로 보냅니다."),
        new(
            HongdalWorkflow.SalesChannelFulfillment,
            HongdalWorkflow.CommunityTrust,
            HongdalWorkflowRelationKind.PublishesSignalTo,
            "판매 후기와 상품 여정 신호를 동의된 범위에서 커뮤니티 신뢰 흐름으로 보냅니다.")
    ];

    public static IReadOnlyList<HongdalWorkflowRelation> GetAll() => Items;

    public static IReadOnlyList<HongdalWorkflowRelation> GetOutgoing(HongdalWorkflow source)
        => Items.Where(item => item.Source == source).ToArray();

    public static IReadOnlyList<HongdalWorkflowRelation> GetIncoming(HongdalWorkflow target)
        => Items.Where(item => item.Target == target).ToArray();
}

public static class HongdalWorkflowParticipants
{
    private static readonly IReadOnlyList<HongdalWorkflowParticipant> Items =
    [
        new(
            HongdalWorkflow.DomesticTransport,
            "Shipper",
            "화주",
            true,
            "운송 의뢰, 상차·하차 조건, 결제 조건을 제시합니다."),
        new(
            HongdalWorkflow.DomesticTransport,
            "Driver",
            "기사",
            true,
            "추천 의뢰를 수락하고 상차, 운행, 하차, 증빙 제출을 수행합니다."),
        new(
            HongdalWorkflow.DomesticTransport,
            "Recipient",
            "수령자",
            false,
            "하차 확인, 인수 확인, 수령 관련 정보를 확인합니다."),
        new(
            HongdalWorkflow.DomesticTransport,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "배차, 예외, 정산 지연, 분쟁 상태를 관리합니다."),
        new(
            HongdalWorkflow.WarehouseFulfillment,
            "WarehouseManager",
            "창고 관리자",
            true,
            "입고, 검수, 적재, 재고, 포장, 출고 작업을 관리합니다."),
        new(
            HongdalWorkflow.WarehouseFulfillment,
            "ShipperOrSeller",
            "화주·판매자",
            false,
            "입고 상품과 출고 요청의 기준 정보를 제공합니다."),
        new(
            HongdalWorkflow.CustomsAndTradeData,
            "CustomsBroker",
            "관세사",
            true,
            "HS 코드, 통관 단계, 수출입 검토, 서류 보정 의견을 제공합니다."),
        new(
            HongdalWorkflow.CustomsAndTradeData,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "공공 데이터 조회 결과와 내부 원장을 연결합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            "OrdererGroupLeader",
            "주문자 집단 대표",
            true,
            "공동주문 개설, 배송 방식 선택, 분배 기준 합의를 이끕니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            "Orderer",
            "주문자",
            true,
            "구매 의사, 비용 분담, 수령 확인, 투표에 참여합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            "OverseasSellerOrForwarder",
            "해외 판매자·배송대행지",
            false,
            "상품 정보, 포장 정보, BL/AWB, 송장·스티커 정보를 제공합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "원장, 통관 조회, 국내 운송 인계, 비용 정산 기준을 관리합니다."),
        new(
            HongdalWorkflow.SalesChannelFulfillment,
            "Seller",
            "판매자",
            true,
            "판매채널 계정, 상품 출품, 가격, 재고 판매 정책을 관리합니다."),
        new(
            HongdalWorkflow.SalesChannelFulfillment,
            "WarehouseManager",
            "창고 관리자",
            false,
            "주문 발생 뒤 재고 확인과 출고 작업을 수행합니다."),
        new(
            HongdalWorkflow.CommunityTrust,
            "CommunityMember",
            "커뮤니티 참여자",
            true,
            "후기, 문의, 투표, 활동 신호를 확인하고 소통합니다."),
        new(
            HongdalWorkflow.CommunityTrust,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "개인정보 보호, 신고, 숨김, 공개 범위 정책을 관리합니다."),
        new(
            HongdalWorkflow.HrParticipation,
            "Worker",
            "참여 인력",
            true,
            "분류, 배분, 보조 업무, 경비·관리 보조 같은 실제 일을 수행합니다."),
        new(
            HongdalWorkflow.HrParticipation,
            "EmployerOrOperatingEntity",
            "고용·운영 주체",
            true,
            "역할, 근로계약, 보상, 4대보험 신고 준비 책임을 가집니다."),
        new(
            HongdalWorkflow.FoodDelivery,
            "Restaurant",
            "음식점",
            true,
            "주문 접수, 조리, 픽업 가능 상태를 관리합니다."),
        new(
            HongdalWorkflow.FoodDelivery,
            "FoodDeliveryDriver",
            "배달 기사",
            true,
            "픽업, 이동, 고객 전달, 완료 증빙을 수행합니다."),
        new(
            HongdalWorkflow.HongdalMart,
            "MartOperator",
            "알뜰살뜰 마트 운영자",
            true,
            "상품, 도심 재고, 피킹·포장 기준을 관리합니다."),
        new(
            HongdalWorkflow.HongdalMart,
            "Driver",
            "기사",
            false,
            "포장 완료 물품을 인계받아 배송합니다.")
    ];

    public static IReadOnlyList<HongdalWorkflowParticipant> GetAll() => Items;

    public static IReadOnlyList<HongdalWorkflowParticipant> GetByWorkflow(HongdalWorkflow workflow)
        => Items.Where(item => item.Workflow == workflow).ToArray();

    public static string GetBoundarySummary(HongdalWorkflow workflow)
    {
        return workflow switch
        {
            HongdalWorkflow.DomesticTransport => "운송 의뢰가 배차되어 상차, 하차, 증빙, 정산 후보 상태까지 진행되는 범위를 책임집니다.",
            HongdalWorkflow.WarehouseFulfillment => "물품이 창고에 들어온 뒤 재고화되고 출고 가능 상태가 되거나 출고 배치로 넘어가는 범위를 책임집니다.",
            HongdalWorkflow.CustomsAndTradeData => "수출입 판단에 필요한 공공 데이터, HS 코드, 통관 상태, 관세사 검토 정보를 제공하는 범위를 책임집니다.",
            HongdalWorkflow.GroupPurchaseImport => "주문자 집단이 해외 상품을 공동으로 들여와 통관, 국내 반출, 분배 또는 3PL 입고로 넘기는 범위를 책임집니다.",
            HongdalWorkflow.SalesChannelFulfillment => "상품을 판매채널에 출품하고 주문을 창고 출고 또는 운송 인계로 연결하는 범위를 책임집니다.",
            HongdalWorkflow.CommunityTrust => "업무 활동에서 공개 가능한 신뢰 신호, 후기, 투표, 관계 기록을 개인정보 보호 범위 안에서 다루는 책임을 가집니다.",
            HongdalWorkflow.HrParticipation => "플랫폼 업무에 참여하는 인력의 역할, 계약, 보상, 신고 준비 상태를 관리하는 범위를 책임집니다.",
            HongdalWorkflow.FoodDelivery => "음식 주문이 조리, 픽업, 고객 전달, 완료 증빙으로 이어지는 범위를 책임집니다.",
            HongdalWorkflow.HongdalMart => "알뜰살뜰 마트 주문이 도심 재고, 피킹, 포장, 기사 인계로 이어지는 범위를 책임집니다.",
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unknown Hongdal workflow.")
        };
    }
}

public static class HongdalWorkflowScreens
{
    private static readonly IReadOnlyList<HongdalWorkflowScreen> Items =
    [
        new(
            HongdalWorkflow.DomesticTransport,
            "Shipper",
            "HongdalApp",
            "화주 앱",
            "운송 의뢰",
            "/shipper/request",
            "화주가 상차지, 하차지, 화물 조건, 결제 조건을 입력합니다."),
        new(
            HongdalWorkflow.DomesticTransport,
            "Driver",
            "DriverApp",
            "기사 앱",
            "추천 목록",
            "/driver/recommendations",
            "기사가 추천된 일반 화물, 공동주문 운송, 배송 의뢰를 구분해 확인합니다."),
        new(
            HongdalWorkflow.DomesticTransport,
            "Driver",
            "DriverApp",
            "기사 앱",
            "진행 중 운송",
            "/driver/transports/current",
            "기사의 상차, 하차, 인수 확인, 증빙 제출 흐름을 진행합니다."),
        new(
            HongdalWorkflow.DomesticTransport,
            "Driver",
            "DriverApp",
            "기사 앱",
            "월 정산",
            "/driver/settlements/current-month",
            "기사 정산 후보, 지급 상태, 이용료 정보를 확인합니다."),
        new(
            HongdalWorkflow.DomesticTransport,
            "PlatformOperator",
            "HongdalAdmin",
            "관리자 앱",
            "운송 관리",
            "/transports",
            "운송 진행, 예외, 분쟁, 운영 상태를 관리합니다."),
        new(
            HongdalWorkflow.WarehouseFulfillment,
            "WarehouseManager",
            "WarehouseManagerApp",
            "창고 관리자 앱",
            "입고 작업",
            "/work/inbound",
            "창고 작업자가 입고 시작과 입고 검수 흐름으로 진입합니다."),
        new(
            HongdalWorkflow.WarehouseFulfillment,
            "WarehouseManager",
            "WarehouseManagerApp",
            "창고 관리자 앱",
            "작업 보드",
            "/work-board",
            "대기 중인 입고, 포장, 출고 작업을 확인합니다."),
        new(
            HongdalWorkflow.WarehouseFulfillment,
            "WarehouseManager",
            "WarehouseManagerApp",
            "창고 관리자 앱",
            "스캔 스테이션",
            "/scan",
            "입고, 출고, 포장 공정의 스캔과 현장 확인을 수행합니다."),
        new(
            HongdalWorkflow.WarehouseFulfillment,
            "ShipperOrSeller",
            "HongdalApp",
            "화주 앱",
            "창고 재고",
            "/shipper/warehouse/inventory",
            "화주나 판매자가 입고상품, 재고, 출고 가능 상태를 확인합니다."),
        new(
            HongdalWorkflow.CustomsAndTradeData,
            "CustomsBroker",
            "Hongdal.WebApp",
            "통합 웹앱",
            "관세사 홈",
            "/",
            "관세사가 HS 코드, 식품/일반화물 분류, 통관 주의 태그를 보정합니다."),
        new(
            HongdalWorkflow.CustomsAndTradeData,
            "ShipperOrSeller",
            "HongdalApp",
            "화주 앱",
            "HS 코드 검토",
            "/shipper/customs/hs-reviews",
            "화주가 상품의 HS 코드 후보, 통관 리스크, 관세사 검토 필요성을 확인합니다."),
        new(
            HongdalWorkflow.CustomsAndTradeData,
            "PlatformOperator",
            "HongdalAdmin",
            "관리자 앱",
            "HS 코드 운영",
            "/customs/hs-codes",
            "운영자가 HS 코드 데이터와 통관 보정 정보를 관리합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            "Orderer",
            "HongdalApp",
            "홍달 앱",
            "공동수입 상품 선택",
            "/community/group-import",
            "주문자가 HS 코드와 통관 주의 태그를 확인하고 공동수입 상품 후보를 선택합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            "OrdererGroupLeader",
            "HongdalApp",
            "홍달 앱",
            "공동수입 선택안",
            "/community/group-import",
            "주문자 집단 대표가 HS 코드 상품 후보, 운송 방식과 공동 선택 조건을 조정합니다."),
        new(
            HongdalWorkflow.GroupPurchaseImport,
            "PlatformOperator",
            "HongdalAdmin",
            "관리자 앱",
            "공동주문 운영",
            "/dashboard",
            "운영자가 공동주문 원장, 통관 연계, 국내 운송 인계, 예외를 추적합니다."),
        new(
            HongdalWorkflow.SalesChannelFulfillment,
            "Seller",
            "HongdalApp",
            "화주 앱",
            "판매채널 연결",
            "/shipper/sales/channels",
            "판매자가 스마트스토어, 쿠팡, Amazon 같은 판매채널 계정을 연결합니다."),
        new(
            HongdalWorkflow.SalesChannelFulfillment,
            "Seller",
            "HongdalApp",
            "화주 앱",
            "상품 출품",
            "/shipper/sales/listings",
            "판매자가 판매상품을 채널별 출품 후보와 상세 정보로 준비합니다."),
        new(
            HongdalWorkflow.SalesChannelFulfillment,
            "Seller",
            "HongdalApp",
            "화주 앱",
            "주문 이행",
            "/shipper/sales/orders",
            "판매채널 주문을 창고 출고와 운송 인계로 연결합니다."),
        new(
            HongdalWorkflow.CommunityTrust,
            "CommunityMember",
            "OrdererApp",
            "주문자 앱",
            "홈 커뮤니티",
            "/",
            "주문자가 커뮤니티 글, 후기, 활동 신호를 확인합니다."),
        new(
            HongdalWorkflow.CommunityTrust,
            "PlatformOperator",
            "HongdalAdmin",
            "관리자 앱",
            "홈 커뮤니티",
            "/",
            "운영자가 커뮤니티 글, 신고, 숨김, 고정 상태를 관리합니다."),
        new(
            HongdalWorkflow.HrParticipation,
            "EmployerOrOperatingEntity",
            "HongdalAdmin",
            "관리자 앱",
            "인력·4대보험 신고 준비",
            "/dashboard",
            "운영자가 역할, 근로계약, 4대보험 신고 준비 상태를 관리합니다."),
        new(
            HongdalWorkflow.FoodDelivery,
            "Restaurant",
            "OrdererApp",
            "주문자 앱",
            "음식점",
            "/food/restaurants",
            "주문자가 음식점과 메뉴를 확인하고 음식 주문 흐름으로 진입합니다."),
        new(
            HongdalWorkflow.FoodDelivery,
            "FoodDeliveryDriver",
            "DriverApp",
            "기사 앱",
            "추천 목록",
            "/driver/recommendations",
            "배달 기사가 음식 픽업·전달 추천 의뢰를 확인합니다."),
        new(
            HongdalWorkflow.HongdalMart,
            "MartOperator",
            "WarehouseManagerApp",
            "창고 관리자 앱",
            "알뜰살뜰 마트 작업 홈",
            "/mart",
            "마트 운영자가 도심 재고, 피킹, 포장, 기사 인계 작업으로 진입합니다."),
        new(
            HongdalWorkflow.HongdalMart,
            "Orderer",
            "OrdererApp",
            "주문자 앱",
            "알뜰살뜰 마트 주문",
            "/food/mart",
            "주문자가 도심 창고 재고 기반 마트 상품을 주문합니다.")
    ];

    public static IReadOnlyList<HongdalWorkflowScreen> GetAll() => Items;

    public static IReadOnlyList<HongdalWorkflowScreen> GetByWorkflow(HongdalWorkflow workflow)
        => Items.Where(item => item.Workflow == workflow).ToArray();

    public static IReadOnlyList<HongdalWorkflowScreen> GetByWorkflowAndActor(HongdalWorkflow workflow, string actorCode)
        => Items
            .Where(item => item.Workflow == workflow &&
                string.Equals(item.ActorCode, actorCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();
}

public static class HongdalApiGrowthTrackLabels
{
    public static string GetLabel(HongdalApiGrowthTrack track)
    {
        return track switch
        {
            HongdalApiGrowthTrack.CoreLogistics => "Core Logistics",
            HongdalApiGrowthTrack.Community => "Community",
            HongdalApiGrowthTrack.Warehouse => "Warehouse",
            HongdalApiGrowthTrack.Customs => "Customs",
            HongdalApiGrowthTrack.OrdererGroupCommerce => "Orderer Group Commerce",
            HongdalApiGrowthTrack.FoodDelivery => "Food Delivery",
            HongdalApiGrowthTrack.HongdalMart => "Hongdal Mart",
            HongdalApiGrowthTrack.PlatformOperations => "Platform Operations",
            _ => throw new ArgumentOutOfRangeException(nameof(track), track, "Unknown Hongdal API growth track.")
        };
    }
}
