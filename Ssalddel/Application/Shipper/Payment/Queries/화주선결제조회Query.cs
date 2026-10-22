using FluentResults;
using Ssalddel.Contracts.Shipper.Payment;

namespace Ssalddel.Application.Shipper.Payment;

public sealed record 화주선결제조회Query(string 의뢰Id) : IRequest<Result<화주선결제조회응답>>;
