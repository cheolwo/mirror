using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public static class 활동정산Navigation
{
    public const string Page = "/activity-settlements";
    public const string Api = "api/v1/neighborhood-settlements";
    public static string Href(string? agreementId = null, string? returnUrl = null)
    {
        var route = Page + (RoleWorkspaceNavigation.StableId(agreementId) is { } id ? "/" + Uri.EscapeDataString(id) : "");
        return RoleWorkspaceNavigation.LocalRoute(returnUrl) is { } home ? RoleWorkspaceNavigation.WithReturn(route, home)! : route;
    }
    public static string DetailApi(string id) => Api + "/" + Uri.EscapeDataString(id);
}

public interface I활동정산Client
{
    Task<IReadOnlyList<NeighborhoodSettlementSummaryDto>> MineAsync(CancellationToken ct);
    Task<NeighborhoodSettlementSummaryDto?> DetailAsync(string id, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto?> ReceiptAsync(Guid requestId, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto?> ConsentAsync(string id, NeighborhoodSettlementConsentRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto?> ConfirmAsync(string id, NeighborhoodSettlementEntryConfirmRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto?> DisputeAsync(string id, NeighborhoodSettlementDisputeRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto?> ReceiveAsync(string id, NeighborhoodSettlementPaymentReceiptRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto?> AccrueRewardAsync(string id, NeighborhoodSettlementActivityRewardAccrualRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto?> ReportPaymentAsync(string id, NeighborhoodSettlementPaymentReportRequest request, CancellationToken ct);
    Task<WorkerWeeklyStatementDto?> WeeklyAsync(DateOnly? completionDateKst, CancellationToken ct);
}

/// <summary>본인 정산·주간 명세와 당사자 지급 신고를 연결합니다. 실제 송금이나 새 채무 생성은 제공하지 않습니다.</summary>
public sealed class 활동정산Client(ISsalddelJsonApiClient api) : I활동정산Client
{
    public Task<WorkerWeeklyStatementDto?> WeeklyAsync(DateOnly? completionDateKst, CancellationToken ct)
        => api.GetAsync<WorkerWeeklyStatementDto>(활동정산Navigation.Api + "/weekly" +
            (completionDateKst is { } date ? "?completionDateKst=" + date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : ""),
            "내 주간 업무 명세", allowNotFound: false, cancellationToken: ct);
    public Task<NeighborhoodSettlementReceiptDto?> ReportPaymentAsync(string id, NeighborhoodSettlementPaymentReportRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodSettlementPaymentReportRequest, NeighborhoodSettlementReceiptDto>(HttpMethod.Post,
            활동정산Navigation.DetailApi(id) + "/payment-reports", request, "내 지급 신고 기록", cancellationToken: ct);
    public async Task<IReadOnlyList<NeighborhoodSettlementSummaryDto>> MineAsync(CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<NeighborhoodSettlementSummaryDto>>(활동정산Navigation.Api, "내 활동·정산 내역", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidOperationException("정산 조회 응답 미확인");
    public Task<NeighborhoodSettlementSummaryDto?> DetailAsync(string id, CancellationToken ct)
        => api.GetAsync<NeighborhoodSettlementSummaryDto>(활동정산Navigation.DetailApi(id), "선택 활동·정산 내역", cancellationToken: ct);
    public Task<NeighborhoodSettlementReceiptDto?> ReceiptAsync(Guid requestId, CancellationToken ct)
        => api.GetAsync<NeighborhoodSettlementReceiptDto>($"{활동정산Navigation.Api}/receipts/{requestId:D}", "정산 처리 결과", cancellationToken: ct);
    public Task<NeighborhoodSettlementReceiptDto?> ConsentAsync(string id, NeighborhoodSettlementConsentRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodSettlementConsentRequest, NeighborhoodSettlementReceiptDto>(HttpMethod.Post,
            활동정산Navigation.DetailApi(id) + "/consents", request, "현재 정산 약정 동의", cancellationToken: ct);
    public Task<NeighborhoodSettlementReceiptDto?> ConfirmAsync(string id, NeighborhoodSettlementEntryConfirmRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodSettlementEntryConfirmRequest, NeighborhoodSettlementReceiptDto>(HttpMethod.Post,
            활동정산Navigation.DetailApi(id) + "/entry-confirmations", request, "정산 항목 확인", cancellationToken: ct);
    public Task<NeighborhoodSettlementReceiptDto?> DisputeAsync(string id, NeighborhoodSettlementDisputeRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodSettlementDisputeRequest, NeighborhoodSettlementReceiptDto>(HttpMethod.Post,
            활동정산Navigation.DetailApi(id) + "/disputes", request, "정산 항목 이의제기", cancellationToken: ct);
    public Task<NeighborhoodSettlementReceiptDto?> ReceiveAsync(string id, NeighborhoodSettlementPaymentReceiptRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodSettlementPaymentReceiptRequest, NeighborhoodSettlementReceiptDto>(HttpMethod.Post,
            활동정산Navigation.DetailApi(id) + "/payment-receipts", request, "지급액 수령 확인", cancellationToken: ct);
    public Task<NeighborhoodSettlementReceiptDto?> AccrueRewardAsync(string id, NeighborhoodSettlementActivityRewardAccrualRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodSettlementActivityRewardAccrualRequest, NeighborhoodSettlementReceiptDto>(HttpMethod.Post,
            활동정산Navigation.DetailApi(id) + "/activity-reward-accruals", request, "성과금 발생 내역 기록", cancellationToken: ct);
}
