using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using 홍달.Data;

namespace Hongdal.Services.Development;

public static class CommunityLedgerDevelopmentDataSeeder
{
    private const string ShipperUserName = "shipper1";
    private const string DriverUserName = "driver1@hongdal.local";

    public static async Task SeedAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HongdalContext>();
        var ledgerStore = scope.ServiceProvider.GetRequiredService<I커뮤니티원장저장소>();
        var sharingPolicyStore = scope.ServiceProvider.GetRequiredService<I커뮤니티원장공유정책저장소>();

        var users = await db.Users
            .AsNoTracking()
            .Where(user => user.UserName == ShipperUserName || user.UserName == DriverUserName)
            .Select(user => new { user.Id, user.UserName })
            .ToListAsync(cancellationToken);
        var shipperUserId = users.FirstOrDefault(user => user.UserName == ShipperUserName)?.Id;
        var driverUserId = users.FirstOrDefault(user => user.UserName == DriverUserName)?.Id;
        if (string.IsNullOrWhiteSpace(shipperUserId) || string.IsNullOrWhiteSpace(driverUserId))
        {
            logger.LogWarning(
                "Community ledger development samples were skipped because seed users are missing. Shipper={ShipperUser} Driver={DriverUser}",
                ShipperUserName,
                DriverUserName);
            return;
        }

        var linkedTransport = await db.운송원장
            .FirstOrDefaultAsync(
                transport => transport.운송번호 == "V1-DEV-TRN-001",
                cancellationToken);
        if (linkedTransport is not null
            && !string.Equals(
                linkedTransport.커뮤니티원장Id,
                "dev-ledger-cargo-bookshelf",
                StringComparison.Ordinal))
        {
            linkedTransport.커뮤니티원장Id = "dev-ledger-cargo-bookshelf";
            linkedTransport.커뮤니티원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport;
            linkedTransport.커뮤니티원장상태 = 커뮤니티원장상태.진행중;
            linkedTransport.커뮤니티원장동기화시각Utc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var createdCount = 0;
        var updatedCount = 0;
        var samples = CreateSamples(
            shipperUserId,
            driverUserId,
            linkedTransport?.Id,
            linkedTransport?.상태);
        foreach (var request in samples)
        {
            var ledger = await ledgerStore.원장조회Async(request.원장Id!, cancellationToken);
            if (ledger is null)
            {
                ledger = await ledgerStore.원장저장Async(request, shipperUserId, cancellationToken);
                createdCount++;
            }
            else if (ShouldAttachTransportProjection(ledger, request))
            {
                var updateRequest = BuildTransportProjectionLinkRequest(ledger, request);
                ledger = await ledgerStore.원장저장Async(updateRequest, shipperUserId, cancellationToken);
                updatedCount++;
            }

            var existingPolicy = await sharingPolicyStore.조회Async(ledger.원장Id, cancellationToken);
            if (existingPolicy is null)
            {
                await sharingPolicyStore.저장Async(
                    new 커뮤니티원장공유정책
                    {
                        원장Id = ledger.원장Id,
                        소유자UserId = shipperUserId,
                        공개범위 = 커뮤니티원장공개범위.전체공개,
                        재사용허용여부 = true,
                        재공유허용여부 = true,
                        공개항목Key목록 = BuildPublicItemKeys(ledger)
                    },
                    기대Revision: null,
                    cancellationToken);
            }
        }

        logger.LogInformation(
            "Community ledger development samples are ready. Created={CreatedCount} Updated={UpdatedCount} Total={TotalCount}",
            createdCount,
            updatedCount,
            samples.Count);
    }

