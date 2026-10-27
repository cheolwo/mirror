using Ssalddel.Contracts.Admin.Settlement;
using SsalddelAdmin.Services;

namespace SsalddelAdmin.ViewModels;

/// <summary>수납과 지급 표시를 조립합니다. 기존 호출 계약은 유지합니다.</summary>
public sealed class TransportWorkflowSettlementViewModel
{
    public TransportCollectionPresentation Collection { get; private init; } = new();
    public TransportPayoutPresentation Payout { get; private init; } = new();
    public bool CollectionConfirmed => Collection.Confirmed;
    public string CollectionStatus => Collection.Status;
    public string PreparationStatus => Payout.PreparationStatus;
    public bool QueryCompleted => Payout.QueryCompleted;
    public bool QueryFailed => Payout.QueryFailed;
    public string QueryScope => Payout.QueryScope;
    public IReadOnlyList<기사지급승인응답> PayoutRequests => Payout.Requests;
    public bool HasActualTransferConfirmation => Payout.HasActualTransferConfirmation;
    public bool RequiresAttention => Payout.RequiresAttention;
    public string ActualTransferStatus => Payout.ActualTransferStatus;
    public string RequestStatus => Payout.RequestStatus;

    public static TransportWorkflowSettlementViewModel Create(운송워크플로우관제상세응답 detail)
        => Create(detail.의뢰Id, detail.의뢰, detail.결제, detail.운송, detail.기사운송료지급조회);
    public static TransportWorkflowSettlementViewModel Create(string requestId, 화주운송의뢰응답? request,
        결제목록응답? payment, 운송진행응답? transport, 기사운송료지급조회응답? query)
        => new() { Collection = TransportCollectionPresentation.Create(request, payment), Payout = TransportPayoutPresentation.Create(requestId, transport, query) };

    public static string RequestStatusOf(기사지급승인응답 item) => TransportPayoutPresentation.RequestStatusOf(item);
    public static bool IsActualTransferConfirmed(기사지급승인응답 item) => TransportPayoutPresentation.IsActualTransferConfirmed(item);
    public static string ExecutionModeOf(기사지급승인응답 item) => TransportPayoutPresentation.ExecutionModeOf(item);
    public static string ExpectedAmountOf(기사지급승인응답 item) => TransportPayoutPresentation.ExpectedAmountOf(item);
}
