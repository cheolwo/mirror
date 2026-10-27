using Ssalddel.Contracts.Admin.Management;

namespace Ssalddel.Application.Admin.Management;

public sealed record 차량추천기준수정Command(
    string 차량코드,
    decimal? 권장최대CBM,
    int 추천우선순위,
    bool 추천사용여부,
    int? 운영권장중량Kg,
    int? 팔레트적재개수,
    int? 적재함길이Mm = null,
    int? 적재함폭Mm = null,
    int? 적재함높이Mm = null,
    int? 최대적재중량Kg = null) : IRequest<차량추천기준응답?>;
