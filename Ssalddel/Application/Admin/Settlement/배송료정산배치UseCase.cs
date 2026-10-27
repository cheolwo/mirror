using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Contracts.Admin.Settlement;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Admin.Settlement;

public interface I배송료정산배치UseCase
{
    Task<Result<배송료정산배치응답>> 준비Async(배송료정산배치준비요청 request, CancellationToken cancellationToken = default);
    Task<Result<배송료정산배치응답>> 조회Async(long batchId, CancellationToken cancellationToken = default);
    Task<Result<배송료정산배치응답>> 재검증Async(long batchId, 배송료정산배치재검증요청 request, CancellationToken cancellationToken = default);
}

[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase("배송료 정산 배치 준비", Summary = "승인된 운송별 지급 채무를 건별·주간 명세로 연결하고 현재 지급 조건을 재검사합니다. 실제 송금을 실행하지 않습니다.")]
[SsalddelUseCaseActor(SsalddelActor.PlatformOperator)]
public sealed partial class 배송료정산배치UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUser,
    ISsalddelExecutionModePolicy executionMode,
    IOptions<DriverPayoutBatchOptions> options,
    TimeProvider timeProvider,
    I화물분쟁지급보류Query? disputes = null) : I배송료정산배치UseCase, I배송료주간자동준비Service
{
    public async Task<Result<배송료정산배치응답>> 준비Async(배송료정산배치준비요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAdmin()) return Fail("서버관리자만 배송료 정산 명세를 준비할 수 있습니다.", 403);
        return await PrepareCoreAsync(request, currentUser.UserId!, cancellationToken);
    }

    private async Task<Result<배송료정산배치응답>> PrepareCoreAsync(
        배송료정산배치준비요청 request, string actorId, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Fail("배송료 정산 배치 준비가 비활성입니다. 기존 명세 조회는 가능합니다.", 409);
        var schedule = request.ScheduleCode?.Trim() ?? options.Value.DefaultScheduleCode;
        var error = Validate(request, schedule);
        if (error is not null) return Fail(error, 400);
        var ids = request.PayoutRequestIds.OrderBy(x => x).ToArray();
        var scheduledAt = ScheduledAt(schedule, request.PeriodEndUtc);
        var digest = Digest(request, schedule, ids, executionMode.Mode.ToString(), scheduledAt);
        try
        {
            await using var tx = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            var batch = await db.기사운송대금정산배치.Include(x => x.행목록)
                .SingleOrDefaultAsync(x => x.멱등키 == request.IdempotencyKey.Trim(), cancellationToken);
            if (batch is not null)
            {
                if (batch.ContentDigest != digest) return Fail("같은 멱등 키에 다른 정산 명세를 사용할 수 없습니다.", 409);
                // 재시도도 현재 조건을 다시 검사하며 과거의 준비 상태를 최신 지급 허가로 사용하지 않습니다.
                await RefreshAsync(batch, cancellationToken);
                batch.Revision++;
                await db.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);
                return Result.Ok(ToDto(batch, true));
            }

            var payouts = await db.기사운송대금지급요청.AsNoTracking().Where(x => Enumerable.Contains<long>(ids, x.Id)).ToArrayAsync(cancellationToken);
            if (payouts.Length != ids.Length) return Fail("선택한 승인 지급요청 중 찾을 수 없는 건이 있습니다.", 404);
            if (payouts.Any(x => x.기사Id != request.DriverId.Trim() || x.통화코드 != "KRW"
                || x.실행모드코드 != executionMode.Mode.ToString()))
                return Fail("같은 기사·KRW·실행 모드의 지급요청만 묶을 수 있습니다.", 409);
            var transportIds = payouts.Select(x => x.운송Id).ToArray();
            var transports = await db.운송원장.AsNoTracking().Where(x => Enumerable.Contains<long>(transportIds, x.Id)).ToArrayAsync(cancellationToken);
            if (transports.Any(x => x.도착.HasValue && (x.도착.Value < request.PeriodStartUtc || x.도착.Value >= request.PeriodEndUtc)))
                return Fail("실제 운송 완료일이 같은 업무 기간에 속한 지급요청만 묶을 수 있습니다. 늦은 승인은 원래 완료 주에 귀속됩니다.", 409);
            var approvedTotal = payouts.Sum(x => x.지급예정금액);
            if (approvedTotal != request.ConfirmedTotalAmount || payouts.Any(x => x.지급예정금액 <= 0 || decimal.Truncate(x.지급예정금액) != x.지급예정금액))
                return Fail("확인한 합계가 서버의 승인 지급요청 금액과 일치하지 않습니다.", 409);
            if (await db.기사운송대금정산배치행.AsNoTracking().AnyAsync(x => Enumerable.Contains<long>(ids, x.지급요청Id), cancellationToken))
                return Fail("이미 정산 명세에 편입된 지급요청이 있습니다. 건별·주간 명세에 중복 편입할 수 없습니다.", 409);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            batch = new 기사운송대금정산배치
            {
                기사Id = request.DriverId.Trim(), 실행모드코드 = executionMode.Mode.ToString(),
                정책판본 = request.PolicyRevision.Trim(), 일정코드 = schedule,
                기간시작Utc = request.PeriodStartUtc, 기간종료Utc = request.PeriodEndUtc,
                기간기준Code = 배송료정산배치Codes.작업완료일, PayoutScheduledAtUtc = scheduledAt,
                멱등키 = request.IdempotencyKey.Trim(), ContentDigest = digest,
                승인합계금액 = approvedTotal, 생성관리자Id = actorId, CreatedAtUtc = now, UpdatedAtUtc = now,
                행목록 = payouts.OrderBy(x => x.Id).Select(x => new 기사운송대금정산배치행
                {
                    지급요청Id = x.Id, 운송Id = x.운송Id, 의뢰Id = x.의뢰Id, 동결금액 = x.지급예정금액
                }).ToList()
            };
            await RefreshAsync(batch, cancellationToken);
            db.기사운송대금정산배치.Add(batch);
            await db.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);
            return Result.Ok(ToDto(batch));
        }
        catch (DbUpdateConcurrencyException)
        {
            DetachPendingBatches(); return Fail("정산 명세가 동시에 변경되었습니다. 최신 명세를 다시 조회해 주세요.", 409);
        }
        catch (Exception ex) when (IsDatabaseContention(ex))
        {
            DetachPendingBatches(); return Fail("같은 지급요청이나 멱등 키가 동시에 처리되었습니다. 기존 명세를 다시 조회해 주세요.", 409);
        }
    }

    public async Task<Result<배송료정산배치응답>> 조회Async(long batchId, CancellationToken cancellationToken = default)
    {
        if (!IsAdmin()) return Fail("서버관리자만 배송료 정산 명세를 조회할 수 있습니다.", 403);
        var batch = await db.기사운송대금정산배치.AsNoTracking().Include(x => x.행목록).SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken);
        return batch is null ? Fail("정산 명세를 찾을 수 없습니다.", 404) : Result.Ok(ToDto(batch));
    }

    public async Task<Result<배송료정산배치응답>> 재검증Async(long batchId, 배송료정산배치재검증요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAdmin()) return Fail("서버관리자만 배송료 정산 명세를 재검증할 수 있습니다.", 403);
        return await RevalidateCoreAsync(batchId, request, cancellationToken);
    }

    private async Task<Result<배송료정산배치응답>> RevalidateCoreAsync(long batchId,
        배송료정산배치재검증요청 request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Fail("배송료 정산 배치 준비가 비활성입니다. 기존 명세 조회는 가능합니다.", 409);
        if (request.ExpectedRevision <= 0) return Fail("확인한 정산 명세 판본이 필요합니다.", 400);
        try
        {
            await using var tx = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            var batch = await db.기사운송대금정산배치.Include(x => x.행목록).SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken);
            if (batch is null) return Fail("정산 명세를 찾을 수 없습니다.", 404);
            if (batch.Revision != request.ExpectedRevision) return Fail("정산 명세가 변경되었습니다. 최신 판본을 다시 확인해 주세요.", 409);
            await RefreshAsync(batch, cancellationToken);
            batch.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);
            return Result.Ok(ToDto(batch));
        }
        catch (DbUpdateConcurrencyException)
        {
            DetachPendingBatches(); return Fail("정산 명세가 동시에 변경되었습니다. 최신 명세를 다시 조회해 주세요.", 409);
        }
        catch (Exception ex) when (IsDatabaseContention(ex))
        {
            DetachPendingBatches(); return Fail("정산 명세의 동시 처리가 감지되었습니다. 최신 명세를 다시 조회해 주세요.", 409);
        }
    }

    private async Task RefreshAsync(기사운송대금정산배치 batch, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var line in batch.행목록)
        {
            var decision = await EvaluateAsync(batch, line, ct);
            line.상태코드 = decision.Code.Length == 0 ? 배송료정산배치Codes.준비 : 배송료정산배치Codes.보류;
            line.보류사유코드 = decision.Code; line.보류메시지 = decision.Message; line.최근검증시각Utc = now;
        }
        batch.준비건수 = batch.행목록.Count(x => x.상태코드 == 배송료정산배치Codes.준비);
        batch.보류건수 = batch.행목록.Count - batch.준비건수;
        batch.준비합계금액 = batch.행목록.Where(x => x.상태코드 == 배송료정산배치Codes.준비).Sum(x => x.동결금액);
        batch.상태코드 = batch.준비건수 == 0 ? 배송료정산배치Codes.보류
            : batch.보류건수 == 0 ? 배송료정산배치Codes.준비 : 배송료정산배치Codes.일부보류;
        batch.UpdatedAtUtc = now;
    }

    private async Task<(string Code, string Message)> EvaluateAsync(기사운송대금정산배치 batch, 기사운송대금정산배치행 line, CancellationToken ct)
    {
        if (batch.실행모드코드 != executionMode.Mode.ToString()) return ("ExecutionModeMismatch", "명세를 준비한 실행 모드와 현재 실행 모드가 다릅니다.");
        if (batch.정책판본 != options.Value.PolicyRevision) return ("PolicyRevisionChanged", "정산 준비 정책 판본이 변경되어 운영자 확인이 필요합니다.");
        if (options.Value.WeekStartDay != DayOfWeek.Wednesday || options.Value.PayoutDay is not (DayOfWeek.Wednesday or DayOfWeek.Thursday)
            || options.Value.PayoutHourLocal is < 0 or > 23
            || batch.기간기준Code != 배송료정산배치Codes.작업완료일 || !ValidPeriod(batch.일정코드, batch.기간시작Utc, batch.기간종료Utc)
            || batch.PayoutScheduledAtUtc != ScheduledAt(batch.일정코드, batch.기간종료Utc))
            return ("PayoutScheduleChanged", "완료일 기준 업무 주 또는 예정 지급 일정이 변경되어 운영자 확인이 필요합니다.");
        var payout = await db.기사운송대금지급요청.AsNoTracking().SingleOrDefaultAsync(x => x.Id == line.지급요청Id, ct);
        if (payout is null || payout.운송Id != line.운송Id || payout.의뢰Id != line.의뢰Id || payout.기사Id != batch.기사Id
            || payout.지급예정금액 != line.동결금액 || payout.통화코드 != batch.통화코드 || payout.실행모드코드 != batch.실행모드코드)
            return ("ApprovedDebtChanged", "승인 지급요청의 원천·기사·금액·통화·모드가 명세와 일치하지 않습니다.");
        if (payout.상태코드 is not (기사지급요청상태코드.승인됨 or 기사지급요청상태코드.Simulation검증완료
            or 기사지급요청상태코드.재시도대기 or 기사지급요청상태코드.운영Provider미구성))
            return ("PayoutApprovalStateUnknown", "지급요청의 현재 상태가 지급 준비 대상으로 확인되지 않습니다.");
        var transport = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == line.운송Id, ct);
        if (transport is null || !transport.도착.HasValue || transport.상태 == "취소"
            || transport.도착.Value < batch.기간시작Utc || transport.도착.Value >= batch.기간종료Utc
            || (transport.의뢰Id != line.의뢰Id && transport.운송번호 != line.의뢰Id)
            || (transport.확정기사Id ?? transport.기사_운송자) != batch.기사Id)
            return ("TransportCompletionChanged", "완료 운송과 확정 기사 연결을 다시 확인해야 합니다.");
        var source = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == line.의뢰Id, ct);
        if (source?.상태 == 상태값.의뢰상태.취소 || source?.배차상태 == 상태값.배차상태.취소
            || source?.결제상태 is 상태값.결제상태.결제취소 or 상태값.결제상태.환불됨
            || source?.정산상태 == 운임정산상태.정산취소.ToString())
            return ("CollectionCancelledOrRefunded", "취소·환불된 원천 의뢰는 지급 준비를 보류합니다.");
        var fare = await db.운임구성.AsNoTracking().Where(x => x.의뢰Id == line.의뢰Id).OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var account = await db.Set<기사정산계좌>().AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == batch.기사Id, ct);
        var incident = await db.비정상운송사건.AsNoTracking().AnyAsync(x => x.운송의뢰Id == line.의뢰Id
            && (x.정산보류적용여부 || x.상태Code == 비정상운송사건상태Codes.운영검토대기), ct);
        var readiness = Ssalddel.Application.Settlement.기사지급준비Policy.판정(source, fare?.기사지급예정운임, account is not null,
            account?.확인상태 == 기사정산계좌확인상태.확인완료, incident);
        if (!readiness.IsReady) return (readiness.Code, readiness.Message);
        var dispute = await 보호지원화물지급보류Query.확인Async(disputes, line.의뢰Id, ct);
        if (dispute.Hold) return (dispute.Code, dispute.Message);
        if (fare!.기사지급예정운임 != line.동결금액) return ("FareChanged", "현재 운임의 기사 지급액이 승인 채무와 달라 재검토가 필요합니다.");

        // 의뢰의 결제완료 문자열만으로 PG 수납을 확정하지 않습니다. 수동 입금은 기존 확인 원장을 유지합니다.
        var payment = await db.결제.AsNoTracking().Where(x => x.결제대상유형 == 결제공통정의.결제대상유형.용달운송의뢰
            && (x.대상Id == line.의뢰Id || x.의뢰Id == line.의뢰Id)).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (payment is null)
            return source!.정산상태 is nameof(운임정산상태.입금확인완료) or nameof(운임정산상태.정산완료)
                ? (string.Empty, string.Empty) : ("CollectionEvidenceMissing", "정본 결제 수납 또는 수동 입금 확인 근거가 필요합니다.");
        var expectedCollection = source!.결제예정금액 is > 0 ? source.결제예정금액.Value : fare.최종운임;
        // 승인된 혜택을 차감한 PG 실수납액을 원운임과 혼동하지 않습니다. 동결 근거가 없는 과거 혜택은 추정하지 않습니다.
        var hasPreparation = !string.IsNullOrWhiteSpace(payment.준비근거Json);
        if (hasPreparation && 배송료결제준비근거Policy.현재근거검증(payment, source).IsFailed)
            return ("CollectionPreparationChanged", "배송료의 원운임·혜택·결제 준비 근거가 현재 원천과 일치하지 않습니다.");
        if (payment.의뢰Id != source.의뢰Id || (!string.IsNullOrEmpty(payment.대상Id) && payment.대상Id != source.의뢰Id)
            || payment.화주Id != source.화주Id || payment.통화 != "KRW" || expectedCollection is not > 0
            || !hasPreparation && payment.결제금액 != expectedCollection || payment.취소일시.HasValue
            || payment.공통결제상태 != 결제공통정의.결제상태.승인완료 || payment.결제상태 != 상태값.결제상태.결제완료)
            return ("CollectionEvidenceMismatch", "정본 수납의 당사자·금액·통화·완료 상태가 현재 의뢰와 일치하지 않습니다.");
        if (payment.결제제공자 == 결제공통정의.결제제공자.TossPayments)
            return TossPaymentCollectionEvidencePolicy.수납확인됨(payment) ? (string.Empty, string.Empty)
                : ("CollectionProviderEvidenceMissing", "PG의 전액 수납 완료 근거를 확인할 수 없어 지급 준비를 보류합니다.");
        if (payment.결제제공자 == 결제공통정의.결제제공자.ManualBankTransfer
            && source.정산상태 is not (nameof(운임정산상태.입금확인완료) or nameof(운임정산상태.정산완료)))
            return ("ManualCollectionEvidenceMissing", "수동 입금 확인 원장을 확인해야 합니다.");
        if (payment.결제제공자 == 결제공통정의.결제제공자.ManualBankTransfer
            || executionMode.IsSimulation && payment.결제제공자 == 결제공통정의.결제제공자.FakePG)
            return payment.승인일시.HasValue ? (string.Empty, string.Empty) : ("CollectionApprovedAtMissing", "수납 확인 시각을 확인해야 합니다.");
        return ("CollectionProviderUnsupported", "현재 수납 제공자의 지급용 근거 검사가 준비되지 않았습니다.");
    }

    private string? Validate(배송료정산배치준비요청 r, string schedule)
    {
        if (string.IsNullOrWhiteSpace(r.DriverId) || r.DriverId.Trim().Length > 450
            || string.IsNullOrWhiteSpace(r.IdempotencyKey) || r.IdempotencyKey.Trim().Length > 128)
            return "기사와 128자 이하의 멱등 키가 필요합니다.";
        if (schedule is not (배송료정산배치Codes.건별 or 배송료정산배치Codes.주간)) return "정산 일정은 Single 또는 Weekly여야 합니다.";
        if (string.IsNullOrWhiteSpace(r.PolicyRevision) || r.PolicyRevision.Trim() != options.Value.PolicyRevision
            || r.PolicyRevision.Trim().Length > 100) return "현재 정산 준비 정책 판본을 확인해야 합니다.";
        if (options.Value.WeekStartDay != DayOfWeek.Wednesday || options.Value.PayoutDay is not (DayOfWeek.Wednesday or DayOfWeek.Thursday)
            || options.Value.PayoutHourLocal is < 0 or > 23) return "주간 시작은 수요일, 예정 지급은 수요일 또는 목요일의 유효한 한국 시간이어야 합니다.";
        if (r.PeriodStartUtc.Kind != DateTimeKind.Utc || r.PeriodEndUtc.Kind != DateTimeKind.Utc || !ValidPeriod(schedule, r.PeriodStartUtc, r.PeriodEndUtc))
            return "완료일 기준 UTC 포함 시작·제외 종료 기간이 필요합니다. 주간은 한국 시간 수요일 00시부터 다음 수요일 00시, 건별은 최대 1일입니다.";
        if (r.PayoutRequestIds is null || r.PayoutRequestIds.Count is < 1 or > 200 || r.PayoutRequestIds.Any(x => x <= 0)
            || r.PayoutRequestIds.Distinct().Count() != r.PayoutRequestIds.Count || schedule == 배송료정산배치Codes.건별 && r.PayoutRequestIds.Count != 1)
            return "중복 없는 승인 지급요청을 지정해야 합니다. 건별 1건, 주간 최대 200건입니다.";
        return r.ConfirmedTotalAmount <= 0 || decimal.Truncate(r.ConfirmedTotalAmount) != r.ConfirmedTotalAmount
            ? "화면에서 확인한 양수 원 단위 합계가 필요합니다." : null;
    }

    private bool IsAdmin() => !string.IsNullOrWhiteSpace(currentUser.UserId) && currentUser.Role == 역할명.서버관리자;
    private static string Digest(배송료정산배치준비요청 r, string schedule, long[] ids, string mode, DateTime? scheduledAt)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            DriverId = r.DriverId.Trim(), ScheduleCode = schedule, r.PeriodStartUtc, r.PeriodEndUtc,
            PayoutRequestIds = ids, ConfirmedTotalAmount = r.ConfirmedTotalAmount.ToString("G29", CultureInfo.InvariantCulture),
            PolicyRevision = r.PolicyRevision.Trim(), ExecutionModeCode = mode, CurrencyCode = "KRW",
            PeriodBasisCode = 배송료정산배치Codes.작업완료일, PayoutScheduledAtUtc = scheduledAt
        }))).ToLowerInvariant();

    private static TimeZoneInfo SeoulTimeZone => TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Seoul", out var zone)
        ? zone : TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
    private static bool ValidPeriod(string schedule, DateTime start, DateTime end)
    {
        if (start >= end) return false;
        if (schedule == 배송료정산배치Codes.건별) return end - start <= TimeSpan.FromDays(1);
        if (schedule != 배송료정산배치Codes.주간 || end - start != TimeSpan.FromDays(7)) return false;
        // DB DateTime의 Kind는 공급자에 따라 Unspecified로 복원되어도 이 필드의 의미는 UTC입니다.
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(start, DateTimeKind.Utc), SeoulTimeZone);
        return local.DayOfWeek == DayOfWeek.Wednesday && local.TimeOfDay == TimeSpan.Zero;
    }
    private DateTime? ScheduledAt(string schedule, DateTime periodEnd)
    {
        if (schedule != 배송료정산배치Codes.주간) return null;
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(periodEnd, DateTimeKind.Utc), SeoulTimeZone);
        var dayOffset = ((int)options.Value.PayoutDay - (int)endLocal.DayOfWeek + 7) % 7;
        // 은행 휴일 달력 미연결: 임의의 영업일 보정이나 송금 완료 처리를 하지 않습니다.
        return TimeZoneInfo.ConvertTimeToUtc(endLocal.Date.AddDays(dayOffset).AddHours(options.Value.PayoutHourLocal), SeoulTimeZone);
    }

    private void DetachPendingBatches()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is 기사운송대금정산배치 or 기사운송대금정산배치행).ToArray())
            entry.State = EntityState.Detached;
    }
    private static bool IsDatabaseContention(Exception exception)
    {
        var cause = exception.GetBaseException();
        return cause.GetType().FullName == "MySqlConnector.MySqlException"
            && cause.GetType().GetProperty("Number")?.GetValue(cause) is int mysql && mysql is 1062 or 1205 or 1213
            || cause.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
            && (cause.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(cause) is int code && code is 1555 or 2067
                || cause.GetType().GetProperty("SqliteErrorCode")?.GetValue(cause) is int sqlite && sqlite is 5 or 6);
    }
    private static Result<배송료정산배치응답> Fail(string message, int status)
        => Result.Fail<배송료정산배치응답>(new Error(message).WithMetadata("StatusCode", status));
    private static 배송료정산배치응답 ToDto(기사운송대금정산배치 x, bool replay = false) => new()
    {
        BatchId = x.Id, Revision = x.Revision, DriverId = x.기사Id, CurrencyCode = x.통화코드,
        ExecutionModeCode = x.실행모드코드, PolicyRevision = x.정책판본, ScheduleCode = x.일정코드,
        PeriodStartUtc = DateTime.SpecifyKind(x.기간시작Utc, DateTimeKind.Utc), PeriodEndUtc = DateTime.SpecifyKind(x.기간종료Utc, DateTimeKind.Utc), StatusCode = x.상태코드,
        PeriodBasisCode = x.기간기준Code, PayoutScheduledAtUtc = x.PayoutScheduledAtUtc is { } scheduled ? DateTime.SpecifyKind(scheduled, DateTimeKind.Utc) : null,
        PayoutTimeZoneId = "Asia/Seoul", BankCalendarApplied = false,
        ApprovedTotalAmount = x.승인합계금액, ReadyTotalAmount = x.준비합계금액, ReadyCount = x.준비건수, HeldCount = x.보류건수,
        LastValidatedAtUtc = x.UpdatedAtUtc, IsIdempotentReplay = replay, IsActualTransferCompleted = false,
        Lines = x.행목록.OrderBy(y => y.지급요청Id).Select(y => new 배송료정산배치행응답
        {
            PayoutRequestId = y.지급요청Id, TransportId = y.운송Id, RequestId = y.의뢰Id, FrozenAmount = y.동결금액,
            StatusCode = y.상태코드, HoldReasonCode = y.보류사유코드, HoldMessage = y.보류메시지
        }).ToArray()
    };
}
