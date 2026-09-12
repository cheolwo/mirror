using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.ApiMetadata;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class SsalddelApiVersionAttribute : Attribute
{
    public SsalddelApiVersionAttribute(SsalddelProductVersion version)
    {
        Version = version;
    }

    public SsalddelProductVersion Version { get; }

    public string VersionLabel => SsalddelProductVersionLabels.GetLabel(Version);

    public string ProductName
        => SsalddelProductRoadmapCatalog.Find(VersionLabel).ProductName;

    public string VersionDisplayName
        => SsalddelProductRoadmapCatalog.Find(VersionLabel).FullDisplayName;

    public string? FeatureKey { get; set; }

    public string? WorkflowKey { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class SsalddelApiWorkflowAttribute : Attribute
{
    public SsalddelApiWorkflowAttribute(SsalddelWorkflow workflow)
    {
        Workflow = workflow;
    }

    public SsalddelWorkflow Workflow { get; }

    public string WorkflowLabel => SsalddelWorkflowLabels.GetLabel(Workflow);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class SsalddelApiGrowthTrackAttribute : Attribute
{
    public SsalddelApiGrowthTrackAttribute(SsalddelApiGrowthTrack track)
    {
        Track = track;
    }

    public SsalddelApiGrowthTrack Track { get; }

    public string TrackLabel => SsalddelApiGrowthTrackLabels.GetLabel(Track);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SsalddelUseCaseAttribute : Attribute
{
    public SsalddelUseCaseAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string Summary { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class SsalddelUseCaseActorAttribute : Attribute
{
    public SsalddelUseCaseActorAttribute(SsalddelActor actor, SsalddelUseCaseActorRole role = SsalddelUseCaseActorRole.Primary)
    {
        Actor = actor;
        Role = role;
    }

    public SsalddelActor Actor { get; }

    public SsalddelUseCaseActorRole Role { get; }

    public string ActorCode => Actor.ToString();

    public string ActorLabel => SsalddelActorLabels.GetLabel(Actor);

    public string RoleLabel => SsalddelUseCaseActorRoleLabels.GetLabel(Role);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class SsalddelUseCaseRelationAttribute : Attribute
{
    public SsalddelUseCaseRelationAttribute(SsalddelUseCaseRelationKind kind, string targetUseCaseCode)
    {
        Kind = kind;
        TargetUseCaseCode = targetUseCaseCode;
    }

    public SsalddelUseCaseRelationKind Kind { get; }

    public string TargetUseCaseCode { get; }

    public string KindLabel => SsalddelUseCaseRelationKindLabels.GetLabel(Kind);

    public string Condition { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}

public enum SsalddelProductVersion
{
    V0_0 = 0,
    V0_5 = 50,
    V1_0 = 100,
    V1_5 = 150,
    V2_0 = 200,
    V2_5 = 250,
    V3_0 = 300,
    V3_5 = 350
}

public enum SsalddelApiGrowthTrack
{
    CoreLogistics = 100,
    Community = 200,
    Warehouse = 300,
    Customs = 400,
    OrdererGroupCommerce = 500,
    FoodDelivery = 600,
    SsalddelMart = 700,
    PlatformOperations = 800
}

public enum SsalddelOperatingSystem
{
    DomesticCargoTransport = 100,
    WarehouseCommerceFulfillment = 200,
    GroupPurchaseImport = 300,
    FoodDelivery = 400,
    SsalddelMartUrbanLogistics = 500,
    CommunityTrust = 600,
    PlatformOperations = 700,
    GroupPurchaseDemand = 800,
    ShipperTransportManagement = 900
}

public enum SsalddelSchedulingPolicyKind
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

public enum SsalddelWorkflow
{
    DomesticTransport = 100,
    WarehouseFulfillment = 200,
    CustomsAndTradeData = 300,
    GroupPurchaseImport = 400,
    SalesChannelFulfillment = 500,
    CommunityTrust = 600,
    HrParticipation = 700,
    FoodDelivery = 800,
    SsalddelMart = 900,
    GroupPurchaseDemand = 1000
}

public enum SsalddelWorkflowRelationKind
{
    References = 100,
    Calls = 200,
    HandsOffTo = 300,
    Feeds = 400,
    PublishesSignalTo = 500,
    OperatesWith = 600
}

public enum SsalddelActor
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

public enum SsalddelUseCaseActorRole
{
    Primary = 100,
    Supporting = 200
}

public enum SsalddelUseCaseRelationKind
{
    Include = 100,
    Extend = 200
}

public sealed record SsalddelWorkflowRelation(
    SsalddelWorkflow Source,
    SsalddelWorkflow Target,
    SsalddelWorkflowRelationKind Kind,
    string Summary);

public sealed record SsalddelWorkflowParticipant(
    SsalddelWorkflow Workflow,
    string ActorCode,
    string ActorName,
    bool IsPrimary,
    string Responsibility);

public sealed record SsalddelWorkflowScreen(
    SsalddelWorkflow Workflow,
    string ActorCode,
    string AppCode,
    string AppName,
    string ScreenName,
    string Route,
    string Purpose);

public sealed record SsalddelOperatingSystemEngine(
    string EngineCode,
    string EngineName,
    string AdjustmentPolicy);

public sealed record SsalddelSchedulingPolicy(
    SsalddelSchedulingPolicyKind Kind,
    string PolicyCode,
    string PolicyName,
    string TargetQueue,
    string AppliedEngineCode,
    string Rule,
    string StarvationGuard);

public sealed record SsalddelOperatingSystemDefinition(
    SsalddelOperatingSystem OperatingSystem,
    string Name,
    string Purpose,
    IReadOnlyList<SsalddelWorkflow> Workflows,
    IReadOnlyList<SsalddelOperatingSystemEngine> Engines,
    IReadOnlyList<SsalddelSchedulingPolicy> SchedulingPolicies);

public static class SsalddelProductVersionLabels
{
    public static string GetLabel(SsalddelProductVersion version)
    {
        return version switch
        {
            SsalddelProductVersion.V0_0 => "0.0",
            SsalddelProductVersion.V0_5 => "0.5",
            SsalddelProductVersion.V1_0 => "1.0",
            SsalddelProductVersion.V1_5 => "1.5",
            SsalddelProductVersion.V2_0 => "2.0",
            SsalddelProductVersion.V2_5 => "2.5",
            SsalddelProductVersion.V3_0 => "3.0",
            SsalddelProductVersion.V3_5 => "3.5",
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown Ssalddel product version.")
        };
    }
}

public static class SsalddelOperatingSystemLabels
{
    public static string GetLabel(SsalddelOperatingSystem operatingSystem)
    {
        return operatingSystem switch
        {
            SsalddelOperatingSystem.DomesticCargoTransport => "국내 화물 운송 OS",
            SsalddelOperatingSystem.WarehouseCommerceFulfillment => "창고·커머스 이행 OS",
            SsalddelOperatingSystem.GroupPurchaseDemand => "공동구매 수요·모집 OS",
            SsalddelOperatingSystem.GroupPurchaseImport => "같이 주문 수입",
            SsalddelOperatingSystem.FoodDelivery => "음식 배달 OS",
            SsalddelOperatingSystem.SsalddelMartUrbanLogistics => "알뜰살뜰 마트 도심 물류 OS",
            SsalddelOperatingSystem.CommunityTrust => "커뮤니티 신뢰 OS",
            SsalddelOperatingSystem.PlatformOperations => "플랫폼 운영 OS",
            SsalddelOperatingSystem.ShipperTransportManagement => "화주 운송관리 OS",
            _ => throw new ArgumentOutOfRangeException(nameof(operatingSystem), operatingSystem, "Unknown Ssalddel operating system.")
        };
    }
}

public static class SsalddelSchedulingPolicyKindLabels
{
    public static string GetLabel(SsalddelSchedulingPolicyKind kind)
    {
        return kind switch
        {
            SsalddelSchedulingPolicyKind.Fcfs => "FCFS",
            SsalddelSchedulingPolicyKind.Sjf => "SJF",
            SsalddelSchedulingPolicyKind.Priority => "Priority",
            SsalddelSchedulingPolicyKind.Edf => "EDF",
            SsalddelSchedulingPolicyKind.Mlfq => "MLFQ",
            SsalddelSchedulingPolicyKind.Aging => "Aging",
            SsalddelSchedulingPolicyKind.Batching => "Batching",
            SsalddelSchedulingPolicyKind.Affinity => "Affinity",
            SsalddelSchedulingPolicyKind.GeoNearest => "Geo Nearest",
            SsalddelSchedulingPolicyKind.FitFirst => "Fit First",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Ssalddel scheduling policy kind.")
        };
    }
}

public static class SsalddelWorkflowLabels
{
    public static string GetLabel(SsalddelWorkflow workflow)
    {
        return workflow switch
        {
            SsalddelWorkflow.DomesticTransport => "국내 화물 운송",
            SsalddelWorkflow.WarehouseFulfillment => "창고 입출고",
            SsalddelWorkflow.CustomsAndTradeData => "통관·무역 데이터",
            SsalddelWorkflow.GroupPurchaseDemand => "공동구매 수요·모집",
            SsalddelWorkflow.GroupPurchaseImport => "같이 주문 수입",
            SsalddelWorkflow.SalesChannelFulfillment => "판매채널 출고",
            SsalddelWorkflow.CommunityTrust => "커뮤니티 신뢰",
            SsalddelWorkflow.HrParticipation => "참여 인력 관리",
            SsalddelWorkflow.FoodDelivery => "음식 배달",
            SsalddelWorkflow.SsalddelMart => "알뜰살뜰 마트",
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unknown Ssalddel workflow.")
        };
    }
}

public static class SsalddelOperatingSystems
{
    private static readonly IReadOnlyList<SsalddelOperatingSystemDefinition> Items =
    [
        new(
            SsalddelOperatingSystem.ShipperTransportManagement,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.ShipperTransportManagement),
            "화주가 화물·상하차·운임·정산 조건을 확정하고 실제 운송 실행 책임을 국내 화물 운송 OS에 명시적으로 인계한 뒤 인수와 정산 결과를 확인합니다.",
            [SsalddelWorkflow.DomesticTransport, SsalddelWorkflow.WarehouseFulfillment, SsalddelWorkflow.SalesChannelFulfillment],
            [],
            []),
        new(
            SsalddelOperatingSystem.DomesticCargoTransport,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.DomesticCargoTransport),
            "화주 의뢰, 창고 출고품, 같이 주문 국내 운송, 음식/마트 배송처럼 실제 이동이 필요한 대상을 기사 추천, 상차, 하차, 증빙, 정산 후보 흐름으로 실행합니다.",
            [SsalddelWorkflow.DomesticTransport, SsalddelWorkflow.WarehouseFulfillment, SsalddelWorkflow.GroupPurchaseImport, SsalddelWorkflow.SalesChannelFulfillment, SsalddelWorkflow.FoodDelivery, SsalddelWorkflow.SsalddelMart],
            [
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "운송 의뢰 원천을 분류한 뒤 차량 적합성, 거리, 일정 삽입 가능성, 기사 상태를 기준으로 화물 기사 또는 음식 배달 기사 후보를 조정합니다.")
            ],
            [
                new(SsalddelSchedulingPolicyKind.FitFirst, "CargoFitFirst", "차량·화물 적합성 우선", "배차대기", EngineFamilyIds.TransportRequestDispatch, "차량 제원, 온도 조건, 파손 주의, FCL/LCL, 상하차 장비 조건을 먼저 통과한 기사만 후보로 둡니다.", "적합 기사 없음 상태가 반복되면 공개배차 또는 운영자 보류 큐로 승격합니다."),
                new(SsalddelSchedulingPolicyKind.Edf, "PickupDeadlineEdf", "상차 마감 임박 우선", "배차대기", EngineFamilyIds.TransportRequestDispatch, "상차 시간창 종료가 가까운 운송 의뢰에 우선 점수를 부여합니다.", "마감이 여유로운 의뢰도 대기 시간이 길어지면 Aging 점수를 더합니다."),
                new(SsalddelSchedulingPolicyKind.GeoNearest, "NearestDriver", "상차지 근접 기사 우선", "배차추천", EngineFamilyIds.TransportRequestDispatch, "상차지까지의 거리와 현재 기사 위치를 기준으로 추천 점수를 보정합니다.", "가까운 기사에게만 반복 노출되지 않도록 추천 라운드와 거절 이력을 반영합니다."),
                new(SsalddelSchedulingPolicyKind.Mlfq, "DispatchQueueMlfq", "계획배차·추천배차·공개배차 단계 큐", "배차대기", EngineFamilyIds.TransportRequestDispatch, "계획배차에서 후보가 실패하면 추천배차, 공개배차 단계로 큐를 승격합니다.", "최대 추천 라운드를 넘기면 공개배차로 전환해 기아 상태를 막습니다."),
                new(SsalddelSchedulingPolicyKind.Aging, "DispatchAging", "장기 대기 보정", "배차대기", EngineFamilyIds.TransportRequestDispatch, "오래 대기한 운송 의뢰에는 추천 점수와 노출 우선순위를 점진적으로 보정합니다.", "대기 시간이 임계값을 넘으면 운영자 확인 또는 공개배차 전환 대상으로 표시합니다.")
            ]),
        new(
            SsalddelOperatingSystem.WarehouseCommerceFulfillment,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.WarehouseCommerceFulfillment),
            "입고상품을 재고화하고 판매채널 주문이나 화주 출고 요청을 출고, 피킹, 포장, 운송 인계로 연결합니다.",
            [SsalddelWorkflow.WarehouseFulfillment, SsalddelWorkflow.SalesChannelFulfillment, SsalddelWorkflow.DomesticTransport],
            [
                new(EngineFamilyIds.OutboundBatch, "출고 배치 엔진", "주문/출고 요청을 어느 창고와 재고로 처리할지 조정합니다."),
                new(EngineFamilyIds.PickingBatch, "피킹 배치 엔진", "출고 라인을 적재대, 피킹 작업자, 포장 작업자 단위로 조정합니다."),
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "포장 완료 또는 출고 예정 화물을 운송 의뢰로 넘깁니다.")
            ],
            [
                new(SsalddelSchedulingPolicyKind.Sjf, "SimpleOutboundSjf", "단순 출고 우선", "출고예정", EngineFamilyIds.OutboundBatch, "단일 상품, 단일 창고, 재고 충분 주문처럼 처리 시간이 짧은 출고 요청을 먼저 계획합니다.", "마감 임박 또는 장기 대기 주문은 Priority/Aging 점수로 SJF 뒤에 밀리지 않게 합니다."),
                new(SsalddelSchedulingPolicyKind.Batching, "WarehouseZoneBatching", "창고·배송권 묶음 처리", "출고예정", EngineFamilyIds.OutboundBatch, "같은 창고, 같은 배송권, 같은 상품군을 묶어 출고 계획과 운송 인계를 줄입니다.", "묶음 형성 대기 시간이 길어지면 단독 출고로 풀어줍니다."),
                new(SsalddelSchedulingPolicyKind.Affinity, "PickerZoneAffinity", "작업자·구역 친화도", "피킹대기", EngineFamilyIds.PickingBatch, "작업자가 익숙한 구역, 현재 위치와 가까운 적재대, 같은 로트 작업을 우선 배정합니다.", "특정 작업자에게 몰리지 않도록 작업자 부하와 장기 대기 작업을 같이 반영합니다."),
                new(SsalddelSchedulingPolicyKind.Priority, "ColdChainPriority", "냉장·냉동 우선", "출고예정", EngineFamilyIds.OutboundBatch, "온도 민감 상품과 보관 시간 제한이 있는 상품에 우선순위를 부여합니다.", "냉장·냉동 작업이 일반 작업을 계속 밀어내지 않도록 일반 작업 Aging 점수를 유지합니다.")
            ]),
        new(
            SsalddelOperatingSystem.GroupPurchaseDemand,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.GroupPurchaseDemand),
            "비구속 구매 의사를 품목·배송권·수령 조건별로 안전하게 모으고, 모집 진행과 마감을 조율하며, 사람의 확인을 거쳐 공급·무역 준비 단계로 인계합니다.",
            [SsalddelWorkflow.GroupPurchaseDemand, SsalddelWorkflow.CommunityTrust],
            [
                new(EngineFamilyIds.GroupPurchaseClustering, "주문자 집단화 엔진", "상품, 배송권, 보관 온도와 물류 방식이 같은 수요를 결정적으로 묶고 배치·보류 이유를 반환합니다.")
            ],
            [
                new(SsalddelSchedulingPolicyKind.Batching, "DemandClusterBatching", "수요 집단화 묶음", "비구속수요대기", EngineFamilyIds.GroupPurchaseClustering, "같은 상품, 배송권, 보관 온도와 물류 방식의 수요를 같은 모집 후보로 묶습니다.", "모집 마감이나 목표 미달 시 자동 확정하지 않고 보류 이유와 새 모집 회차 선택지를 표시합니다."),
                new(SsalddelSchedulingPolicyKind.Edf, "RecruitmentDeadlineEdf", "모집 마감 임박 검토", "모집중", EngineFamilyIds.GroupPurchaseClustering, "모집 종료가 가까운 집단을 재계산·안내 대상으로 먼저 올립니다.", "마감 임박을 참여자 차별이나 자동 구매 확정 근거로 사용하지 않습니다."),
                new(SsalddelSchedulingPolicyKind.Aging, "DemandRecruitmentAging", "장기 모집 정체 보정", "모집중", EngineFamilyIds.GroupPurchaseClustering, "오래 정체된 모집을 운영자 검토와 추가 모집 안내 대상으로 올립니다.", "효율이 낮은 참여자를 제외하지 않고 더 넓은 모집권, 다른 시간창 또는 수령 방식을 제안합니다.")
            ]),
        new(
            SsalddelOperatingSystem.GroupPurchaseImport,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.GroupPurchaseImport),
            "확인된 공동구매 모집 결과를 공급·무역 준비 원장으로 받아 해외 선적, 통관, 보세구역 반출, 국내 3PL 입고 또는 세대 배송 인계를 조정합니다.",
            [SsalddelWorkflow.GroupPurchaseImport, SsalddelWorkflow.CustomsAndTradeData, SsalddelWorkflow.WarehouseFulfillment, SsalddelWorkflow.DomesticTransport, SsalddelWorkflow.CommunityTrust],
            [
                new(EngineFamilyIds.OutboundBatch, "출고 배치 엔진", "국내 3PL 입고 뒤 판매나 재출고가 필요한 물량을 창고 기준으로 조정합니다."),
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "보세구역 반출 뒤 3PL 입고 운송 또는 세대 직배송을 조정합니다.")
            ],
            [
                new(SsalddelSchedulingPolicyKind.Priority, "CustomsReadyPriority", "통관·반출 가능 우선", "수입반출대기", EngineFamilyIds.TransportRequestDispatch, "통관 완료, 반출 가능 시각 확정, BL/AWB 확인 완료 건을 국내 운송 후보로 우선 올립니다.", "통관 지연 건은 장기 보류 알림과 운영자 확인 큐로 분리합니다."),
                new(SsalddelSchedulingPolicyKind.Edf, "BondedReleaseEdf", "보세구역 반출 마감 우선", "수입반출대기", EngineFamilyIds.TransportRequestDispatch, "보세구역 반출 가능 시간창과 보관 비용 증가 시점을 기준으로 우선순위를 계산합니다.", "마감 임박 건만 계속 선점하지 않도록 집단별 비용 부담과 Aging을 같이 반영합니다.")
            ]),
        new(
            SsalddelOperatingSystem.FoodDelivery,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.FoodDelivery),
            "음식 주문의 조리 상태, 픽업 가능 시각, 고객 전달 시간창을 기준으로 배달 기사 배차를 조정합니다.",
            [SsalddelWorkflow.FoodDelivery, SsalddelWorkflow.DomesticTransport],
            [
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "음식점 주문은 조리/픽업 시간과 짧은 반경 기사 위치를 중심으로 조정합니다.")
            ],
            [
                new(SsalddelSchedulingPolicyKind.Edf, "FoodPickupEdf", "픽업·전달 마감 우선", "음식배달대기", EngineFamilyIds.TransportRequestDispatch, "조리 완료 예상 시각, 픽업 마감, 고객 전달 마감이 가까운 주문을 우선 배차합니다.", "짧은 마감 주문이 몰리면 오래 대기한 일반 주문에 Aging 점수를 부여합니다."),
                new(SsalddelSchedulingPolicyKind.GeoNearest, "FoodNearestRider", "근접 배달 기사 우선", "음식배달대기", EngineFamilyIds.TransportRequestDispatch, "음식점과 가까운 배달 기사, 픽업 후 고객까지의 이동거리를 기준으로 후보를 보정합니다.", "같은 기사에게 연속 배차가 몰리지 않도록 휴식/부하 상태를 반영합니다."),
                new(SsalddelSchedulingPolicyKind.Batching, "FoodRouteBatching", "동선 묶음 배달", "음식배달대기", EngineFamilyIds.TransportRequestDispatch, "같은 생활권, 유사 도착 방향, 품질 저하 허용 범위 안의 주문을 묶음 후보로 둡니다.", "품질 저하 예상 시간이 임계값을 넘으면 묶음을 해제합니다.")
            ]),
        new(
            SsalddelOperatingSystem.SsalddelMartUrbanLogistics,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.SsalddelMartUrbanLogistics),
            "도심 내 협소한 창고 재고를 피킹/포장 통합 방식으로 처리하고 포장 완료 뒤 배달 기사 인계로 연결합니다.",
            [SsalddelWorkflow.SsalddelMart, SsalddelWorkflow.WarehouseFulfillment, SsalddelWorkflow.FoodDelivery, SsalddelWorkflow.DomesticTransport],
            [
                new(EngineFamilyIds.OutboundBatch, "출고 배치 엔진", "도심 재고와 가까운 배송권을 우선해 출고 물량을 조정합니다."),
                new(EngineFamilyIds.PickingBatch, "피킹 배치 엔진", "알뜰살뜰 마트 도심 창고는 피킹·포장 통합 옵션을 우선 적용합니다."),
                new(EngineFamilyIds.TransportRequestDispatch, "운송 의뢰 배차 엔진", "포장 완료 시점과 묶음 배송 가능성을 기준으로 기사 후보를 조정합니다.")
            ],
            [
                new(SsalddelSchedulingPolicyKind.Sjf, "MartSmallPickSjf", "소량 피킹 우선", "마트피킹대기", EngineFamilyIds.PickingBatch, "도심 창고의 협소한 공간을 고려해 소량·단순 위치 피킹을 빠르게 처리합니다.", "대량 주문은 마감 시간과 Aging 점수로 별도 보정합니다."),
                new(SsalddelSchedulingPolicyKind.Affinity, "MartPickPackAffinity", "피킹·포장 통합 작업자 우선", "마트피킹대기", EngineFamilyIds.PickingBatch, "알뜰살뜰 마트 도심 창고는 같은 작업자가 피킹과 포장을 함께 처리하는 옵션을 우선 적용합니다.", "특정 작업자에게 몰리면 포장 분리 모드로 전환할 수 있게 합니다."),
                new(SsalddelSchedulingPolicyKind.Edf, "MartPromiseEdf", "즉시배송 약속 시간 우선", "마트배송대기", EngineFamilyIds.TransportRequestDispatch, "고객 약속 시간과 포장 완료 예상 시각이 가까운 주문을 먼저 기사 인계합니다.", "묶음 배송 대기 시간이 길어지면 단독 배송으로 전환합니다."),
                new(SsalddelSchedulingPolicyKind.Batching, "ApartmentDropBatching", "단지·동선 묶음 배송", "마트배송대기", EngineFamilyIds.TransportRequestDispatch, "같은 아파트 단지, 같은 동선, 유사 도착 시간대의 주문을 묶습니다.", "묶음 때문에 약속 시간이 깨지는 주문은 EDF 정책으로 분리합니다.")
            ]),
        new(
            SsalddelOperatingSystem.CommunityTrust,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.CommunityTrust),
            "각 운영 체제에서 발생한 공개 가능한 활동 신호를 후기, 투표, 문서, 관계 기록으로 전환합니다.",
            [SsalddelWorkflow.CommunityTrust, SsalddelWorkflow.DomesticTransport, SsalddelWorkflow.GroupPurchaseImport, SsalddelWorkflow.SalesChannelFulfillment],
            [
                new(EngineFamilyIds.CommunitySignal, "커뮤니티 활동 신호 엔진", "개인정보 보호 범위 안에서 공개 가능한 업무 행동을 커뮤니티 신호로 조정합니다.")
            ],
            [
                new(SsalddelSchedulingPolicyKind.Priority, "SafetyModerationPriority", "신고·안전 이슈 우선", "커뮤니티운영대기", EngineFamilyIds.CommunitySignal, "신고, 개인정보 노출 가능성, 분쟁 관련 글을 운영자 확인 큐에서 우선 처리합니다.", "일반 게시판 개설 요청은 FCFS와 Aging으로 장기 대기를 막습니다."),
                new(SsalddelSchedulingPolicyKind.Fcfs, "BoardRequestFcfs", "게시판 개설 신청 순서 처리", "게시판개설대기", EngineFamilyIds.CommunitySignal, "사용자 게시판 개설 신청은 접수 순서를 기본으로 처리합니다.", "오래 대기한 신청은 운영자 알림 우선순위를 높입니다."),
                new(SsalddelSchedulingPolicyKind.Aging, "CommunitySignalAging", "활동 신호 장기 미처리 보정", "활동신호대기", EngineFamilyIds.CommunitySignal, "공개 가능 여부 판단이 오래 걸린 활동 신호를 운영자 검토 대상으로 올립니다.", "민감도가 높은 신호는 자동 공개하지 않고 보류합니다.")
            ]),
        new(
            SsalddelOperatingSystem.PlatformOperations,
            SsalddelOperatingSystemLabels.GetLabel(SsalddelOperatingSystem.PlatformOperations),
            "운영자 승인, 기능 플래그, 참여 인력, 정산, 예외 처리를 여러 운영 체제 위에 공통 정책으로 적용합니다.",
            [SsalddelWorkflow.HrParticipation, SsalddelWorkflow.CommunityTrust, SsalddelWorkflow.DomesticTransport, SsalddelWorkflow.WarehouseFulfillment],
            [
                new(EngineFamilyIds.WorkflowPolicy, "워크플로우 정책 엔진", "기능 노출, 권한, 보조 기능, 예외 처리를 운영 목적에 맞게 조정합니다.")
            ],
            [
                new(SsalddelSchedulingPolicyKind.Priority, "IncidentPriority", "운영 사고 우선", "운영예외대기", EngineFamilyIds.WorkflowPolicy, "결제, 정산, 개인정보, 운송 지연, 냉장/냉동 사고처럼 손실 위험이 큰 예외를 먼저 처리합니다.", "낮은 심각도 예외도 Aging으로 장기 미처리를 막습니다."),
                new(SsalddelSchedulingPolicyKind.Fcfs, "ApprovalFcfs", "운영 승인 접수 순서 처리", "운영승인대기", EngineFamilyIds.WorkflowPolicy, "일반 승인 요청은 접수 순서를 기본으로 처리합니다.", "업무 마감이나 법정 신고 기한이 있으면 Priority/EDF로 승격합니다."),
                new(SsalddelSchedulingPolicyKind.Edf, "LegalDeadlineEdf", "신고·정산 기한 우선", "기한업무대기", EngineFamilyIds.WorkflowPolicy, "4대보험 신고 준비, 정산 지급, 문서 제출처럼 기한이 있는 업무는 마감이 가까운 순서로 처리합니다.", "기한 없는 운영 업무도 Aging 점수를 부여합니다.")
            ])
    ];

    public static IReadOnlyList<SsalddelOperatingSystemDefinition> GetAll() => Items;

    public static IReadOnlyList<SsalddelOperatingSystemDefinition> GetByWorkflow(SsalddelWorkflow workflow)
        => Items.Where(item => item.Workflows.Contains(workflow)).ToArray();

    public static string GetCanonicalId(SsalddelOperatingSystem operatingSystem)
        => operatingSystem switch
        {
            SsalddelOperatingSystem.DomesticCargoTransport => OperatingSystemIds.DomesticCargoTransport,
            SsalddelOperatingSystem.WarehouseCommerceFulfillment => OperatingSystemIds.WarehouseCommerceFulfillment,
            SsalddelOperatingSystem.GroupPurchaseDemand => OperatingSystemIds.GroupPurchaseDemand,
            SsalddelOperatingSystem.GroupPurchaseImport => OperatingSystemIds.GroupPurchaseImport,
            SsalddelOperatingSystem.FoodDelivery => OperatingSystemIds.FoodDelivery,
            SsalddelOperatingSystem.SsalddelMartUrbanLogistics => OperatingSystemIds.SsalddelMartUrbanLogistics,
            SsalddelOperatingSystem.CommunityTrust => OperatingSystemIds.CommunityTrust,
            SsalddelOperatingSystem.PlatformOperations => OperatingSystemIds.PlatformOperations,
            SsalddelOperatingSystem.ShipperTransportManagement => OperatingSystemIds.ShipperTransportManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(operatingSystem), operatingSystem, "Unknown Ssalddel operating system.")
        };
}

