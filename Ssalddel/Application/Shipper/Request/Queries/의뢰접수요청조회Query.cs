using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

/// <summary>새 등록을 보내지 않고 현재 사용자가 제출한 접수 번호의 결과만 확인합니다.</summary>
public sealed record 의뢰접수요청조회Query(string ClientRequestId) : IRequest<화주운송의뢰응답?>;
