using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Services.Community;

namespace Hongdal.Services.Orderer;

public sealed record 공동구매개별주문원장연결결과(
    string 공동구매주문집계원장Id,
    string 개별주문원장Id,
    string 입고예정원장Id);

public interface I공동구매개별주문원장Service
{
    Task<공동구매개별주문원장연결결과> 생성및연결Async(
        공동구매자동집단응답 group,
        공동구매자동수요응답 demand,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 예약 결제된 개인 수요를 개별 주문으로 만들고 공동구매 내부 주문집계에 연결합니다.
/// 공동구매 원장은 사용자에게 보이는 전체 흐름, 내부 주문집계 원장은 개별 주문 집계,
/// 개별 주문 원장은 주문자별 계약·수령권의 경계로 유지합니다.
/// </summary>
public sealed class 공동구매개별주문원장Service : I공동구매개별주문원장Service
{
    private readonly I커뮤니티원장저장소 _원장저장소;
    private readonly I주문원장통합UseCase _주문원장통합UseCase;

    public 공동구매개별주문원장Service(
        I커뮤니티원장저장소 원장저장소,
        I주문원장통합UseCase 주문원장통합UseCase)
    {
        _원장저장소 = 원장저장소;
        _주문원장통합UseCase = 주문원장통합UseCase;
    }

    public async Task<공동구매개별주문원장연결결과> 생성및연결Async(
        공동구매자동집단응답 group,
        공동구매자동수요응답 demand,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(demand);
        if (demand.도착창고Id is not > 0)
        {
            throw new InvalidOperationException("개별 주문 원장을 만들려면 주문자의 도착 창고 ID가 필요합니다.");
        }

        var sourceLedger = await _원장저장소.원장조회Async(demand.커뮤니티원장Id, cancellationToken)
            ?? throw new InvalidOperationException("개별 주문을 연결할 공동구매 원장을 찾을 수 없습니다.");
        if (!string.Equals(
                sourceLedger.원장템플릿Key,
                CommunityLedgerTemplateKeys.GroupPurchase,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("개별 주문은 공동구매 원장에만 연결할 수 있습니다.");
        }

        var 공동구매주문집계원장Id = 공동구매주문집계원장Id생성(sourceLedger.원장Id, group.자동집단Id);
        var 주문원장Id = 개별주문원장Id생성(sourceLedger.원장Id, demand.수요Id);
        var 입고원장Id = $"{sourceLedger.원장Id}-{demand.수요Id}-inbound-planned";
        var 변경자 = string.IsNullOrWhiteSpace(demand.주문자키) ? "system" : demand.주문자키;
        var 주문자표시명 = string.IsNullOrWhiteSpace(demand.주문자표시명) ? "공동구매 주문자" : demand.주문자표시명;

        if (await _원장저장소.원장조회Async(입고원장Id, cancellationToken) is null)
        {
            await _원장저장소.원장저장Async(
                new 커뮤니티원장저장요청
                {
                    원장Id = 입고원장Id,
                    커뮤니티Id = sourceLedger.커뮤니티Id,
                    원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseInbound,
                    제목 = $"{주문자표시명} · {group.상품명} 입고 예정",
                    원함 = "주문 물품을 지정한 수령 창고에 인도받고 검수 뒤 입고 완료로 전환합니다.",
                    상태 = 커뮤니티원장상태.초안,
                    현재단계Key = "inbound-planned",
                    생성자UserId = 변경자,
                    생성자표시명 = 주문자표시명,
                    블록목록 =
                    [
                        new 커뮤니티원장블록Dto
                        {
                            BlockId = "receiving-right",
                            BlockType = CommunityLedgerBlockTypes.Generic,
                            Title = "입고 예정·수령 권리",
                            State = 공동구매개별주문입고상태코드.입고예정,
                            Data = 안전한입고참조(group, demand, 주문원장Id)
                        }
                    ],
                    외부참조 = 안전한입고참조(group, demand, 주문원장Id)
                },
                변경자,
                cancellationToken);
        }

        if (await _원장저장소.원장조회Async(주문원장Id, cancellationToken) is null)
        {
            await _원장저장소.원장저장Async(
                new 커뮤니티원장저장요청
                {
                    원장Id = 주문원장Id,
                    커뮤니티Id = sourceLedger.커뮤니티Id,
                    원장템플릿Key = CommunityLedgerTemplateKeys.Order,
                    제목 = $"{주문자표시명} · {group.상품명} 개별 주문",
                    원함 = "공동구매 안에서 개인 주문 수량과 수령 창고별 입고 예정 상태를 추적합니다.",
                    상태 = 커뮤니티원장상태.초안,
                    현재단계Key = "receiving-planned",
                    생성자UserId = 변경자,
                    생성자표시명 = 주문자표시명,
                    포함원장목록 =
                    [
                        new 커뮤니티포함원장참조Dto
                        {
                            원장Id = 입고원장Id,
                            원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseInbound,
                            역할 = 주문원장포함역할.창고입고,
                            관계유형 = CommunityLedgerRelationTypes.Contains,
                            필수여부 = true,
                            표시순서 = 0
                        }
                    ],
                    블록목록 =
                    [
                        new 커뮤니티원장블록Dto
                        {
                            BlockId = "individual-order-receiving",
                            BlockType = CommunityLedgerBlockTypes.Generic,
                            Title = "개별 주문·도착 창고",
                            State = 공동구매개별주문입고상태코드.입고예정,
                            Data = 안전한입고참조(group, demand, 주문원장Id)
                        }
                    ],
                    외부참조 = 안전한입고참조(group, demand, 주문원장Id)
                },
                변경자,
                cancellationToken);
        }

        await 주문집계연결및갱신Async(
            sourceLedger,
            group,
            demand,
            공동구매주문집계원장Id,
            주문원장Id,
            변경자,
            주문자표시명,
            cancellationToken);

        return new 공동구매개별주문원장연결결과(공동구매주문집계원장Id, 주문원장Id, 입고원장Id);
    }

    private async Task 주문집계연결및갱신Async(
        커뮤니티원장Dto sourceLedger,
        공동구매자동집단응답 group,
        공동구매자동수요응답 demand,
        string 공동구매주문집계원장Id,
        string 개별주문원장Id,
        string 변경자,
        string 주문자표시명,
        CancellationToken cancellationToken)
    {
        var 주문집계원장 = await _원장저장소.원장조회Async(공동구매주문집계원장Id, cancellationToken);
        if (주문집계원장 is null)
        {
            주문집계원장 = await _원장저장소.원장저장Async(
                new 커뮤니티원장저장요청
                {
                    원장Id = 공동구매주문집계원장Id,
                    커뮤니티Id = sourceLedger.커뮤니티Id,
                    원장템플릿Key = CommunityLedgerTemplateKeys.GroupOrder,
                    제목 = $"{group.상품명} 공동구매 주문집계",
                    원함 = "확정된 개별 주문들을 집합으로 묶고 수량·금액·수령 창고 분포를 개별 주문에서 계산합니다.",
                    상태 = 커뮤니티원장상태.진행중,
                    현재단계Key = "collecting-confirmed-orders",
                    생성자UserId = 변경자,
                    생성자표시명 = 주문자표시명,
                    참여자목록 = [참여자(demand)],
                    포함원장목록 =
                    [
                        new 커뮤니티포함원장참조Dto
                        {
                            원장Id = 개별주문원장Id,
                            원장템플릿Key = CommunityLedgerTemplateKeys.Order,
                            역할 = 주문원장포함역할.개별주문,
                            관계유형 = CommunityLedgerRelationTypes.Contains,
                            필수여부 = true,
                            표시순서 = 0
                        }
                    ],
                    블록목록 = [집계블록(group, demand, sourceLedger.원장Id, [개별주문원장Id])],
                    외부참조 = 주문집계참조(group, sourceLedger.원장Id),
                    확장속성 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["AggregationMode"] = "SumOfIndividualOrders",
                        ["AggregateSourceOfTruth"] = "IncludedIndividualOrderLedgers"
                    }
                },
                변경자,
                cancellationToken);
        }
        else if (!주문집계원장.포함원장목록.Any(x =>
                     string.Equals(x.원장Id, 개별주문원장Id, StringComparison.OrdinalIgnoreCase)))
        {
            var linked = await _주문원장통합UseCase.하위원장연결Async(
                주문집계원장.원장Id,
                new 주문하위원장연결요청
                {
                    하위원장Id = 개별주문원장Id,
                    역할 = 주문원장포함역할.개별주문,
                    필수여부 = true,
                    표시순서 = 주문집계원장.포함원장목록.Count,
                    기대Revision = 주문집계원장.Revision
                },
                변경자,
                cancellationToken);
            if (linked.IsFailed)
            {
                throw new InvalidOperationException(string.Join(" ", linked.Errors.Select(x => x.Message)));
            }

            주문집계원장 = linked.Value.주문원장;
        }

        주문집계원장 = await 주문집계갱신Async(
            주문집계원장,
            group,
            demand,
            sourceLedger.원장Id,
            변경자,
            cancellationToken);

        var 최신공동구매원장 = await _원장저장소.원장조회Async(sourceLedger.원장Id, cancellationToken)
            ?? throw new InvalidOperationException("주문집계를 연결할 공동구매 원장을 찾을 수 없습니다.");
        if (최신공동구매원장.포함원장목록.Any(x =>
                string.Equals(x.원장Id, 주문집계원장.원장Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var groupLinked = await _주문원장통합UseCase.하위원장연결Async(
            최신공동구매원장.원장Id,
            new 주문하위원장연결요청
            {
                하위원장Id = 주문집계원장.원장Id,
                역할 = 주문원장포함역할.주문집계,
                필수여부 = true,
                표시순서 = 최신공동구매원장.포함원장목록.Count,
                기대Revision = 최신공동구매원장.Revision
            },
            변경자,
            cancellationToken);
        if (groupLinked.IsFailed)
        {
            throw new InvalidOperationException(string.Join(" ", groupLinked.Errors.Select(x => x.Message)));
        }
    }

    private async Task<커뮤니티원장Dto> 주문집계갱신Async(
        커뮤니티원장Dto 주문집계원장,
        공동구매자동집단응답 group,
        공동구매자동수요응답 demand,
        string sourceLedgerId,
        string 변경자,
        CancellationToken cancellationToken)
    {
        var orderLedgerIds = 주문집계원장.포함원장목록
            .Where(x => string.Equals(x.역할, 주문원장포함역할.개별주문, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.원장Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blocks = 주문집계원장.블록목록
            .Where(x => !string.Equals(x.BlockId, "individual-order-aggregation", StringComparison.OrdinalIgnoreCase))
            .Append(집계블록(group, demand, sourceLedgerId, orderLedgerIds))
            .ToArray();
        var participants = 주문집계원장.참여자목록
            .Where(x => !string.Equals(x.UserId, demand.주문자키, StringComparison.OrdinalIgnoreCase))
            .Append(참여자(demand))
            .ToArray();

        return await _원장저장소.원장저장Async(
            new 커뮤니티원장저장요청
            {
                원장Id = 주문집계원장.원장Id,
                기대Revision = 주문집계원장.Revision,
                커뮤니티Id = 주문집계원장.커뮤니티Id,
                원장템플릿Key = CommunityLedgerTemplateKeys.GroupOrder,
                제목 = 주문집계원장.제목,
                원함 = 주문집계원장.원함,
                상태 = 주문집계원장.상태,
                현재단계Key = 주문집계원장.현재단계Key,
                대상OsCode = 주문집계원장.대상OsCode,
                대상OsName = 주문집계원장.대상OsName,
                생성자UserId = 주문집계원장.생성자UserId,
                생성자표시명 = 주문집계원장.생성자표시명,
                블록목록 = blocks,
                참여자목록 = participants,
                포함원장목록 = 주문집계원장.포함원장목록,
                다이어그램스냅샷 = 주문집계원장.다이어그램스냅샷,
                외부참조 = 주문집계참조(group, sourceLedgerId),
                확장속성 = 주문집계원장.확장속성
            },
            변경자,
            cancellationToken);
    }

    private static 커뮤니티원장블록Dto 집계블록(
        공동구매자동집단응답 group,
        공동구매자동수요응답 currentDemand,
        string sourceLedgerId,
        IEnumerable<string> includedOrderLedgerIds)
    {
        var ids = includedOrderLedgerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var demands = group.수요목록
            .Where(x => ids.Contains(개별주문원장Id생성(sourceLedgerId, x.수요Id)))
            .Append(currentDemand)
            .Where(x => ids.Contains(개별주문원장Id생성(sourceLedgerId, x.수요Id)))
            .GroupBy(x => x.수요Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToArray();
        var units = demands.Select(x => x.수량단위)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new 커뮤니티원장블록Dto
        {
            BlockId = "individual-order-aggregation",
            BlockType = CommunityLedgerBlockTypes.Generic,
            Title = "개별 주문 집계",
            State = "calculated",
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AggregationMode"] = "SumOfIndividualOrders",
                ["ConfirmedIndividualOrderCount"] = demands.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["ConfirmedOrdererCount"] = demands.Select(x => x.주문자키).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["TotalRequestedQuantity"] = demands.Sum(x => x.희망수량)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["QuantityUnit"] = units.Length == 1 ? units[0] : units.Length == 0 ? string.Empty : "mixed",
                ["TotalReservedPaymentAmount"] = demands.Sum(x => Math.Max(0, x.예약결제금액 ?? 0))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["DestinationWarehouseCount"] = demands.Where(x => x.도착창고Id is > 0).Select(x => x.도착창고Id).Distinct().Count()
                    .ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };
    }

    private static 커뮤니티원장참여자Dto 참여자(공동구매자동수요응답 demand)
        => new()
        {
            UserId = demand.주문자키,
            DisplayName = string.IsNullOrWhiteSpace(demand.주문자표시명) ? "공동구매 참여자" : demand.주문자표시명,
            RoleLabel = "개별 주문자",
            ParticipationState = "주문확정"
        };

    private static IReadOnlyDictionary<string, string> 주문집계참조(
        공동구매자동집단응답 group,
        string sourceLedgerId)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceGroupPurchaseLedgerId"] = sourceLedgerId,
            ["AutomaticGroupId"] = group.자동집단Id,
            ["ProductKey"] = group.상품키,
            ["ProductName"] = group.상품명,
            ["AggregationMode"] = "SumOfIndividualOrders"
        };

    private static string 공동구매주문집계원장Id생성(string sourceLedgerId, string automaticGroupId)
        => $"{sourceLedgerId}-{automaticGroupId}-group-order";

    private static string 개별주문원장Id생성(string sourceLedgerId, string demandId)
        => $"{sourceLedgerId}-{demandId}-individual-order";

    private static IReadOnlyDictionary<string, string> 안전한입고참조(
        공동구매자동집단응답 group,
        공동구매자동수요응답 demand,
        string 주문원장Id)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AutomaticGroupId"] = group.자동집단Id,
            ["DemandId"] = demand.수요Id,
            ["IndividualOrderLedgerId"] = 주문원장Id,
            ["DestinationWarehouseId"] = demand.도착창고Id?.ToString() ?? string.Empty,
            ["DestinationWarehouseType"] = demand.도착창고유형,
            ["DestinationWarehouseName"] = demand.도착창고명,
            ["ReceivingAddressReference"] = demand.수령지주소참조키,
            ["InboundMeaningStatus"] = 공동구매개별주문입고상태코드.입고예정,
            ["VirtualWarehouse"] = string.Equals(
                demand.도착창고유형,
                창고유형코드.가상창고,
                StringComparison.OrdinalIgnoreCase).ToString(),
            ["ProductKey"] = group.상품키,
            ["ProductName"] = group.상품명,
            ["RequestedQuantity"] = demand.희망수량.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["QuantityUnit"] = demand.수량단위,
            ["SourceGroupPurchaseLedgerId"] = demand.커뮤니티원장Id
        };
}