public static class SsalddelWorkflowRelationKindLabels
{
    public static string GetLabel(SsalddelWorkflowRelationKind kind)
    {
        return kind switch
        {
            SsalddelWorkflowRelationKind.References => "참조",
            SsalddelWorkflowRelationKind.Calls => "호출",
            SsalddelWorkflowRelationKind.HandsOffTo => "인계",
            SsalddelWorkflowRelationKind.Feeds => "공급",
            SsalddelWorkflowRelationKind.PublishesSignalTo => "신호 공개",
            SsalddelWorkflowRelationKind.OperatesWith => "공동 운영",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Ssalddel workflow relation kind.")
        };
    }
}

public static class SsalddelActorLabels
{
    public static string GetLabel(SsalddelActor actor)
    {
        return actor switch
        {
            SsalddelActor.Shipper => "화주",
            SsalddelActor.Driver => "기사",
            SsalddelActor.Recipient => "수령자",
            SsalddelActor.PlatformOperator => "플랫폼 운영자",
            SsalddelActor.WarehouseManager => "창고 관리자",
            SsalddelActor.ShipperOrSeller => "화주·판매자",
            SsalddelActor.CustomsBroker => "관세사",
            SsalddelActor.OrdererGroupLeader => "주문자 집단 대표",
            SsalddelActor.Orderer => "주문자",
            SsalddelActor.OverseasSellerOrForwarder => "해외 판매자·배송대행지",
            SsalddelActor.Seller => "판매자",
            SsalddelActor.CommunityMember => "커뮤니티 참여자",
            SsalddelActor.Worker => "참여 인력",
            SsalddelActor.EmployerOrOperatingEntity => "고용·운영 주체",
            SsalddelActor.Restaurant => "음식점",
            SsalddelActor.FoodDeliveryDriver => "배달 기사",
            SsalddelActor.MartOperator => "알뜰살뜰 마트 운영자",
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, "Unknown Ssalddel actor.")
        };
    }
}

