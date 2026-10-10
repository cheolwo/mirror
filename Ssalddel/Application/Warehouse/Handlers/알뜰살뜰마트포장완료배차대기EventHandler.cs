using Ssalddel.Application.Warehouse.Events;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using MediatR;

namespace Ssalddel.Application.Warehouse.Handlers;

public sealed class 알뜰살뜰마트포장완료배차대기EventHandler(
    I알뜰살뜰마트배차대기Service 배차대기Service,
    ILogger<알뜰살뜰마트포장완료배차대기EventHandler> logger) :
    INotificationHandler<창고포장완료됨Event>,
    INotificationHandler<창고포장배차재확인요청됨Event>
{
    public Task Handle(창고포장완료됨Event notification, CancellationToken cancellationToken)
        => 배차재확인Async(notification.입고상품Id, notification.사용자Id, cancellationToken);

    public Task Handle(창고포장배차재확인요청됨Event notification, CancellationToken cancellationToken)
        => 배차재확인Async(notification.입고상품Id, notification.사용자Id, cancellationToken);

    private async Task 배차재확인Async(long 입고상품Id, string 사용자Id, CancellationToken cancellationToken)
    {
        var results = await 배차대기Service.입고상품포장완료반영Async(
            입고상품Id,
            사용자Id,
            cancellationToken);

        foreach (var result in results)
        {
            if (result.생성또는조회됨)
            {
                logger.LogInformation(
                    "알뜰살뜰 마트 포장 완료 후 배차대기 생성 완료. 주문참조번호={주문참조번호}, 배차대기Id={배차대기Id}, 의뢰Id={의뢰Id}",
                    result.주문참조번호,
                    result.배차대기Id,
                    result.의뢰Id);
            }
            else if (result.결과코드 != 알뜰살뜰마트배차대기결과코드.출고예정없음)
            {
                logger.LogDebug(
                    "알뜰살뜰 마트 포장 완료 후 배차대기 보류. 주문참조번호={주문참조번호}, 결과코드={결과코드}, 메시지={메시지}",
                    result.주문참조번호,
                    result.결과코드,
                    result.메시지);
            }
        }
    }
}
