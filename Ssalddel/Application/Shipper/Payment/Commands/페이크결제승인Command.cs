using FluentResults;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Shipper.Payment;

namespace Ssalddel.Application.Shipper.Payment;

[Ssalddel재무영향Profile(재무영향ProfileIds.결제승인, ConditionCode = "SimulationOnly")]
public sealed record 페이크결제승인Command(
    string 의뢰Id,
    int Amount,
    string? 결제수단,
    string? 메모,
    string? IdempotencyKey)
    : IRequest<Result<페이크결제승인응답>>;