public static class SsalddelUseCaseActorRoleLabels
{
    public static string GetLabel(SsalddelUseCaseActorRole role)
    {
        return role switch
        {
            SsalddelUseCaseActorRole.Primary => "주 액터",
            SsalddelUseCaseActorRole.Supporting => "보조 액터",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Ssalddel use case actor role.")
        };
    }
}

public static class SsalddelUseCaseRelationKindLabels
{
    public static string GetLabel(SsalddelUseCaseRelationKind kind)
    {
        return kind switch
        {
            SsalddelUseCaseRelationKind.Include => "포함",
            SsalddelUseCaseRelationKind.Extend => "확장",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Ssalddel use case relation kind.")
        };
    }
}

public static class SsalddelWorkflowRelations
{
    private static readonly IReadOnlyList<SsalddelWorkflowRelation> Items =
    [
        new(
            SsalddelWorkflow.GroupPurchaseDemand,
            SsalddelWorkflow.GroupPurchaseImport,
            SsalddelWorkflowRelationKind.HandsOffTo,
            "목표를 충족한 모집도 자동 주문으로 확정하지 않고, 참여 의사와 공급 조건을 사람이 확인한 뒤 공급·무역 준비로 인계합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseDemand,
            SsalddelWorkflow.CommunityTrust,
            SsalddelWorkflowRelationKind.PublishesSignalTo,
            "공동구매 모집, 참여자 수, 목표 수량과 진행 상태를 개인정보 보호 범위 안에서 커뮤니티 신호로 보냅니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            SsalddelWorkflow.CustomsAndTradeData,
            SsalddelWorkflowRelationKind.References,
            "같이 주문 수입은 HS 코드, BL/AWB, 문서관리번호, 통관 단계, 수출입 단가 데이터를 참조합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            SsalddelWorkflow.DomesticTransport,
            SsalddelWorkflowRelationKind.HandsOffTo,
            "보세구역 반출 뒤 아파트 직행 배송이나 국내 3PL 이동이 필요하면 국내 화물 운송으로 인계합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            SsalddelWorkflow.WarehouseFulfillment,
            SsalddelWorkflowRelationKind.HandsOffTo,
            "국내 3PL 입고를 선택하면 같이 수입 물품을 창고 입고, 재고, 출고 가능 상태로 넘깁니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            SsalddelWorkflow.SalesChannelFulfillment,
            SsalddelWorkflowRelationKind.Feeds,
            "같이 수입 재고를 스마트스토어, 쿠팡, Amazon 같은 판매채널 출품 후보로 공급합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            SsalddelWorkflow.HrParticipation,
            SsalddelWorkflowRelationKind.OperatesWith,
            "같이 주문 분류, 배분, 단지 내부 보조 업무가 필요하면 참여 인력 관리와 함께 운영합니다."),
        new(
            SsalddelWorkflow.SalesChannelFulfillment,
            SsalddelWorkflow.WarehouseFulfillment,
            SsalddelWorkflowRelationKind.Calls,
            "판매채널 주문이 들어오면 재고 확인과 출고 배치를 창고 입출고 워크플로우에 요청합니다."),
        new(
            SsalddelWorkflow.SalesChannelFulfillment,
            SsalddelWorkflow.DomesticTransport,
            SsalddelWorkflowRelationKind.HandsOffTo,
            "출고 뒤 화물 배송이나 재위탁 운송이 필요하면 국내 화물 운송으로 인계합니다."),
        new(
            SsalddelWorkflow.SsalddelMart,
            SsalddelWorkflow.WarehouseFulfillment,
            SsalddelWorkflowRelationKind.Calls,
            "알뜰살뜰 마트 주문은 도심 재고, 피킹, 포장 처리를 창고 입출고 흐름과 연결합니다."),
        new(
            SsalddelWorkflow.SsalddelMart,
            SsalddelWorkflow.DomesticTransport,
            SsalddelWorkflowRelationKind.HandsOffTo,
            "포장 완료 뒤 기사 인계와 배송 증빙이 필요하면 국내 화물 운송으로 인계합니다."),
        new(
            SsalddelWorkflow.FoodDelivery,
            SsalddelWorkflow.DomesticTransport,
            SsalddelWorkflowRelationKind.HandsOffTo,
            "음식점 픽업과 고객 전달은 운송 실행, 위치, 완료 증빙 흐름으로 합류합니다."),
        new(
            SsalddelWorkflow.DomesticTransport,
            SsalddelWorkflow.CommunityTrust,
            SsalddelWorkflowRelationKind.PublishesSignalTo,
            "상하차 완료, 감사, 후기 같은 공개 가능한 운송 활동 신호를 커뮤니티 신뢰 흐름으로 보냅니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            SsalddelWorkflow.CommunityTrust,
            SsalddelWorkflowRelationKind.PublishesSignalTo,
            "공급·무역 준비와 분배 후기를 개인정보 보호 범위 안에서 커뮤니티 신호로 보냅니다."),
        new(
            SsalddelWorkflow.SalesChannelFulfillment,
            SsalddelWorkflow.CommunityTrust,
            SsalddelWorkflowRelationKind.PublishesSignalTo,
            "판매 후기와 상품 여정 신호를 동의된 범위에서 커뮤니티 신뢰 흐름으로 보냅니다.")
    ];

