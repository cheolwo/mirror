using MediatR;

namespace Ssalddel.Application.Warehouse.Events;

/// <summary>
/// 인증된 동일 포장 완료 재요청의 배차 후속만 다시 확인합니다.
/// 새 포장 사실이나 문서·알림을 발행하는 완료 Event가 아닙니다.
/// </summary>
public sealed record 창고포장배차재확인요청됨Event(
    string 사용자Id,
    long 입고상품Id,
    DateTime 포장완료시각Utc,
    DateTime 요청시각Utc) : INotification;
