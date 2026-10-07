using System.Text.Json;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace OrdererApp.Services.Security;

public sealed class OrdererFoodOrderPendingSubmissionStore : IFoodOrderPendingSubmissionStore
{
    private const string Key = "ssalddel.orderer.food.pending-submission.v1";
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await ReadAsync(); }
        finally { gate.Release(); }
    }
    private static async Task<FoodOrderPendingSubmission?> ReadAsync()
    {
        var json = await SecureStorage.Default.GetAsync(Key);
        return string.IsNullOrWhiteSpace(json) ? null : FoodOrderPendingSubmissionSerialization.Deserialize(json);
    }
    public async Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { await SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(snapshot)); }
        finally { gate.Release(); }
    }
    public async Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { if ((await ReadAsync())?.Request.클라이언트요청Id == requestId) SecureStorage.Default.Remove(Key); }
        finally { gate.Release(); }
    }
}
