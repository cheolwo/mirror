using System.Text.Json;

namespace FDriverApp.Services;

/// <summary>기존 기사 SecureStorage 경계를 사용하되 음식 요청 저장 키와 분리합니다.</summary>
public sealed class 살뜰마트배송SecurePendingStore : I살뜰마트배송PendingStore
{
    private const string Key = "ssalddel.fdriver.mart-pending.v1";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<살뜰마트배송미확인요청?> LoadAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try { var value = await ReadAsync(); token.ThrowIfCancellationRequested(); return value; }
        finally { _gate.Release(); }
    }
    public async Task SaveAsync(살뜰마트배송미확인요청 request, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested(); Validate(request);
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(request, JsonOptions));
            token.ThrowIfCancellationRequested();
        }
        finally { _gate.Release(); }
    }
    public async Task ClearAsync(string ownerId, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            if ((await ReadAsync())?.OwnerId == ownerId && !Microsoft.Maui.Storage.SecureStorage.Default.Remove(Key))
                throw new IOException("이전 요청 기록을 지우지 못했습니다.");
        }
        finally { _gate.Release(); }
    }
    private static async Task<살뜰마트배송미확인요청?> ReadAsync()
    {
        var text = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(Key);
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = JsonSerializer.Deserialize<살뜰마트배송미확인요청>(text, JsonOptions)
            ?? throw new InvalidDataException("이전 요청을 확인하지 못했습니다.");
        Validate(value); return value;
    }
    private static void Validate(살뜰마트배송미확인요청 value)
    {
        if (string.IsNullOrWhiteSpace(value.OwnerId) || value.RequestId == Guid.Empty
            || string.IsNullOrWhiteSpace(value.OfferId) || value.OfferId.Length > 200
            || !Enum.IsDefined(value.Action) || value.Revision < 0 || value.LabelRevision < 0
            || value.RecommendationRound < 0
            || value.Action == 마트배송행동.Pickup && (value.CodeHash?.Length != 64 || !value.CodeHash.All(Uri.IsHexDigit)))
            throw new InvalidDataException("이전 요청을 확인하지 못했습니다.");
    }
}