    public static IReadOnlyList<SsalddelWorkflowRelation> GetAll() => Items;

    public static IReadOnlyList<SsalddelWorkflowRelation> GetOutgoing(SsalddelWorkflow source)
        => Items.Where(item => item.Source == source).ToArray();

    public static IReadOnlyList<SsalddelWorkflowRelation> GetIncoming(SsalddelWorkflow target)
        => Items.Where(item => item.Target == target).ToArray();
}

public static class SsalddelWorkflowParticipants
{
    private static readonly IReadOnlyList<SsalddelWorkflowParticipant> Items =
    [
        new(
            SsalddelWorkflow.DomesticTransport,
            "Shipper",
            "화주",
            true,
            "운송 의뢰, 상차·하차 조건, 결제 조건을 제시합니다."),
        new(
            SsalddelWorkflow.DomesticTransport,
            "Driver",
            "기사",
            true,
            "추천 의뢰를 수락하고 상차, 운행, 하차, 증빙 제출을 수행합니다."),
        new(
            SsalddelWorkflow.DomesticTransport,
            "Recipient",
            "수령자",
            false,
            "하차 확인, 인수 확인, 수령 관련 정보를 확인합니다."),
        new(
            SsalddelWorkflow.DomesticTransport,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "배차, 예외, 정산 지연, 분쟁 상태를 관리합니다."),
        new(
            SsalddelWorkflow.WarehouseFulfillment,
            "WarehouseManager",
            "창고 관리자",
            true,
            "입고, 검수, 적재, 재고, 포장, 출고 작업을 관리합니다."),
        new(
            SsalddelWorkflow.WarehouseFulfillment,
            "ShipperOrSeller",
            "화주·판매자",
            false,
            "입고 상품과 출고 요청의 기준 정보를 제공합니다."),
        new(
            SsalddelWorkflow.CustomsAndTradeData,
            "CustomsBroker",
            "관세사",
            true,
            "HS 코드, 통관 단계, 수출입 검토, 서류 보정 의견을 제공합니다."),
        new(
            SsalddelWorkflow.CustomsAndTradeData,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "공공 데이터 조회 결과와 내부 원장을 연결합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseDemand,
            "Orderer",
            "주문자",
            true,
            "비구속 구매 의사와 희망 수량·수령 조건을 등록·변경·철회합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseDemand,
            "OrdererGroupLeader",
            "주문자 집단 대표",
            false,
            "모집 목표와 조건을 제안하고 목표 충족 뒤 다음 단계 인계 여부를 함께 확인합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseDemand,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "집단화 정책, 모집 마감, 보류와 인계 예외를 관리합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            "OrdererGroupLeader",
            "주문자 집단 대표",
            true,
            "같이 주문 개설, 배송 방식 선택, 분배 기준 합의를 이끕니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            "Orderer",
            "주문자",
            true,
            "구매 의사, 비용 분담, 수령 확인, 투표에 참여합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            "OverseasSellerOrForwarder",
            "해외 판매자·배송대행지",
            false,
            "상품 정보, 포장 정보, BL/AWB, 송장·스티커 정보를 제공합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "원장, 통관 조회, 국내 운송 인계, 비용 정산 기준을 관리합니다."),
        new(
            SsalddelWorkflow.SalesChannelFulfillment,
            "Seller",
            "판매자",
            true,
            "판매채널 계정, 상품 출품, 가격, 재고 판매 정책을 관리합니다."),
        new(
            SsalddelWorkflow.SalesChannelFulfillment,
            "WarehouseManager",
            "창고 관리자",
            false,
            "주문 발생 뒤 재고 확인과 출고 작업을 수행합니다."),
        new(
            SsalddelWorkflow.CommunityTrust,
            "CommunityMember",
            "커뮤니티 참여자",
            true,
            "후기, 문의, 투표, 활동 신호를 확인하고 소통합니다."),
        new(
            SsalddelWorkflow.CommunityTrust,
            "PlatformOperator",
            "플랫폼 운영자",
            false,
            "개인정보 보호, 신고, 숨김, 공개 범위 정책을 관리합니다."),
        new(
            SsalddelWorkflow.HrParticipation,
            "Worker",
            "참여 인력",
            true,
            "분류, 배분, 보조 업무, 경비·관리 보조 같은 실제 일을 수행합니다."),
        new(
            SsalddelWorkflow.HrParticipation,
            "EmployerOrOperatingEntity",
            "고용·운영 주체",
            true,
            "역할, 근로계약, 보상, 4대보험 신고 준비 책임을 가집니다."),
        new(
            SsalddelWorkflow.FoodDelivery,
            "Restaurant",
            "음식점",
            true,
            "주문 접수, 조리, 픽업 가능 상태를 관리합니다."),
        new(
            SsalddelWorkflow.FoodDelivery,
            "FoodDeliveryDriver",
            "배달 기사",
            true,
            "픽업, 이동, 고객 전달, 완료 증빙을 수행합니다."),
        new(
            SsalddelWorkflow.SsalddelMart,
            "MartOperator",
            "알뜰살뜰 마트 운영자",
            true,
            "상품, 도심 재고, 피킹·포장 기준을 관리합니다."),
        new(
            SsalddelWorkflow.SsalddelMart,
            "Driver",
            "기사",
            false,
            "포장 완료 물품을 인계받아 배송합니다.")
    ];

    public static IReadOnlyList<SsalddelWorkflowParticipant> GetAll() => Items;

    public static IReadOnlyList<SsalddelWorkflowParticipant> GetByWorkflow(SsalddelWorkflow workflow)
        => Items.Where(item => item.Workflow == workflow).ToArray();

    public static string GetBoundarySummary(SsalddelWorkflow workflow)
    {
        return workflow switch
        {
            SsalddelWorkflow.DomesticTransport => "운송 의뢰가 배차되어 상차, 하차, 증빙, 정산 후보 상태까지 진행되는 범위를 책임집니다.",
            SsalddelWorkflow.WarehouseFulfillment => "물품이 창고에 들어온 뒤 재고화되고 출고 가능 상태가 되거나 출고 배치로 넘어가는 범위를 책임집니다.",
            SsalddelWorkflow.CustomsAndTradeData => "수출입 판단에 필요한 공공 데이터, HS 코드, 통관 상태, 관세사 검토 정보를 제공하는 범위를 책임집니다.",
            SsalddelWorkflow.GroupPurchaseDemand => "비구속 구매 의사를 결정적 기준으로 묶고 모집 진행·마감·철회를 반영하며, 사람의 확인 전에는 주문·결제·계약을 만들지 않는 범위를 책임집니다.",
            SsalddelWorkflow.GroupPurchaseImport => "주문자 집단이 해외 상품을 공동으로 들여와 통관, 국내 반출, 분배 또는 3PL 입고로 넘기는 범위를 책임집니다.",
            SsalddelWorkflow.SalesChannelFulfillment => "상품을 판매채널에 출품하고 주문을 창고 출고 또는 운송 인계로 연결하는 범위를 책임집니다.",
            SsalddelWorkflow.CommunityTrust => "업무 활동에서 공개 가능한 신뢰 신호, 후기, 투표, 관계 기록을 개인정보 보호 범위 안에서 다루는 책임을 가집니다.",
            SsalddelWorkflow.HrParticipation => "플랫폼 업무에 참여하는 인력의 역할, 계약, 보상, 신고 준비 상태를 관리하는 범위를 책임집니다.",
            SsalddelWorkflow.FoodDelivery => "음식 주문이 조리, 픽업, 고객 전달, 완료 증빙으로 이어지는 범위를 책임집니다.",
            SsalddelWorkflow.SsalddelMart => "알뜰살뜰 마트 주문이 도심 재고, 피킹, 포장, 기사 인계로 이어지는 범위를 책임집니다.",
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unknown Ssalddel workflow.")
        };
    }
}

public static class SsalddelWorkflowScreens
{
    private static readonly IReadOnlyList<SsalddelWorkflowScreen> Items =
    [
        new(
            SsalddelWorkflow.DomesticTransport,
            "Shipper",
            "SsalddelApp",
            "화주 앱",
            "운송 의뢰",
            "/shipper/request",
            "화주가 상차지, 하차지, 화물 조건, 결제 조건을 입력합니다."),
        new(
            SsalddelWorkflow.DomesticTransport,
            "Driver",
            "DriverApp",
            "기사 앱",
            "추천 목록",
            "/driver/recommendations",
            "기사가 추천된 일반 화물, 같이 주문 운송, 배송 의뢰를 구분해 확인합니다."),
        new(
            SsalddelWorkflow.DomesticTransport,
            "Driver",
            "DriverApp",
            "기사 앱",
            "진행 중 운송",
            "/driver/transports/current",
            "기사의 상차, 하차, 인수 확인, 증빙 제출 흐름을 진행합니다."),
        new(
            SsalddelWorkflow.DomesticTransport,
            "Driver",
            "DriverApp",
            "기사 앱",
            "월 정산",
            "/driver/settlements/current-month",
            "기사 정산 후보, 지급 상태, 이용료 정보를 확인합니다."),
        new(
            SsalddelWorkflow.DomesticTransport,
            "PlatformOperator",
            "SsalddelAdmin",
            "관리자 앱",
            "운송 관리",
            "/transports",
            "운송 진행, 예외, 분쟁, 운영 상태를 관리합니다."),
        new(
            SsalddelWorkflow.WarehouseFulfillment,
            "WarehouseManager",
            "WarehouseManagerApp",
            "창고 관리자 앱",
            "입고 작업",
            "/work/inbound",
            "창고 작업자가 입고 시작과 입고 검수 흐름으로 진입합니다."),
        new(
            SsalddelWorkflow.WarehouseFulfillment,
            "WarehouseManager",
            "WarehouseManagerApp",
            "창고 관리자 앱",
            "작업 보드",
            "/work-board",
            "대기 중인 입고, 포장, 출고 작업을 확인합니다."),
        new(
            SsalddelWorkflow.WarehouseFulfillment,
            "WarehouseManager",
            "WarehouseManagerApp",
            "창고 관리자 앱",
            "스캔 스테이션",
            "/scan",
            "입고, 출고, 포장 공정의 스캔과 현장 확인을 수행합니다."),
        new(
            SsalddelWorkflow.WarehouseFulfillment,
            "ShipperOrSeller",
            "SsalddelApp",
            "화주 앱",
            "창고 재고",
            "/shipper/warehouse/inventory",
            "화주나 판매자가 입고상품, 재고, 출고 가능 상태를 확인합니다."),
        new(
            SsalddelWorkflow.CustomsAndTradeData,
            "CustomsBroker",
            "Ssalddel.WebApp",
            "통합 웹앱",
            "관세사 홈",
            "/",
            "관세사가 HS 코드, 식품/일반화물 분류, 통관 주의 태그를 보정합니다."),
        new(
            SsalddelWorkflow.CustomsAndTradeData,
            "ShipperOrSeller",
            "SsalddelApp",
            "화주 앱",
            "HS 코드 검토",
            "/shipper/customs/hs-reviews",
            "화주가 상품의 HS 코드 후보, 통관 리스크, 관세사 검토 필요성을 확인합니다."),
        new(
            SsalddelWorkflow.CustomsAndTradeData,
            "PlatformOperator",
            "SsalddelAdmin",
            "관리자 앱",
            "HS 코드 운영",
            "/customs/hs-codes",
            "운영자가 HS 코드 데이터와 통관 보정 정보를 관리합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseDemand,
            "Orderer",
            "Ssalddel.WebApp",
            "통합 웹앱",
            "공동구매 수요·모집",
            "/community/group-purchase",
            "주문자가 공개 모집을 확인하고 본인의 비구속 수요를 등록·변경·철회합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseDemand,
            "Orderer",
            "OrdererApp",
            "주문자 앱",
            "공동구매 의사 표시·집단화",
            "/group-purchase",
            "주문자가 저장 전 집단화 미리보기와 모집 진행 상태를 확인합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            "Orderer",
            "SsalddelApp",
            "살뜰 앱",
            "같이 수입 상품 선택",
            "/community/group-import",
            "주문자가 HS 코드와 통관 주의 태그를 확인하고 같이 수입 상품 후보를 선택합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            "OrdererGroupLeader",
            "SsalddelApp",
            "살뜰 앱",
            "같이 수입 선택안",
            "/community/group-import",
            "주문자 집단 대표가 HS 코드 상품 후보, 운송 방식과 공동 선택 조건을 조정합니다."),
        new(
            SsalddelWorkflow.GroupPurchaseImport,
            "PlatformOperator",
            "SsalddelAdmin",
            "관리자 앱",
            "같이 주문 운영",
            "/dashboard",
            "운영자가 같이 주문 원장, 통관 연계, 국내 운송 인계, 예외를 추적합니다."),
        new(
            SsalddelWorkflow.SalesChannelFulfillment,
            "Seller",
            "SsalddelApp",
            "화주 앱",
            "판매채널 연결",
            "/shipper/sales/channels",
            "판매자가 스마트스토어, 쿠팡, Amazon 같은 판매채널 계정을 연결합니다."),
        new(
            SsalddelWorkflow.SalesChannelFulfillment,
            "Seller",
            "SsalddelApp",
            "화주 앱",
            "상품 출품",
            "/shipper/sales/listings",
            "판매자가 판매상품을 채널별 출품 후보와 상세 정보로 준비합니다."),
        new(
            SsalddelWorkflow.SalesChannelFulfillment,
            "Seller",
            "SsalddelApp",
            "화주 앱",
            "주문 이행",
            "/shipper/sales/orders",
            "판매채널 주문을 창고 출고와 운송 인계로 연결합니다."),
        new(
            SsalddelWorkflow.CommunityTrust,
            "CommunityMember",
            "OrdererApp",
            "주문자 앱",
            "홈 커뮤니티",
            "/",
            "주문자가 커뮤니티 글, 후기, 활동 신호를 확인합니다."),
        new(
            SsalddelWorkflow.CommunityTrust,
            "PlatformOperator",
            "SsalddelAdmin",
            "관리자 앱",
            "홈 커뮤니티",
            "/",
            "운영자가 커뮤니티 글, 신고, 숨김, 고정 상태를 관리합니다."),
        new(
            SsalddelWorkflow.HrParticipation,
            "EmployerOrOperatingEntity",
            "SsalddelAdmin",
            "관리자 앱",
            "인력·4대보험 신고 준비",
            "/dashboard",
            "운영자가 역할, 근로계약, 4대보험 신고 준비 상태를 관리합니다."),
        new(
            SsalddelWorkflow.FoodDelivery,
            "Restaurant",
            "OrdererApp",
            "주문자 앱",
            "음식점",
            "/food/restaurants",
            "주문자가 음식점과 메뉴를 확인하고 음식 주문 흐름으로 진입합니다."),
        new(
            SsalddelWorkflow.FoodDelivery,
            "FoodDeliveryDriver",
            "DriverApp",
            "기사 앱",
            "추천 목록",
            "/driver/recommendations",
            "배달 기사가 음식 픽업·전달 추천 의뢰를 확인합니다."),
        new(
            SsalddelWorkflow.FoodDelivery,
            "PlatformOperator",
            "SsalddelAdmin",
            "관리자 앱",
            "음식 주문 운영 추적",
            "/food/order-trace",
            "운영자가 음식 주문번호로 배차·추천·운송·Outbox 상관관계와 복구 필요 상태를 확인합니다."),
        new(
            SsalddelWorkflow.SsalddelMart,
            "MartOperator",
            "WarehouseManagerApp",
            "창고 관리자 앱",
            "알뜰살뜰 마트 작업 홈",
            "/mart",
            "마트 운영자가 도심 재고, 피킹, 포장, 기사 인계 작업으로 진입합니다."),
        new(
            SsalddelWorkflow.SsalddelMart,
            "Orderer",
            "OrdererApp",
            "주문자 앱",
            "알뜰살뜰 마트 주문",
            "/food/mart",
            "주문자가 도심 창고 재고 기반 마트 상품을 주문합니다.")
    ];

