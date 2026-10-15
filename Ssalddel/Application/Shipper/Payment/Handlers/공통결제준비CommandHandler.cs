using FluentResults;
using Ssalddel.Contracts.Common.Payments;

namespace Ssalddel.Application.Shipper.Payment;

public sealed class 공통결제준비CommandHandler : IRequestHandler<공통결제준비Command, Result<공통결제준비응답>>
{
    private readonly ISender _sender;

    public 공통결제준비CommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<공통결제준비응답>> Handle(공통결제준비Command request, CancellationToken cancellationToken)
    {
        if (request.결제대상유형 != 계약결제대상유형.용달운송의뢰)
        {
            return Result.Fail<공통결제준비응답>("현재는 용달운송의뢰 결제만 지원합니다.");
        }

        if (request.결제제공자 != 계약결제제공자.TossPayments)
        {
            return Result.Fail<공통결제준비응답>("현재는 TossPayments 제공자만 지원합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.대상Id) || request.금액 <= 0)
            return Result.Fail<공통결제준비응답>("의뢰와 서버 확정 금액을 확인해 주세요.");

        var tossResult = await _sender.Send(new 토스결제준비Command(request.대상Id, request.금액), cancellationToken);
        if (tossResult.IsFailed)
        {
            return Result.Fail<공통결제준비응답>(tossResult.Errors.Select(x => x.Message));
        }

        return Result.Ok(new 공통결제준비응답
        {
            결제요청Id = tossResult.Value.결제Id,
            대상Id = tossResult.Value.의뢰Id,
            결제제공자 = 계약결제제공자.TossPayments,
            OrderId = tossResult.Value.OrderId,
            Amount = tossResult.Value.Amount,
            ClientKey = tossResult.Value.ClientKey
        });
    }
}
