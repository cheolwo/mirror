using FluentResults;
using Ssalddel.Contracts.Common.Finance;

namespace Ssalddel.Application.Shipper.Payment;

[Ssalddel재무영향Profile(재무영향ProfileIds.결제승인)]
public sealed record 토스결제승인Command(string PaymentKey, string OrderId, int Amount) : IRequest<Result<Ssalddel.Contracts.Shipper.Payment.토스결제승인응답>>;
