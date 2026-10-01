using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Sales;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;
using HongdalApp.Services.Application;
using HongdalApp.Services.Commerce;
using HongdalApp.Services.Commerce.Orders;
using HongdalApp.Services.Customs;
using HongdalApp.Services.Warehouse.Fulfillment;
using HongdalApp.Models.Shipper;

namespace HongdalApp.Services;

public sealed class InMemoryShipperStore
{
    private readonly List<창고요약응답> _warehouses =
    [
        new 창고요약응답
        {
            Id = 101,
            창고명 = "서울 서부 허브",
            소유자UserId = "shipper-demo",
            창고유형 = "실제창고",
            물류대행지분류 = LogisticsProxySiteTypes.DeliveryAgency,
            주소 = "서울 강서구 화곡로 100",
            담당자명 = "김창고",
            연락처 = "010-1111-2222",
            IsActive = true
        },
        new 창고요약응답
        {
            Id = 102,
            창고명 = "수원 냉장 센터",
            소유자UserId = "shipper-demo",
            창고유형 = "실제창고",
            물류대행지분류 = LogisticsProxySiteTypes.MarketFulfillment,
            주소 = "경기 수원시 권선구 산업로 20",
            담당자명 = "박냉장",
            연락처 = "010-3333-4444",
            IsActive = true
        },
        new 창고요약응답
        {
            Id = 103,
            창고명 = "오사카 통관 배송 대행지",
            소유자UserId = "shipper-demo",
            창고유형 = "실제창고",
            물류대행지분류 = LogisticsProxySiteTypes.OverseasCustomsAgency,
            주소 = "Osaka, Japan",
            담당자명 = "글로벌통관팀",
            연락처 = "+81-6-0000-0000",
            IsActive = true
        }
    ];

    private readonly List<입고요청항목응답> _inbounds =
    [
        new 입고요청항목응답
        {
            Id = 2001,
            창고Id = 101,
            입고흐름유형 = 입고흐름유형코드.계약기반입고,
            입고생성경로 = "계약 DB 기반 등록",
            계약선행여부 = true,
            자동생성여부 = false,
            주문자UserId = "shipper-demo",
            공급처명 = "한빛가구",
            원주문참조번호 = "PO-240701-01",
            상태 = "입고예정",
            예정도착일 = DateTime.Today.AddDays(1),
            계약정보 = new 입고계약스냅샷
            {
                계약번호 = "CTR-STO-240701-01",
                계약유형 = 입고계약유형코드.보관대행,
                계약상대방명 = "한빛가구",
                정산방식 = "월 보관료 정산",
                보관료일단가 = 120m,
                계약시작일 = DateTime.Today.AddDays(-10)
            }.Normalize()
        },
        new 입고요청항목응답
        {
            Id = 2002,
            창고Id = 102,
            입고흐름유형 = 입고흐름유형코드.주문자동입고예정,
            입고생성경로 = "주문/구매 흐름 자동 생성",
            계약선행여부 = false,
            자동생성여부 = true,
            주문자UserId = "shipper-demo",
            공급처명 = "청해푸드",
            원주문참조번호 = "PO-240701-02",
            상태 = "입고완료",
            예정도착일 = DateTime.Today.AddDays(-1),
            입고완료일시 = DateTime.Now.AddHours(-5),
            계약정보 = new 입고계약스냅샷
            {
                계약번호 = "CTR-MKT-240701-02",
                계약유형 = 입고계약유형코드.마켓풀필먼트,
                계약상대방명 = "청해푸드",
                정산방식 = "판매 건별 정산",
                판매수수료율 = 8m,
                보관료일단가 = 80m,
                계약시작일 = DateTime.Today.AddMonths(-1)
            }.Normalize()
        }
    ];

    private readonly List<재고항목응답> _inventory =
    [
        new 재고항목응답
        {
            입고상품Id = 3001,
            창고Id = 101,
            창고명 = "서울 서부 허브",
            소유자UserId = "shipper-demo",
            판매자UserId = "seller-a",
            상품명 = "원목 의자 세트",
            SKU = "CHAIR-SET-01",
            옵션명 = "월넛",
            가용수량 = 8,
            예약수량 = 1,
            상태 = "보관중",
            보관위치 = "A-01-03",
            계약정보 = new 입고계약스냅샷
            {
                계약번호 = "CTR-COM-240701-01",
                계약유형 = 입고계약유형코드.위탁판매,
                계약상대방명 = "한빛가구",
                정산방식 = "월 판매대금 정산",
                판매수수료율 = 12m,
                보관료일단가 = 120m,
                계약시작일 = DateTime.Today.AddMonths(-1)
            }.Normalize()
        },
        new 재고항목응답
        {
            입고상품Id = 3002,
            창고Id = 102,
            창고명 = "수원 냉장 센터",
            소유자UserId = "shipper-demo",
            판매자UserId = "seller-b",
            상품명 = "냉동 만두 박스",
            SKU = "FOOD-DUMPLING-77",
            옵션명 = "1kg x 10",
            가용수량 = 20,
            예약수량 = 4,
            상태 = "보관중",
            보관위치 = "C-02-01",
            계약정보 = new 입고계약스냅샷
            {
                계약번호 = "CTR-MKT-240701-02",
                계약유형 = 입고계약유형코드.마켓풀필먼트,
                계약상대방명 = "청해푸드",
                정산방식 = "판매 건별 정산",
                판매수수료율 = 8m,
                보관료일단가 = 80m,
                계약시작일 = DateTime.Today.AddMonths(-1)
            }.Normalize()
        }
    ];

    private readonly List<판매채널계정항목응답> _accounts =
    [
        new 판매채널계정항목응답
        {
            Id = 4001,
            채널종류 = CommerceChannelKeys.SmartStore,
            상점명 = "살뜰 셀렉트",
            연결상태 = SalesStatusCodes.AccountConnected,
            마지막동기화일시 = DateTime.UtcNow.AddMinutes(-15)
        },
        new 판매채널계정항목응답
        {
            Id = 4002,
            채널종류 = CommerceChannelKeys.Coupang,
            상점명 = "살뜰 로켓제휴",
            연결상태 = SalesStatusCodes.AccountConnected,
            마지막동기화일시 = DateTime.UtcNow.AddHours(-2)
        },
        new 판매채널계정항목응답
        {
            Id = 4003,
            채널종류 = CommerceChannelKeys.Shopify,
            상점명 = "Hongdal Global Store",
            연결상태 = SalesStatusCodes.AccountConnected,
            마지막동기화일시 = DateTime.UtcNow.AddHours(-4)
        },
        new 판매채널계정항목응답
        {
            Id = 4004,
            채널종류 = CommerceChannelKeys.Amazon,
            상점명 = "Hongdal Amazon Seller",
            연결상태 = SalesStatusCodes.AccountConnected,
            마지막동기화일시 = DateTime.UtcNow.AddHours(-6)
        },
        new 판매채널계정항목응답
        {
            Id = 4005,
            채널종류 = CommerceChannelKeys.Ebay,
            상점명 = "Hongdal eBay",
            연결상태 = SalesStatusCodes.AccountConnected,
            마지막동기화일시 = DateTime.UtcNow.AddHours(-8)
        }
    ];

    private readonly List<WarehouseStorageBinInventory> _storageBins =
    [
        new()
        {
            WarehouseId = 101,
            WarehouseName = "서울 서부 허브",
            BinCode = "A-01-03",
            Sku = "SALE-CHAIR-SET-01",
            ProductName = "원목 의자 세트",
            AvailableQuantity = 3,
            ReceivedAt = DateTime.UtcNow.AddDays(-12),
            ExpirationDate = null,
            PickPriority = 1
        },
        new()
        {
            WarehouseId = 101,
            WarehouseName = "서울 서부 허브",
            BinCode = "A-02-01",
            Sku = "SALE-CHAIR-SET-01",
            ProductName = "원목 의자 세트",
            AvailableQuantity = 5,
            ReceivedAt = DateTime.UtcNow.AddDays(-5),
            ExpirationDate = null,
            PickPriority = 2
        },
        new()
        {
            WarehouseId = 102,
            WarehouseName = "수원 냉장 센터",
            BinCode = "C-02-01",
            Sku = "SKU-FOOD-002",
            ProductName = "냉장 간편식 박스",
            AvailableQuantity = 1,
            ReceivedAt = DateTime.UtcNow.AddDays(-7),
            ExpirationDate = DateTime.UtcNow.AddDays(6),
            PickPriority = 1
        },
        new()
        {
            WarehouseId = 102,
            WarehouseName = "수원 냉장 센터",
            BinCode = "C-03-02",
            Sku = "SKU-FOOD-002",
            ProductName = "냉장 간편식 박스",
            AvailableQuantity = 10,
            ReceivedAt = DateTime.UtcNow.AddDays(-3),
            ExpirationDate = DateTime.UtcNow.AddDays(12),
            PickPriority = 2
        }
    ];

