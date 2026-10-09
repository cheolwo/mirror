using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace RoleWorkspacePreview;

/// <summary>실제 서버와 분리한 제품 화면 확인용 응답입니다.</summary>
public sealed class RootOrderSelectionPreviewTransport(string scenario) : I분산이행Transport
{
    public event Action? Changed { add { } remove { } }
    public event Action? Posted;
    public int PostCount { get; private set; }
    public string? LastRootId { get; private set; }
    public long? LastRootRevision { get; private set; }
    private int candidateCalls;
    private readonly Dictionary<Guid, DistributedReceiptDto> receipts = [];
    private readonly DistributedWaveDto wave = new()
    {
        WaveId = "preview-wave", Label = "오후 동네 배송", StatusCode = "Open", Revision = 5,
        AllowedActions = ["add-order"], DeliveryStartUtc = DateTime.UtcNow.AddHours(1), DeliveryEndUtc = DateTime.UtcNow.AddHours(3)
    };
    public RoleWorkspaceIdentity Identity(string roleKey) => new("preview-seller", 1, true);
    public Task InitializeAsync(string roleKey, CancellationToken ct) => Task.CompletedTask;
    public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (path.Contains("/order-ledger-candidates", StringComparison.Ordinal))
        {
            await Task.Delay(180, ct);
            if (scenario == "error" && ++candidateCalls == 1)
                throw new RoleWorkspaceAccessException(503, "원주문 목록을 확인하지 못했습니다. 다시 조회해 주세요.");
            if (scenario == "withdrawn")
                throw new RoleWorkspaceAccessException(403, "이 의뢰의 원주문을 조회할 권한이 없습니다.");
            var cargoId = path.Contains("preview-cargo-b", StringComparison.Ordinal) ? "preview-cargo-b" : "preview-cargo";
            var json = JsonSerializer.Serialize(new
            {
                WaveId = wave.WaveId, CargoRequestId = cargoId, CurrentUserId = "preview-seller", WaveRevision = wave.Revision,
                Candidates = scenario == "empty" ? Array.Empty<object>() : new object[]
                {
                    new { RootOrderLedgerId = "preview-root", Revision = 7L, Label = "생활 물품 주문", StatusCode = "초안" },
                    new { RootOrderLedgerId = "preview-root-b", Revision = 3L, Label = "이웃 공동 주문", StatusCode = "진행중" }
                }
            });
            return JsonSerializer.Deserialize<T>(json)!;
        }
        if (path.Contains("/receipts/", StringComparison.Ordinal))
        {
            var id = Guid.Parse(path.Split("/receipts/")[1].Split('?')[0]);
            return (T)(object)(receipts.GetValueOrDefault(id) ?? throw new RoleWorkspaceAccessException(404, "아직 접수되지 않았습니다."));
        }
        if (!path.Contains("/workspace?", StringComparison.Ordinal)) return (T)(object)wave;
        return (T)(object)new DistributedWorkspaceDto
        {
            CurrentUserId = "preview-seller", IsSimulation = true, Waves = [wave],
            CargoCandidates = [new("preview-cargo", "동네 물품 배송", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), wave.DeliveryStartUtc, wave.DeliveryEndUtc),
                new("preview-cargo-b", "두 번째 물품 배송", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), wave.DeliveryStartUtc, wave.DeliveryEndUtc)],
            Lots = [new("preview-lot", "preview-site", "preview-location", "preview-seller", "preview-seller", "BOOK-01", "lot-1", 1, 10, 10, 0, "판매자", "이웃 도서 세트")]
        };
    }
    public Task<T?> PostAsync<TRequest, T>(string roleKey, string path, TRequest body, CancellationToken ct)
    {
        if (body is not DistributedOrderRequest request) throw new InvalidOperationException("이 검토 화면의 접수 요청이 아닙니다.");
        if (scenario == "stale" && request.RootOrderLedgerId is not null)
            throw new RoleWorkspaceAccessException(409, "원주문이 변경되었습니다. 최신 원주문을 다시 선택해 주세요.");
        PostCount++; LastRootId = request.RootOrderLedgerId; LastRootRevision = request.ExpectedRootRevision;
        wave.Revision++;
        receipts[request.RequestId] = new(request.RequestId, "AddOrder", wave.WaveId, null, request.Lines.Sum(x => x.Quantity), wave.Revision, DateTime.UtcNow);
        Posted?.Invoke();
        return Task.FromResult((T?)(object)new DistributedCommandResponse(request.RequestId, wave.WaveId, wave.Revision, "Recorded", false));
    }
    public void Dispose() { }
}
