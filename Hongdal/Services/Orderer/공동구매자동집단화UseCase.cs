using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Services.Orderer;

public interface I공동구매자동집단화UseCase
{
    Task<공동구매처리결과<IReadOnlyList<공동구매자동집단응답>>> 목록조회Async(
        공동구매자동집단조회조건 조건,
        CancellationToken cancellationToken = default);

    Task<공동구매처리결과<공동구매자동집단응답>> 수요등록Async(
        공동구매자동수요등록Command command,
        CancellationToken cancellationToken = default);
}

[HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
[HongdalUseCase("공동구매 자동 집단화", Summary = "주문자의 구매 의사를 배송권 기준으로 모아 공동구매 집단 후보를 형성합니다.")]
[HongdalUseCaseActor(HongdalActor.Orderer)]
[HongdalUseCaseActor(HongdalActor.OrdererGroupLeader, HongdalUseCaseActorRole.Supporting)]
[HongdalUseCaseActor(HongdalActor.PlatformOperator, HongdalUseCaseActorRole.Supporting)]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Include,
    "공공데이터조회UseCase",
    Condition = "주소, 공동주택, 배송권 기준으로 주문자를 묶는 경우",
    Summary = "주문자 집단화는 주소와 생활권 판단에 필요한 공공 데이터 조회를 포함합니다.")]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "커뮤니티게시글UseCase",
    Condition = "구매 의사를 다른 주문자에게 공개해 모집하거나 토론하는 경우",
    Summary = "자동 집단화 후보를 커뮤니티 게시글과 태그 기반 모집 흐름으로 확장합니다.")]
public sealed class 공동구매자동집단화UseCase : I공동구매자동집단화UseCase
{
    private readonly I공동구매자동집단화저장소 _저장소;
    private readonly I공동구매수령창고Service _수령창고Service;
    private readonly I공동구매개별주문원장Service _개별주문원장Service;

    public 공동구매자동집단화UseCase(
        I공동구매자동집단화저장소 저장소,
        I공동구매수령창고Service 수령창고Service,
        I공동구매개별주문원장Service 개별주문원장Service)
    {
        _저장소 = 저장소;
        _수령창고Service = 수령창고Service;
        _개별주문원장Service = 개별주문원장Service;
    }

    public async Task<공동구매처리결과<IReadOnlyList<공동구매자동집단응답>>> 목록조회Async(
        공동구매자동집단조회조건 조건,
        CancellationToken cancellationToken = default)
    {
        var items = await _저장소.집단목록조회Async(조건, cancellationToken);
        return 공동구매처리결과<IReadOnlyList<공동구매자동집단응답>>.성공결과(items);
    }

    public async Task<공동구매처리결과<공동구매자동집단응답>> 수요등록Async(
        공동구매자동수요등록Command command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var 주문확정수요 = 주문확정수요인가(command);
            if (주문확정수요)
            {
                var warehouse = await _수령창고Service.확보Async(command, cancellationToken);
                command.도착창고Id = warehouse.창고Id;
                command.도착창고유형 = warehouse.창고유형;
                command.도착창고명 = warehouse.창고명;
                command.수령지주소참조키 = warehouse.주소참조키;
            }

            var group = await _저장소.수요등록Async(command, cancellationToken);
            if (주문확정수요)
            {
                var demand = group.수요목록.FirstOrDefault(x =>
                    string.Equals(x.수요출처키, command.수요출처키, StringComparison.Ordinal))
                    ?? group.수요목록.FirstOrDefault(x =>
                        string.Equals(x.주문자키, command.주문자키, StringComparison.Ordinal));
                if (demand is null)
                {
                    throw new InvalidOperationException("등록한 주문자 수요를 자동집단에서 찾을 수 없습니다.");
                }

                if (string.IsNullOrWhiteSpace(demand.공동구매주문집계원장Id)
                    || string.IsNullOrWhiteSpace(demand.개별주문원장Id)
                    || string.IsNullOrWhiteSpace(demand.입고예정원장Id))
                {
                    var ledgers = await _개별주문원장Service.생성및연결Async(
                        group,
                        demand,
                        cancellationToken);
                    group = await _저장소.개별주문원장연결Async(
                        group.자동집단Id,
                        demand.수요Id,
                        ledgers.공동구매주문집계원장Id,
                        ledgers.개별주문원장Id,
                        ledgers.입고예정원장Id,
                        cancellationToken);
                }
            }

            return 공동구매처리결과<공동구매자동집단응답>.성공결과(group);
        }
        catch (InvalidOperationException ex)
        {
            return 공동구매처리결과<공동구매자동집단응답>.잘못된요청(ex.Message);
        }
    }

    private static bool 주문확정수요인가(공동구매자동수요등록Command command)
        => command.수요유형 == 공동구매자동수요유형코드.예약결제
           || command.결제상태 is 공동구매자동결제상태코드.예약됨
               or 공동구매자동결제상태코드.결제확정;
}
