using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Admin.Settlement;
using 살뜰.도메인.기사;

namespace Ssalddel.Application.Admin.Settlement;

/// <summary>HTTP에 노출하지 않는 서버 작업 포트. 승인된 운송 채무의 명세만 준비합니다.</summary>
public interface I배송료주간자동준비Service
{
    Task<주간명세자동준비결과> 준비Async(CancellationToken cancellationToken = default);
}

public sealed record 주간명세자동준비결과(bool Enabled, int PreparedBatches, int HeldLines, int Conflicts,
    int RevalidatedBatches = 0, int ReviewRequiredLines = 0);

public sealed partial class 배송료정산배치UseCase
{
    async Task<주간명세자동준비결과> I배송료주간자동준비Service.준비Async(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !options.Value.AutomaticPreparationEnabled)
            return new(false, 0, 0, 0);
        if (options.Value.WeekStartDay != DayOfWeek.Wednesday
            || options.Value.PayoutDay is not (DayOfWeek.Wednesday or DayOfWeek.Thursday)
            || options.Value.PayoutHourLocal is < 0 or > 23
            || options.Value.DefaultScheduleCode != 배송료정산배치Codes.주간
            || string.IsNullOrWhiteSpace(options.Value.PolicyRevision))
            throw new InvalidOperationException("주간 자동 명세의 정책·일정 설정이 유효하지 않습니다.");

        var cutoff = WeekStart(timeProvider.GetUtcNow().UtcDateTime);
        var mode = executionMode.Mode.ToString();
        var take = Math.Clamp(options.Value.AutomaticPreparationTake, 1, 2000);
        var prepared = 0; var held = 0; var conflicts = 0; var reviewed = 0; var rejected = 0;
        // 이미 편입된 준비/보류 명세도 오래된 검증부터 다시 검사한다. 새 승인·지급은 실행하지 않는다.
        var batches = await db.기사운송대금정산배치.AsNoTracking()
            .Where(x => x.실행모드코드 == mode && x.일정코드 == 배송료정산배치Codes.주간
                && x.기간종료Utc <= cutoff && x.생성관리자Id == "system:weekly-statement-preparation")
            .OrderBy(x => x.UpdatedAtUtc).ThenBy(x => x.Revision).ThenBy(x => x.Id).Take(take)
            .Select(x => new { x.Id, x.Revision }).ToArrayAsync(cancellationToken);
        foreach (var batch in batches)
        {
            var result = await RevalidateCoreAsync(batch.Id, new() { ExpectedRevision = batch.Revision }, cancellationToken);
            if (result.IsSuccess) { reviewed++; held += result.Value.HeldCount; }
            else conflicts++;
        }
        // 완료 주가 닫힌, 아직 명세에 들어가지 않은 기존 승인 채무만 읽습니다.
        // 정산 대기 주문을 새로 승인하거나 지급 Outbox를 만들지 않습니다.
        var candidates = await (
            from payout in db.기사운송대금지급요청.AsNoTracking()
            join transport in db.운송원장.AsNoTracking() on payout.운송Id equals transport.Id
            where payout.실행모드코드 == mode && payout.통화코드 == "KRW"
                && (payout.상태코드 == 기사지급요청상태코드.승인됨
                    || payout.상태코드 == 기사지급요청상태코드.Simulation검증완료
                    || payout.상태코드 == 기사지급요청상태코드.재시도대기
                    || payout.상태코드 == 기사지급요청상태코드.운영Provider미구성)
                && transport.도착.HasValue && transport.도착.Value < cutoff
                && !db.기사운송대금정산배치행.Any(line => line.지급요청Id == payout.Id)
            // 거부된 후보를 앞에 고정하지 않고, DB에 진행을 남겨 재시작 후에도 미검토를 먼저 처리한다.
            orderby payout.자동명세검토시각Utc.HasValue, payout.자동명세검토시각Utc, transport.도착, payout.Id
            select new { payout.Id, payout.기사Id, payout.지급예정금액, CompletedAt = transport.도착!.Value })
            .Take(take).ToArrayAsync(cancellationToken);

        var valid = new List<(long Id, string DriverId, decimal Amount, DateTime Start)>();
        foreach (var candidate in candidates)
        {
            var reviewCode = string.IsNullOrWhiteSpace(candidate.기사Id) || candidate.기사Id.Trim().Length > 450
                ? "ApprovedDebtDriverInvalid"
                : candidate.지급예정금액 <= 0 || decimal.Truncate(candidate.지급예정금액) != candidate.지급예정금액
                    ? "ApprovedDebtAmountInvalid" : null;
            DateTime start = default;
            if (reviewCode is null)
            {
                try { start = WeekStart(candidate.CompletedAt); }
                catch (ArgumentException) { reviewCode = "TransportCompletionDateInvalid"; }
            }
            if (reviewCode is not null)
            {
                await RecordAutomaticReviewAsync(candidate.Id, reviewCode, cancellationToken); rejected++; continue;
            }
            valid.Add((candidate.Id, candidate.기사Id, candidate.지급예정금액, start));
        }
        foreach (var group in valid.GroupBy(x => new { x.DriverId, x.Start }))
        {
            foreach (var chunk in group.OrderBy(x => x.Id).Chunk(200))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ids = chunk.Select(x => x.Id).ToArray();
                var keyInput = $"{mode}|{options.Value.PolicyRevision}|{group.Key.DriverId}|{group.Key.Start:O}|{string.Join(',', ids)}";
                var key = "weekly-auto:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyInput))).ToLowerInvariant();
                var request = new 배송료정산배치준비요청
                {
                    DriverId = group.Key.DriverId, ScheduleCode = 배송료정산배치Codes.주간,
                    PeriodStartUtc = group.Key.Start, PeriodEndUtc = group.Key.Start.AddDays(7),
                    PayoutRequestIds = ids, ConfirmedTotalAmount = chunk.Sum(x => x.Amount),
                    PolicyRevision = options.Value.PolicyRevision, IdempotencyKey = key
                };
                var result = await PrepareCoreAsync(request, "system:weekly-statement-preparation", cancellationToken);
                if (result.IsSuccess) { prepared++; held += result.Value.HeldCount; }
                else
                {
                    conflicts++; rejected += ids.Length;
                    foreach (var id in ids) await RecordAutomaticReviewAsync(id, "PreparationConflictReviewRequired", cancellationToken);
                }
            }
        }
        return new(true, prepared, held, conflicts, reviewed, rejected);
    }

    private async Task RecordAutomaticReviewAsync(long payoutId, string code, CancellationToken ct)
    {
        // Provider 결과·승인 상태·채무 금액은 바꾸지 않는다. 성공적으로 편입된 동시 요청은 덮지 않는다.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await db.기사운송대금지급요청.Where(x => x.Id == payoutId && x.실행모드코드 == executionMode.Mode.ToString()
                && !db.기사운송대금정산배치행.Any(line => line.지급요청Id == x.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.자동명세검토시각Utc, now)
                    .SetProperty(x => x.자동명세검토코드, code), ct);
        }
        catch (Exception ex) when (IsDatabaseContention(ex))
        {
            // 동시 명세 준비와 진행 표시가 경합한 경우 다음 회차의 정본 조회로 이어간다.
            // 승인·지급을 재전송하거나 정상 검토 기록을 만든 것으로 처리하지 않는다.
        }
    }

    private static DateTime WeekStart(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SeoulTimeZone);
        var offset = ((int)local.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7;
        return TimeZoneInfo.ConvertTimeToUtc(local.Date.AddDays(-offset), SeoulTimeZone);
    }
}
