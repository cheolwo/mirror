namespace FDriverApp.Services;

/// <summary>완료 목록과 선택 상세 사이의 화면 이동만 담당합니다.</summary>
public interface IFDriverCompletedDeliveryNavigator
{
    Task OpenListAsync(DateOnly? date = null);
    Task OpenDetailAsync(FDriverCompletedDeliveryNavigationTarget target);
    Task BackAsync();
    Task ReturnToWorkspaceAsync();
}

/// <summary>URL이나 화면 인계에 고객 정보 없이 완료 기록의 식별자만 전달합니다.</summary>
public sealed record FDriverCompletedDeliveryNavigationTarget(
    string SettlementId, DateOnly CompletionDateKst, string? ExpectedOrderNo = null,
    string? ExpectedAttemptId = null);