    private static IReadOnlyList<커뮤니티원장저장요청> CreateSamples(
        string shipperUserId,
        string driverUserId,
        long? linkedTransportId,
        string? linkedTransportState)
    {
        var bookshelfBlocks = new[]
        {
            Block("participants", CommunityLedgerBlockTypes.Participant, "참여자", "확인 완료",
                ("요청자", "목동 생활가구 나눔"), ("운반자", "개발용 기사")),
            Block("pickup", CommunityLedgerBlockTypes.Place, "상차지", "확인 완료",
                ("주소", "서울 양천구 목동 7단지"), ("희망시간", "토요일 10:00")),
            Block("dropoff", CommunityLedgerBlockTypes.Place, "하차지", "확인 완료",
                ("주소", "경기 부천시 중동"), ("엘리베이터", "있음")),
            Block("cargo", CommunityLedgerBlockTypes.Item, "운송 물품", "확인 완료",
                ("품목", "원목 책장 1개"), ("크기", "180 x 80 x 35cm")),
            Block("transport", CommunityLedgerBlockTypes.State, "상차·하차", "상차 확인 대기",
                ("다음행동", "상차 사진과 인수 확인")),
            Block("settlement", CommunityLedgerBlockTypes.Settlement, "정산 표시", "하차 후 확인",
                ("방식", "당사자 간 직접 확인"))
        };

        var refrigeratorBlocks = new[]
        {
            Block("participants", CommunityLedgerBlockTypes.Participant, "참여자", "운반자 모집",
                ("요청자", "살뜰 중고가전 모임"), ("수령자", "익명 참여자")),
            Block("pickup", CommunityLedgerBlockTypes.Place, "상차지", "확인 완료",
                ("주소", "서울 강서구 화곡동"), ("층수", "3층, 엘리베이터 없음")),
            Block("dropoff", CommunityLedgerBlockTypes.Place, "하차지", "확인 완료",
                ("주소", "서울 영등포구 당산동"), ("층수", "1층")),
            Block("cargo", CommunityLedgerBlockTypes.Item, "운송 물품", "포장 필요",
                ("품목", "소형 냉장고 1대"), ("주의사항", "세워서 운송")),
            Block("transport", CommunityLedgerBlockTypes.State, "운송 진행", "조건 조율 중",
                ("다음행동", "운반 가능 시간과 작업비 확인"))
        };

        var neighborhoodBlocks = new[]
        {
            Block("participants", CommunityLedgerBlockTypes.Participant, "참여자", "3명 참여",
                ("요청자", "동네 반찬 나눔 모임"), ("전달자", "익명 참여자")),
            Block("request", CommunityLedgerBlockTypes.Order, "생활 요청", "접수 완료",
                ("내용", "공유 냉장고 반찬 꾸러미 전달"), ("수량", "3꾸러미")),
            Block("pickup", CommunityLedgerBlockTypes.Place, "수령 장소", "확정",
                ("주소", "서울 양천구 주민센터 앞"), ("시간", "오늘 18:30")),
            Block("handoff", CommunityLedgerBlockTypes.Handoff, "전달", "준비 중",
                ("다음행동", "꾸러미 수량 확인 후 전달"))
        };

        var saleBlocks = new[]
        {
            Block("sale-item", CommunityLedgerBlockTypes.Item, "공동 판매 상품", "판매 확정",
                ("상품", "재생 세제 리필 2L"), ("단가", "8,500원")),
            Block("sale-stock", CommunityLedgerBlockTypes.Inventory, "판매 가능 수량", "재고 확보",
                ("확보수량", "40개"), ("판매자", "살뜰 생활상점")),
            Block("sale-settlement", CommunityLedgerBlockTypes.Settlement, "판매 정산", "주문 합계 대기",
                ("방식", "공동주문 확정 후 결제 표시"))
        };

        var warehouseBlocks = new[]
        {
            Block("outbound-order", CommunityLedgerBlockTypes.Order, "공동주문 출고 지시", "출고 예정",
                ("상품", "재생 세제 리필 2L"), ("예정수량", "24개")),
            Block("picking", CommunityLedgerBlockTypes.Inventory, "피킹·포장", "작업 대기",
                ("피킹단위", "6개입 4상자"), ("포장", "누액 방지 완충")),
            Block("transport-handoff", CommunityLedgerBlockTypes.Handoff, "운송 인계", "인계 전",
                ("인계장소", "양천 생활물류 거점"))
        };

        var orderA상세Blocks = new[]
        {
            Block("orderer", CommunityLedgerBlockTypes.Participant, "주문자", "참여 확정",
                ("표시명", "101동 공동주문 참여자")),
            Block("order-item", CommunityLedgerBlockTypes.Order, "주문 항목", "주문 확인",
                ("상품", "재생 세제 리필 2L"), ("수량", "12개")),
            Block("fulfillment", CommunityLedgerBlockTypes.State, "주문 이행", "상품 준비",
                ("수령", "아파트 공동 수령"))
        };

        var orderB상세Blocks = new[]
        {
            Block("orderer", CommunityLedgerBlockTypes.Participant, "주문자", "참여 확정",
                ("표시명", "102동 공동주문 참여자")),
            Block("order-item", CommunityLedgerBlockTypes.Order, "주문 항목", "주문 확인",
                ("상품", "재생 세제 리필 2L"), ("수량", "12개")),
            Block("fulfillment", CommunityLedgerBlockTypes.State, "주문 이행", "상품 준비",
                ("수령", "아파트 공동 수령"))
        };

        var groupPurchaseBlocks = new[]
        {
            Block("individual-orders", CommunityLedgerBlockTypes.Order, "개별 주문 집계", "2건 연결",
                ("참여동", "101동, 102동"), ("총수량", "24개")),
            Block("group-condition", CommunityLedgerBlockTypes.Decision, "공동 조건", "조건 확정",
                ("최소수량", "20개"), ("공동수령", "양천 생활물류 거점")),
            Block("group-progress", CommunityLedgerBlockTypes.State, "공동주문 진행", "판매·출고 준비",
                ("다음행동", "피킹·포장 후 운송 인계"))
        };

        List<커뮤니티원장저장요청> samples =
        [
            CreateRequest(
                "dev-ledger-cargo-bookshelf",
                CommunityLedgerTemplateKeys.CargoTransport,
                "목동 책장 운송 준비",
                "중고 책장을 안전하게 옮기고 상차와 하차 확인을 함께 남기고 싶어요.",
                커뮤니티원장상태.진행중,
                "상차 확인 대기",
                CommunityLedgerOperatingSystemCodes.DomesticCargoTransport,
                "국내 화물 운송 OS",
                shipperUserId,
                driverUserId,
                bookshelfBlocks),
            CreateRequest(
                "dev-ledger-cargo-refrigerator",
                CommunityLedgerTemplateKeys.CargoTransport,
                "주말 소형 냉장고 운송",
                "가까운 이웃과 운반 시간을 맞춰 소형 냉장고를 옮기고 싶어요.",
                커뮤니티원장상태.보류,
                "운반자 모집",
                CommunityLedgerOperatingSystemCodes.DomesticCargoTransport,
                "국내 화물 운송 OS",
                shipperUserId,
                driverUserId,
                refrigeratorBlocks),
            CreateRequest(
                "dev-ledger-neighborhood-handoff",
                CommunityLedgerTemplateKeys.Errand,
                "동네 반찬 꾸러미 전달",
                "커뮤니티 공유 냉장고의 반찬 꾸러미를 약속 장소에서 나누고 싶어요.",
                커뮤니티원장상태.진행중,
                "전달 준비",
                CommunityLedgerOperatingSystemCodes.CommunityTrust,
                "커뮤니티 신뢰 OS",
                shipperUserId,
                driverUserId,
                neighborhoodBlocks)
        ];

        if (linkedTransportId.HasValue)
        {
            var bookshelf = samples[0];
            bookshelf.현재단계Key = string.IsNullOrWhiteSpace(linkedTransportState)
                ? bookshelf.현재단계Key
                : linkedTransportState;
            bookshelf.외부참조 = new Dictionary<string, string>
            {
                ["운송실행투영Id"] = linkedTransportId.Value.ToString(),
                ["운송번호"] = "V1-DEV-TRN-001"
            };
            bookshelf.확장속성 = bookshelf.확장속성
                .Concat(new[]
                {
                    new KeyValuePair<string, string>("운송상태", linkedTransportState ?? "배차확정")
                })
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }

        samples.AddRange(
        [
            CreateRequest(
                "dev-ledger-group-sale",
                CommunityLedgerTemplateKeys.LocalSale,
                "살뜰 생활상점 세제 판매",
                "공동주문 참여자에게 재생 세제를 공급하고 싶어요.",
                커뮤니티원장상태.진행중,
                "재고 확보",
                CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
                "창고·커머스 이행 OS",
                shipperUserId,
                driverUserId,
                saleBlocks),
            CreateRequest(
                "dev-ledger-group-warehouse-outbound",
                CommunityLedgerTemplateKeys.WarehouseOutbound,
                "공동주문 세제 출고",
                "확정된 공동주문 수량을 피킹하고 포장해 운송 담당자에게 넘기고 싶어요.",
                커뮤니티원장상태.진행중,
                "피킹 대기",
                CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
                "창고·커머스 이행 OS",
                shipperUserId,
                driverUserId,
                warehouseBlocks),
            CreateRequest(
                "dev-ledger-group-order-101",
                CommunityLedgerTemplateKeys.Order,
                "101동 재생 세제 주문",
                "101동 참여자 몫의 주문과 판매, 출고, 운송 상태를 한 번에 보고 싶어요.",
                커뮤니티원장상태.진행중,
                "상품 준비",
                CommunityLedgerOperatingSystemCodes.CommunityTrust,
                "커뮤니티 신뢰 OS",
                shipperUserId,
                driverUserId,
                orderA상세Blocks,
                [
                    Included("dev-ledger-group-sale", CommunityLedgerTemplateKeys.LocalSale, 주문원장포함역할.판매, true, 0),
                    Included("dev-ledger-group-warehouse-outbound", CommunityLedgerTemplateKeys.WarehouseOutbound, 주문원장포함역할.창고출고, true, 1),
                    Included("dev-ledger-cargo-bookshelf", CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.운송, true, 2)
                ]),
            CreateRequest(
                "dev-ledger-group-order-102",
                CommunityLedgerTemplateKeys.Order,
                "102동 재생 세제 주문",
                "102동 참여자 몫의 주문과 공동 수령 상태를 확인하고 싶어요.",
                커뮤니티원장상태.진행중,
                "상품 준비",
                CommunityLedgerOperatingSystemCodes.CommunityTrust,
                "커뮤니티 신뢰 OS",
                shipperUserId,
                driverUserId,
                orderB상세Blocks,
                [
                    Included("dev-ledger-group-sale", CommunityLedgerTemplateKeys.LocalSale, 주문원장포함역할.판매, true, 0),
                    Included("dev-ledger-group-warehouse-outbound", CommunityLedgerTemplateKeys.WarehouseOutbound, 주문원장포함역할.창고출고, true, 1),
                    Included("dev-ledger-cargo-bookshelf", CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.운송, false, 2)
                ]),
            CreateRequest(
                "dev-ledger-group-purchase",
                CommunityLedgerTemplateKeys.GroupPurchase,
                "양천 아파트 재생 세제 공동주문",
                "여러 동의 개별 주문을 묶어 판매, 출고와 운송까지 함께 확인하고 싶어요.",
                커뮤니티원장상태.진행중,
                "판매·출고 준비",
                CommunityLedgerOperatingSystemCodes.CommunityTrust,
                "커뮤니티 신뢰 OS",
                shipperUserId,
                driverUserId,
                groupPurchaseBlocks,
                [
                    Included("dev-ledger-group-order-101", CommunityLedgerTemplateKeys.Order, 주문원장포함역할.개별주문, true, 0),
                    Included("dev-ledger-group-order-102", CommunityLedgerTemplateKeys.Order, 주문원장포함역할.개별주문, true, 1)
                ])
        ]);

        return samples;
    }

