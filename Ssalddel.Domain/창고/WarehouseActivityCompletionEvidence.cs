using System.Text.Json;

namespace 살뜰.도메인.창고;

/// <summary>작업 완료와 같은 저장에서 보존하는 비공개 업무 사실입니다. 합의·보수 발생·검수·송금이 아닙니다.</summary>
public sealed record WarehouseActivityCompletionEvidence
{
    public int Version { get; init; } = 1;
    public long TaskId { get; init; }
    public string TaskKey { get; init; } = "";
    public string WorkKind { get; init; } = "";
    public string OrderReference { get; init; } = "";
    public long OutboundId { get; init; }
    public long InventoryItemId { get; init; }
    public long WarehouseId { get; init; }
    public int Quantity { get; init; }
    public string ActorUserId { get; init; } = "";
    /// <summary>실제로 시작 명령이 관측된 시각입니다. 완료시각으로 채워지는 기존 포장 표시값을 복사하지 않습니다.</summary>
    public DateTime? StartedAtUtc { get; init; }
    /// <summary>피킹은 기록된 시작, 포장은 같은 출고의 최초 피킹 시작입니다. 실제 포장 시작을 추정하지 않습니다.</summary>
    public DateTime? AgreementDeadlineAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; init; }
    public string? PackingBatchKey { get; init; }
    public int? PackingBatchQuantity { get; init; }
    public IReadOnlyList<WarehousePackingBatchShare> PackingBatchShares { get; init; } = [];
    // 현재 일반 창고 완료 화면은 사진을 입력받지 않습니다. 사진 없는 작업에 사진 근거를 만들지 않습니다.
    public IReadOnlyList<string> PhotoEvidenceReferences { get; init; } = [];

    public static string Capture(피킹포장작업 task, string actor, DateTime completedAtUtc,
        string? packingBatchKey = null, int? packingBatchQuantity = null,
        IReadOnlyList<WarehousePackingBatchShare>? packingShares = null, DateTime? agreementDeadlineAtUtc = null)
        => JsonSerializer.Serialize(new WarehouseActivityCompletionEvidence
        {
            TaskId = task.Id, TaskKey = task.작업Key, WorkKind = task.작업유형,
            OrderReference = task.주문참조번호, OutboundId = task.출고예정Id ?? 0,
            InventoryItemId = task.입고상품Id ?? 0, WarehouseId = task.창고Id, Quantity = task.수량,
            ActorUserId = actor, StartedAtUtc = task.작업유형 == 피킹포장작업유형.피킹 && task.시작일시Utc.HasValue
                ? AsUtc(task.시작일시Utc.Value) : null,
            AgreementDeadlineAtUtc = agreementDeadlineAtUtc.HasValue ? AsUtc(agreementDeadlineAtUtc.Value)
                : task.작업유형 == 피킹포장작업유형.피킹 && task.시작일시Utc.HasValue ? AsUtc(task.시작일시Utc.Value) : null,
            CompletedAtUtc = AsUtc(completedAtUtc), PackingBatchKey = packingBatchKey,
            PackingBatchQuantity = packingBatchQuantity, PackingBatchShares = packingShares ?? []
        });

    public static WarehouseActivityCompletionEvidence? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 65_536) return null;
        try { return JsonSerializer.Deserialize<WarehouseActivityCompletionEvidence>(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>SQL datetime(6)과 보호 JSON이 같은 시각을 보존하도록 UTC microsecond 정밀도로 동결합니다.</summary>
    public static DateTime NormalizeUtc(DateTime value)
        => new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);

    private static DateTime AsUtc(DateTime value) => NormalizeUtc(value);
}

public sealed record WarehousePackingBatchShare(long TaskId, int Quantity);
