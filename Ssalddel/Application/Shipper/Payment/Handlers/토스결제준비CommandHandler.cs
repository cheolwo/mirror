using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Shipper.Payment;
using Microsoft.Extensions.Options;
using 살뜰.Services.Payments;
using 살뜰.Services.Options;
using 살뜰.도메인.결제;

namespace Ssalddel.Application.Shipper.Payment;

public sealed class 토스결제준비CommandHandler : IRequestHandler<토스결제준비Command, Result<토스결제준비응답>>
{
    private readonly SsalddelContext _db;
    private readonly IOptions<TossPaymentsOptions> _options;
    private readonly I콘텐츠혜택계산Service _benefitService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IOptions<TransportPrepaymentOptions> _prepaymentOptions;

    public 토스결제준비CommandHandler(
        SsalddelContext db,
        IOptions<TossPaymentsOptions> options,
        I콘텐츠혜택계산Service benefitService,
        ICurrentUserAccessor currentUserAccessor,
        IOptions<TransportPrepaymentOptions>? prepaymentOptions = null)
    {
        _db = db;
        _options = options;
        _benefitService = benefitService;
        _currentUserAccessor = currentUserAccessor;
        _prepaymentOptions = prepaymentOptions ?? Microsoft.Extensions.Options.Options.Create(new TransportPrepaymentOptions());
    }

    public async Task<Result<토스결제준비응답>> Handle(토스결제준비Command request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.의뢰Id))
        {
            return Result.Fail<토스결제준비응답>("의뢰Id is required");
        }

        if (request.Amount <= 0)
        {
            return Result.Fail<토스결제준비응답>("amount must be greater than 0");
        }

        var shipperRequest = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == request.의뢰Id, cancellationToken);
        if (shipperRequest == null)
        {
            return Result.Fail<토스결제준비응답>("의뢰를 찾을 수 없습니다.");
        }

        var 진행검증 = 화주운송결제진행정책.결제준비요청검증(
            shipperRequest,
            _currentUserAccessor.UserId,
            _currentUserAccessor.Role,
            _prepaymentOptions.Value.Enabled);
        if (!진행검증.통과)
        {
            return Result.Fail<토스결제준비응답>(진행검증.실패사유);
        }

        var gross = 배송료결제준비근거Policy.서버원운임조회(shipperRequest);
        if (gross.IsFailed) return Result.Fail<토스결제준비응답>(gross.Errors);
        if (request.Amount != gross.Value)
        {
            return Result.Fail<토스결제준비응답>(new Error("요청 금액이 서버의 확정 운임과 다릅니다. 금액을 다시 확인해 주세요.")
                .WithMetadata("ErrorCode", "PreparationAmountMismatch"));
        }

        var benefitEstimate = await _benefitService.계산Async(shipperRequest.화주Id, gross.Value, cancellationToken);
        var requestedAmount = benefitEstimate.최종결제금액;
        if (requestedAmount <= 0)
        {
            return Result.Fail<토스결제준비응답>("적용 가능한 보상으로 결제금액이 0원이 되어 온라인 결제를 진행할 수 없습니다.");
        }

        var pendingPayments = await _db.결제
            .Where(x => x.의뢰Id == shipperRequest.의뢰Id && x.결제상태 == 상태값.결제상태.결제대기)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var startedPayments = pendingPayments.Where(x => !string.IsNullOrWhiteSpace(x.PaymentKey)).ToArray();
        if (startedPayments.Length > 1)
            return Result.Fail<토스결제준비응답>(new Error("시작된 결제가 여러 건입니다. 기존 결제 결과를 먼저 확인해 주세요.")
                .WithMetadata("ErrorCode", "PreparationResultCheckRequired"));

        foreach (var existingPendingPayment in (startedPayments.Length == 1 ? startedPayments.AsEnumerable() : pendingPayments)
                     .Where(x => x.결제금액 == requestedAmount))
        {
            var existingEvidence = 배송료결제준비근거Policy.현재근거검증(existingPendingPayment, shipperRequest);
            var existingBenefit = 배송료결제준비근거Policy.혜택근거검증(existingPendingPayment, benefitEstimate);
            if (existingEvidence.IsSuccess && existingBenefit.IsSuccess)
            {
                return Result.Ok(new 토스결제준비응답
                {
                    결제Id = existingPendingPayment.결제Id,
                    의뢰Id = existingPendingPayment.의뢰Id,
                    OrderId = existingPendingPayment.OrderId,
                    Amount = existingPendingPayment.결제금액,
                    ClientKey = _options.Value.ClientKey
                });
            }
        }

        // 금액이 달라도 이미 PG 키가 생긴 결제는 입금 결과부터 확인합니다.
        // 명시적 재준비는 새 근거를 만들며 이전 금액·근거는 소급 변경하지 않습니다.
        if (pendingPayments.Any(x => !string.IsNullOrWhiteSpace(x.PaymentKey)))
            return Result.Fail<토스결제준비응답>(new Error("이미 시작된 결제의 결과를 먼저 확인해 주세요. 새 결제를 만들지 않습니다.")
                .WithMetadata("ErrorCode", "PreparationResultCheckRequired"));

        var orderId = 주문번호생성();
        var payment = new 살뜰.도메인.결제.결제
        {
            결제Id = Guid.NewGuid().ToString("N"),
            의뢰Id = shipperRequest.의뢰Id,
            화주Id = shipperRequest.화주Id,
            결제대상유형 = 결제공통정의.결제대상유형.용달운송의뢰,
            대상Id = shipperRequest.의뢰Id,
            결제제공자 = 결제공통정의.결제제공자.TossPayments,
            공통결제상태 = 결제공통정의.결제상태.요청생성,
            결제금액 = requestedAmount,
            통화 = "KRW",
            주문명 = $"살뜰 용달 운송의뢰 {shipperRequest.의뢰Id}",
            결제수단 = shipperRequest.결제수단,
            OrderId = orderId,
            결제상태 = 상태값.결제상태.결제대기,
            CreatedAt = DateTime.UtcNow
        };

        var evidence = 배송료결제준비근거Policy.생성(payment, shipperRequest, benefitEstimate);
        if (evidence.IsFailed) return Result.Fail<토스결제준비응답>(evidence.Errors);
        payment.준비근거Json = evidence.Value;

        shipperRequest.결제상태 = 상태값.결제상태.결제대기;
        shipperRequest.UpdatedAt = DateTime.UtcNow;

        await _db.결제.AddAsync(payment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(new 토스결제준비응답
        {
            결제Id = payment.결제Id,
            의뢰Id = payment.의뢰Id,
            OrderId = payment.OrderId,
            Amount = payment.결제금액,
            ClientKey = _options.Value.ClientKey
        });
    }

    private static string 주문번호생성()
    {
        return $"ssalddel_{Guid.NewGuid():N}";
    }
}
