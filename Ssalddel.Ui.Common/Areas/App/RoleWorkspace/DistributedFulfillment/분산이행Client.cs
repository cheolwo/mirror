using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>기존 호스트의 로그인 수명과 실제 API를 연결하며 별도 계정이나 예시 성공을 만들지 않습니다.</summary>
public interface I분산이행Transport : IDisposable
{
    event Action? Changed;
    RoleWorkspaceIdentity Identity(string roleKey);
    Task InitializeAsync(string roleKey, CancellationToken ct);
    Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct);
    Task<T?> PostAsync<TRequest, T>(string roleKey, string path, TRequest body, CancellationToken ct);
}

public sealed class 분산이행RoleTransport : I분산이행Transport
{
    private readonly IRoleWorkspaceApi api;
    private readonly IRoleWorkspaceAccess access;
    public 분산이행RoleTransport(IRoleWorkspaceApi api, IRoleWorkspaceAccess access)
    { this.api = api; this.access = access; access.Changed += Notify; }
    public event Action? Changed;
    public RoleWorkspaceIdentity Identity(string roleKey) => access.GetIdentity(roleKey);
    public Task InitializeAsync(string roleKey, CancellationToken ct) => access.EnsureInitializedAsync(roleKey, ct);
    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct) => api.GetAsync<T>(roleKey, path, ct);
    public Task<T?> PostAsync<TRequest, T>(string roleKey, string path, TRequest body, CancellationToken ct)
        => api.PostAsync<T>(roleKey, path, body!, ct);
    private void Notify() => Changed?.Invoke();
    public void Dispose() => access.Changed -= Notify;
}

/// <summary>전문 앱의 보호된 JSON client와 기존 인증 원장을 그대로 사용합니다.</summary>
public sealed class 분산이행JsonTransport(
    ISsalddelJsonApiClient api,
    Func<RoleWorkspaceIdentity> identity,
    Func<CancellationToken, Task> initialize) : I분산이행Transport
{
    public event Action? Changed;
    private readonly CancellationTokenSource identityWatch = new();
    private RoleWorkspaceIdentity? observed;
    private bool watching;
    private bool disposed;
    public RoleWorkspaceIdentity Identity(string roleKey) => identity();
    public async Task InitializeAsync(string roleKey, CancellationToken ct)
    {
        await initialize(ct); ct.ThrowIfCancellationRequested();
        if (!watching && !disposed) { watching = true; observed = identity(); _ = WatchIdentityAsync(); }
    }
    private async Task WatchIdentityAsync()
    {
        // 기존 native 세션에는 Changed가 없는 호스트도 있어 메모리의 사용자·판본만 관찰합니다. HTTP 조회는 하지 않습니다.
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(identityWatch.Token))
            {
                var current = identity();
                if (current != observed) { observed = current; Changed?.Invoke(); }
            }
        }
        catch (OperationCanceledException) when (identityWatch.IsCancellationRequested) { }
    }
    public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
        => await api.GetAsync<T>(path, "분산 배송 업무 조회", allowNotFound: false, cancellationToken: ct)
           ?? throw new InvalidOperationException("업무 조회 응답을 확인하지 못했습니다.");
    public Task<T?> PostAsync<TRequest, T>(string roleKey, string path, TRequest body, CancellationToken ct)
        => api.SendAsync<TRequest, T>(HttpMethod.Post, path, body, "분산 배송 업무 처리", cancellationToken: ct);
    public void NotifyAuthenticationChanged() => Changed?.Invoke();
    public void Dispose() { if (disposed) return; disposed = true; identityWatch.Cancel(); identityWatch.Dispose(); }
}

