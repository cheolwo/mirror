using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Shipper.Payment;
using 살뜰.Services.Options;
using 살뜰.도메인.설정;
using 살뜰.도메인.결제;

namespace Ssalddel.Application.Shipper.Payment;

/// <summary>본인 의뢰의 수납 근거를 조회합니다. 조회는 결제 승인·접수·지급을 실행하지 않습니다.</summary>
public sealed class 화주선결제조회QueryHandler(SsalddelContext db, ICurrentUserAccessor actor,
    IOptions<TransportPrepaymentOptions> options, IHostEnvironment environment, ISsalddelExecutionModePolicy execution)
    : IRequestHandler<화주선결제조회Query, Result<화주선결제조회응답>>
{
    public async Task<Result<화주선결제조회응답>> Handle(화주선결제조회Query query, CancellationToken ct)
    {
        var id = query.의뢰Id?.Trim();
        var source = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == id, ct);
        if (source is null || !화주결제권한정책.의뢰결제권한있음(source, actor.UserId, actor.Role))
            return Result.Fail<화주선결제조회응답>("의뢰를 찾을 수 없습니다.");

        var payment = await db.결제.AsNoTracking()
            .Where(x => x.의뢰Id == source.의뢰Id && x.화주Id == source.화주Id
                && x.결제대상유형 == 결제공통정의.결제대상유형.용달운송의뢰 && x.대상Id == source.의뢰Id
                && x.결제상태 == 상태값.결제상태.결제완료 && x.공통결제상태 == 결제공통정의.결제상태.승인완료)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var confirmed = source.결제상태 == 상태값.결제상태.결제완료 && payment is not null
            && payment.취소일시 is null && 배송료결제준비근거Policy.현재근거검증(payment, source).IsSuccess
            && (payment.결제제공자 == 결제공통정의.결제제공자.FakePG
                || payment.결제제공자 == 결제공통정의.결제제공자.TossPayments && TossPaymentCollectionEvidencePolicy.수납확인됨(payment));
        var amount = 배송료결제준비근거Policy.서버원운임조회(source);
        // 승인 원장 투영이 지연되거나 시작된 PG 결과가 미확정이면 새 결제를 유도하지 않습니다.
        var hasStartedPayment = await db.결제.AsNoTracking().AnyAsync(x => x.의뢰Id == source.의뢰Id
            && (x.결제상태 == 상태값.결제상태.결제완료 || x.PaymentKey != null && x.PaymentKey != ""), ct);
        var canPay = options.Value.Enabled && source.정산시점 == "선결제" && !confirmed
            && !hasStartedPayment && amount.IsSuccess && 화주운송결제진행정책.결제준비요청검증(source, actor.UserId, actor.Role, true).통과;
        return Result.Ok(new 화주선결제조회응답
        {
            의뢰Id = source.의뢰Id, Amount = amount.IsSuccess ? amount.Value : 0,
            Enabled = options.Value.Enabled, CanPay = canPay,
            SimulationAvailable = options.Value.Enabled && execution.IsSimulation,
            PaymentConfirmed = confirmed,
            ResultCheckRequired = !confirmed && (hasStartedPayment || source.결제상태 == 상태값.결제상태.결제완료),
            SimulationPayment = payment?.결제제공자 == 결제공통정의.결제제공자.FakePG,
            DispatchStatus = source.배차상태
        });
    }
}
