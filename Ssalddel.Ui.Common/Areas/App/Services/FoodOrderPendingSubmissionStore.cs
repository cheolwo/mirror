using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public sealed record FoodOrderPendingSubmission(string OwnerId, 음식주문등록요청 Request, DateTime CreatedAtUtc);

public static class FoodOrderPendingSubmissionSerialization
{
    public static FoodOrderPendingSubmission Deserialize(string json)
    {
        var snapshot = JsonSerializer.Deserialize<FoodOrderPendingSubmission>(json);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.OwnerId)
            || snapshot.Request is null || snapshot.Request.클라이언트요청Id == Guid.Empty
            || snapshot.Request.상품목록 is null || snapshot.Request.상품목록.Any(x => x is null))
            throw new JsonException("저장된 주문 제출 정보를 확인하지 못했습니다.");
        return snapshot;
    }
}

public interface IFoodOrderPendingSubmissionStore
{
    Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default);
    Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default);
}

/// <summary>웹에서는 현재 탭 수명에 한정합니다. 모바일은 보안 저장소 adapter를 등록합니다.</summary>
public sealed class BrowserFoodOrderPendingSubmissionStore(IJSRuntime js) : IFoodOrderPendingSubmissionStore
{
    private const string Key = "ssalddel.food.pending-submission.v1";
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await ReadAsync(cancellationToken); }
        finally { gate.Release(); }
    }
    private async Task<FoodOrderPendingSubmission?> ReadAsync(CancellationToken cancellationToken)
    {
        var json = await js.InvokeAsync<string?>("sessionStorage.getItem", cancellationToken, Key);
        return string.IsNullOrWhiteSpace(json) ? null : FoodOrderPendingSubmissionSerialization.Deserialize(json);
    }
    public async Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { await js.InvokeVoidAsync("sessionStorage.setItem", cancellationToken, Key, JsonSerializer.Serialize(snapshot)); }
        finally { gate.Release(); }
    }
    public async Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if ((await ReadAsync(cancellationToken))?.Request.클라이언트요청Id == requestId)
                await js.InvokeVoidAsync("sessionStorage.removeItem", cancellationToken, Key);
        }
        finally { gate.Release(); }
    }
}
