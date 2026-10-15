using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Contracts.Shipper.Payment;
using 살뜰.Data;
using 살뜰.Services.External.Toss;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Shipper.Payment;

public static class TossPaymentCollectionEvidencePolicy
{
    public static bool 수납확인됨(결제 payment) => payment.결제제공자 == 결제공통정의.결제제공자.TossPayments
        && payment.결제상태 == 상태값.결제상태.결제완료 && payment.공통결제상태 == 결제공통정의.결제상태.승인완료 && payment.취소일시 is null
        && TossPaymentResponsePolicy.TryRead(payment.Toss응답Json ?? "", out var fact)
        && fact!.Status == "DONE" && Matches(payment, fact);
    internal static bool Matches(결제 payment, TossPaymentFact fact) => payment.PaymentKey == fact.PaymentKey
        && payment.OrderId == fact.OrderId && payment.결제금액 == fact.TotalAmount && payment.통화 == fact.Currency;
}

/// <summary>승인 시도를 먼저 기록하고 이후의 재요청은 같은 PG 결제를 조회합니다. 외부 승인 POST를 자동 반복하지 않습니다.</summary>
public sealed class 토스결제입금재조회UseCase(SsalddelContext db, I공통결제Service payments,
    I콘텐츠혜택계산Service benefits, ICurrentUserAccessor actor, IOptions<TransportPrepaymentOptions> options,
    ISsalddelExecutionModePolicy? executionMode = null)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    public Task<Result<토스결제승인응답>> 승인Async(string key, string order, int amount, CancellationToken ct = default)
        => RunAsync(order, key, amount, false, false, ct);
    public Task<Result<토스결제승인응답>> 재조회Async(string order, CancellationToken ct = default)
        => RunAsync(order, null, null, true, false, ct);

    public async Task 입금알림Async(string order, string secret, CancellationToken ct = default)
    {
        if (executionMode?.IsOperational == false || !options.Value.EnableDepositWebhook || string.IsNullOrWhiteSpace(order) || order.Length > 128
            || string.IsNullOrWhiteSpace(secret) || secret.Length > 512) return;
        var payment = await db.결제.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == order, ct);
        if (payment?.PG입금알림검증Hash is not { Length: 64 } hash) return;
        byte[] expected;
        try { expected = Convert.FromHexString(hash); } catch (FormatException) { return; }
        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(TossPaymentResponsePolicy.HashSecret(secret)))) return;
        await RunAsync(order, null, null, true, true, ct);
    }

    private async Task<Result<토스결제승인응답>> RunAsync(string order, string? key, int? amount, bool lookupOnly, bool trustedTrigger, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(order) || order.Length > 128 || order != order.Trim()
            || !lookupOnly && (string.IsNullOrWhiteSpace(key) || key.Length > 200 || amount is not > 0))
            return Fail("PaymentInputInvalid", "결제 요청을 확인해 주세요.");
        var gate = Gates.GetOrAdd(order, _ => new(1, 1)); await gate.WaitAsync(ct);
        try
        {
            var payment = await db.결제.SingleOrDefaultAsync(x => x.OrderId == order, ct);
            if (payment is null || payment.결제제공자 != 결제공통정의.결제제공자.TossPayments) return Fail("PaymentNotFound", "결제 요청을 찾을 수 없습니다.");
            var permission = await ValidateAsync(payment, trustedTrigger, validatePreparation: false, ct);
            if (permission.IsFailed) return Result.Fail<토스결제승인응답>(permission.Errors);
            if (!lookupOnly && Terminal(payment))
                return Fail("PaymentCollectionReviewRequired", "취소 또는 변경된 결제는 운영 검토가 필요합니다.");
            if (!lookupOnly && payment.결제상태 == 상태값.결제상태.결제완료)
            {
                if (payment.결제금액 != amount || !string.IsNullOrWhiteSpace(payment.PaymentKey) && payment.PaymentKey != key)
                    return Fail("PaymentEvidenceMismatch", "같은 결제의 확인 정보를 다시 확인해 주세요.");
                return Result.Ok(Response(payment));
            }
            if (executionMode?.IsOperational == false) return Fail("OperationalModeRequired", "PG 승인·입금 재조회는 운영 모드에서만 사용할 수 있습니다.");
            if (amount.HasValue && payment.결제금액 != amount) return Fail("PaymentAmountMismatch", "결제 금액이 일치하지 않습니다.");

            var mayConfirm = false;
            var prepared = await LocalAsync(payment, async () =>
            {
                if (!lookupOnly && Terminal(payment))
                    return Fail("PaymentCollectionReviewRequired", "취소 또는 변경된 결제는 운영 검토가 필요합니다.");
                var check = await ValidateAsync(payment, trustedTrigger, payment.결제상태 != 상태값.결제상태.결제완료, ct);
                if (check.IsFailed) return Result.Fail<토스결제승인응답>(check.Errors);
                if (string.IsNullOrWhiteSpace(payment.PaymentKey))
                {
                    if (lookupOnly) return Fail("PaymentLookupKeyUnavailable", "동일 결제의 승인 결과를 먼저 확인해 주세요.");
                    payment.PaymentKey = key; payment.공통결제상태 = 결제공통정의.결제상태.승인대기;
                    payment.원본응답Json = "{\"code\":\"ConfirmationAttemptRecorded\"}";
                    mayConfirm = true;
                }
                else if (key is not null && payment.PaymentKey != key) return Fail("PaymentEvidenceMismatch", "같은 결제의 확인 정보를 다시 확인해 주세요.");
                return Result.Ok(Response(payment));
            }, ct);
            if (prepared.IsFailed) return prepared;

            var request = new 결제승인요청(payment.PaymentKey!, payment.OrderId, payment.결제금액);
            결제승인결과 result;
            try
            {
                result = mayConfirm ? await payments.결제승인Async(payment.결제제공자, request, ct)
                    : await payments.결제조회Async(payment.결제제공자, request, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException) { return Fail("PaymentOutcomeUncertain", "승인 결과가 불확실합니다. 같은 결제의 입금 결과를 다시 조회해 주세요."); }
            if (!result.IsSuccess || !TossPaymentResponsePolicy.TryRead(result.ResponseJson, out var fact)
                || !TossPaymentCollectionEvidencePolicy.Matches(payment, fact!))
                return Fail(result.IsUncertain ? "PaymentOutcomeUncertain" : "PaymentEvidenceMismatch",
                    "결제 결과를 확정하지 않았습니다. 같은 결제를 다시 조회해 주세요.");

            return await LocalAsync(payment, async () =>
            {
                var check = await ValidateAsync(payment, trustedTrigger, payment.결제상태 != 상태값.결제상태.결제완료, ct);
                if (check.IsFailed) return Result.Fail<토스결제승인응답>(check.Errors);
                if (!TossPaymentCollectionEvidencePolicy.Matches(payment, fact!)) return Fail("PaymentEvidenceMismatch", "결제 확인 정보가 변경되었습니다.");
                var cancelled = fact!.Status is "CANCELED" or "PARTIAL_CANCELED";
                if (Terminal(payment) && !cancelled) return Result.Ok(Response(payment));
                if (payment.결제상태 == 상태값.결제상태.결제완료 && !cancelled) return Result.Ok(Response(payment));
                var sanitized = TossPaymentResponsePolicy.Sanitize(result.ResponseJson, out var rawHash);
                payment.Toss응답Json = payment.원본응답Json = sanitized;
                payment.PG입금알림검증Hash ??= result.DepositCallbackVerificationHash ?? rawHash;
                if (!string.IsNullOrWhiteSpace(fact.Method)) payment.결제수단 = fact.Method;
                if (cancelled)
                {
                    payment.결제상태 = 상태값.결제상태.결제취소; payment.공통결제상태 = 결제공통정의.결제상태.취소완료; payment.취소일시 = DateTime.UtcNow;
                    var source = await SourceAsync(payment, ct, tracked: true);
                    if (source is not null) { source.결제상태 = 상태값.결제상태.결제취소; source.정산상태 = "비정상운송검토보류"; source.UpdatedAt = DateTime.UtcNow; }
                }
                else if (fact.Status == "DONE")
                {
                    payment.결제상태 = 상태값.결제상태.결제완료; payment.공통결제상태 = 결제공통정의.결제상태.승인완료;
                    payment.외부거래번호 = fact.PaymentKey; payment.승인일시 = DateTime.UtcNow;
                    if (!await db.결제승인완료Outbox.AnyAsync(x => x.결제레코드Id == payment.Id, ct))
                    {
                        var payload = new 결제승인완료Event(payment.Id, payment.결제Id, payment.결제대상유형, payment.대상Id,
                            payment.결제제공자, payment.결제금액, payment.통화, payment.승인일시.Value);
                        db.결제승인완료Outbox.Add(new() { 결제레코드Id = payment.Id, 결제Id = payment.결제Id, 결제대상유형 = payment.결제대상유형,
                            대상Id = payment.대상Id, 결제제공자 = payment.결제제공자, 결제금액 = payment.결제금액, 통화 = payment.통화,
                            승인일시Utc = payment.승인일시.Value, PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                            처리상태 = "Pending", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
                        await benefits.보상사용처리Async(payment.화주Id, ct);
                    }
                }
                else { payment.결제상태 = 상태값.결제상태.결제대기; payment.공통결제상태 = 결제공통정의.결제상태.승인대기; }
                return Result.Ok(Response(payment));
            }, ct);
        }
        finally { gate.Release(); }
    }

    private async Task<Result> ValidateAsync(결제 payment, bool trusted, bool validatePreparation, CancellationToken ct)
    {
        var source = await SourceAsync(payment, ct);
        if (!trusted && (string.IsNullOrWhiteSpace(actor.UserId) || !화주결제권한정책.결제승인권한있음(payment, source, actor.UserId, actor.Role))) return Result.Fail("결제 요청을 찾을 수 없습니다.");
        if (source?.모의처리처수행 == true || payment.결제대상유형 == 결제공통정의.결제대상유형.음식주문
            && await db.음식주문.AsNoTracking().AnyAsync(x => x.주문번호 == payment.대상Id && x.모의처리처수행, ct))
            return Result.Fail("모의 처리처 수행은 결제·지급·정산 대상이 아닙니다.");
        if (validatePreparation && payment.결제대상유형 == 결제공통정의.결제대상유형.용달운송의뢰)
        {
            var evidence = 배송료결제준비근거Policy.현재근거검증(payment, source);
            if (evidence.IsFailed) return evidence;
            return await 배송료결제준비근거Policy.현재혜택검증Async(payment, benefits, ct);
        }
        return Result.Ok();
    }
    private Task<화주운송의뢰?> SourceAsync(결제 payment, CancellationToken ct, bool tracked = false) => payment.결제대상유형 == 결제공통정의.결제대상유형.용달운송의뢰
        ? (tracked ? db.화주운송의뢰 : db.화주운송의뢰.AsNoTracking()).FirstOrDefaultAsync(x => x.의뢰Id == (payment.대상Id == "" ? payment.의뢰Id : payment.대상Id), ct)
        : Task.FromResult<화주운송의뢰?>(null);
    private async Task<Result<토스결제승인응답>> LocalAsync(결제 payment, Func<Task<Result<토스결제승인응답>>> action, CancellationToken ct)
    {
        var attempt = 0;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) { db.ChangeTracker.Clear(); db.Attach(payment); }
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            if (tx is not null)
            {
                await db.결제.Where(x => x.Id == payment.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.공통결제상태, x => x.공통결제상태), ct);
                await db.Entry(payment).ReloadAsync(ct);
            }
            var value = await action();
            if (value.IsSuccess) { await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); }
            return value;
        });
    }
    private static 토스결제승인응답 Response(결제 payment) => new() { 결제Id = payment.결제Id, 의뢰Id = payment.의뢰Id,
        OrderId = payment.OrderId, PaymentKey = payment.PaymentKey ?? "", 결제상태 = payment.결제상태,
        결제응답 = TossPaymentResponsePolicy.Sanitize(payment.Toss응답Json ?? "{}", out _) };
    private static bool Terminal(결제 payment) => payment.취소일시 is not null
        || payment.결제상태 is 상태값.결제상태.결제취소 or 상태값.결제상태.환불됨
        || payment.공통결제상태 is 결제공통정의.결제상태.취소완료 or 결제공통정의.결제상태.환불완료;
    private static Result<토스결제승인응답> Fail(string code, string message)
        => Result.Fail<토스결제승인응답>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", 409));
}
