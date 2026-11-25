using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Explanation;

/// <summary>현재 업무 관계로 접근을 확인하고 저장 당시의 보호 사건만 읽는다. 배차를 재계산하지 않는다.</summary>
public sealed class CargoDispatchExplanationService(SsalddelContext db, IPersonalDataEncryptionService protection,
    ICargoDispatchFeedbackStore feedback, TimeProvider? clock = null) : ICargoDispatchExplanationService
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public async Task<CargoDispatchNoticeContext> GetNoticeContextAsync(string userId, string requestId,
        string evidenceId, CancellationToken ct = default)
    {
        // Recheck the recipient's current relation even when an old event ID is already known.
        var saved = await EvidenceAsync(userId, requestId, evidenceId, ct);
        var item = ToItem(evidenceId, saved, []);
        var basis = item.Basis!;
        return new()
        {
            EvidenceId = item.EvidenceId, RequestId = item.RequestId, OccurredAtUtc = item.OccurredAtUtc,
            RecommendationRound = item.RecommendationRound, MethodLabel = item.MethodLabel,
            Purpose = "preparation-only", TemplateVersion = "cargo-dispatch-notice.r1", Subject = "배차 추천 기록 안내",
            Basis = basis,
            Lines = new[] { "추천 저장 당시 확인한 기록입니다. 최초 접수 시점의 원본이나 AI의 모든 선택 원인을 뜻하지 않습니다.",
                    $"추천 저장 시각: {KoreaTime(item.OccurredAtUtc)}", item.MethodLabel }
                .Concat(item.Facts.Select(x => $"{x.Label}: {x.Value}"))
                .Concat(basis.Entries.Where(x => x.State != "missing").Select(x => $"공통 설명 · {x.Label}: {x.Explanation}"))
                .Concat(item.Limitations).ToArray()
        };
    }

    public async Task<CargoDispatchExplanationResponse> GetAsync(string userId, string requestId, CancellationToken ct = default)
    {
        var access = await AccessAsync(userId, requestId, ct);
        var events = db.운송이벤트.AsNoTracking()
            .Where(x => x.의뢰Id == requestId && x.이벤트타입 == CargoDispatchExplanationRecorder.EventType)
            .OrderByDescending(x => x.이벤트시각).ThenByDescending(x => x.Id).AsAsyncEnumerable();
        var items = new List<CargoDispatchExplanationItem>();
        await foreach (var row in events.WithCancellation(ct))
        {
            var saved = CargoDispatchExplanationCodec.Read(row, protection);
            if (!CanRead(access, saved, userId)) continue;
            var evidenceId = row.Id.ToString(CultureInfo.InvariantCulture);
            var receipts = await feedback.ListAsync(requestId, evidenceId, userId, ct);
            items.Add(ToItem(evidenceId, saved, receipts));
            // Apply the display limit after participant filtering, so another driver's later
            // rounds cannot hide this driver's own immutable history or turn it into "unrecorded".
            if (items.Count == 100) break;
        }
        return items.Count == 0 ? new() : new()
        {
            Status = "recorded", Notice = "추천이 저장된 당시의 접수 정보입니다. 현재 조건과 다를 수 있습니다.", Items = items
        };
    }

    public async Task<CargoDispatchFeedbackReceipt> SubmitFeedbackAsync(string userId, string requestId,
        CargoDispatchFeedbackRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Freeze caller-owned values before awaiting authorization or storage.
        var id = request.ClientRequestId;
        var evidenceId = request.EvidenceId;
        var category = request.CategoryCode;
        var comment = request.Comment ?? string.Empty;
        if (id == Guid.Empty || !CargoDispatchFeedbackCategories.All.Contains(category, StringComparer.Ordinal)
            || comment.Length > 500 || comment.Any(char.IsControl))
            throw new ArgumentException("CargoDispatchFeedbackInvalid");
        await EvidenceAsync(userId, requestId, evidenceId, ct);
        var stored = await feedback.InsertAsync(new(requestId, evidenceId, userId, id, category, comment,
            time.GetUtcNow().UtcDateTime, FeedbackId(evidenceId, userId, id)), ct);
        return stored.ToReceipt();
    }

    public async Task<CargoDispatchFeedbackReceipt?> GetFeedbackAsync(string userId, string requestId, string evidenceId,
        Guid clientRequestId, CancellationToken ct = default)
    {
        if (clientRequestId == Guid.Empty) throw new ArgumentException("CargoDispatchFeedbackInvalid");
        await EvidenceAsync(userId, requestId, evidenceId, ct);
        return (await feedback.GetAsync(requestId, evidenceId, userId, clientRequestId, ct))?.ToReceipt();
    }

    private async Task<CargoDispatchExplanationSnapshot> EvidenceAsync(string userId, string requestId,
        string evidenceId, CancellationToken ct)
    {
        var access = await AccessAsync(userId, requestId, ct);
        if (!long.TryParse(evidenceId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0
            || id.ToString(CultureInfo.InvariantCulture) != evidenceId)
            throw new ArgumentException("CargoDispatchExplanationIdentifierInvalid");
        var row = await db.운송이벤트.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.의뢰Id == requestId
            && x.이벤트타입 == CargoDispatchExplanationRecorder.EventType, ct)
            ?? throw new CargoDispatchExplanationNotFoundException();
        var saved = CargoDispatchExplanationCodec.Read(row, protection);
        if (!CanRead(access, saved, userId)) throw new UnauthorizedAccessException();
        return saved;
    }

    private async Task<Access> AccessAsync(string userId, string requestId, CancellationToken ct)
    {
        CargoDispatchExplanationCodec.ValidateIdentifier(userId, 450);
        CargoDispatchExplanationCodec.ValidateIdentifier(requestId, 200);
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct)
            ?? throw new CargoDispatchExplanationNotFoundException();
        if (request.화주Id == userId || request.주문자UserId == userId) return new(true);
        var now = time.GetUtcNow().UtcDateTime;
        var related = await db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId
            && x.배차업무유형 == 상태값.배차업무유형.용달운송
            && ((x.기사_운송자 == userId && (x.확정기사Id == null || x.확정기사Id == "" || x.확정기사Id == userId))
                || (x.확정기사Id == userId && (x.기사_운송자 == null || x.기사_운송자 == "" || x.기사_운송자 == userId))
                || (x.현재추천대상기사Id == userId && x.배차큐단계 == 상태값.배차큐단계.배차추천
                    && x.배차노출상태 == 상태값.배차노출상태.추천중 && x.추천만료시각 > now)), ct);
        if (related) return new(false);
        // An expired/rejected/reassigned offer does not erase the original recipient's own explanation.
        // Protected history may establish only that recipient's access, never another driver's records.
        var history = await db.운송이벤트.AsNoTracking().Where(x => x.의뢰Id == requestId
            && x.이벤트타입 == CargoDispatchExplanationRecorder.EventType).ToArrayAsync(ct);
        foreach (var row in history)
        {
            CargoDispatchExplanationSnapshot saved;
            try { saved = CargoDispatchExplanationCodec.Read(row, protection); }
            catch (CargoDispatchExplanationUnavailableException) { continue; } // Unreadable data cannot grant access.
            if (saved.DriverId == userId) return new(false);
        }
        throw new UnauthorizedAccessException();
    }

    private static bool CanRead(Access access, CargoDispatchExplanationSnapshot saved, string userId)
        => access.IsOwner || saved.DriverId == userId;

    private static CargoDispatchExplanationItem ToItem(string evidenceId, CargoDispatchExplanationSnapshot saved,
        IReadOnlyList<CargoDispatchFeedbackStored> myFeedback)
    {
        var basis = CargoDispatchEvidenceLibrary.Project(saved);
        var facts = new List<CargoDispatchExplanationFact>();
        if (saved.CargoWeightKg is { } weight) facts.Add(new("당시 접수 중량", weight.ToString("0.###", CultureInfo.InvariantCulture) + "kg"));
        if (saved.TransportMode is not null) facts.Add(new("당시 운송 방식", saved.TransportMode));
        if (saved.Temperature is not null) facts.Add(new("당시 온도 조건", saved.Temperature));
        if (saved.PickupDeadlineUtc is { } pickup) facts.Add(new("당시 상차 마감", KoreaTime(pickup)));
        if (saved.DropoffDeadlineUtc is { } dropoff) facts.Add(new("당시 하차 마감", KoreaTime(dropoff)));
        if (saved.DecisionFacts?.FullPlanFeasible is { } fullPlan)
            facts.Add(new("당시 수행계획", fullPlan ? "가능 확인" : "가능 미확인"));
        if (saved.DecisionFacts?.SegmentLoadingVerified is { } loading)
            facts.Add(new("당시 구간 적재", loading ? "가능 확인" : "가능 미확인"));
        if (saved.DecisionFacts?.OwnPickupOrder is { } pickupOrder)
            facts.Add(new("이번 상차 순서", pickupOrder.ToString(CultureInfo.InvariantCulture) + "번째 정차"));
        if (saved.DecisionFacts?.OwnDropoffOrder is { } dropoffOrder)
            facts.Add(new("이번 하차 순서", dropoffOrder.ToString(CultureInfo.InvariantCulture) + "번째 정차"));
        if (basis.Status is "recorded" or "partial")
        {
            // New records use their archived definition labels/values. The hard-coded legacy
            // projection above remains only for events that never linked a valid catalog.
            facts.Clear();
            facts.AddRange(basis.Entries.Where(x => x.State != "missing")
                .Select(x => new CargoDispatchExplanationFact(x.Label, x.Value!)));
        }
        return new()
        {
            EvidenceId = evidenceId, RequestId = saved.RequestId, RecommendationRound = saved.RecommendationRound,
            OccurredAtUtc = saved.OccurredAtUtc, DecisionKind = "recommendation",
            MethodLabel = saved.MethodCode.StartsWith("coordination/", StringComparison.Ordinal) ? "화물 조율 추천 저장"
                : saved.MethodCode.StartsWith("manual/", StringComparison.Ordinal) ? "수동 추천 저장" : "화물 배차 엔진 추천 저장",
            Facts = facts,
            Limitations = ["당시 접수 정보와 추천 저장 경로를 기록했습니다. 각 항목이 AI의 선택 원인이었다는 뜻은 아닙니다.",
                "이 설명은 현재 조건의 재계산, 배차 수락, 운송 완료 또는 정산 완료를 뜻하지 않습니다.",
                CargoDispatchEvidenceLibrary.CoverageNotice(basis),
                "공통 설명 판본은 알고리즘·모델의 버전이 아닙니다. 실제 알고리즘 버전·최초 접수 근거·전체 후보 비교는 이 기록에 연결되지 않았습니다."],
            CanSubmitFeedback = true,
            MyFeedback = myFeedback.Select(x => x.ToReceipt()).ToArray(), Basis = basis
        };
    }

    private static string KoreaTime(DateTime value) => new DateTimeOffset(CargoDispatchExplanationCodec.Utc(value))
        .ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm '(한국시간)'", CultureInfo.InvariantCulture);
    private static string FeedbackId(string evidenceId, string userId, Guid clientRequestId)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new[] { "cargo-dispatch-feedback-v1", evidenceId, userId, clientRequestId.ToString("D") }))));
    private sealed record Access(bool IsOwner);
}