    private static bool ShouldAttachTransportProjection(
        커뮤니티원장Dto ledger,
        커뮤니티원장저장요청 request)
    {
        if (!string.Equals(ledger.원장Id, "dev-ledger-cargo-bookshelf", StringComparison.Ordinal)
            || !ledger.확장속성.TryGetValue("개발샘플", out var sampleFlag)
            || !string.Equals(sampleFlag, "true", StringComparison.OrdinalIgnoreCase)
            || !request.외부참조.TryGetValue("운송실행투영Id", out var requestedTransportId))
        {
            return false;
        }

        return !ledger.외부참조.TryGetValue("운송실행투영Id", out var currentTransportId)
               || !string.Equals(currentTransportId, requestedTransportId, StringComparison.Ordinal);
    }

    private static 커뮤니티원장저장요청 BuildTransportProjectionLinkRequest(
        커뮤니티원장Dto ledger,
        커뮤니티원장저장요청 request)
    {
        var references = ledger.외부참조.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.외부참조)
        {
            references[item.Key] = item.Value;
        }

        var attributes = ledger.확장속성.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        if (request.확장속성.TryGetValue("운송상태", out var transportState))
        {
            attributes["운송상태"] = transportState;
        }

        return new 커뮤니티원장저장요청
        {
            원장Id = ledger.원장Id,
            기대Revision = ledger.Revision,
            커뮤니티Id = ledger.커뮤니티Id,
            원장템플릿Key = ledger.원장템플릿Key,
            제목 = ledger.제목,
            원함 = ledger.원함,
            상태 = ledger.상태,
            현재단계Key = request.현재단계Key ?? ledger.현재단계Key,
            대상OsCode = ledger.대상OsCode,
            대상OsName = ledger.대상OsName,
            생성자UserId = ledger.생성자UserId,
            생성자표시명 = ledger.생성자표시명,
            블록목록 = ledger.블록목록,
            참여자목록 = ledger.참여자목록,
            포함원장목록 = ledger.포함원장목록,
            다이어그램스냅샷 = ledger.다이어그램스냅샷,
            외부참조 = references,
            확장속성 = attributes
        };
    }

    private static 커뮤니티원장저장요청 CreateRequest(
        string ledgerId,
        string templateKey,
        string title,
        string wish,
        string state,
        string currentStep,
        string osCode,
        string osName,
        string shipperUserId,
        string driverUserId,
        IReadOnlyList<커뮤니티원장블록Dto> blocks,
        IReadOnlyList<커뮤니티포함원장참조Dto>? includedLedgers = null)
        => new()
        {
            원장Id = ledgerId,
            커뮤니티Id = "platform",
            원장템플릿Key = templateKey,
            제목 = title,
            원함 = wish,
            상태 = state,
            현재단계Key = currentStep,
            대상OsCode = osCode,
            대상OsName = osName,
            생성자UserId = shipperUserId,
            생성자표시명 = "살뜰 운영자",
            블록목록 = blocks,
            포함원장목록 = includedLedgers,
            참여자목록 =
            [
                new 커뮤니티원장참여자Dto
                {
                    UserId = shipperUserId,
                    DisplayName = "살뜰 운영자",
                    RoleLabel = "요청자",
                    ParticipationState = "참여중"
                },
                new 커뮤니티원장참여자Dto
                {
                    UserId = driverUserId,
                    DisplayName = "개발용 참여자",
                    RoleLabel = templateKey == CommunityLedgerTemplateKeys.CargoTransport ? "운반자" : "전달자",
                    ParticipationState = "참여중"
                }
            ],
            다이어그램스냅샷 = BuildDiagram(ledgerId, templateKey, title, blocks),
            확장속성 = new Dictionary<string, string>
            {
                ["개발샘플"] = "true",
                ["표시용도"] = "원장 선택 화면 검증"
            }
        };

    private static 커뮤니티포함원장참조Dto Included(
        string ledgerId,
        string templateKey,
        string role,
        bool required,
        int displayOrder)
        => new()
        {
            원장Id = ledgerId,
            원장템플릿Key = templateKey,
            역할 = role,
            필수여부 = required,
            표시순서 = displayOrder
        };

    private static 커뮤니티원장블록Dto Block(
        string blockId,
        string blockType,
        string title,
        string state,
        params (string Key, string Value)[] data)
        => new()
        {
            BlockId = blockId,
            BlockType = blockType,
            Title = title,
            State = state,
            Data = data.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
        };

    private static DiagramSnapshotDto BuildDiagram(
        string ledgerId,
        string templateKey,
        string title,
        IReadOnlyList<커뮤니티원장블록Dto> blocks)
    {
        var nodes = blocks.Select((block, index) => new DiagramNodeDto
        {
            NodeId = block.BlockId,
            Kind = block.BlockType,
            Title = block.Title,
            GroupLabel = "원장 블록",
            Description = block.State,
            X = 80 + (index % 3 * 250),
            Y = 90 + (index / 3 * 180),
            Data = block.Data
        }).ToArray();
        var edges = nodes.Zip(nodes.Skip(1), (from, to) => new DiagramEdgeDto
        {
            EdgeId = $"{from.NodeId}-{to.NodeId}",
            FromNodeId = from.NodeId,
            ToNodeId = to.NodeId,
            Label = "다음 단계",
            MeaningCode = CommunityLedgerRelationTypes.Flow
        }).ToArray();

        return new DiagramSnapshotDto
        {
            DiagramId = $"diagram-{ledgerId}",
            DiagramName = $"{title} 흐름",
            LedgerId = ledgerId,
            LedgerTemplateKey = templateKey,
            WorkflowModeKey = templateKey,
            Nodes = nodes,
            Edges = edges,
            Metadata = new Dictionary<string, string>
            {
                ["개발샘플"] = "true"
            }
        };
    }

    private static IReadOnlyList<string> BuildPublicItemKeys(커뮤니티원장Dto ledger)
    {
        var keys = new List<string>
        {
            커뮤니티원장공개항목Key.제목,
            커뮤니티원장공개항목Key.상태,
            커뮤니티원장공개항목Key.현재단계,
            커뮤니티원장공개항목Key.다이어그램구조
        };
        foreach (var block in ledger.블록목록)
        {
            keys.Add(커뮤니티원장공개항목Key.블록제목(block.BlockId));
            keys.Add(커뮤니티원장공개항목Key.블록상태(block.BlockId));
            keys.AddRange(block.Data.Keys.Select(key => 커뮤니티원장공개항목Key.블록Data(block.BlockId, key)));
        }

        return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
