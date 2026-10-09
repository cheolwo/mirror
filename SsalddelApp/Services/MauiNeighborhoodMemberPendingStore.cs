using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace SsalddelApp.Services;

// 같은 계정의 미확정 명령만 암호화해 재사용합니다. 촬영 원본·Base64 사진은 이 저장소에 넣지 않습니다.
public sealed class MauiNeighborhoodMemberPendingStore(IAuthSession session) : INeighborhoodMemberPendingStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<NeighborhoodMemberPendingOperation?> LoadAsync(string owner, CancellationToken ct)
    {
        var revision = RequireOwner(owner, ct);
        await _gate.WaitAsync(ct);
        try
        {
            var json = await SecureStorage.Default.GetAsync(Key(owner));
            RequireOwner(owner, ct, revision);
            if (string.IsNullOrWhiteSpace(json)) return null;
            var operation = JsonSerializer.Deserialize<NeighborhoodMemberPendingOperation>(json, Json)
                ?? throw new InvalidDataException("이전 배송 요청을 확인하지 못했습니다.");
            if (operation.Owner != owner) throw new InvalidDataException("이전 배송 요청의 계정을 확인하지 못했습니다.");
            Validate(operation);
            return operation;
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(NeighborhoodMemberPendingOperation operation, CancellationToken ct)
    {
        var revision = RequireOwner(operation.Owner, ct);
        Validate(operation);
        await _gate.WaitAsync(ct);
        try
        {
            RequireOwner(operation.Owner, ct, revision);
            await SecureStorage.Default.SetAsync(Key(operation.Owner), JsonSerializer.Serialize(operation, Json));
            RequireOwner(operation.Owner, ct, revision);
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(string owner, CancellationToken ct)
    {
        var revision = RequireOwner(owner, ct);
        await _gate.WaitAsync(ct);
        try { RequireOwner(owner, ct, revision); SecureStorage.Default.Remove(Key(owner)); }
        finally { _gate.Release(); }
    }

    private long RequireOwner(string owner, CancellationToken ct, long? revision = null)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 200 || !session.IsLoggedIn
            || session.UserId != owner || revision is not null && session.SessionRevision != revision)
            throw new OperationCanceledException("현재 로그인한 계정의 배송 요청만 확인할 수 있습니다.", ct);
        return session.SessionRevision;
    }

    private static string Key(string owner)
        => "ssalddel.member.delivery.pending.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner))).ToLowerInvariant();

    private static void Validate(NeighborhoodMemberPendingOperation operation)
    {
        if (operation.ClientRequestId == Guid.Empty || operation.ExpectedRevision < 0
            || string.IsNullOrWhiteSpace(operation.Kind) || operation.Kind.Length > 80
            || string.IsNullOrWhiteSpace(operation.Target) || operation.Target.Length > 200
            || string.IsNullOrWhiteSpace(operation.PayloadJson) || operation.PayloadJson.Length > 65536)
            throw new InvalidDataException("이전 배송 요청 정보를 확인해 주세요.");
        using var payload = JsonDocument.Parse(operation.PayloadJson);
        if (ContainsPhoto(payload.RootElement)) throw new InvalidDataException("사진 원본은 배송 요청 복구 저장소에 보관할 수 없습니다.");
    }

    private static bool ContainsPhoto(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return value.EnumerateObject().Any(property => property.Name.Contains("base64", StringComparison.OrdinalIgnoreCase)
                || ContainsPhoto(property.Value));
        return value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(ContainsPhoto);
    }
}