/// <summary>상태·권한·수량·방문 순서는 서버 계약이 소유하고 client는 인증된 경로만 소비합니다.</summary>
public sealed class 분산이행Client(I분산이행Transport transport, string roleKey)
{
    public I분산이행Transport Transport { get; } = transport;
    public string RoleKey { get; } = 분산이행Navigation.ValidateRole(roleKey);
    public string ServerRole => RoleKey switch
    { "cargo-driver" => "driver", "operator" => "warehouse", _ => RoleKey };
    private string Path(string suffix) => DistributedFulfillmentCodes.BaseRoute + "/" + suffix
        + "?roleKey=" + Uri.EscapeDataString(ServerRole);
    public Task<DistributedWorkspaceDto> WorkspaceAsync(CancellationToken ct)
        => Transport.GetAsync<DistributedWorkspaceDto>(RoleKey, Path("workspace"), ct);
    public Task<DistributedWaveDto> WaveAsync(string id, CancellationToken ct)
        => Transport.GetAsync<DistributedWaveDto>(RoleKey, Path("waves/" + Segment(id)), ct);
    public Task<DistributedRootOrderCandidatesDto> RootOrderCandidatesAsync(string waveId, string cargoId, CancellationToken ct)
        => Transport.GetAsync<DistributedRootOrderCandidatesDto>(RoleKey,
            Path("waves/" + Segment(waveId) + "/order-ledger-candidates") + "&cargoRequestId=" + Segment(cargoId), ct);
    public Task<DistributedReceiptDto> ReceiptAsync(Guid requestId, CancellationToken ct)
        => Transport.GetAsync<DistributedReceiptDto>(RoleKey, Path("receipts/" + requestId.ToString("D")), ct);
    public Task<DistributedPhotoEvidenceDto?> UploadPhotoAsync(DistributedPhotoUploadRequest request, CancellationToken ct)
        => Transport.PostAsync<DistributedPhotoUploadRequest, DistributedPhotoEvidenceDto>(RoleKey, Path("evidence"), request, ct);
    public Task<DistributedPhotoEvidenceDto> PhotoByUploadAsync(Guid uploadRequestId, CancellationToken ct)
        => Transport.GetAsync<DistributedPhotoEvidenceDto>(RoleKey, Path("evidence/uploads/" + uploadRequestId.ToString("D")), ct);
    public Task<DistributedCommandResponse?> SendAsync(string suffix, DistributedCommandRequest body, CancellationToken ct)
    {
        if (body.RequestId == Guid.Empty || body.ExpectedRevision < 0)
            throw new InvalidOperationException("업무 요청 번호와 판본을 확인해 주세요.");
        if (suffix.Split('/').Any(part => RoleWorkspaceNavigation.StableId(part) is null || part is "." or ".."))
            throw new InvalidOperationException("업무 처리 경로를 확인해 주세요.");
        // 실제 DTO 형식을 유지해 기존 요청 암호화와 서버 계약을 소비합니다.
        return body switch
        {
            DistributedSiteCreateRequest request => Post(request),
            DistributedConsentRequest request => Post(request),
            DistributedLocationCreateRequest request => Post(request),
            DistributedExistingLotRequest request => Post(request),
            DistributedInboundPlanRequest request => Post(request),
            DistributedInboundReceiptRequest request => Post(request),
            DistributedWaveCreateRequest request => Post(request),
            DistributedOrderRequest request => Post(request),
            DistributedOrderLedgerLinkRequest request => Post(request),
            DistributedRoundPlanRequest request => Post(request),
            DistributedRoundAcceptRequest request => Post(request),
            DistributedPickRequest request => Post(request),
            DistributedDeliveryRequest request => Post(request),
            DistributedCancelRequest request => Post(request),
            DistributedReturnRequest request => Post(request),
            DistributedAcknowledgementRequest request => Post(request),
            DistributedStorageAgreementRequest request => Post(request),
            DistributedStorageAgreementConsentRequest request => Post(request),
            DistributedInboundReceiptBatchRequest request => Post(request),
            DistributedPickBatchRequest request => Post(request),
            DistributedDeliveryBatchRequest request => Post(request),
            DistributedReturnBatchRequest request => Post(request),
            _ when body.GetType() == typeof(DistributedCommandRequest) => Post(body),
            _ => throw new InvalidOperationException("지원하는 업무 요청을 확인해 주세요.")
        };
        Task<DistributedCommandResponse?> Post<T>(T request)
            => Transport.PostAsync<T, DistributedCommandResponse>(RoleKey, Path(suffix), request, ct);
    }
    public static string Segment(string value) => value is not "." and not ".." && RoleWorkspaceNavigation.StableId(value) is { } id
        ? Uri.EscapeDataString(id) : throw new InvalidOperationException("업무 대상을 확인해 주세요.");
}
