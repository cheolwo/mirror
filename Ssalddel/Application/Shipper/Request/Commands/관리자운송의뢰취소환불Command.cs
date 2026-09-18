using FluentResults;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

[Ssalddel재무영향Profile(재무영향ProfileIds.고객환불상태기록, ConditionCode = "RefundRequired")]
public sealed record 관리자운송의뢰취소환불Command(
    string RequestId,
    string 확인의뢰Id,
    string 사유) : IRequest<Result<화주운송의뢰응답>>;
