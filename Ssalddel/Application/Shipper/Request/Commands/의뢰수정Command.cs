using FluentResults;

namespace Ssalddel.Application.Shipper.Request;

public sealed record 의뢰수정Command(
    string RequestId,
    운송조건입력값 운송조건,
    화물정보입력값 화물정보,
    위치정보입력값 픽업지,
    위치정보입력값 하차지,
    요청조건입력값 요청조건,
    정산조건입력값? 정산조건,
    Ssalddel.Contracts.Shipper.Request.PricingDTO? 요금옵션 = null,
    int? 결제예정금액 = null) : IRequest<Result<Ssalddel.Contracts.Shipper.Request.화주운송의뢰응답>>;
