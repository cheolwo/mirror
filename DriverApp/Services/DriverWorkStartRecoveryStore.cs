using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DriverApp.Services;

/// <summary>요청 식별값·공개 선택·시작 전 수신 의사 판본만 저장한다. GPS/주소/인증값은 저장하지 않는다.</summary>
public sealed record DriverWorkStartRecoveryRecord(
    string OwnerId, Guid ClientRequestId, DateTime CreatedAtUtc,
    bool CommunityPublicationRequested, bool CoarseLocationConsent,
    Guid? RejectedClientRequestId = null, int? RejectionStatusCode = null,
    bool ReceptionIntentObserved = false, DateTimeOffset? ReceptionIntentObservedAtUtc = null,
    string? ReceptionIntentVersionToken = null);

public interface IDriverWorkStartRecoveryStore
{
    Task<DriverWorkStartRecoveryRecord?> LoadAsync(string ownerId, CancellationToken cancellationToken);
    Task SaveAsync(DriverWorkStartRecoveryRecord value, CancellationToken cancellationToken);
    Task RemoveAsync(string ownerId, CancellationToken cancellationToken);
}

/// <summary>The app supplies its encrypted platform store; tests use an isolated in-memory store.</summary>
public sealed class DriverWorkStartRecoveryStore(
    Func<string, Task<string?>> read,
    Func<string, string, Task> write,
    Action<string> remove) : IDriverWorkStartRecoveryStore
{
    private static string Key(string owner)
        => "driver.work-start.recovery.r75." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner)));

    public async Task<DriverWorkStartRecoveryRecord?> LoadAsync(string ownerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await read(Key(ownerId));
        cancellationToken.ThrowIfCancellationRequested();
        if (json is null) return null;
        var record = JsonSerializer.Deserialize<DriverWorkStartRecoveryRecord>(json);
        if (record is null || record.ClientRequestId == Guid.Empty || !string.Equals(record.OwnerId, ownerId, StringComparison.Ordinal))
            throw new InvalidOperationException("저장된 운행 시작 요청을 확인하지 못했습니다. 새 요청을 보내지 않았습니다.");
        return record;
    }

    public async Task SaveAsync(DriverWorkStartRecoveryRecord value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await write(Key(value.OwnerId), JsonSerializer.Serialize(value));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task RemoveAsync(string ownerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        remove(Key(ownerId));
        return Task.CompletedTask;
    }
}