    private readonly List<판매상품항목응답> _products =
    [
        new 판매상품항목응답
        {
            Id = 5001,
            입고상품Id = 3001,
            대표상품명 = "원목 의자 세트",
            판매SKU = "SALE-CHAIR-SET-01",
            판매가 = 159000m,
            상태 = SalesStatusCodes.ProductActive
        },
        new 판매상품항목응답
        {
            Id = 5002,
            입고상품Id = 3002,
            대표상품명 = "냉장 간편식 박스",
            판매SKU = "SKU-FOOD-002",
            판매가 = 42000m,
            상태 = SalesStatusCodes.ProductActive
        }
    ];

    private readonly Dictionary<string, int> _sellerSafetyStockBySku = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SALE-CHAIR-SET-01"] = 6,
        ["SKU-FOOD-002"] = 12
    };

    private readonly List<SellerRestockNotificationPreference> _sellerRestockNotificationPreferences =
    [
        new()
        {
            SellerUserId = "seller-a",
            SellerName = "한빛가구",
            KakaoTalkChannelName = "한빛가구 물류 담당",
            AdminAllowsKakaoTalk = true,
            SellerWantsKakaoTalk = true
        },
        new()
        {
            SellerUserId = "seller-b",
            SellerName = "청해푸드",
            KakaoTalkChannelName = "청해푸드 입고 담당",
            AdminAllowsKakaoTalk = true,
            SellerWantsKakaoTalk = false
        }
    ];

    private readonly List<채널출품항목응답> _listings =
    [
        new 채널출품항목응답
        {
            Id = 6001,
            판매상품Id = 5001,
            판매채널계정Id = 4001,
            채널상품번호 = "NS-90001",
            출품상태 = SalesStatusCodes.ListingCompleted,
            동기화상태 = SalesStatusCodes.SyncNormal
        }
    ];

    private readonly List<WarehouseOutboundNotification> _warehouseOutboundNotifications = [];
    private readonly List<InboundRestockNotification> _inboundRestockNotifications = [];
    private readonly List<RestockKakaoTalkOutboxMessage> _restockKakaoTalkOutboxMessages = [];
    private readonly List<WarehouseOrderPickingTask> _orderPickingTasks = [];
    private readonly List<WarehousePackingTask> _packingTasks = [];

    private readonly List<CustomsHsReviewRequest> _customsHsReviews =
    [
        new()
        {
            Id = 8001,
            TransportRequestId = "SHP-EXP-1001",
            ShipperUserId = "shipper-demo",
            CargoName = "원목 의자 세트",
            FlowDirection = CustomsFlowDirectionCodes.Export,
            PickupLocation = "서울 서부 허브",
            DropoffLocation = "Seattle, WA, United States",
            Status = CustomsHsReviewStatusCodes.Requested,
            Suggestions =
            [
                new()
                {
                    HsCode = "9401.69",
                    Description = "목재 프레임 의자류 후보",
                    ConfidenceScore = 0.82m,
                    Reason = "가구/의자 키워드와 수출 흐름 기반 후보입니다."
                }
            ],
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        }
    ];

    private readonly List<AppEventLogEntry> _appEventLogs = [];

    private readonly List<Hongdal.Contracts.Shipper.Request.공개화물요약응답> _publicCargo = new()
    {
        new Hongdal.Contracts.Shipper.Request.공개화물요약응답
        {
            의뢰Id = "SHP-1001",
            화물종류 = "가구",
            운송방식 = "일반운송",
            차량종류 = "1톤",
            화물수량 = 4,
            화물중량Kg = 320.5m,
            의뢰상태 = "공개",
            배차상태 = "대기",
            생성일시 = DateTime.Now.AddHours(-3)
        },
        new Hongdal.Contracts.Shipper.Request.공개화물요약응답
        {
            의뢰Id = "SHP-1002",
            화물종류 = "냉장식품",
            운송방식 = "냉동운송",
            차량종류 = "냉동탑차",
            화물수량 = 12,
            화물중량Kg = 1140.2m,
            의뢰상태 = "공개",
            배차상태 = "대기",
            생성일시 = DateTime.Now.AddHours(-1)
        }
    };

    private readonly List<ShipperRequestItem> _requests = new()
    {
        new ShipperRequestItem
        {
            의뢰Id = "SHP-1001",
            의뢰상태 = "접수",
            결제상태 = "결제대기",
            배차상태 = "매칭중",
            정산상태 = "정산대기",
            운송방식 = "일반운송",
            차량종류 = "1톤",
            결제수단 = "카드",
            결제예정금액 = 180000,
            예상거리Km = 24m,
            기준운임 = 180000,
            기사지급예정운임 = 145000,
            알선단계 = 1,
            재알선금지 = true,
            정책위반 = false,
            재알선의심 = false,
            생성일시 = DateTime.Now.AddHours(-3),
            픽업지 = "서울 강서구",
            하차지 = "경기 수원시"
        },
        new ShipperRequestItem
        {
            의뢰Id = "SHP-1002",
            의뢰상태 = "진행중",
            결제상태 = "결제완료",
            배차상태 = "상차완료",
            정산상태 = "입금대기",
            운송방식 = "냉동운송",
            차량종류 = "냉동탑차",
            결제수단 = "현금영수증",
            결제예정금액 = 420000,
            예상거리Km = 31m,
            기준운임 = 317500,
            기사지급예정운임 = 180000,
            알선단계 = 2,
            재알선금지 = true,
            정책위반 = true,
            재알선의심 = true,
            정책경고목록 =
            [
                "재알선차단필요: 재알선 금지 의뢰인데 알선 단계가 2단계 이상입니다.",
                "기사 지급 예정 운임이 기준운임의 70%보다 낮아 알선 단계 확인이 필요합니다."
            ],
            생성일시 = DateTime.Now.AddHours(-1),
            픽업지 = "인천 연수구",
            하차지 = "서울 송파구"
        },
        new ShipperRequestItem
        {
            의뢰Id = "SHP-SMOKE-PAY",
            의뢰상태 = "진행중",
            결제상태 = "결제대기",
            배차상태 = "상차완료",
            정산상태 = "입금대기",
            운송방식 = "냉동운송",
            차량종류 = "냉동탑차",
            결제수단 = "카드",
            결제예정금액 = 420000,
            예상거리Km = 31m,
            기준운임 = 317500,
            기사지급예정운임 = 240000,
            알선단계 = 1,
            재알선금지 = true,
            정책위반 = false,
            재알선의심 = false,
            생성일시 = DateTime.Now.AddMinutes(-30),
            픽업지 = "인천 연수구",
            하차지 = "서울 송파구"
        }
    };

    private readonly List<decimal> _vehicleRates = new() { 120000, 180000, 240000 };
    private long _warehouseSequence = 104;
    private long _inboundSequence = 2003;
    private long _inventorySequence = 3003;
    private long _accountSequence = 4006;
    private long _productSequence = 5003;
    private long _listingSequence = 6003;
    private long _warehouseOutboundNotificationSequence = 7001;
    private long _inboundRestockNotificationSequence = 7101;
    private long _restockKakaoTalkOutboxSequence = 7201;
    private long _orderPickingTaskSequence = 7501;
    private long _packingTaskSequence = 7601;
    private long _customsHsReviewSequence = 8002;
    private long _appEventLogSequence = 9001;
    private int _requestSequence = 1003;

    public IReadOnlyList<Hongdal.Contracts.Shipper.Request.공개화물요약응답> GetPublicCargo() => _publicCargo.OrderByDescending(x => x.생성일시).ToList();

    public IReadOnlyList<ShipperRequestItem> GetRequests() => _requests.OrderByDescending(x => x.생성일시).ToList();

    public IReadOnlyList<창고요약응답> GetWarehouses() => _warehouses.OrderBy(x => x.Id).ToList();

    public IReadOnlyList<입고요청항목응답> GetInbounds() => _inbounds.OrderByDescending(x => x.Id).ToList();

    public IReadOnlyList<재고항목응답> GetInventory() => _inventory.OrderByDescending(x => x.입고상품Id).ToList();

    public IReadOnlyList<판매채널계정항목응답> GetAccounts() => _accounts.OrderByDescending(x => x.Id).ToList();

    public IReadOnlyList<판매상품항목응답> GetProducts() => _products.OrderByDescending(x => x.Id).ToList();

    public IReadOnlyList<채널출품항목응답> GetListings() => _listings.OrderByDescending(x => x.Id).ToList();

    public IReadOnlyList<WarehouseOutboundNotification> GetWarehouseOutboundNotifications()
        => _warehouseOutboundNotifications.OrderByDescending(x => x.CreatedAt).ToList();

    public IReadOnlyList<WarehouseOrderPickingTask> GetOrderPickingTasks()
        => _orderPickingTasks.OrderByDescending(x => x.CreatedAt).ToList();

    public IReadOnlyList<WarehousePackingTask> GetPackingTasks()
        => _packingTasks.OrderByDescending(x => x.CreatedAt).ToList();

    public IReadOnlyList<MarketInventorySnapshot> GetMarketInventorySnapshots()
        => _products
            .Select(product =>
            {
                var sku = product.판매SKU;
                var bins = _storageBins.Where(x => string.Equals(x.Sku, sku, StringComparison.OrdinalIgnoreCase)).ToList();
                var inventory = FindInventoryByInboundProductId(product.입고상품Id);
                var contract = inventory?.계약정보 ?? 입고계약스냅샷.Default();
                var canSellToMarket = contract.마켓판매가능여부;
                return new MarketInventorySnapshot
                {
                    Sku = sku,
                    ProductName = product.대표상품명,
                    AvailableQuantity = canSellToMarket ? bins.Sum(x => x.OrderableQuantity) : 0,
                    ReservedQuantity = inventory?.예약수량 ?? 0,
                    SafetyStockQuantity = GetSellerSafetyStockQuantity(sku),
                    ContractNo = contract.계약번호,
                    ContractType = 입고계약유형코드.GetDisplayName(contract.계약유형),
                    CanSellToMarket = canSellToMarket,
                    RequiresCustoms = contract.통관필요여부
                };
            })
            .OrderBy(x => x.ProductName)
            .ToList();

    public IReadOnlyList<CustomsHsReviewRequest> GetCustomsHsReviews()
        => _customsHsReviews.OrderByDescending(x => x.CreatedAt).ToList();

    public IReadOnlyList<AppEventLogEntry> GetAppEventLogs()
        => _appEventLogs.OrderByDescending(x => x.OccurredAt).ToList();

    public IReadOnlyList<InboundRestockNotification> GetInboundRestockNotifications()
        => _inboundRestockNotifications
            .Where(x => x.IsInternalNotificationVisible)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

    public IReadOnlyList<SellerRestockNotificationPreference> GetSellerRestockNotificationPreferences()
        => _sellerRestockNotificationPreferences.OrderBy(x => x.SellerName).ToList();

    public IReadOnlyList<RestockKakaoTalkOutboxMessage> GetRestockKakaoTalkOutboxMessages()
        => _restockKakaoTalkOutboxMessages.OrderByDescending(x => x.CreatedAt).ToList();

    public void UpdateSellerRestockNotificationPreference(
        string sellerUserId,
        bool? adminAllowsKakaoTalk = null,
        bool? sellerWantsKakaoTalk = null,
        bool? useInternalNotification = null)
    {
        var preference = GetOrCreateSellerRestockNotificationPreference(sellerUserId);
        if (adminAllowsKakaoTalk.HasValue)
        {
            preference.AdminAllowsKakaoTalk = adminAllowsKakaoTalk.Value;
        }

        if (sellerWantsKakaoTalk.HasValue)
        {
            preference.SellerWantsKakaoTalk = sellerWantsKakaoTalk.Value;
        }

        if (useInternalNotification.HasValue)
        {
            preference.UseInternalNotification = useInternalNotification.Value;
        }
    }

    public CustomsHsReviewRequest? FindCustomsHsReviewByTransportRequestId(string transportRequestId)
        => _customsHsReviews.FirstOrDefault(x => string.Equals(x.TransportRequestId, transportRequestId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<WarehouseStorageBinInventory> GetStorageBinInventory(long warehouseId, string sku)
        => _storageBins
            .Where(x => x.WarehouseId == warehouseId && string.Equals(x.Sku, sku, StringComparison.OrdinalIgnoreCase) && x.OrderableQuantity > 0)
            .ToList();

    public bool HasWarehouseOutboundNotification(string channelType, string channelOrderNo)
        => _warehouseOutboundNotifications.Any(x =>
            string.Equals(x.ChannelType, channelType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ChannelOrderNo, channelOrderNo, StringComparison.OrdinalIgnoreCase));

    public 판매채널계정항목응답? FindAccount(long accountId) => _accounts.FirstOrDefault(x => x.Id == accountId);

    public 판매상품항목응답? FindProduct(long productId) => _products.FirstOrDefault(x => x.Id == productId);

    public 판매상품항목응답? FindProductBySku(string sku)
        => _products.FirstOrDefault(x => string.Equals(x.판매SKU, sku, StringComparison.OrdinalIgnoreCase))
            ?? _products.FirstOrDefault(x => string.Equals(x.샘플데이터코드, sku, StringComparison.OrdinalIgnoreCase));

    public 재고항목응답? FindInventoryByInboundProductId(long inboundProductId)
        => _inventory.FirstOrDefault(x => x.입고상품Id == inboundProductId);

    public 창고요약응답? FindWarehouse(long warehouseId)
        => _warehouses.FirstOrDefault(x => x.Id == warehouseId);

    public bool TryReserveInventory(long inboundProductId, int quantity)
    {
        var inventoryItem = FindInventoryByInboundProductId(inboundProductId);
        if (inventoryItem is null || quantity <= 0 || inventoryItem.가용수량 < quantity)
        {
            return false;
        }

        inventoryItem.가용수량 -= quantity;
        inventoryItem.예약수량 += quantity;
        return true;
    }

    public bool ReserveStorageBins(WarehousePickPlan pickPlan)
    {
        if (!pickPlan.IsComplete)
        {
            return false;
        }

        foreach (var instruction in pickPlan.Instructions)
        {
            var bin = _storageBins.FirstOrDefault(x =>
                string.Equals(x.BinCode, instruction.BinCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Sku, instruction.Sku, StringComparison.OrdinalIgnoreCase));
            if (bin is null || bin.OrderableQuantity < instruction.PickQuantity)
            {
                return false;
            }
        }

        foreach (var instruction in pickPlan.Instructions)
        {
            var bin = _storageBins.FirstOrDefault(x =>
                string.Equals(x.BinCode, instruction.BinCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Sku, instruction.Sku, StringComparison.OrdinalIgnoreCase));
            if (bin is not null && bin.OrderableQuantity >= instruction.PickQuantity)
            {
                bin.ReservedQuantity += instruction.PickQuantity;
            }
        }

        return true;
    }

    public bool TryReserveFulfillmentOrder(IEnumerable<WarehouseFulfillmentReservationRequest> requests)
    {
        var materialized = requests.ToList();
        if (materialized.Count == 0 || materialized.Any(x => x.Quantity <= 0 || !x.PickPlan.IsComplete))
        {
            return false;
        }

        foreach (var inventoryGroup in materialized.GroupBy(x => x.InboundProductId))
        {
            var inventoryItem = FindInventoryByInboundProductId(inventoryGroup.Key);
            if (inventoryItem is null
                || !inventoryItem.계약정보.마켓판매가능여부
                || inventoryItem.가용수량 < inventoryGroup.Sum(x => x.Quantity))
            {
                return false;
            }
        }

        var binRequests = materialized
            .SelectMany(x => x.PickPlan.Instructions)
            .GroupBy(x => new { Bin = x.BinCode.ToUpperInvariant(), Sku = x.Sku.ToUpperInvariant() });

        foreach (var binGroup in binRequests)
        {
            var bin = _storageBins.FirstOrDefault(x =>
                string.Equals(x.BinCode, binGroup.First().BinCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Sku, binGroup.First().Sku, StringComparison.OrdinalIgnoreCase));
            if (bin is null || bin.OrderableQuantity < binGroup.Sum(x => x.PickQuantity))
            {
                return false;
            }
        }

        foreach (var inventoryGroup in materialized.GroupBy(x => x.InboundProductId))
        {
            var inventoryItem = FindInventoryByInboundProductId(inventoryGroup.Key)!;
            var quantity = inventoryGroup.Sum(x => x.Quantity);
            inventoryItem.가용수량 -= quantity;
            inventoryItem.예약수량 += quantity;
        }

        foreach (var binGroup in binRequests)
        {
            var bin = _storageBins.First(x =>
                string.Equals(x.BinCode, binGroup.First().BinCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Sku, binGroup.First().Sku, StringComparison.OrdinalIgnoreCase));
            bin.ReservedQuantity += binGroup.Sum(x => x.PickQuantity);
        }

        return true;
    }

    private void MoveReservedToPicking(WarehouseOrderPickingLine line)
    {
        var bin = FindStorageBin(line);
        if (bin is null)
        {
            return;
        }

        var quantity = Math.Min(line.PickQuantity, bin.ReservedQuantity);
        bin.ReservedQuantity -= quantity;
        bin.PickingQuantity += quantity;
    }

    private void CompletePickedQuantity(WarehouseOrderPickingLine line)
    {
        var bin = FindStorageBin(line);
        if (bin is null)
        {
            return;
        }

        var quantity = Math.Min(line.PickQuantity, bin.PickingQuantity);
        bin.PickingQuantity -= quantity;
        bin.PickedQuantity += quantity;
    }

    private WarehouseStorageBinInventory? FindStorageBin(WarehouseOrderPickingLine line)
        => _storageBins.FirstOrDefault(x =>
            string.Equals(x.BinCode, line.BinCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Sku, line.Sku, StringComparison.OrdinalIgnoreCase));

    public int GetSellerSafetyStockQuantity(string sku)
        => _sellerSafetyStockBySku.TryGetValue(sku, out var quantity) ? Math.Max(0, quantity) : 0;

    public int GetOrderableQuantityBySku(string sku)
        => _storageBins
            .Where(x => string.Equals(x.Sku, sku, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.OrderableQuantity);

    public InboundRestockNotification? CreateInboundRestockNotificationIfNeeded(
        string channelType,
        string channelOrderNo,
        판매상품항목응답 product,
        재고항목응답 inventory)
    {
        var safetyStockQuantity = GetSellerSafetyStockQuantity(product.판매SKU);
        if (safetyStockQuantity <= 0)
        {
            return null;
        }

        var availableQuantity = GetOrderableQuantityBySku(product.판매SKU);
        if (availableQuantity > safetyStockQuantity)
        {
            return null;
        }

        var exists = _inboundRestockNotifications.Any(x =>
            string.Equals(x.ChannelType, channelType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ChannelOrderNo, channelOrderNo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Sku, product.판매SKU, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            return null;
        }

        var preference = GetOrCreateSellerRestockNotificationPreference(inventory.판매자UserId);
        var suggestedInboundQuantity = Math.Max(safetyStockQuantity * 2 - availableQuantity, safetyStockQuantity);
        var notification = new InboundRestockNotification
        {
            Id = _inboundRestockNotificationSequence++,
            ChannelType = channelType,
            ChannelOrderNo = channelOrderNo,
            InboundProductId = inventory.입고상품Id,
            SellerUserId = inventory.판매자UserId,
            ProductName = product.대표상품명,
            Sku = product.판매SKU,
            ContractNo = inventory.계약정보.계약번호,
            ContractPartnerName = inventory.계약정보.계약상대방명,
            AvailableQuantity = availableQuantity,
            SafetyStockQuantity = safetyStockQuantity,
            SuggestedInboundQuantity = suggestedInboundQuantity,
            IsInternalNotificationVisible = preference.UseInternalNotification,
            CreatedAt = DateTime.UtcNow
        };
        notification.Message = $"{product.대표상품명}({product.판매SKU}) 재고가 {availableQuantity}개로 판매자 설정 하한 {safetyStockQuantity}개 이하입니다. {inventory.계약정보.계약상대방명} 입고 계약 기준으로 {suggestedInboundQuantity}개 입고 요청을 검토하세요.";

        _inboundRestockNotifications.Add(notification);
        CreateRestockKakaoTalkOutbox(notification);
        return notification;
    }

    private SellerRestockNotificationPreference GetOrCreateSellerRestockNotificationPreference(string sellerUserId)
    {
        var preference = _sellerRestockNotificationPreferences.FirstOrDefault(x =>
            string.Equals(x.SellerUserId, sellerUserId, StringComparison.OrdinalIgnoreCase));
        if (preference is not null)
        {
            return preference;
        }

        preference = new SellerRestockNotificationPreference
        {
            SellerUserId = sellerUserId,
            SellerName = sellerUserId,
            KakaoTalkChannelName = $"{sellerUserId} 카카오 알림",
            AdminAllowsKakaoTalk = false,
            SellerWantsKakaoTalk = false
        };
        _sellerRestockNotificationPreferences.Add(preference);
        return preference;
    }

    private RestockKakaoTalkOutboxMessage CreateRestockKakaoTalkOutbox(InboundRestockNotification notification)
    {
        var preference = GetOrCreateSellerRestockNotificationPreference(notification.SellerUserId);
        var canSend = preference.CanSendKakaoTalk;
        var outbox = new RestockKakaoTalkOutboxMessage
        {
            Id = _restockKakaoTalkOutboxSequence++,
            RestockNotificationId = notification.Id,
            SellerUserId = notification.SellerUserId,
            SellerName = preference.SellerName,
            ChannelType = notification.ChannelType,
            ChannelOrderNo = notification.ChannelOrderNo,
            Sku = notification.Sku,
            Message = CreateRestockKakaoTalkMessage(notification, preference),
            Status = canSend ? "Pending" : "Suppressed",
            SuppressedReason = canSend ? null : preference.StatusLabel,
            CreatedAt = DateTime.UtcNow
        };

        _restockKakaoTalkOutboxMessages.Add(outbox);
        return outbox;
    }

    private static string CreateRestockKakaoTalkMessage(
        InboundRestockNotification notification,
        SellerRestockNotificationPreference preference)
        => $"[{preference.SellerName}] {notification.ProductName} 재고가 {notification.AvailableQuantity}개로 안전재고 {notification.SafetyStockQuantity}개 이하입니다. 입고 계약 {notification.ContractNo} 기준 권장 입고수량은 {notification.SuggestedInboundQuantity}개입니다.";

    public WarehouseOutboundNotification CreateWarehouseOutboundNotification(WarehouseOutboundNotification notification)
    {
        notification.Id = _warehouseOutboundNotificationSequence++;
        notification.CreatedAt = DateTime.UtcNow;
        _warehouseOutboundNotifications.Add(notification);
        return notification;
    }

    public WarehouseOrderPickingTask? CreateOrUpdateOrderPickingTask(string channelType, string channelOrderNo)
    {
        var readyNotifications = _warehouseOutboundNotifications
            .Where(x =>
                string.Equals(x.ChannelType, channelType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ChannelOrderNo, channelOrderNo, StringComparison.OrdinalIgnoreCase)
                && x.Status == WarehouseOutboundNotificationStatusCodes.Ready
                && x.PickPlan?.IsComplete == true)
            .ToList();

        if (readyNotifications.Count == 0)
        {
            return null;
        }

        WarehouseOrderPickingTask? firstCreated = null;
        foreach (var group in readyNotifications.GroupBy(x => x.WarehouseId))
        {
            var existing = _orderPickingTasks.FirstOrDefault(x =>
                string.Equals(x.ChannelType, channelType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ChannelOrderNo, channelOrderNo, StringComparison.OrdinalIgnoreCase)
                && x.WarehouseId == group.Key);
            if (existing is not null)
            {
                firstCreated ??= existing;
                continue;
            }

            var groupNotifications = group.ToList();
            var lines = groupNotifications
                .SelectMany(notification => notification.PickPlan!.Instructions.Select(instruction => new WarehouseOrderPickingLine
                {
                    NotificationId = notification.Id,
                    BinCode = instruction.BinCode,
                    Sku = instruction.Sku,
                    ProductName = instruction.ProductName,
                    PickQuantity = instruction.PickQuantity,
                    RouteSequence = instruction.RouteSequence
                }))
                .OrderBy(x => CreateRouteSortKey(x.BinCode), StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
                .Select((line, index) =>
                {
                    line.RouteSequence = index + 1;
                    return line;
                })
                .ToList();

            var first = groupNotifications[0];
            var task = new WarehouseOrderPickingTask
            {
                Id = _orderPickingTaskSequence++,
                ChannelType = channelType,
                ChannelOrderNo = channelOrderNo,
                WarehouseId = first.WarehouseId,
                WarehouseName = first.WarehouseName,
                RecipientName = first.RecipientName,
                RecipientAddress = first.RecipientAddress,
                Status = WarehouseOrderPickingStatusCodes.ReadyForPicking,
                Lines = lines,
                CreatedAt = DateTime.UtcNow
            };

            _orderPickingTasks.Add(task);
            firstCreated ??= task;
        }

        return firstCreated;
    }

    public WarehousePickingScanResult ScanOrderPickingTask(long taskId, string barcode)
    {
        var task = _orderPickingTasks.FirstOrDefault(x => x.Id == taskId);
        if (task is null)
        {
            return new WarehousePickingScanResult { IsSuccess = false, Message = "피킹 작업을 찾을 수 없습니다." };
        }

        if (task.Status is WarehouseOrderPickingStatusCodes.PickingCompleted or WarehouseOrderPickingStatusCodes.PackingReady)
        {
            return new WarehousePickingScanResult { IsSuccess = false, Message = "이미 피킹이 완료된 작업입니다.", Task = task };
        }

        var normalized = barcode.Trim();
        var next = task.NextLine;
        if (next is null)
        {
            CompletePickingTask(task);
            return new WarehousePickingScanResult { IsSuccess = true, Message = "모든 상품 피킹이 완료되었습니다. 포장 작업으로 이동할 수 있습니다.", Task = task };
        }

        if (!next.BinScanned)
        {
            if (!string.Equals(normalized, next.BinBarcode, StringComparison.OrdinalIgnoreCase))
            {
                return new WarehousePickingScanResult
                {
                    IsSuccess = false,
                    Message = $"다음 적재함 {next.BinBarcode}를 먼저 스캔해야 합니다.",
                    Task = task
                };
            }

            next.BinScanned = true;
            MoveReservedToPicking(next);
            task.Status = WarehouseOrderPickingStatusCodes.PickingInProgress;
            MarkNotifications(task, WarehouseOutboundNotificationStatusCodes.Picking);
            return new WarehousePickingScanResult
            {
                IsSuccess = true,
                Message = $"{next.BinCode} 적재함 확인. 이제 상품 {next.ProductBarcode}를 스캔하세요.",
                Task = task
            };
        }

        if (!string.Equals(normalized, next.ProductBarcode, StringComparison.OrdinalIgnoreCase))
        {
            return new WarehousePickingScanResult
            {
                IsSuccess = false,
                Message = $"주문 상품 {next.ProductBarcode}를 스캔해야 합니다.",
                Task = task
            };
        }

        next.ProductScanned = true;
        next.PickedAt = DateTime.UtcNow;
        CompletePickedQuantity(next);

        if (task.Lines.All(x => x.IsPicked))
        {
            CompletePickingTask(task);
            return new WarehousePickingScanResult
            {
                IsSuccess = true,
                Message = "피킹 완료. 포장 작업으로 이동할 수 있습니다.",
                Task = task
            };
        }

        task.Status = WarehouseOrderPickingStatusCodes.PickingInProgress;
        return new WarehousePickingScanResult
        {
            IsSuccess = true,
            Message = "상품 피킹 완료. 다음 추천 적재함으로 이동하세요.",
            Task = task
        };
    }

    public WarehousePickingScanResult HoldOrderPickingTask(long taskId, string reason)
    {
        var task = _orderPickingTasks.FirstOrDefault(x => x.Id == taskId);
        if (task is null)
        {
            return new WarehousePickingScanResult { IsSuccess = false, Message = "피킹 작업을 찾을 수 없습니다." };
        }

        if (task.Status is WarehouseOrderPickingStatusCodes.PickingCompleted or WarehouseOrderPickingStatusCodes.PackingReady or WarehouseOrderPickingStatusCodes.Cancelled)
        {
            return new WarehousePickingScanResult { IsSuccess = false, Message = "이미 종료된 피킹 작업입니다.", Task = task };
        }

        task.Status = WarehouseOrderPickingStatusCodes.PickingOnHold;
        task.ExceptionReason = string.IsNullOrWhiteSpace(reason) ? "작업자 보류" : reason.Trim();
        MarkNotifications(task, WarehouseOutboundNotificationStatusCodes.Blocked);
        return new WarehousePickingScanResult { IsSuccess = true, Message = "피킹 작업을 보류했습니다.", Task = task };
    }

    public WarehousePickingScanResult CancelOrderPickingTask(long taskId, string reason)
    {
        var task = _orderPickingTasks.FirstOrDefault(x => x.Id == taskId);
        if (task is null)
        {
            return new WarehousePickingScanResult { IsSuccess = false, Message = "피킹 작업을 찾을 수 없습니다." };
        }

        if (task.Status is WarehouseOrderPickingStatusCodes.PickingCompleted or WarehouseOrderPickingStatusCodes.PackingReady)
        {
            return new WarehousePickingScanResult { IsSuccess = false, Message = "피킹 완료 작업은 취소할 수 없습니다.", Task = task };
        }

        ReleaseUnpickedQuantities(task);
        task.Status = WarehouseOrderPickingStatusCodes.Cancelled;
        task.ExceptionReason = string.IsNullOrWhiteSpace(reason) ? "작업 취소" : reason.Trim();
        MarkNotifications(task, WarehouseOutboundNotificationStatusCodes.Blocked);
        return new WarehousePickingScanResult { IsSuccess = true, Message = "피킹 작업을 취소하고 미완료 예약 수량을 해제했습니다.", Task = task };
    }

    public WarehousePackingActionResult StartPackingTask(long packingTaskId)
    {
        var task = _packingTasks.FirstOrDefault(x => x.Id == packingTaskId);
        if (task is null)
        {
            return new WarehousePackingActionResult { IsSuccess = false, Message = "포장 작업을 찾을 수 없습니다." };
        }

        if (task.Status == WarehousePackingStatusCodes.Packed)
        {
            return new WarehousePackingActionResult { IsSuccess = false, Message = "이미 포장이 완료된 작업입니다.", Task = task };
        }

        task.Status = WarehousePackingStatusCodes.PackingInProgress;
        task.StartedAt ??= DateTime.UtcNow;
        MarkNotificationsByPickingTask(task.PickingTaskId, WarehouseOutboundNotificationStatusCodes.Packing);
        return new WarehousePackingActionResult { IsSuccess = true, Message = "포장 작업을 시작했습니다.", Task = task };
    }

    public WarehousePackingActionResult CompletePackingTask(long packingTaskId)
    {
        var task = _packingTasks.FirstOrDefault(x => x.Id == packingTaskId);
        if (task is null)
        {
            return new WarehousePackingActionResult { IsSuccess = false, Message = "포장 작업을 찾을 수 없습니다." };
        }

        if (task.Status == WarehousePackingStatusCodes.ReadyForPacking)
        {
            return new WarehousePackingActionResult { IsSuccess = false, Message = "포장 시작 후 완료 처리할 수 있습니다.", Task = task };
        }

        task.Status = WarehousePackingStatusCodes.Packed;
        task.CompletedAt = DateTime.UtcNow;
        MarkNotificationsByPickingTask(task.PickingTaskId, WarehouseOutboundNotificationStatusCodes.Packed);
        return new WarehousePackingActionResult { IsSuccess = true, Message = "포장 작업을 완료했습니다.", Task = task };
    }

    private void CompletePickingTask(WarehouseOrderPickingTask task)
    {
        task.Status = WarehouseOrderPickingStatusCodes.PickingCompleted;
        task.CompletedAt = DateTime.UtcNow;
        MarkNotifications(task, WarehouseOutboundNotificationStatusCodes.PackingReady);
        CreatePackingTask(task);
    }

    private void CreatePackingTask(WarehouseOrderPickingTask pickingTask)
    {
        if (_packingTasks.Any(x => x.PickingTaskId == pickingTask.Id))
        {
            return;
        }

        _packingTasks.Add(new WarehousePackingTask
        {
            Id = _packingTaskSequence++,
            PickingTaskId = pickingTask.Id,
            ChannelType = pickingTask.ChannelType,
            ChannelOrderNo = pickingTask.ChannelOrderNo,
            WarehouseId = pickingTask.WarehouseId,
            WarehouseName = pickingTask.WarehouseName,
            RecipientName = pickingTask.RecipientName,
            RecipientAddress = pickingTask.RecipientAddress,
            LineCount = pickingTask.TotalLineCount,
            Status = WarehousePackingStatusCodes.ReadyForPacking,
            CreatedAt = DateTime.UtcNow
        });
    }

    private void MarkNotifications(WarehouseOrderPickingTask task, string status)
    {
        var notificationIds = task.Lines.Select(x => x.NotificationId).ToHashSet();
        foreach (var notification in _warehouseOutboundNotifications.Where(x => notificationIds.Contains(x.Id)))
        {
            notification.Status = status;
        }
    }

    private void MarkNotificationsByPickingTask(long pickingTaskId, string status)
    {
        var pickingTask = _orderPickingTasks.FirstOrDefault(x => x.Id == pickingTaskId);
        if (pickingTask is null)
        {
            return;
        }

        MarkNotifications(pickingTask, status);
    }

    private void ReleaseUnpickedQuantities(WarehouseOrderPickingTask task)
    {
        foreach (var line in task.Lines.Where(x => !x.IsPicked))
        {
            var bin = FindStorageBin(line);
            if (bin is null)
            {
                continue;
            }

            if (line.BinScanned)
            {
                var pickingQuantity = Math.Min(line.PickQuantity, bin.PickingQuantity);
                bin.PickingQuantity -= pickingQuantity;
            }
            else
            {
                var reservedQuantity = Math.Min(line.PickQuantity, bin.ReservedQuantity);
                bin.ReservedQuantity -= reservedQuantity;
            }
        }
    }

    private static string CreateRouteSortKey(string binCode)
    {
        var parts = binCode.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return string.Join('-', parts.Select(x => int.TryParse(x, out var number) ? number.ToString("D4") : x.ToUpperInvariant()));
    }

    public CustomsHsReviewRequest CreateCustomsHsReview(CustomsHsReviewRequest review)
    {
        review.Id = _customsHsReviewSequence++;
        review.CreatedAt = DateTime.UtcNow;
        _customsHsReviews.Add(review);
        return review;
    }

    public void AssignCustomsBroker(long reviewId, CustomsBrokerProfile broker)
    {
        var review = _customsHsReviews.FirstOrDefault(x => x.Id == reviewId)
            ?? throw new InvalidOperationException("HS 검토 요청을 찾을 수 없습니다.");

        review.AssignedBrokerId = broker.BrokerId;
        review.AssignedBrokerName = broker.BrokerName;
        review.Status = CustomsHsReviewStatusCodes.BrokerAssigned;
    }

    public void CompleteCustomsHsReview(long reviewId, string hsCode, string comment)
    {
        var review = _customsHsReviews.FirstOrDefault(x => x.Id == reviewId)
            ?? throw new InvalidOperationException("HS 검토 요청을 찾을 수 없습니다.");

        review.ConfirmedHsCode = hsCode;
        review.BrokerComment = comment;
        review.Status = CustomsHsReviewStatusCodes.Completed;
        review.ReviewedAt = DateTime.UtcNow;
    }

    public void AddAppEventLog(string eventName, string summary, DateTime occurredAt)
    {
        _appEventLogs.Add(new AppEventLogEntry
        {
            Id = _appEventLogSequence++,
            EventName = eventName,
            Summary = summary,
            OccurredAt = occurredAt
        });
    }

    public IReadOnlyList<string> GetVehicleTypes() => new[] { "오토바이 퀵", "1톤", "1.4톤", "냉동탑차" };

    public decimal EstimateFare(string vehicleType, decimal distanceKm)
    {
        var (baseFare, unitRate, minimumFare) = vehicleType switch
        {
            "오토바이 퀵" => (5000m, 1000m, 5000m),
            "1.4톤" => (45000m, 1550m, 45000m),
            "냉동탑차" => (35000m, 1500m, 35000m),
            _ => (35000m, 1300m, 35000m)
        };

        var fare = baseFare + decimal.Round(Math.Max(0m, distanceKm) * unitRate, 0, MidpointRounding.AwayFromZero);
        return fare < minimumFare ? minimumFare : fare;
    }

    public void AddRequest(ShipperRequestItem request)
    {
        _requests.Insert(0, request);
        _publicCargo.Insert(0, new Hongdal.Contracts.Shipper.Request.공개화물요약응답
        {
            의뢰Id = request.의뢰Id,
            화물종류 = request.화물종류,
            운송방식 = request.운송방식,
            차량종류 = request.차량종류,
            화물수량 = 1,
            화물중량Kg = 100,
            의뢰상태 = request.의뢰상태,
            배차상태 = request.배차상태,
            생성일시 = request.생성일시
        });
    }

    public void UpdateRequest(ShipperRequestItem request)
    {
        var index = _requests.FindIndex(x => string.Equals(x.의뢰Id, request.의뢰Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException("수정할 운송의뢰를 찾을 수 없습니다.");
        }

        _requests[index] = request;
        var publicIndex = _publicCargo.FindIndex(x => string.Equals(x.의뢰Id, request.의뢰Id, StringComparison.OrdinalIgnoreCase));
        if (publicIndex >= 0)
        {
            var item = _publicCargo[publicIndex];
            item.화물종류 = request.화물종류;
            item.운송방식 = request.운송방식;
            item.차량종류 = request.차량종류;
            item.의뢰상태 = request.의뢰상태;
            item.배차상태 = request.배차상태;
        }
    }

    public void DeleteRequest(string requestId)
    {
        var removed = _requests.RemoveAll(x => string.Equals(x.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            throw new InvalidOperationException("삭제할 운송의뢰를 찾을 수 없습니다.");
        }

        _publicCargo.RemoveAll(x => string.Equals(x.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase));
    }

    public 창고요약응답 CreateWarehouse(창고저장요청 payload, string userId)
    {
        var warehouse = new 창고요약응답
        {
            Id = _warehouseSequence++,
            창고명 = payload.창고명,
            소유자유형 = payload.소유자유형,
            창고유형 = payload.창고유형,
            물류대행지분류 = LogisticsProxySiteTypes.Normalize(payload.물류대행지분류),
            주소 = payload.주소,
            담당자명 = payload.담당자명,
            연락처 = payload.연락처,
            소유자UserId = userId,
            IsActive = true
        };

        _warehouses.Add(warehouse);
        return warehouse;
    }

    public 입고요청항목응답 CreateInbound(입고요청저장요청 payload, string userId)
    {
        var contract = (payload.계약정보 ?? 입고계약스냅샷.Default(payload.공급처명)).Normalize();
        if (string.IsNullOrWhiteSpace(contract.계약상대방명))
        {
            contract.계약상대방명 = payload.공급처명.Trim();
        }

        var inbound = new 입고요청항목응답
        {
            Id = _inboundSequence++,
            창고Id = payload.창고Id,
            입고흐름유형 = 입고흐름유형코드.Normalize(payload.입고흐름유형),
            입고생성경로 = string.IsNullOrWhiteSpace(payload.입고생성경로)
                ? BuildInboundSourceLabel(payload.입고흐름유형)
                : payload.입고생성경로.Trim(),
            계약선행여부 = payload.계약선행여부,
            자동생성여부 = payload.자동생성여부,
            주문자UserId = userId,
            공급처명 = payload.공급처명,
            원주문참조번호 = string.IsNullOrWhiteSpace(payload.원주문참조번호) ? $"PO-{DateTime.Now:yyMMdd}-{_inboundSequence}" : payload.원주문참조번호,
            상태 = "입고예정",
            예정도착일 = payload.예정도착일,
            계약정보 = contract
        };

        _inbounds.Add(inbound);
        return inbound;
    }

    private static string BuildInboundSourceLabel(string? flowType)
        => 입고흐름유형코드.Normalize(flowType) switch
        {
            입고흐름유형코드.현장임시입고 => "창고 관리자 수기 등록",
            입고흐름유형코드.주문자동입고예정 => "주문/구매 흐름 자동 생성",
            _ => "계약 DB 기반 등록"
        };

    public 입고상품목록응답 CompleteInbound(long inboundId, 입고완료요청 payload, string userId)
    {
        var inbound = _inbounds.FirstOrDefault(x => x.Id == inboundId)
            ?? throw new InvalidOperationException("입고 요청을 찾을 수 없습니다.");
        var warehouse = _warehouses.FirstOrDefault(x => x.Id == inbound.창고Id)
            ?? throw new InvalidOperationException("창고를 찾을 수 없습니다.");

        inbound.상태 = "입고완료";
        inbound.입고완료일시 = DateTime.Now;

        var createdItems = payload.Items.Select(item =>
        {
            var inventoryItem = new 재고항목응답
            {
                입고상품Id = _inventorySequence++,
                창고Id = warehouse.Id,
                창고명 = warehouse.창고명,
                소유자UserId = userId,
                판매자UserId = userId,
                상품명 = item.상품명,
                SKU = item.SKU,
                옵션명 = item.옵션명,
                가용수량 = Math.Max(0, item.입고수량 - item.불량수량),
                예약수량 = 0,
                상태 = "보관중",
                보관위치 = item.보관위치,
                계약정보 = inbound.계약정보
            };

            _inventory.Add(inventoryItem);

            return new 입고상품항목응답
            {
                Id = inventoryItem.입고상품Id,
                입고요청Id = inbound.Id,
                창고Id = warehouse.Id,
                소유자UserId = userId,
                판매자UserId = userId,
                상품명 = item.상품명,
                SKU = item.SKU,
                옵션명 = item.옵션명,
                입고수량 = item.입고수량,
                가용수량 = inventoryItem.가용수량,
                불량수량 = item.불량수량,
                보관위치 = item.보관위치,
                상태 = inventoryItem.상태,
                입고완료일시 = inbound.입고완료일시,
                계약정보 = inbound.계약정보
            };
        }).ToArray();

        return new 입고상품목록응답
        {
            Items = createdItems
        };
    }

    public 화주운송의뢰응답 CreateReconsignment(재고운송의뢰생성요청 payload, string userId)
    {
        var inventoryItem = _inventory.FirstOrDefault(x => x.입고상품Id == payload.입고상품Id)
            ?? throw new InvalidOperationException("재고를 찾을 수 없습니다.");

        if (payload.요청수량 <= 0 || payload.요청수량 > inventoryItem.가용수량)
        {
            throw new InvalidOperationException("재위탁 수량이 가용수량을 초과했습니다.");
        }

        inventoryItem.가용수량 -= payload.요청수량;
        inventoryItem.상태 = inventoryItem.가용수량 == 0 ? "재위탁출고" : "보관중";

        var requestId = $"SHP-{_requestSequence++}";
        var request = new ShipperRequestItem
        {
            의뢰Id = requestId,
            화물종류 = payload.화물종류,
            의뢰상태 = "접수",
            결제상태 = "결제대기",
            배차상태 = "배차대기",
            운송방식 = "재위탁운송",
            차량종류 = payload.차량종류,
            결제수단 = "후불정산",
            결제예정금액 = (int)EstimateFare(payload.차량종류, 18),
            생성일시 = DateTime.Now,
            픽업지 = inventoryItem.창고명,
            하차지 = payload.하차지주소
        };

        AddRequest(request);

        return new 화주운송의뢰응답
        {
            의뢰Id = requestId,
            주문자UserId = userId,
            화주Id = userId,
            의뢰상태 = request.의뢰상태,
            결제상태 = request.결제상태,
            정산상태 = "정산대기",
            배차상태 = request.배차상태,
            운송방식 = request.운송방식,
            차량종류 = request.차량종류,
            결제수단 = request.결제수단,
            결제예정금액 = request.결제예정금액,
            생성일시 = request.생성일시,
            픽업지 = request.픽업지 ?? string.Empty,
            픽업상세지 = inventoryItem.보관위치,
            하차지 = payload.하차지주소,
            하차상세지 = payload.하차지상세주소,
            요약 = new 화주운송의뢰응답.요약DTO
            {
                화물종류 = payload.화물종류,
                픽업지 = request.픽업지 ?? string.Empty,
                하차지 = payload.하차지주소
            }
        };
    }

    public 판매채널계정항목응답 CreateAccount(판매채널계정저장요청 payload)
    {
        var account = new 판매채널계정항목응답
        {
            Id = _accountSequence++,
            채널종류 = payload.채널종류,
            상점명 = payload.상점명,
            연결상태 = SalesStatusCodes.AccountConnected,
            마지막동기화일시 = DateTime.UtcNow
        };

        _accounts.Add(account);
        return account;
    }

    public 판매채널계정항목응답 UpdateAccount(long accountId, 판매채널계정저장요청 payload)
    {
        var account = _accounts.FirstOrDefault(x => x.Id == accountId)
            ?? throw new KeyNotFoundException("판매채널 계정을 찾을 수 없습니다.");

        account.채널종류 = payload.채널종류;
        account.상점명 = payload.상점명;
        account.마지막동기화일시 = DateTime.UtcNow;
        return account;
    }

    public void DeleteAccount(long accountId)
    {
        var account = _accounts.FirstOrDefault(x => x.Id == accountId)
            ?? throw new KeyNotFoundException("판매채널 계정을 찾을 수 없습니다.");

        if (_listings.Any(x => x.판매채널계정Id == accountId))
        {
            throw new InvalidOperationException("출품에서 사용 중인 판매채널 계정은 삭제할 수 없습니다.");
        }

        _accounts.Remove(account);
    }

    public 판매상품항목응답 CreateProduct(판매상품저장요청 payload)
    {
        var product = new 판매상품항목응답
        {
            Id = _productSequence++,
            입고상품Id = payload.입고상품Id,
            대표상품명 = payload.대표상품명,
            판매SKU = payload.판매SKU,
            판매가 = payload.판매가,
            상태 = SalesStatusCodes.ProductReady
        };

        _products.Add(product);
        return product;
    }

    public 판매상품항목응답 UpdateProduct(long productId, 판매상품저장요청 payload)
    {
        var product = _products.FirstOrDefault(x => x.Id == productId)
            ?? throw new KeyNotFoundException("판매상품을 찾을 수 없습니다.");

        product.입고상품Id = payload.입고상품Id;
        product.대표상품명 = payload.대표상품명;
        product.판매SKU = payload.판매SKU;
        product.판매가 = payload.판매가;
        product.샘플데이터여부 = payload.샘플데이터여부;
        product.샘플데이터코드 = payload.샘플데이터코드;
        return product;
    }

    public void DeleteProduct(long productId)
    {
        var product = _products.FirstOrDefault(x => x.Id == productId)
            ?? throw new KeyNotFoundException("판매상품을 찾을 수 없습니다.");

        if (_listings.Any(x => x.판매상품Id == productId))
        {
            throw new InvalidOperationException("출품에서 사용 중인 판매상품은 삭제할 수 없습니다.");
        }

        _products.Remove(product);
    }

    public 채널출품항목응답 CreateListing(채널출품저장요청 payload)
    {
        var product = _products.FirstOrDefault(x => x.Id == payload.판매상품Id)
            ?? throw new KeyNotFoundException("판매상품을 찾을 수 없습니다.");
        var account = _accounts.FirstOrDefault(x => x.Id == payload.판매채널계정Id)
            ?? throw new KeyNotFoundException("판매채널 계정을 찾을 수 없습니다.");
        var listing = new 채널출품항목응답
        {
            Id = _listingSequence++,
            판매상품Id = payload.판매상품Id,
            판매채널계정Id = payload.판매채널계정Id,
            채널상품번호 = $"CH-{DateTime.Now:yyMMdd}-{_listingSequence}",
            출품상태 = SalesStatusCodes.ListingCompleted,
            동기화상태 = SalesStatusCodes.SyncNormal
        };

        _listings.Add(listing);
        product.상태 = SalesStatusCodes.ProductActive;
        account.마지막동기화일시 = DateTime.UtcNow;
        return listing;
    }

    public 채널출품항목응답 UpdateListing(long listingId, 채널출품저장요청 payload)
    {
        var listing = _listings.FirstOrDefault(x => x.Id == listingId)
            ?? throw new KeyNotFoundException("채널 출품을 찾을 수 없습니다.");
        var previousProductId = listing.판매상품Id;
        var product = _products.FirstOrDefault(x => x.Id == payload.판매상품Id)
            ?? throw new KeyNotFoundException("판매상품을 찾을 수 없습니다.");
        var account = _accounts.FirstOrDefault(x => x.Id == payload.판매채널계정Id)
            ?? throw new KeyNotFoundException("판매채널 계정을 찾을 수 없습니다.");

        listing.판매상품Id = payload.판매상품Id;
        listing.판매채널계정Id = payload.판매채널계정Id;
        listing.동기화상태 = SalesStatusCodes.SyncNormal;
        listing.에러메시지 = string.Empty;
        product.상태 = SalesStatusCodes.ProductActive;
        account.마지막동기화일시 = DateTime.UtcNow;
        if (previousProductId != product.Id
            && _listings.All(x => x.Id == listingId || x.판매상품Id != previousProductId))
        {
            var previousProduct = _products.FirstOrDefault(x => x.Id == previousProductId);
            if (previousProduct is not null)
            {
                previousProduct.상태 = SalesStatusCodes.ProductReady;
            }
        }
        return listing;
    }

    public void DeleteListing(long listingId)
    {
        var listing = _listings.FirstOrDefault(x => x.Id == listingId)
            ?? throw new KeyNotFoundException("채널 출품을 찾을 수 없습니다.");

        _listings.Remove(listing);

        if (_listings.All(x => x.판매상품Id != listing.판매상품Id))
        {
            var product = _products.FirstOrDefault(x => x.Id == listing.판매상품Id);
            if (product is not null)
            {
                product.상태 = SalesStatusCodes.ProductReady;
            }
        }
    }

    public void UpdateListingSync(long listingId, string syncStatus, string message)
    {
        var listing = _listings.FirstOrDefault(x => x.Id == listingId);
        if (listing is null)
        {
            return;
        }

        listing.동기화상태 = syncStatus;
        listing.에러메시지 = message;
    }

    public 화주운송의뢰일괄미리보기응답 CreateBulkPreview(string userId)
    {
        var rows = new List<화주운송의뢰일괄미리보기행응답>
        {
            CreatePreviewRow(1, userId, "가구", 4, 320.5m, "서울 강서구 공항대로 10", "경기 수원시 영통구 산업로 55", "1톤 카고", true),
            CreatePreviewRow(2, userId, "냉장식품", 12, 1140.2m, "인천 연수구 센터로 1", "서울 송파구 물류로 19", "냉동탑차", true),
            CreatePreviewRow(3, userId, "전자제품", null, 220.0m, "경기 김포시 창고로 12", "대전 유성구 테크노로 88", "1.4톤 윙바디", false)
        };

        return new 화주운송의뢰일괄미리보기응답
        {
            전체행수 = rows.Count,
            유효행수 = rows.Count(x => x.유효함),
            오류행수 = rows.Count(x => !x.유효함),
            행목록 = rows
        };
    }

    public 화주운송의뢰일괄등록결과응답 ConfirmBulk(화주운송의뢰일괄확정등록요청 request, string userId)
    {
        var results = new List<화주운송의뢰일괄등록행결과>();

        foreach (var row in request.행목록)
        {
            if (!row.등록여부)
            {
                results.Add(new 화주운송의뢰일괄등록행결과
                {
                    행번호 = row.행번호,
                    성공 = false,
                    오류 = ["사용자가 등록 대상에서 제외했습니다."]
                });
                continue;
            }

            if (!row.원본행.화물수량.HasValue)
            {
                results.Add(new 화주운송의뢰일괄등록행결과
                {
                    행번호 = row.행번호,
                    성공 = false,
                    오류 = ["화물수량이 없어 등록할 수 없습니다."]
                });
                continue;
            }

            var requestId = $"SHP-{_requestSequence++}";
            var vehicleType = string.IsNullOrWhiteSpace(row.최종선택차량종류) ? row.원본행.차량종류 ?? "1톤 카고" : row.최종선택차량종류;
            AddRequest(new ShipperRequestItem
            {
                의뢰Id = requestId,
                화물종류 = row.원본행.화물종류,
                의뢰상태 = "접수",
                결제상태 = "결제대기",
                배차상태 = "배차대기",
                운송방식 = row.원본행.운송방식 ?? "일반운송",
                차량종류 = vehicleType ?? "1톤 카고",
                결제수단 = row.원본행.결제수단 ?? "카드",
                결제예정금액 = (int)EstimateFare(vehicleType ?? "1톤 카고", 25),
                생성일시 = DateTime.Now.AddMinutes(-row.행번호),
                픽업지 = row.원본행.픽업도로명주소,
                하차지 = row.원본행.하차도로명주소
            });

            results.Add(new 화주운송의뢰일괄등록행결과
            {
                행번호 = row.행번호,
                성공 = true,
                의뢰Id = requestId,
                추천결과 = new 화주운송의뢰추천결과
                {
                    차량종류 = vehicleType ?? "1톤 카고",
                    운송방식 = row.원본행.운송방식 ?? "일반운송"
                }
            });
        }

        return new 화주운송의뢰일괄등록결과응답
        {
            전체행수 = results.Count,
            성공행수 = results.Count(x => x.성공),
            실패행수 = results.Count(x => !x.성공),
            행결과목록 = results
        };
    }

    private static 화주운송의뢰일괄미리보기행응답 CreatePreviewRow(
        int rowNumber,
        string userId,
        string cargoType,
        int? quantity,
        decimal? weightKg,
        string pickupAddress,
        string dropoffAddress,
        string recommendedVehicle,
        bool isValid)
    {
        var recommendation = new 화주운송의뢰추천결과
        {
            운송방식 = cargoType.Contains("냉장", StringComparison.Ordinal) ? "냉동운송" : "일반운송",
            차량종류 = recommendedVehicle,
            결제수단 = "카드",
            정산시점 = "운송완료 후",
            추천사유 = "샘플 데이터 기반 추천",
            추정화물부피Cbm = weightKg.HasValue ? Math.Round(weightKg.Value / 250m, 2) : 1.2m,
            추천사유목록 = ["화물 중량과 수량 기준", "현재 화면 확인용 샘플 추천"],
            경고목록 = isValid ? Array.Empty<string>() : ["수량 누락 행은 등록 전 수정이 필요합니다."],
            후보차량목록 =
            [
                new 차량추천후보응답 { 우선순위 = 1, 차량종류 = recommendedVehicle, 설명 = "권장", 적재가능중량Kg = 1500, 적재가능부피Cbm = 8.5m },
                new 차량추천후보응답 { 우선순위 = 2, 차량종류 = "1톤 카고", 설명 = "대체 가능", 적재가능중량Kg = 1000, 적재가능부피Cbm = 6.0m }
            ]
        };

        return new 화주운송의뢰일괄미리보기행응답
        {
            행번호 = rowNumber,
            유효함 = isValid,
            등록대상여부 = isValid,
            최종선택차량종류 = recommendedVehicle,
            원본행 = new 화주운송의뢰일괄등록행입력
            {
                행번호 = rowNumber,
                화주Id = userId,
                화물종류 = cargoType,
                화물수량 = quantity,
                화물중량Kg = weightKg,
                픽업도로명주소 = pickupAddress,
                하차도로명주소 = dropoffAddress,
                운송방식 = recommendation.운송방식,
                차량종류 = recommendedVehicle,
                결제수단 = recommendation.결제수단,
                정산시점 = recommendation.정산시점,
                클라이언트행Id = $"sample-{rowNumber}"
            },
            추천결과 = recommendation,
            오류목록 = isValid ? Array.Empty<string>() : ["화물수량이 비어 있습니다."],
            경고목록 = recommendation.경고목록
        };
    }
}
