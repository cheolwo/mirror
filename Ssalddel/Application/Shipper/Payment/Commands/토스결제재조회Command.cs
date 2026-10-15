using FluentResults;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Shipper.Payment;

namespace Ssalddel.Application.Shipper.Payment;

[Ssalddel재무영향Profile(재무영향ProfileIds.결제승인)]
public sealed record 토스결제재조회Command(string OrderId) : IRequest<Result<토스결제승인응답>>;