    public static IReadOnlyList<SsalddelWorkflowScreen> GetAll() => Items;

    public static IReadOnlyList<SsalddelWorkflowScreen> GetByWorkflow(SsalddelWorkflow workflow)
        => Items.Where(item => item.Workflow == workflow).ToArray();

    public static IReadOnlyList<SsalddelWorkflowScreen> GetByWorkflowAndActor(SsalddelWorkflow workflow, string actorCode)
        => Items
            .Where(item => item.Workflow == workflow &&
                string.Equals(item.ActorCode, actorCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();
}

public static class SsalddelApiGrowthTrackLabels
{
    public static string GetLabel(SsalddelApiGrowthTrack track)
    {
        return track switch
        {
            SsalddelApiGrowthTrack.CoreLogistics => "Core Logistics",
            SsalddelApiGrowthTrack.Community => "Community",
            SsalddelApiGrowthTrack.Warehouse => "Warehouse",
            SsalddelApiGrowthTrack.Customs => "Customs",
            SsalddelApiGrowthTrack.OrdererGroupCommerce => "Orderer Group Commerce",
            SsalddelApiGrowthTrack.FoodDelivery => "Food Delivery",
            SsalddelApiGrowthTrack.SsalddelMart => "Ssalddel Mart",
            SsalddelApiGrowthTrack.PlatformOperations => "Platform Operations",
            _ => throw new ArgumentOutOfRangeException(nameof(track), track, "Unknown Ssalddel API growth track.")
        };
    }
}
