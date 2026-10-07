using System.Text.Json;
using Ssalddel.Contracts.Common.Education;
using Ssalddel.Domain.Community;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;

namespace Ssalddel.Services.Community;

public interface I커뮤니티원장상태이벤트Service
{
    Task 저장이벤트기록Async(
        커뮤니티원장Dto 원장,
        string updatedBy,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task 상태변경이벤트기록Async(
        커뮤니티원장상태변경요청 request,
        커뮤니티원장Dto 원장,
        string updatedBy,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class 커뮤니티원장상태이벤트Service : I커뮤니티원장상태이벤트Service
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SsalddelContext _db;

    public 커뮤니티원장상태이벤트Service(SsalddelContext db)
    {
        _db = db;
    }

    public Task 저장이벤트기록Async(
        커뮤니티원장Dto 원장,
        string updatedBy,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
        => 기록Async(
            원장,
            커뮤니티원장상태이벤트유형.저장,
            이전상태: 원장.상태이력.LastOrDefault()?.이전상태,
            상태: 원장.상태,
            현재단계Key: 원장.현재단계Key,
            변경사유: "원장 저장",
            updatedBy,
            eventId,
            occurredAtUtc,
            cancellationToken);

    public Task 상태변경이벤트기록Async(
        커뮤니티원장상태변경요청 request,
        커뮤니티원장Dto 원장,
        string updatedBy,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
        => 기록Async(
            원장,
            커뮤니티원장상태이벤트유형.상태변경,
            request.이전상태,
            request.상태,
            request.현재단계Key ?? 원장.현재단계Key,
            request.메모,
            updatedBy,
            eventId,
            occurredAtUtc,
            cancellationToken);

    private async Task 기록Async(
        커뮤니티원장Dto 원장,
        string eventType,
        string? 이전상태,
        string 상태,
        string? 현재단계Key,
        string? 변경사유,
        string updatedBy,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(원장.원장Id))
        {
            throw new InvalidOperationException("커뮤니티 원장 상태 이벤트에는 원장Id가 필요합니다.");
        }
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new InvalidOperationException("커뮤니티 원장 상태 이벤트에는 EventId가 필요합니다.");
        }

        var normalizedEventId = eventId.Trim();
        if (await _db.커뮤니티원장상태이벤트
                .AsNoTracking()
                .AnyAsync(x => x.EventId == normalizedEventId, cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var snapshot = BuildSnapshot(원장, 상태, 현재단계Key);

        _db.커뮤니티원장상태이벤트.Add(new 커뮤니티원장상태이벤트
        {
            EventId = normalizedEventId,
            커뮤니티원장Id = Clean(원장.원장Id),
            커뮤니티Id = Clean(원장.커뮤니티Id),
            원장템플릿Key = Clean(원장.원장템플릿Key),
            EventType = eventType,
            이전상태 = CleanNullable(이전상태),
            상태 = Clean(상태),
            현재단계Key = CleanNullable(현재단계Key),
            변경사유 = IsDeliveryLedger(원장) ? null : CleanNullable(변경사유),
            UpdatedBy = Clean(updatedBy),
            CorrelationId = CleanNullable(원장.외부참조.TryGetValue("CorrelationId", out var correlationId) ? correlationId : null),
            SnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions),
            OccurredAtUtc = occurredAtUtc == default ? now : occurredAtUtc,
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static object BuildSnapshot(커뮤니티원장Dto ledger, string state, string? currentStep)
    {
        if (IsDeliveryLedger(ledger) || string.Equals(
                ledger.원장템플릿Key,
                현장체험활동원장상수.원장템플릿Key,
                StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                ledger.원장Id,
                ledger.커뮤니티Id,
                ledger.원장템플릿Key,
                상태 = state,
                현재단계Key = currentStep,
                블록수 = ledger.블록목록.Count,
                참여자수 = ledger.참여자목록.Count,
                개인정보비식별투영 = true,
                ledger.수정시각Utc
            };
        }

        return new
        {
            ledger.원장Id,
            ledger.커뮤니티Id,
            ledger.원장템플릿Key,
            ledger.제목,
            ledger.원함,
            상태 = state,
            현재단계Key = currentStep,
            대상OsCode = ledger.대상OsCode,
            대상OsName = ledger.대상OsName,
            블록수 = ledger.블록목록.Count,
            참여자수 = ledger.참여자목록.Count,
            ledger.외부참조,
            ledger.확장속성,
            ledger.수정시각Utc
        };
    }

    // State events need lifecycle facts, not private delivery instructions or arbitrary ledger extensions.
    // Applying this before persistence also prevents a delayed state-event writer from restoring a purged snapshot.
    private static bool IsDeliveryLedger(커뮤니티원장Dto ledger)
        => ledger.원장템플릿Key is "food-order" or "food-delivery" or "cargo-transport";

    private static string Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? CleanNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
