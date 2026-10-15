using FluentResults;
using Ssalddel.Contracts.Shipper.Payment;

namespace Ssalddel.Application.Shipper.Payment;

public sealed class 토스결제재조회CommandHandler(토스결제입금재조회UseCase useCase)
    : IRequestHandler<토스결제재조회Command, Result<토스결제승인응답>>
{
    public Task<Result<토스결제승인응답>> Handle(토스결제재조회Command request, CancellationToken cancellationToken)
        => useCase.재조회Async(request.OrderId, cancellationToken);
}
