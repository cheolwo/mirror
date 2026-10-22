using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

public sealed record RoleWorkspaceField(string Label, string Value);
public sealed record RoleWorkspaceSection(string Title, IReadOnlyList<RoleWorkspaceField> Fields);
/// <summary>펼치지 않아도 현재 판단·수행에 필요한 목적지와 짧은 핵심 정보입니다.</summary>
public sealed record RoleWorkspaceSummary(string DestinationLabel, string Destination,
    IReadOnlyList<RoleWorkspaceField>? Metrics = null, RoleWorkspaceField? Request = null,
    IReadOnlyList<string>? ExperienceLines = null);
public sealed record RoleWorkspaceAction(string Key, string Label, string? Route = null,
    bool IsPrimary = false, bool Enabled = true, string? DisabledReason = null, bool RequiresConfirmation = true);
public sealed record RoleWorkspaceItem(string Id, string Title, string Status, string? Subtitle = null,
    IReadOnlyList<RoleWorkspaceSection>? Sections = null, IReadOnlyList<RoleWorkspaceAction>? Actions = null,
    IReadOnlyList<NeighborhoodMapMarker>? Markers = null, IReadOnlyList<NeighborhoodMapRoute>? Routes = null,
    bool IsCurrent = false, RoleWorkspaceSummary? Summary = null);
public sealed record RoleWorkspaceSnapshot(string RoleKey, IReadOnlyList<RoleWorkspaceItem> Items,
    string? Message = null, string? SelectedId = null, IReadOnlyList<RoleWorkspaceAction>? EmptyActions = null);
public sealed record RoleWorkspaceIdentity(string? OwnerId, long Revision, bool IsAuthenticated);
public sealed record RoleWorkspaceLocation(double Latitude, double Longitude, double? AccuracyMeters, DateTimeOffset MeasuredAt);

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.Contract,
    "공통 역할 셸이 조회·행동·초기화를 위임하는 포트",
    StepKey = "contract.workspace-port", FlowOrder = 190, ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "역할별 구현의 내부 저장소·권한을 소유하지 않는 호출 계약이다.")]
public interface IRoleWorkspaceAdapter
{
    string RoleKey { get; }
    /// <summary>같은 인증 역할 안의 업무 구분입니다. 생략하면 기존 역할의 기본 업무를 사용합니다.</summary>
    string? BusinessCode => null;
    Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken);
    Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken);
    void Clear();
}
/// <summary>공통 셸은 역할의 내부 구현 대신 미확정 요청과 조회 수명 계약만 소비합니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.Contract,
    "미확정 행동과 정본 조회 복구를 공통 셸에 제공하는 포트",
    StepKey = "contract.recovery-port", FlowOrder = 191, ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "구현체별 복구 판단을 노출하며 재전송·영속 저장 자체를 수행하지 않는다.")]
public interface IRoleWorkspaceRecovery
{
    bool StartStateUnconfirmed { get; }
    bool PreferenceStateUnconfirmed { get; }
    bool BlocksAction(string actionKey);
    string? RecordUnconfirmedAction(string actionKey);
    RoleWorkspaceRecoveryResult ResolveCanonicalActions();
    void ClearTransientSnapshot();
}
public sealed record RoleWorkspaceRecoveryResult(IReadOnlyList<string> CompletedActions, bool OutcomeConfirmed,
    string? Notice = null, string? ActionKey = null);
public interface IRoleWorkspaceAccess
{
    event Action? Changed;
    RoleWorkspaceIdentity GetIdentity(string roleKey);
    Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default);
    Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default);
    Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default);
}
public interface IRoleWorkspaceApi
{
    Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken);
    Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken);
    Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken);
    async Task AcknowledgeOfferPresentationAsync(string offerId, int recommendationRound, CancellationToken cancellationToken)
        => _ = await PutAsync<object>("food-driver",
            $"api/v1/driver/operational-dispatch/offers/{Uri.EscapeDataString(offerId)}/receipt",
            new Ssalddel.Contracts.Common.Dispatch.운영배차제안표시확인요청 { 추천라운드 = recommendationRound }, cancellationToken);
    Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken);
}
public interface IRoleWorkspaceLocationProvider
{
    Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken);
    /// <summary>자동 갱신에서는 권한을 요청하지 않고 이미 허용된 위치만 확인합니다.</summary>
    Task<RoleWorkspaceLocation?> GetCurrentWithoutPermissionPromptAsync(CancellationToken cancellationToken)
        => Task.FromResult<RoleWorkspaceLocation?>(null);
}
public sealed class RoleWorkspaceAccessException(int statusCode, string message, string? errorCode = null, string? responseBody = null,
    Guid? rejectedWorkStartRequestId = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? ErrorCode { get; } = errorCode;
    public string? ResponseBody { get; } = responseBody;
    public Guid? RejectedWorkStartRequestId { get; } = rejectedWorkStartRequestId;
}
