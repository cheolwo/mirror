using FluentResults;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Common.Payments;

namespace Ssalddel.Application.Shipper.Payment;

[Ssalddel재무영향Profile(재무영향ProfileIds.결제승인)]
public sealed record 공통결제승인Command(
    int 결제제공자,
    string PaymentKey,
    string OrderId,
    int Amount)
    : IRequest<Result<공통결제승인응답>>;
