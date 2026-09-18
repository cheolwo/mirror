using FluentResults;
using Ssalddel.Contracts.Common.Finance;

namespace Ssalddel.Application.Shipper.Payment;

[Ssalddel재무영향Profile(재무영향ProfileIds.결제준비)]
public sealed record 토스결제준비Command(string 의뢰Id, int Amount) : IRequest<Result<Ssalddel.Contracts.Shipper.Payment.토스결제준비응답>>;
