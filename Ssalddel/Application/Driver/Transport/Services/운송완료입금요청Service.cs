using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.공통;

namespace Ssalddel.Application.Driver.Transport;

public interface I운송완료입금요청Service
{
    Task<운송완료입금요청결과> 조기준비Async(운송상차완료됨Event notification, CancellationToken cancellationToken = default);
    Task<운송완료입금요청결과> 준비Async(운송인수완료됨Event notification, CancellationToken cancellationToken = default);
}

public sealed partial class 운송완료입금요청Service : I운송완료입금요청Service
{
    private readonly SsalddelContext _db;

    public 운송완료입금요청Service(SsalddelContext db)
    {
        _db = db;
    }

    public Task<운송완료입금요청결과> 조기준비Async(
        운송상차완료됨Event notification,
        CancellationToken cancellationToken = default)
        => 준비Async(
            new 운송입금요청Context(
                notification.운송번호,
                notification.운송Id,
                notification.기사Id,
                notification.발생시각Utc,
                notification.TraceId,
                nameof(운송상차완료Command),
                nameof(운송상차완료됨Event),
                상태값.배차상태.상차완료,
                운송입금요청종류.상차완료조기정산,
                "상차 완료 후 화주 승인 기준 플랫폼 조기 정산"),
            cancellationToken);

    public async Task<운송완료입금요청결과> 준비Async(
        운송인수완료됨Event notification,
        CancellationToken cancellationToken = default)
        => await 준비Async(
            new 운송입금요청Context(
                notification.운송번호,
                notification.운송Id,
                notification.기사Id,
                notification.발생시각Utc,
                notification.TraceId,
                nameof(운송인수완료Command),
                nameof(운송인수완료됨Event),
                상태값.배차상태.인수완료,
                운송입금요청종류.운송완료후정산,
                "운송 완료 후 정산"),
            cancellationToken);

    private async Task<운송완료입금요청결과> 준비Async(
        운송입금요청Context context,
        CancellationToken cancellationToken)
    {
        var request = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == context.운송번호, cancellationToken);
        if (request is null)
        {
            return new 운송완료입금요청결과(false, "화주 운송 의뢰 없음");
        }

        request.배차상태 = context.배차상태;

        var hasOpenAbnormalTransportIncident = await _db.비정상운송사건
            .AsNoTracking()
            .AnyAsync(
                x => x.운송의뢰Id == request.의뢰Id
                     && (x.정산보류적용여부
                         || x.상태Code == 비정상운송사건상태Codes.운영검토대기),
                cancellationToken);
        if (hasOpenAbnormalTransportIncident)
        {
            request.정산상태 = 운임정산상태.비정상운송검토보류.ToString();
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return new 운송완료입금요청결과(
                false,
                "비정상 운송 사건의 운영 검토가 끝날 때까지 입금 요청을 보류합니다.",
                request.의뢰Id);
        }

        var 입금요청판정 = 운송완료입금요청정책.입금요청가능여부(request, context.입금요청종류);
        if (!입금요청판정.가능)
        {
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return new 운송완료입금요청결과(false, 입금요청판정.사유, request.의뢰Id);
        }

        var amount = 운송완료입금요청정책.입금요청금액(request);
        if (amount <= 0)
        {
            request.정산상태 = 운임정산상태.미수발생.ToString();
            request.정산메모 = MergeMemo(request.정산메모, "운송 완료 후 입금 요청 금액을 산정하지 못했습니다.");
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return new 운송완료입금요청결과(false, "입금 요청 금액 없음", request.의뢰Id);
        }

        var payment = await 결제대기건가져오거나생성Async(request, context, amount, cancellationToken);
        request.결제상태 = 상태값.결제상태.결제대기;
        request.정산상태 = 운임정산상태.입금대기.ToString();
        request.결제수단 = 운송완료입금요청정책.토스가상계좌결제수단;
        request.정산메모 = MergeMemo(
            request.정산메모,
            $"{context.정산메모} 토스페이먼츠 가상계좌 입금 요청 생성: 결제Id={payment.결제Id}, OrderId={payment.OrderId}");
        request.UpdatedAt = DateTime.UtcNow;

        var scheduledCount = await 입금요청알림예약Async(request, context, payment, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return new 운송완료입금요청결과(
            true,
            "입금 요청 준비 완료",
            request.의뢰Id,
            payment.결제Id,
            payment.OrderId,
            scheduledCount);
    }

    private static string MergeMemo(string existing, string memo)
        => string.IsNullOrWhiteSpace(existing) ? memo : $"{existing}\n{memo}";
}

public sealed record 운송입금요청Context(
    string 운송번호,
    long 운송Id,
    string 기사Id,
    DateTime 발생시각Utc,
    string TraceId,
    string CommandName,
    string EventName,
    string 배차상태,
    운송입금요청종류 입금요청종류,
    string 정산메모);
