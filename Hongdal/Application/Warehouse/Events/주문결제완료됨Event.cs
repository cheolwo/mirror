using MediatR;

namespace Hongdal.Application.Warehouse;

public sealed record 주문결제완료됨Event(
    long? 주문Id,
    string 주문참조번호,
    string 주문자UserId,
    string 판매자UserId,
    IReadOnlyList<주문결제완료상품항목> 상품목록,
    DateTime 발생시각Utc,
    string TraceId,
    long? 수령창고Id = null,
    string? 수령지표시명 = null,
    string? 수령도로명주소 = null,
    string? 수령상세주소 = null) : INotification;

public sealed record 주문결제완료상품항목(
    long? 판매상품Id,
    long? 입고상품Id,
    string 상품명,
    string SKU,
    int 수량);
