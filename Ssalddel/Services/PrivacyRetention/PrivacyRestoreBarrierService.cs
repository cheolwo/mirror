namespace Ssalddel.Services.PrivacyRetention;

/// <summary>Singleton ledger stores consult the durable manifest without capturing scoped business/SQL services.</summary>
public sealed class 개인정보복원차단Service(I개인정보보존Store store) : I개인정보복원차단Service
{
    public async Task<bool> 복원허용Async(string sourceCode, string recordId, CancellationToken ct = default)
        => !await store.HasDeletionAsync(sourceCode, recordId, ct);
}
