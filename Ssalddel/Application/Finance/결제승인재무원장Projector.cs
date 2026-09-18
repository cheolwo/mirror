using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Contracts.Common.Finance;
using 살뜰.Data;
using 살뜰.도메인.정산;

namespace Ssalddel.Application.Finance;

/// <summary>
/// 결제 승인 원본을 수정하지 않고 기준 중립 재무 사건과 관리계정 사본으로 멱등 투영합니다.
/// </summary>
public sealed class 결제승인재무원장ProjectorEventHandler(
    SsalddelContext db,
    TimeProvider timeProvider,
    ILogger<결제승인재무원장ProjectorEventHandler> logger)
    : INotificationHandler<결제승인완료Event>
{
    private const long SourceRevision = 1;

    public async Task Handle(결제승인완료Event notification, CancellationToken cancellationToken)
    {
        var profile = 재무영향ProfileCatalog.Find(재무영향ProfileIds.결제승인)
            ?? throw new InvalidOperationException("결제 승인 재무 영향 Profile을 찾을 수 없습니다.");
        var sourceStableId = $"payment:{notification.결제레코드Id.ToString(CultureInfo.InvariantCulture)}";
        var stableId = $"financial-event:payment-approved:{notification.결제레코드Id.ToString(CultureInfo.InvariantCulture)}:r{SourceRevision}";
        var currency = (notification.통화 ?? string.Empty).Trim().ToUpperInvariant();
        var evidenceHash = BuildEvidenceHash(notification, currency);

        if (notification.결제금액 <= 0 || currency.Length != 3)
        {
            await RecordExceptionAsync(
                stableId,
                "InvalidPaymentApprovedPayload",
                notification.결제금액 <= 0 ? "PaymentAmountNotPositive" : "CurrencyCodeInvalid",
                cancellationToken);
            return;
        }

        var existing = await db.재무사건
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.StableId == stableId, cancellationToken);
        if (existing is not null)
        {
            if (existing.금액 != notification.결제금액
                || !string.Equals(existing.통화Code, currency, StringComparison.Ordinal)
                || !string.Equals(existing.증빙Hash, evidenceHash, StringComparison.Ordinal))
            {
                await RecordExceptionAsync(
                    stableId,
                    "FinancialEventSourceConflict",
                    "PaymentApprovedReplayMismatch",
                    cancellationToken);
            }

            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var financialEvent = new 재무사건
        {
            StableId = stableId,
            원본Event유형 = nameof(결제승인완료Event),
            원본StableId = sourceStableId,
            원본Revision = SourceRevision,
            재무영향ProfileStableId = profile.StableId,
            재무의미Code = profile.FinancialMeaningCode,
            재무영향유형Code = profile.ImpactKind.ToString(),
            금액 = notification.결제금액,
            통화Code = currency,
            업무발생일시Utc = EnsureUtc(notification.승인일시Utc),
            상대역할Code = $"PaymentTargetType:{notification.결제대상유형.ToString(CultureInfo.InvariantCulture)}",
            증빙Hash = evidenceHash,
            투영상태Code = 재무사건투영상태Codes.완료,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var lineNumber = 1;
        foreach (var account in profile.Accounts)
        {
            var side = account.Role switch
            {
                재무영향계정역할.DebitCandidate => "DebitCandidate",
                재무영향계정역할.CreditCandidate => "CreditCandidate",
                _ => throw new InvalidOperationException("결제 승인 Profile에는 차변·대변 후보만 있어야 합니다.")
            };
            financialEvent.관리계정전기목록.Add(new 관리계정전기
            {
                LineNumber = lineNumber++,
                관리계정StableId = account.ManagementAccountStableId,
                전기방향Code = side,
                금액 = notification.결제금액,
                통화Code = currency,
                매핑Revision = profile.MappingRevision,
                CreatedAtUtc = now
            });
        }

        financialEvent.증빙목록.Add(new 재무사건증빙
        {
            증빙유형Code = "PaymentApprovedEvent",
            원본참조 = sourceStableId,
            원본Revision = SourceRevision,
            내용Hash = evidenceHash,
            CreatedAtUtc = now
        });

        db.재무사건.Add(financialEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordExceptionAsync(
        string financialEventStableId,
        string exceptionCode,
        string summaryCode,
        CancellationToken cancellationToken)
    {
        var stableId = $"financial-reconciliation:{financialEventStableId}:{exceptionCode}";
        var exists = await db.재무대사예외
            .AsNoTracking()
            .AnyAsync(x => x.StableId == stableId, cancellationToken);
        if (exists)
        {
            return;
        }

        db.재무대사예외.Add(new 재무대사예외
        {
            StableId = stableId,
            재무사건StableId = financialEventStableId,
            예외Code = exceptionCode,
            상태Code = 재무대사예외상태Codes.확인필요,
            요약Code = summaryCode,
            감지일시Utc = timeProvider.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning(
            "결제 승인 재무 투영을 완료하지 못해 대사 예외을 기록했습니다. FinancialEvent={FinancialEvent} Exception={Exception}",
            financialEventStableId,
            exceptionCode);
    }

    private static string BuildEvidenceHash(결제승인완료Event notification, string currency)
    {
        var canonical = string.Join('|',
            notification.결제레코드Id.ToString(CultureInfo.InvariantCulture),
            notification.결제Id?.Trim() ?? string.Empty,
            notification.결제대상유형.ToString(CultureInfo.InvariantCulture),
            notification.대상Id?.Trim() ?? string.Empty,
            notification.결제제공자.ToString(CultureInfo.InvariantCulture),
            notification.결제금액.ToString(CultureInfo.InvariantCulture),
            currency,
            EnsureUtc(notification.승인일시Utc).ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
