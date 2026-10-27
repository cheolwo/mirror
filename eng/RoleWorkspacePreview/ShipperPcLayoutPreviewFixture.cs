using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RoleWorkspacePreview;

// Browser layout samples only: no API client, database, persisted draft, payment or dispatch.
internal sealed class ShipperPcLayoutPreviewFixture
{
    public 운송의뢰작성ViewModel Authoring { get; } = new()
    {
        화물종류 = "예시 포장 물품",
        화물수량 = 3,
        화물중량Kg = 2,
        화물설명 = "배치 확인을 위한 예시입니다. 실제 접수하지 않습니다."
    };

    public ShipperRequestDetailPageState Summary(bool completed, string? scenario = null) => new()
    {
        SourceBoundaryMessage = "화면 배치 확인용 예시이며 실제 운송 원장을 조회하지 않았습니다.",
        Request = new()
        {
            RequestId = "LAYOUT-EXAMPLE-112",
            CargoType = "예시 포장 물품",
            TransportMethod = "퀵",
            VehicleType = "오토바이",
            RequestStatus = "생성됨",
            PaymentStatus = scenario == "payment-pending" ? "결제대기" : "결제완료",
            SettlementStatus = scenario == "payment-pending" ? "결제대기" : "정산대기",
            SettlementTiming = "선결제",
            DispatchStatus = scenario == "payment-pending" ? "미시작" : "배차확정",
            TransportStatus = scenario == "payment-pending" ? "" : completed ? "인수완료" : "운송중",
            OperationalReviewHeld = scenario == "held",
            AssignedDriverId = scenario == "payment-pending" ? "" : "layout-example-driver",
            AssignedDriverName = scenario == "payment-pending" ? "" : "예시 기사",
            AssignedDriverVehicle = "오토바이",
            PickupAddress = "예시 상차지역",
            DropoffAddress = "예시 전달지역",
            ExpectedPaymentAmount = 5000,
            FinalFare = 5000,
            CreatedAt = new(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc)
        }
    };

    public IReadOnlyList<RequestRow> Rows { get; } =
    [
        new("예시 의뢰 1", "예시 상차지역 A", "예시 전달지역 A", "배차대기"),
        new("예시 의뢰 2", "예시 상차지역 B", "예시 전달지역 B", "운송중"),
        new("예시 의뢰 3", "예시 상차지역 C", "예시 전달지역 C", "인수완료")
    ];

    public sealed record RequestRow(string Name, string Pickup, string Dropoff, string State);
}
