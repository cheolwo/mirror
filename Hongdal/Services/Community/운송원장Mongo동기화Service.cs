using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using 홍달.Data;
using 홍달.도메인.공통;
using 홍달.도메인.운송;
using 홍달.도메인.화주;
using 홍달.Services.Dispatch.Engine;

namespace Hongdal.Services.Community;

public interface I운송원장Mongo동기화Service
{
    Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(
        화주운송의뢰 의뢰,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task<커뮤니티원장Dto?> 운송실행투영동기화Async(
        운송원장 운송실행투영,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task<운송원장Mongo동기화상태> 상태조회Async(
        string 의뢰Id,
        CancellationToken cancellationToken = default);
}

public sealed class 운송원장Mongo동기화Service : I운송원장Mongo동기화Service
{
    private readonly HongdalContext _db;
    private readonly I커뮤니티원장저장소 _원장저장소;
    private readonly ILogger<운송원장Mongo동기화Service> _logger;

    public 운송원장Mongo동기화Service(
        HongdalContext db,
        I커뮤니티원장저장소 원장저장소,
        ILogger<운송원장Mongo동기화Service> logger)
    {
        _db = db;
        _원장저장소 = 원장저장소;
        _logger = logger;
    }

    public async Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(
        화주운송의뢰 의뢰,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var requestId = Clean(의뢰.의뢰Id);
        if (requestId is null)
        {
            return null;
        }

        var 운송실행투영 = await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.의뢰Id == requestId || x.운송번호 == requestId, cancellationToken);

        return await 저장Async(의뢰, 운송실행투영, updatedBy, cancellationToken);
    }

    public async Task<커뮤니티원장Dto?> 운송실행투영동기화Async(
        운송원장 운송실행투영,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var requestId = FirstNonEmpty(운송실행투영.의뢰Id, 운송실행투영.운송번호);
        화주운송의뢰? 의뢰 = null;
        if (requestId is not null)
        {
            의뢰 = await _db.화주운송의뢰
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        }

        return await 저장Async(의뢰, 운송실행투영, updatedBy, cancellationToken);
    }

    public async Task<운송원장Mongo동기화상태> 상태조회Async(
        string 의뢰Id,
        CancellationToken cancellationToken = default)
    {
        var requestId = Clean(의뢰Id);
        if (requestId is null)
        {
            return 운송원장Mongo동기화상태.Empty(string.Empty, "의뢰Id가 비어 있습니다.");
        }

        var 원장Id = 운송원장Mongo동기화Builder.원장Id생성(requestId);
        try
        {
            var 원장 = await _원장저장소.원장조회Async(원장Id, cancellationToken);
            var 운송실행투영존재 = await _db.운송원장
                .AsNoTracking()
                .AnyAsync(x => x.의뢰Id == requestId || x.운송번호 == requestId, cancellationToken);

            return new 운송원장Mongo동기화상태(
                원장Id,
                원장 is not null,
                원장?.상태,
                원장?.현재단계Key,
                원장?.대상OsCode,
                원장?.수정시각Utc,
                원장?.블록목록.Count ?? 0,
                운송실행투영존재,
                원장 is null ? "Mongo 운송 원장 문서가 아직 없습니다." : string.Empty);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Mongo 운송 원장 상태 조회에 실패했습니다. RequestId={RequestId}", requestId);
            return 운송원장Mongo동기화상태.Empty(원장Id, "Mongo 운송 원장 상태 조회에 실패했습니다.");
        }
    }

    private async Task<커뮤니티원장Dto?> 저장Async(
        화주운송의뢰? 의뢰,
        운송원장? 운송실행투영,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            var 저장요청 = 운송원장Mongo동기화Builder.저장요청생성(의뢰, 운송실행투영);
            return await _원장저장소.원장저장Async(저장요청, updatedBy, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            var requestId = FirstNonEmpty(의뢰?.의뢰Id, 운송실행투영?.의뢰Id, 운송실행투영?.운송번호) ?? string.Empty;
            _logger.LogWarning(ex, "운송 원장 Mongo 동기화에 실패했습니다. RequestId={RequestId}", requestId);
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.Select(Clean).FirstOrDefault(value => value is not null);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record 운송원장Mongo동기화상태(
    string 원장Id,
    bool Mongo원장존재,
    string? Mongo원장상태,
    string? 현재단계Key,
    string? 대상OsCode,
    DateTime? Mongo원장UpdatedAtUtc,
    int Mongo원장블록수,
    bool Rdb운송실행투영존재,
    string 메시지)
{
    public static 운송원장Mongo동기화상태 Empty(string 원장Id, string 메시지)
        => new(원장Id, false, null, null, null, null, 0, false, 메시지);
}

public static class 운송원장Mongo동기화Builder
{
    public static string 원장Id생성(string 의뢰Id)
        => $"transport:{의뢰Id.Trim()}";

    public static 커뮤니티원장저장요청 저장요청생성(
        화주운송의뢰? 의뢰,
        운송원장? 운송실행투영)
    {
        var requestId = FirstNonEmpty(의뢰?.의뢰Id, 운송실행투영?.의뢰Id, 운송실행투영?.운송번호)
                        ?? throw new InvalidOperationException("운송 Mongo 원장을 만들려면 의뢰Id 또는 운송번호가 필요합니다.");
        var 원장Id = FirstNonEmpty(운송실행투영?.커뮤니티원장Id, 원장Id생성(requestId))!;
        var 제목 = BuildTitle(의뢰, 운송실행투영, requestId);
        var 상태 = ResolveLedgerState(의뢰, 운송실행투영);
        var 현재단계 = FirstNonEmpty(운송실행투영?.상태, 의뢰?.배차상태, 의뢰?.상태);
        var 원장템플릿Key = ResolveLedgerTemplateKey(운송실행투영);

        return new 커뮤니티원장저장요청
        {
            원장Id = 원장Id,
            커뮤니티Id = "platform",
            원장템플릿Key = 원장템플릿Key,
            제목 = 제목,
            원함 = BuildWish(의뢰, 운송실행투영),
            상태 = 상태,
            현재단계Key = 현재단계,
            대상OsCode = ResolveOsCode(운송실행투영),
            대상OsName = ResolveOsName(운송실행투영),
            생성자UserId = FirstNonEmpty(의뢰?.화주Id, 의뢰?.주문자UserId, 운송실행투영?.화주Id),
            생성자표시명 = "운송 요청자",
            블록목록 = BuildBlocks(의뢰, 운송실행투영),
            참여자목록 = BuildParticipants(의뢰, 운송실행투영),
            다이어그램스냅샷 = BuildDiagram(원장Id, 원장템플릿Key),
            외부참조 = BuildReferences(의뢰, 운송실행투영, requestId),
            확장속성 = BuildAttributes(의뢰, 운송실행투영)
        };
    }

    private static IReadOnlyList<커뮤니티원장블록Dto> BuildBlocks(
        화주운송의뢰? 의뢰,
        운송원장? 운송실행투영)
        =>
        [
            new()
            {
                BlockId = TransportBlockIds.운송의뢰,
                BlockType = CommunityLedgerBlockTypes.Order,
                Title = "운송 의뢰",
                State = FirstNonEmpty(의뢰?.상태, 의뢰?.배차상태),
                Data = Data(
                    ("업무엔티티", "화주운송의뢰"),
                    ("의뢰Id", 의뢰?.의뢰Id ?? FirstNonEmpty(운송실행투영?.의뢰Id, 운송실행투영?.운송번호)),
                    ("화주Id", 의뢰?.화주Id ?? 운송실행투영?.화주Id),
                    ("주문자UserId", 의뢰?.주문자UserId),
                    ("화물종류", 의뢰?.화물종류),
                    ("화물설명", 의뢰?.화물설명),
                    ("화물수량", Format(의뢰?.화물수량)),
                    ("화물길이Mm", Format(의뢰?.화물길이Mm)),
                    ("화물폭Mm", Format(의뢰?.화물폭Mm)),
                    ("화물높이Mm", Format(의뢰?.화물높이Mm)),
                    ("화물중량Kg", Format(의뢰?.화물중량Kg)),
                    ("화물부피Cbm", Format(의뢰?.화물부피Cbm)),
                    ("팔레트개수", Format(의뢰?.화물팔레트개수)),
                    ("화물파손주의여부", Format(의뢰?.화물파손주의여부)),
                    ("화물온도조건", 의뢰?.화물온도조건),
                    ("운송방식", 의뢰?.운송방식),
                    ("차량종류", 의뢰?.차량종류),
                    ("서비스레벨", 의뢰?.서비스레벨),
                    ("요청사항", 의뢰?.요청사항),
                    ("클라이언트요청Id", 의뢰?.클라이언트요청Id),
                    ("의뢰상태", 의뢰?.상태),
                    ("결제상태", 의뢰?.결제상태),
                    ("배차상태", 의뢰?.배차상태),
                    ("원천유형", 운송실행투영?.원본의뢰유형),
                    ("원천Id", 운송실행투영?.원본의뢰Id),
                    ("원천원장템플릿Key", ResolveSourceLedgerTemplateKey(운송실행투영)),
                    ("배차업무유형", Format(운송실행투영?.배차업무유형)),
                    ("입력Api", "POST api/v1/shipper/requests"))
            },
            new()
            {
                BlockId = TransportBlockIds.상차,
                BlockType = CommunityLedgerBlockTypes.Place,
                Title = "상차",
                State = ResolvePickupState(운송실행투영),
                Data = Data(
                    ("업무엔티티", "화주운송의뢰.상차지"),
                    ("주소", FirstNonEmpty(의뢰?.픽업_도로명주소, 운송실행투영?.픽업_도로명주소, 운송실행투영?.출발지)),
                    ("상세주소", FirstNonEmpty(의뢰?.픽업_상세주소, 운송실행투영?.픽업_상세주소)),
                    ("위도", Format(의뢰?.픽업_위도 ?? 운송실행투영?.픽업_위도)),
                    ("경도", Format(의뢰?.픽업_경도 ?? 운송실행투영?.픽업_경도)),
                    ("연락처이름", 의뢰?.픽업_연락처_이름),
                    ("연락처전화번호", 의뢰?.픽업_연락처_전화번호),
                    ("시간창시작", Format(의뢰?.픽업_시간창_시작일시)),
                    ("시간창종료", Format(의뢰?.픽업_시간창_종료일시)),
                    ("상태변경Api", "POST api/v1/driver/transports/{id}/arrive-pickup, POST api/v1/driver/transports/{id}/pickup-complete"))
            },
            new()
            {
                BlockId = TransportBlockIds.하차,
                BlockType = CommunityLedgerBlockTypes.Place,
                Title = "하차",
                State = ResolveDropoffState(운송실행투영),
                Data = Data(
                    ("업무엔티티", "화주운송의뢰.하차지"),
                    ("주소", FirstNonEmpty(의뢰?.하차_도로명주소, 운송실행투영?.하차_도로명주소, 운송실행투영?.도착지)),
                    ("상세주소", FirstNonEmpty(의뢰?.하차_상세주소, 운송실행투영?.하차_상세주소)),
                    ("위도", Format(의뢰?.하차_위도 ?? 운송실행투영?.하차_위도)),
                    ("경도", Format(의뢰?.하차_경도 ?? 운송실행투영?.하차_경도)),
                    ("연락처이름", 의뢰?.하차_연락처_이름),
                    ("연락처전화번호", 의뢰?.하차_연락처_전화번호),
                    ("시간창시작", Format(의뢰?.하차_시간창_시작일시)),
                    ("시간창종료", Format(의뢰?.하차_시간창_종료일시)),
                    ("상태변경Api", "POST api/v1/driver/transports/{id}/dropoff-arrived, POST api/v1/driver/transports/{id}/complete"))
            },
            new()
            {
                BlockId = TransportBlockIds.결제정산,
                BlockType = CommunityLedgerBlockTypes.Settlement,
                Title = "결제 정산",
                State = FirstNonEmpty(의뢰?.정산상태, 의뢰?.결제상태),
                Data = Data(
                    ("업무엔티티", "화주운송의뢰.정산"),
                    ("결제수단", 의뢰?.결제수단),
                    ("정산시점", 의뢰?.정산시점),
                    ("증빙방식", 의뢰?.증빙방식),
                    ("수납주체", 의뢰?.수납주체),
                    ("정산상태", 의뢰?.정산상태),
                    ("결제상태", 의뢰?.결제상태),
                    ("결제예정금액", Format(의뢰?.결제예정금액)),
                    ("최종운임", Format(의뢰?.최종운임 ?? 운송실행투영?.운임)),
                    ("대기료", Format(의뢰?.대기료)),
                    ("수작업비", Format(의뢰?.수작업비)),
                    ("할증", Format(의뢰?.할증)),
                    ("세금계산서필요", Format(의뢰?.세금계산서필요)),
                    ("현금영수증필요", Format(의뢰?.현금영수증필요)),
                    ("정산메모", 의뢰?.정산메모),
                    ("이벤트조회Api", "GET api/v1/transport-request-ledgers/{requestId}/events"))
            }
        ];

    private static IReadOnlyList<커뮤니티원장참여자Dto> BuildParticipants(
        화주운송의뢰? 의뢰,
        운송원장? 운송실행투영)
    {
        var participants = new List<커뮤니티원장참여자Dto>();
        AddParticipant(participants, FirstNonEmpty(의뢰?.화주Id, 운송실행투영?.화주Id), "화주", "요청자");
        AddParticipant(participants, 의뢰?.주문자UserId, "주문자", "요청자");
        AddParticipant(participants, FirstNonEmpty(운송실행투영?.기사_운송자, 운송실행투영?.확정기사Id), "기사", "운반자");
        AddParticipant(participants, null, FirstNonEmpty(의뢰?.픽업_연락처_이름, "상차 확인자")!, "상차 확인자");
        AddParticipant(participants, null, FirstNonEmpty(의뢰?.하차_연락처_이름, "하차 확인자")!, "수령 확인자");

        return participants
            .GroupBy(x => $"{x.UserId}|{x.DisplayName}|{x.RoleLabel}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
    }

    private static DiagramSnapshotDto BuildDiagram(string 원장Id, string 원장템플릿Key)
        => new()
        {
            DiagramId = $"{원장Id}:diagram",
            DiagramName = "화물 운송 원장 흐름",
            LedgerId = 원장Id,
            LedgerTemplateKey = 원장템플릿Key,
            WorkflowModeKey = "hongdal-v1-transport",
            Nodes =
            [
                Node(TransportBlockIds.운송의뢰, "운송 의뢰", CommunityLedgerBlockTypes.Order, "의뢰", 120, 160),
                Node(TransportBlockIds.상차, "상차", CommunityLedgerBlockTypes.Place, "상차", 360, 160),
                Node(TransportBlockIds.하차, "하차", CommunityLedgerBlockTypes.Place, "하차", 600, 160),
                Node(TransportBlockIds.결제정산, "결제 정산", CommunityLedgerBlockTypes.Settlement, "정산", 840, 160)
            ],
            Edges =
            [
                Edge("edge-request-pickup", TransportBlockIds.운송의뢰, TransportBlockIds.상차, "상차 준비", CommunityLedgerRelationTypes.Requires),
                Edge("edge-pickup-dropoff", TransportBlockIds.상차, TransportBlockIds.하차, "운송", CommunityLedgerRelationTypes.Flow),
                Edge("edge-dropoff-settlement", TransportBlockIds.하차, TransportBlockIds.결제정산, "완료 후 정산", CommunityLedgerRelationTypes.Requires),
                Edge("edge-request-settlement", TransportBlockIds.운송의뢰, TransportBlockIds.결제정산, "운임 기준", CommunityLedgerRelationTypes.Reference)
            ],
            Metadata = Data(
                ("Source", "TransportLedgerMongoSync"),
                ("RdbTransportProjectionRole", "운송 실행 투영"),
                ("PrimaryStore", "MongoDB community_ledgers"))
        };

    private static IReadOnlyDictionary<string, string> BuildReferences(
        화주운송의뢰? 의뢰,
        운송원장? 운송실행투영,
        string requestId)
        => Data(
            ("화주운송의뢰Id", requestId),
            ("Rdb화주운송의뢰Pk", Format(의뢰?.Id)),
            ("운송실행투영Id", Format(운송실행투영?.Id)),
            ("커뮤니티원장Id", 운송실행투영?.커뮤니티원장Id),
            ("운송번호", FirstNonEmpty(운송실행투영?.운송번호, requestId)),
            ("원천유형", 운송실행투영?.원본의뢰유형),
            ("원천Id", 운송실행투영?.원본의뢰Id),
            ("원천원장템플릿Key", ResolveSourceLedgerTemplateKey(운송실행투영)),
            ("원천OS", ResolveSourceOsCode(운송실행투영)),
            ("RdbTransportProjectionTable", "운송실행투영"),
            ("RdbTransportProjectionType", nameof(운송원장)));

    private static IReadOnlyDictionary<string, string> BuildAttributes(
        화주운송의뢰? 의뢰,
        운송원장? 운송실행투영)
        => Data(
            ("원장원본저장소", "MongoDB"),
            ("Rdb역할", "운송 실행 투영과 조회 인덱스"),
            ("동기화정책", "Mongo 원장 문서를 원본으로 보고 RDB 운송 데이터는 배차와 조회를 위한 투영으로 갱신합니다."),
            ("원천추적정책", "원본의뢰유형과 원본의뢰Id를 Mongo 외부참조와 블록 속성에 남겨 창고, 마트, 음식, 공동주문 유입을 역추적합니다."),
            ("원천유형", 운송실행투영?.원본의뢰유형),
            ("원천Id", 운송실행투영?.원본의뢰Id),
            ("원천원장템플릿Key", ResolveSourceLedgerTemplateKey(운송실행투영)),
            ("원천OS", ResolveSourceOsCode(운송실행투영)),
            ("운송상태", 운송실행투영?.상태),
            ("배차큐단계", Format(운송실행투영?.배차큐단계)),
            ("배차노출상태", Format(운송실행투영?.배차노출상태)),
            ("의뢰상태", 의뢰?.상태),
            ("배차상태", 의뢰?.배차상태));

    private static string ResolveLedgerTemplateKey(운송원장? 운송실행투영)
    {
        var sourceType = 운송실행투영?.원본의뢰유형;
        if (운송의뢰배차원천유형.Is음식점주문(sourceType))
        {
            return CommunityLedgerTemplateKeys.FoodDelivery;
        }

        if (운송의뢰배차원천유형.Is홍달마트음식주문(sourceType)
            || 운송의뢰배차원천유형.IsAny(sourceType, 운송의뢰배차원천유형.홍달마트출고))
        {
            return CommunityLedgerTemplateKeys.HongdalMart;
        }

        return CommunityLedgerTemplateKeys.CargoTransport;
    }

    private static string ResolveOsCode(운송원장? 운송실행투영)
    {
        var templateKey = ResolveLedgerTemplateKey(운송실행투영);
        return templateKey switch
        {
            CommunityLedgerTemplateKeys.FoodDelivery => CommunityLedgerOperatingSystemCodes.FoodDelivery,
            CommunityLedgerTemplateKeys.HongdalMart => CommunityLedgerOperatingSystemCodes.HongdalMartUrbanLogistics,
            _ => CommunityLedgerOperatingSystemCodes.DomesticCargoTransport
        };
    }

    private static string ResolveOsName(운송원장? 운송실행투영)
    {
        var templateKey = ResolveLedgerTemplateKey(운송실행투영);
        return templateKey switch
        {
            CommunityLedgerTemplateKeys.FoodDelivery => "음식 배달 OS",
            CommunityLedgerTemplateKeys.HongdalMart => "알뜰살뜰 마트 도심 물류 OS",
            _ => "국내 화물 운송 OS"
        };
    }

    private static string? ResolveSourceLedgerTemplateKey(운송원장? 운송실행투영)
    {
        var sourceType = 운송실행투영?.원본의뢰유형;
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return null;
        }

        if (운송의뢰배차원천유형.Is음식점주문(sourceType))
        {
            return CommunityLedgerTemplateKeys.FoodOrder;
        }

        if (운송의뢰배차원천유형.Is홍달마트음식주문(sourceType)
            || 운송의뢰배차원천유형.IsAny(sourceType, 운송의뢰배차원천유형.홍달마트출고))
        {
            return CommunityLedgerTemplateKeys.HongdalMart;
        }

        if (운송의뢰배차원천유형.IsAny(sourceType, 운송의뢰배차원천유형.창고출고연계운송, 운송의뢰배차원천유형.판매채널출고))
        {
            return CommunityLedgerTemplateKeys.WarehouseOutbound;
        }

        if (운송의뢰배차원천유형.IsAny(sourceType, 운송의뢰배차원천유형.공동주문국내운송))
        {
            return CommunityLedgerTemplateKeys.GroupImport;
        }

        return CommunityLedgerTemplateKeys.CargoTransport;
    }

    private static string? ResolveSourceOsCode(운송원장? 운송실행투영)
    {
        var sourceTemplate = ResolveSourceLedgerTemplateKey(운송실행투영);
        return sourceTemplate switch
        {
            CommunityLedgerTemplateKeys.FoodOrder => CommunityLedgerOperatingSystemCodes.FoodDelivery,
            CommunityLedgerTemplateKeys.HongdalMart => CommunityLedgerOperatingSystemCodes.HongdalMartUrbanLogistics,
            CommunityLedgerTemplateKeys.WarehouseOutbound => CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
            CommunityLedgerTemplateKeys.GroupImport => CommunityLedgerOperatingSystemCodes.GroupPurchaseImport,
            CommunityLedgerTemplateKeys.CargoTransport => CommunityLedgerOperatingSystemCodes.DomesticCargoTransport,
            _ => null
        };
    }

    private static string BuildTitle(화주운송의뢰? 의뢰, 운송원장? 운송실행투영, string requestId)
    {
        var origin = FirstNonEmpty(의뢰?.픽업_도로명주소, 운송실행투영?.출발지, 운송실행투영?.픽업_도로명주소);
        var destination = FirstNonEmpty(의뢰?.하차_도로명주소, 운송실행투영?.도착지, 운송실행투영?.하차_도로명주소);
        if (origin is not null && destination is not null)
        {
            return $"{origin} -> {destination} 운송";
        }

        return $"화물 운송 원장 {requestId}";
    }

    private static string BuildWish(화주운송의뢰? 의뢰, 운송원장? 운송실행투영)
    {
        var cargo = FirstNonEmpty(의뢰?.화물종류, 의뢰?.화물설명);
        var destination = FirstNonEmpty(의뢰?.하차_도로명주소, 운송실행투영?.도착지);
        if (cargo is not null && destination is not null)
        {
            return $"{cargo}을(를) {destination}까지 운송하기";
        }

        return cargo is null ? "화물 운송 진행" : $"{cargo} 운송 진행";
    }

    private static string ResolveLedgerState(화주운송의뢰? 의뢰, 운송원장? 운송실행투영)
    {
        if (string.Equals(운송실행투영?.상태, "인수완료", StringComparison.OrdinalIgnoreCase)
            || string.Equals(의뢰?.배차상태, 상태값.배차상태.인수완료, StringComparison.OrdinalIgnoreCase))
        {
            return 커뮤니티원장상태.완료;
        }

        if (운송실행투영 is not null || !string.IsNullOrWhiteSpace(의뢰?.배차상태))
        {
            return 커뮤니티원장상태.진행중;
        }

        return 커뮤니티원장상태.초안;
    }

    private static string ResolvePickupState(운송원장? 운송실행투영)
    {
        var status = Clean(운송실행투영?.상태);
        return status switch
        {
            "상차완료" or "운송중" or "하차지도착" or "인수완료" => "완료",
            "상차지도착" => "도착",
            null => "대기",
            _ => status
        };
    }

    private static string ResolveDropoffState(운송원장? 운송실행투영)
    {
        var status = Clean(운송실행투영?.상태);
        return status switch
        {
            "인수완료" => "완료",
            "하차지도착" => "도착",
            null => "대기",
            _ => status
        };
    }

    private static DiagramNodeDto Node(
        string blockId,
        string title,
        string kind,
        string group,
        double x,
        double y)
        => new()
        {
            NodeId = blockId,
            Kind = kind,
            Title = title,
            GroupLabel = group,
            X = x,
            Y = y,
            RelatedRoute = blockId switch
            {
                TransportBlockIds.운송의뢰 => "/shipper/requests",
                TransportBlockIds.결제정산 => "/shipper/payments",
                _ => "/driver/transports"
            },
            Data = Data(
                ("BlockId", blockId),
                ("원장블록Id", blockId))
        };

    private static DiagramEdgeDto Edge(
        string edgeId,
        string from,
        string to,
        string label,
        string relationType)
        => new()
        {
            EdgeId = edgeId,
            FromNodeId = from,
            ToNodeId = to,
            Label = label,
            MeaningCode = relationType,
            Data = Data(
                ("관계유형", relationType),
                ("Cardinality", CommunityLedgerRelationCardinality.OneToOne),
                ("Required", relationType is CommunityLedgerRelationTypes.Requires ? "true" : "false"))
        };

    private static void AddParticipant(
        List<커뮤니티원장참여자Dto> participants,
        string? userId,
        string displayName,
        string roleLabel)
    {
        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        participants.Add(new 커뮤니티원장참여자Dto
        {
            UserId = Clean(userId),
            DisplayName = Clean(displayName) ?? "익명 참여자",
            RoleLabel = roleLabel,
            ParticipationState = "참여중"
        });
    }

    private static IReadOnlyDictionary<string, string> Data(params (string Key, string? Value)[] values)
        => values
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key.Trim(), x => x.Value!.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
        => values.Select(Clean).FirstOrDefault(value => value is not null);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Format(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private static string? Format(int? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private static string? Format(bool? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private static string? Format(long? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private static string? Format(DateTime? value)
        => value?.ToString("O", CultureInfo.InvariantCulture);

    private static class TransportBlockIds
    {
        public const string 운송의뢰 = "transport-request";
        public const string 상차 = "pickup";
        public const string 하차 = "dropoff";
        public const string 결제정산 = "settlement";
    }
}
