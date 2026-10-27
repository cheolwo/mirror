using System.Text.Json;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.도메인.설정;

namespace Ssalddel.Services.Community;

/// <summary>음식 주문 의도를 현재 원천에 다시 결속해 원장 투영을 처리합니다.</summary>
internal sealed class 음식주문원장동기화PayloadHandler(
    I음식마트원장Mongo동기화Service ledgerSync,
    ISsalddelFoodOrderStore? foodOrderStore)
{
    public async Task 처리Async(
        음식마트원장동기화Outbox item,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var snapshot = JsonSerializer.Deserialize<음식주문응답>(item.PayloadJson, jsonOptions)
                    ?? throw new InvalidOperationException("음식 주문 원장 동기화 payload가 비어 있습니다.");
        if (!string.Equals(snapshot.주문번호, item.원천Id, StringComparison.Ordinal))
            throw new InvalidOperationException("FoodOutboxSourceMismatch");
        // Operational DI supplies the authoritative store. The outbox only retains a minimal intent copy.
        var order = foodOrderStore is null ? snapshot : foodOrderStore.GetOrder(item.원천Id)
            ?? throw new InvalidOperationException("FoodOutboxSourceUnavailable");
        var ledger = await ledgerSync.음식주문동기화Async(order, item.변경자, cancellationToken);
        if (ledger is null)
        {
            throw new InvalidOperationException("음식 주문 Mongo 원장 동기화 결과가 없습니다.");
        }
    }
}
