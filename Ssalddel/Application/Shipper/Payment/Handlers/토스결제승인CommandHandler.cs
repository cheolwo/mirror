using FluentResults;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Shipper.Payment;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;

namespace Ssalddel.Application.Shipper.Payment;

public sealed class 토스결제승인CommandHandler : IRequestHandler<토스결제승인Command, Result<토스결제승인응답>>
{
    private readonly 토스결제입금재조회UseCase _flow;
    public 토스결제승인CommandHandler(SsalddelContext db, I공통결제Service paymentService,
        I콘텐츠혜택계산Service benefitService, ICurrentUserAccessor currentUserAccessor,
        토스결제입금재조회UseCase? reconciliation = null)
        => _flow = reconciliation ?? new(db, paymentService, benefitService, currentUserAccessor, Options.Create(new TransportPrepaymentOptions()));
    public Task<Result<토스결제승인응답>> Handle(토스결제승인Command request, CancellationToken cancellationToken)
        => _flow.승인Async(request.PaymentKey, request.OrderId, request.Amount, cancellationToken);
}
