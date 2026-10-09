using System.Text.Json;
using Ssalddel.Client.Infrastructure.Notifications;

namespace SsalddelApp.Services;

public sealed class MemberDeliveryPushStore : IMemberDeliveryPushStateStore
{
    private const string Key = "ssalddel.member.delivery.push.v1";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<MemberDeliveryPushState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await ReadAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(Func<MemberDeliveryPushState, MemberDeliveryPushState> update,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = update(await ReadAsync(cancellationToken));
            Validate(state);
            cancellationToken.ThrowIfCancellationRequested();
            await SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(state, Json));
        }
        finally { _gate.Release(); }
    }

    private async Task<MemberDeliveryPushState> ReadAsync(CancellationToken cancellationToken)
    {
        var json = await SecureStorage.Default.GetAsync(Key);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(json))
        {
            var state = JsonSerializer.Deserialize<MemberDeliveryPushState>(json, Json)
                ?? throw new InvalidDataException("알림 설치 정보를 확인하지 못했습니다.");
            Validate(state);
            return state;
        }
        var created = new MemberDeliveryPushState(Guid.NewGuid().ToString("N"));
        await SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(created, Json));
        return created;
    }

    private static void Validate(MemberDeliveryPushState state)
    {
        if (!Guid.TryParseExact(state.DeviceInstallationId, "N", out _)
            || state.PushToken?.Length > 2000 || state.RegisteredOwnerId?.Length > 200
            || state.Published.Count > 64 || state.RevocationPending && state.RegisteredOwnerId is null)
            throw new InvalidDataException("알림 설치 정보를 확인해 주세요.");
    }
}
