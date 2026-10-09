using System.Text.Json;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>사진 원본을 제외한 동일 계정의 미확정 명령을 보존합니다. 모바일 구현은 보호 저장소를 사용합니다.</summary>
public sealed record NeighborhoodMemberPendingOperation(string Owner, string Kind, string Target,
    Guid ClientRequestId, long ExpectedRevision, string PayloadJson, DateTime CreatedAtUtc);

public interface INeighborhoodMemberPendingStore
{
    Task<NeighborhoodMemberPendingOperation?> LoadAsync(string owner, CancellationToken ct);
    Task SaveAsync(NeighborhoodMemberPendingOperation operation, CancellationToken ct);
    Task RemoveAsync(string owner, CancellationToken ct);
}

public sealed class VolatileNeighborhoodMemberPendingStore : INeighborhoodMemberPendingStore
{
    private readonly Dictionary<string, NeighborhoodMemberPendingOperation> _items = [];
    public Task<NeighborhoodMemberPendingOperation?> LoadAsync(string owner, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); lock (_items) return Task.FromResult(_items.GetValueOrDefault(owner)); }
    public Task SaveAsync(NeighborhoodMemberPendingOperation operation, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); lock (_items) _items[operation.Owner] = operation; return Task.CompletedTask; }
    public Task RemoveAsync(string owner, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); lock (_items) _items.Remove(owner); return Task.CompletedTask; }
}

public sealed class NeighborhoodMemberDeliverySession(INeighborhoodMemberPendingStore store)
{
    private long _generation;
    private bool _loaded;
    private readonly SemaphoreSlim _storeGate = new(1, 1);
    public string? Owner { get; private set; }
    public NeighborhoodMemberPendingOperation? Pending { get; private set; }
    public bool Matches(string kind, string target) => Pending is { } value && value.Owner == Owner && value.Kind == kind && value.Target == target;
    public async Task BindAsync(string owner, CancellationToken ct)
    {
        if (Owner != owner) { Owner = owner; Pending = null; _loaded = false; ++_generation; }
        var generation = _generation;
        await _storeGate.WaitAsync(ct);
        try
        {
            if (_loaded || Owner != owner || generation != _generation) return;
            var result = await store.LoadAsync(owner, ct);
            if (Owner != owner || generation != _generation) return;
            Pending = result?.Owner == owner ? result : null; _loaded = true;
        }
        finally { _storeGate.Release(); }
    }
    public void Unbind() { Owner = null; Pending = null; _loaded = false; ++_generation; }
    public async Task<T> PrepareAsync<T>(string owner, string kind, string target, Guid requestId, long revision, T body, CancellationToken ct)
    {
        var generation = _generation;
        await _storeGate.WaitAsync(ct);
        try
        {
            if (Owner != owner || generation != _generation) throw new OperationCanceledException("계정이 변경되었습니다.");
            if (Pending is not null)
            {
                if (!Matches(kind, target)) throw new InvalidOperationException("이전에 요청한 업무의 처리 결과를 먼저 확인해 주세요.");
                return JsonSerializer.Deserialize<T>(Pending.PayloadJson) ?? throw new InvalidOperationException("원 요청을 확인하지 못했습니다.");
            }
            var operation = new NeighborhoodMemberPendingOperation(owner, kind, target, requestId, revision, JsonSerializer.Serialize(body), DateTime.UtcNow);
            // 저장 실패 시 API를 보내지 않습니다. 사진 원본은 이 경계를 통과하지 않습니다.
            await store.SaveAsync(operation, ct);
            if (Owner != owner || generation != _generation) throw new OperationCanceledException("계정이 변경되었습니다.");
            Pending = operation; _loaded = true; return body;
        }
        finally { _storeGate.Release(); }
    }
    public async Task ClearAsync(string owner, CancellationToken ct, Guid? expectedRequestId = null)
    {
        var generation = _generation;
        var requestId = expectedRequestId ?? Pending?.ClientRequestId;
        await _storeGate.WaitAsync(ct);
        try
        {
            if (Owner != owner || generation != _generation || Pending?.ClientRequestId != requestId) return;
            await store.RemoveAsync(owner, ct);
            if (Owner == owner && generation == _generation) Pending = null;
        }
        finally { _storeGate.Release(); }
    }
}
