using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Driver.Development;
using Hongdal.Services.Driver.Development;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.도메인.공통;
using 홍달.도메인.기사;
using 홍달.도메인.배차;
using 홍달.도메인.운송;
using 홍달.도메인.화물;
using 홍달.도메인.화주;
using 홍달.도메인.창고;
using 홍달.Services.Storage.Local;

namespace Hongdal.Services.Development;

public static class HongdalV1DevelopmentDataSeeder
{
    private const string DriverUserName = "driver1@hongdal.local";
    private const string ShipperUserName = "shipper1";
    private const string DevelopmentWarehouseName = "V1 개발 3PL 냉장 창고";
    private const string CompletedInboundReference = "V1-DEV-INB-001";
    private const string PlannedInboundReference = "V1-DEV-INB-002";

    private static readonly string[] SampleRequestIds =
    [
        "V1-DEV-REQ-001",
        "V1-DEV-REQ-002",
        "V1-DEV-REQ-003"
    ];

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HongdalContext>();
        var locationStore = scope.ServiceProvider.GetRequiredService<IDriverLocationStore>();
        var driverSnapshotProvider = scope.ServiceProvider.GetRequiredService<I기사개발스냅샷Provider>();

        var driverUser = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserName == DriverUserName, cancellationToken);
        var shipperUser = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserName == ShipperUserName, cancellationToken);

        if (driverUser is null || shipperUser is null)
        {
            logger.LogWarning(
                "Hongdal 1.0 development sample data was skipped because seed users are missing. DriverUser={DriverUser} ShipperUser={ShipperUser}",
                DriverUserName,
                ShipperUserName);
            return;
        }

        var now = DateTime.UtcNow;
        await EnsureDriverAsync(db, driverUser.Id, now, cancellationToken);
        await EnsureDriverShiftAsync(db, driverUser.Id, now, cancellationToken);
        EnsureDriverLocation(locationStore, driverUser.Id, now);

        var sampleRequestId1 = SampleRequestIds[0];
        var sampleRequestId2 = SampleRequestIds[1];
        var sampleRequestId3 = SampleRequestIds[2];
        var existingRequestIds = await db.화주운송의뢰
            .AsNoTracking()
            .Where(x => x.의뢰Id == sampleRequestId1 || x.의뢰Id == sampleRequestId2 || x.의뢰Id == sampleRequestId3)
            .Select(x => x.의뢰Id)
            .ToListAsync(cancellationToken);
        var existingRequestIdSet = existingRequestIds.ToHashSet(StringComparer.Ordinal);

        var scenarios = CreateScenarios(shipperUser.Id, driverUser.Id, now);
        foreach (var scenario in scenarios)
        {
            if (!existingRequestIdSet.Contains(scenario.Request.의뢰Id))
            {
                db.화주운송의뢰.Add(scenario.Request);
                db.화물요구조건.Add(scenario.CargoRequirement);
                db.운송원장.Add(scenario.Queue);
            }
        }

        await EnsureTransportAsync(db, driverUser.Id, now, cancellationToken);
        await EnsureSettlementAsync(db, driverUser.Id, now, cancellationToken);
        await EnsureWarehouseSamplesAsync(db, shipperUser.Id, now, cancellationToken);
        SeedDriverDevelopmentSnapshot(driverSnapshotProvider, driverUser.Id, now, scenarios);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDriverAsync(HongdalContext db, string driverId, DateTime now, CancellationToken cancellationToken)
    {
        var driver = await db.용달기사.FirstOrDefaultAsync(x => x.기사Id == driverId, cancellationToken);
        if (driver is null)
        {
            db.용달기사.Add(new 용달기사
            {
                기사명 = "개발용 기사",
                기사Id = driverId,
                상태 = "활동중",
                연락처 = "010-1000-1000",
                차량 = "1톤 카고",
                운행상태 = 상태값.기사운행상태.운행중,
                주_활동지역 = "서울 강서/양천",
                기본복귀지주소 = "서울 양천구 목동",
                기본복귀지위도 = 37.5268m,
                기본복귀지경도 = 126.8755m,
                집주소를복귀지로사용허용 = true,
                메모 = "Hongdal 1.0 개발 검증용 기사",
                등록일 = now,
                CreatedAt = now,
                UpdatedAt = now
            });
            return;
        }

        driver.차량 = "1톤 카고";
        driver.운행상태 = 상태값.기사운행상태.운행중;
        driver.주_활동지역 = "서울 강서/양천";
        driver.기본복귀지주소 = "서울 양천구 목동";
        driver.기본복귀지위도 = 37.5268m;
        driver.기본복귀지경도 = 126.8755m;
        driver.집주소를복귀지로사용허용 = true;
        driver.UpdatedAt = now;
    }

    private static async Task EnsureDriverShiftAsync(HongdalContext db, string driverId, DateTime now, CancellationToken cancellationToken)
    {
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var hasTodayShift = await db.기사근무.AnyAsync(
            x => x.기사Id == driverId
                 && x.시작시각.HasValue
                 && x.시작시각.Value >= todayStart
                 && x.시작시각.Value < tomorrowStart,
            cancellationToken);
        if (hasTodayShift)
        {
            return;
        }

        db.기사근무.Add(new 기사근무
        {
            기사Id = driverId,
            시작모드 = "일반 운행",
            시작시각 = now.Date.AddHours(8).AddMinutes(30),
            시작위치 = "서울 강서구 화곡동",
            복귀지 = "서울 양천구 목동",
            오늘의복귀지주소 = "서울 양천구 목동",
            오늘의복귀지위도 = 37.5268m,
            오늘의복귀지경도 = 126.8755m,
            복귀지출처 = "오늘복귀지",
            복귀지입력일시 = now.Date.AddHours(8).AddMinutes(20),
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static void EnsureDriverLocation(IDriverLocationStore locationStore, string driverId, DateTime now)
    {
        locationStore.Upsert(new DriverLocationSnapshot(
            driverId,
            37.5412m,
            126.8409m,
            25m,
            상태값.기사운행상태.운행중,
            now.AddMinutes(-3),
            now));
    }

    private static IReadOnlyList<V1DispatchScenario> CreateScenarios(string shipperId, string driverId, DateTime now)
    {
        return
        [
            CreateScenario(
                requestId: "V1-DEV-REQ-001",
                shipperId,
                driverId,
                cargoType: "냉장 식품 박스",
                cargoDescription: "냉장 보관 박스 12개, 비맞음 금지",
                transportType: "혼적",
                vehicleType: "1톤 냉장탑",
                paymentMethod: "카드",
                settlementTime: "선결제",
                evidenceMethod: "인수증",
                pickupAddress: "서울 양천구 목동",
                pickupDetail: "A 물류센터 2번 도크",
                pickupLat: 37.5268m,
                pickupLng: 126.8755m,
                dropoffAddress: "경기 수원시 영통구",
                dropoffDetail: "매장 후문 입고장",
                dropoffLat: 37.2636m,
                dropoffLng: 127.0286m,
                fare: 88000,
                pickupStart: now.AddMinutes(40),
                pickupEnd: now.AddHours(2),
                dropoffStart: now.AddHours(3),
                dropoffEnd: now.AddHours(5),
                queueStage: 상태값.배차큐단계.배차추천,
                exposureState: 상태값.배차노출상태.추천중,
                currentRecommendedDriverId: driverId,
                recommendationExpiresAt: now.AddMinutes(30),
                cargoWeightKg: 180,
                냉장필요: true,
                혼적허용: true),
            CreateScenario(
                requestId: "V1-DEV-REQ-002",
                shipperId,
                driverId,
                cargoType: "전자제품",
                cargoDescription: "파손주의 전자제품 박스 8개",
                transportType: "독차",
                vehicleType: "1톤 탑차",
                paymentMethod: "후불",
                settlementTime: "후불",
                evidenceMethod: "사진증빙",
                pickupAddress: "서울 마포구 성산동",
                pickupDetail: "상가 지하 하역장",
                pickupLat: 37.5638m,
                pickupLng: 126.9084m,
                dropoffAddress: "경기 고양시 덕양구",
                dropoffDetail: "고객사 물류 사무실",
                dropoffLat: 37.6374m,
                dropoffLng: 126.8320m,
                fare: 132000,
                pickupStart: now.AddHours(2),
                pickupEnd: now.AddHours(4),
                dropoffStart: now.AddHours(5),
                dropoffEnd: now.AddHours(7),
                queueStage: 상태값.배차큐단계.배차추천,
                exposureState: 상태값.배차노출상태.추천중,
                currentRecommendedDriverId: driverId,
                recommendationExpiresAt: now.AddMinutes(45),
                cargoWeightKg: 240,
                독차필수: true,
                비맞으면안됨: true),
            CreateScenario(
                requestId: "V1-DEV-REQ-003",
                shipperId,
                driverId,
                cargoType: "생활용품",
                cargoDescription: "생활용품 혼적 가능 박스 20개",
                transportType: "혼적",
                vehicleType: "1톤 카고",
                paymentMethod: "현장지급",
                settlementTime: "하차후정산",
                evidenceMethod: "없음",
                pickupAddress: "경기 부천시 중동",
                pickupDetail: "1층 출고장",
                pickupLat: 37.5034m,
                pickupLng: 126.7660m,
                dropoffAddress: "인천 연수구 송도동",
                dropoffDetail: "아파트 관리사무소 앞",
                dropoffLat: 37.3896m,
                dropoffLng: 126.6426m,
                fare: 74000,
                pickupStart: now.AddHours(4),
                pickupEnd: now.AddHours(6),
                dropoffStart: now.AddHours(7),
                dropoffEnd: now.AddHours(9),
                queueStage: 상태값.배차큐단계.공개배차,
                exposureState: 상태값.배차노출상태.공개중,
                currentRecommendedDriverId: null,
                recommendationExpiresAt: null,
                cargoWeightKg: 320,
                혼적허용: true)
        ];
    }

    private static V1DispatchScenario CreateScenario(
        string requestId,
        string shipperId,
        string driverId,
        string cargoType,
        string cargoDescription,
        string transportType,
        string vehicleType,
        string paymentMethod,
        string settlementTime,
        string evidenceMethod,
        string pickupAddress,
        string pickupDetail,
        decimal pickupLat,
        decimal pickupLng,
        string dropoffAddress,
        string dropoffDetail,
        decimal dropoffLat,
        decimal dropoffLng,
        int fare,
        DateTime pickupStart,
        DateTime pickupEnd,
        DateTime? dropoffStart,
        DateTime? dropoffEnd,
        int queueStage,
        int exposureState,
        string? currentRecommendedDriverId,
        DateTime? recommendationExpiresAt,
        int cargoWeightKg,
        bool 비맞으면안됨 = false,
        bool 냉장필요 = false,
        bool 냉동필요 = false,
        bool 혼적허용 = false,
        bool 독차필수 = false)
    {
        var now = DateTime.UtcNow;
        var request = new 화주운송의뢰
        {
            의뢰Id = requestId,
            화주Id = shipperId,
            주문자UserId = shipperId,
            화물종류 = cargoType,
            화물설명 = cargoDescription,
            화물수량 = 1,
            화물중량Kg = cargoWeightKg,
            화물파손주의여부 = 비맞으면안됨,
            화물온도조건 = 냉동필요 ? "냉동" : 냉장필요 ? "냉장" : "상온",
            운송방식 = transportType,
            차량종류 = vehicleType,
            결제수단 = paymentMethod,
            정산시점 = settlementTime,
            증빙방식 = evidenceMethod,
            수납주체 = paymentMethod == "현장지급" ? "기사" : "플랫폼",
            정산상태 = 상태값.결제상태.결제완료,
            세금계산서필요 = paymentMethod == "후불",
            현금영수증필요 = paymentMethod == "현장지급",
            결제예정금액 = fare,
            픽업_도로명주소 = pickupAddress,
            픽업_상세주소 = pickupDetail,
            픽업_위도 = pickupLat,
            픽업_경도 = pickupLng,
            픽업_연락처_이름 = "개발 상차 담당자",
            픽업_연락처_전화번호 = "010-1111-1000",
            픽업_시간창_시작일시 = pickupStart,
            픽업_시간창_종료일시 = pickupEnd,
            하차_도로명주소 = dropoffAddress,
            하차_상세주소 = dropoffDetail,
            하차_위도 = dropoffLat,
            하차_경도 = dropoffLng,
            하차_연락처_이름 = "개발 수령 담당자",
            하차_연락처_전화번호 = "010-2222-2000",
            하차_시간창_시작일시 = dropoffStart,
            하차_시간창_종료일시 = dropoffEnd,
            서비스레벨 = "V1 개발 검증",
            요청사항 = "상하차 도착 전 연락 후 진행",
            최종운임 = fare,
            클라이언트요청Id = $"dev-{requestId}",
            상태 = 상태값.의뢰상태.생성됨,
            결제상태 = 상태값.결제상태.결제완료,
            배차상태 = 상태값.배차상태.대기,
            CreatedAt = now,
            UpdatedAt = now
        };

        var queue = new 운송원장
        {
            의뢰Id = requestId,
            화주Id = shipperId,
            배차업무유형 = 상태값.배차업무유형.용달운송,
            원본의뢰유형 = "CargoTransport",
            원본의뢰Id = requestId,
            픽업_도로명주소 = pickupAddress,
            픽업_상세주소 = pickupDetail,
            픽업_위도 = pickupLat,
            픽업_경도 = pickupLng,
            하차_도로명주소 = dropoffAddress,
            하차_상세주소 = dropoffDetail,
            하차_위도 = dropoffLat,
            하차_경도 = dropoffLng,
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = queueStage,
            배차노출상태 = exposureState,
            현재추천대상기사Id = currentRecommendedDriverId,
            추천시작시각 = currentRecommendedDriverId is null ? null : now.AddMinutes(-5),
            추천만료시각 = recommendationExpiresAt,
            추천라운드 = currentRecommendedDriverId is null ? 0 : 1,
            계획배차시도횟수 = 1,
            공개전환시각 = queueStage == 상태값.배차큐단계.공개배차 ? now.AddMinutes(-10) : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        var requirement = new 화물요구조건
        {
            의뢰Id = requestId,
            화물길이Mm = 600,
            화물폭Mm = 420,
            화물높이Mm = 360,
            화물무게Kg = cargoWeightKg,
            팔레트개수 = cargoWeightKg >= 300 ? 1 : null,
            비맞으면안됨 = 비맞으면안됨,
            냉장필요 = 냉장필요,
            냉동필요 = 냉동필요,
            혼적허용 = 혼적허용,
            독차필수 = 독차필수,
            주의사항 = cargoDescription,
            CreatedAt = now,
            UpdatedAt = now
        };

        return new V1DispatchScenario(request, queue, requirement);
    }

    private static async Task EnsureTransportAsync(HongdalContext db, string driverId, DateTime now, CancellationToken cancellationToken)
    {
        const string transportNumber1 = "V1-DEV-TRN-001";
        const string transportNumber2 = "V1-DEV-TRN-002";
        var existing = await db.운송원장
            .AsNoTracking()
            .Where(x => x.운송번호 == transportNumber1 || x.운송번호 == transportNumber2)
            .Select(x => x.운송번호)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        if (!existingSet.Contains(transportNumber1))
        {
            db.운송원장.Add(new 운송원장
            {
                운송번호 = transportNumber1,
                상태 = "상차지도착",
                기사_운송자 = driverId,
                출발지 = "서울 강서구 마곡동",
                도착지 = "서울 양천구 목동",
                운임 = 65000m,
                첨부_json = "[]",
                메모 = "상차완료 -> 하차지도착 -> 인수완료 상태 전이 검증용",
                CreatedAt = now.AddHours(-1),
                UpdatedAt = now.AddMinutes(-20)
            });
        }

        if (!existingSet.Contains(transportNumber2))
        {
            db.운송원장.Add(new 운송원장
            {
                운송번호 = transportNumber2,
                상태 = "인수완료",
                출발_픽업 = now.AddDays(-2).AddHours(1),
                도착 = now.AddDays(-2).AddHours(4),
                기사_운송자 = driverId,
                출발지 = "서울 송파구 문정동",
                도착지 = "경기 성남시 분당구",
                운임 = 92000m,
                첨부_json = "[]",
                메모 = "완료 운송 및 인수증/정산 조회 검증용",
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2).AddHours(4)
            });
        }
    }

    private static async Task EnsureSettlementAsync(HongdalContext db, string driverId, DateTime now, CancellationToken cancellationToken)
    {
        var currentYear = now.Year;
        var currentMonth = now.Month;
        var previous = now.AddMonths(-1);

        var hasCurrent = await db.기사월정산.AnyAsync(
            x => x.기사Id == driverId && x.년도 == currentYear && x.월 == currentMonth,
            cancellationToken);
        if (!hasCurrent)
        {
            db.기사월정산.Add(new 기사월정산
            {
                기사Id = driverId,
                년도 = currentYear,
                월 = currentMonth,
                배차건수 = 12,
                이용료 = 5000m,
                결제완료 = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var hasPrevious = await db.기사월정산.AnyAsync(
            x => x.기사Id == driverId && x.년도 == previous.Year && x.월 == previous.Month,
            cancellationToken);
        if (!hasPrevious)
        {
            db.기사월정산.Add(new 기사월정산
            {
                기사Id = driverId,
                년도 = previous.Year,
                월 = previous.Month,
                배차건수 = 18,
                이용료 = 5000m,
                결제완료 = true,
                CreatedAt = now.AddMonths(-1),
                UpdatedAt = now.AddMonths(-1)
            });
        }
    }

    private static async Task EnsureWarehouseSamplesAsync(
        HongdalContext db,
        string shipperId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var warehouse = await EnsureDevelopmentWarehouseAsync(db, shipperId, now, cancellationToken);
        var completedInbound = await EnsureInboundAsync(
            db,
            warehouse.Id,
            shipperId,
            CompletedInboundReference,
            "CN-GP-PORK-202607",
            "중국 냉장식품 공동주문",
            입고상태.입고완료,
            now.AddDays(-1),
            now.AddHours(-18),
            "공동주문 수입 화물이 국내 3PL 냉장 창고에 입고된 검증용 데이터",
            now,
            cancellationToken);

        await EnsureInboundAsync(
            db,
            warehouse.Id,
            shipperId,
            PlannedInboundReference,
            "CN-GP-LIVING-202607",
            "중국 생활용품 공동주문",
            입고상태.예정,
            now.AddDays(2),
            null,
            "통관 완료 후 국내 운송 의뢰로 넘어갈 예정인 검증용 입고",
            now,
            cancellationToken);

        var porkItem = await EnsureInboundItemAsync(
            db,
            completedInbound,
            shipperId,
            "수입 냉장 삼겹살 3kg 묶음",
            "V1-DEV-PORK-3KG",
            "3kg x 33세대 분배",
            inboundQuantity: 33,
            availableQuantity: 29,
            reservedQuantity: 4,
            defectiveQuantity: 0,
            storageLocation: "A-01-03",
            now,
            cancellationToken);

        await EnsureInboundItemAsync(
            db,
            completedInbound,
            shipperId,
            "스마트스토어 판매 샘플 생활용품",
            "V1-DEV-LIVING-BOX",
            "소형 박스 120개",
            inboundQuantity: 120,
            availableQuantity: 96,
            reservedQuantity: 20,
            defectiveQuantity: 4,
            storageLocation: "B-02-01",
            now,
            cancellationToken);

        await EnsureInventoryEvidenceAsync(
            db,
            porkItem,
            completedInbound.주문참조번호,
            shipperId,
            "개발 시드 입고 완료와 재고 이력 검증",
            now,
            cancellationToken);
    }

    private static async Task<창고> EnsureDevelopmentWarehouseAsync(
        HongdalContext db,
        string shipperId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var warehouse = await db.창고
            .FirstOrDefaultAsync(x => x.소유자UserId == shipperId && x.창고명 == DevelopmentWarehouseName, cancellationToken);
        if (warehouse is null)
        {
            warehouse = new 창고
            {
                소유자UserId = shipperId,
                소유자유형 = 창고소유자유형.주문자,
                창고유형 = 창고유형.실제창고,
                물류대행지분류 = LogisticsProxySiteTypes.MarketFulfillment,
                창고명 = DevelopmentWarehouseName,
                사업자번호 = "DEV-3PL-001",
                주소 = "인천 중구 축항대로 123",
                국가코드 = "KR",
                담당자명 = "개발 3PL 담당자",
                연락처 = "032-100-1000",
                위도 = 37.4559m,
                경도 = 126.6243m,
                기본창고여부 = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.창고.Add(warehouse);
            await db.SaveChangesAsync(cancellationToken);
            return warehouse;
        }

        warehouse.소유자유형 = 창고소유자유형.주문자;
        warehouse.창고유형 = 창고유형.실제창고;
        warehouse.물류대행지분류 = LogisticsProxySiteTypes.MarketFulfillment;
        warehouse.주소 = "인천 중구 축항대로 123";
        warehouse.담당자명 = "개발 3PL 담당자";
        warehouse.연락처 = "032-100-1000";
        warehouse.위도 = 37.4559m;
        warehouse.경도 = 126.6243m;
        warehouse.기본창고여부 = true;
        warehouse.IsActive = true;
        warehouse.UpdatedAt = now;
        return warehouse;
    }

    private static async Task<입고요청> EnsureInboundAsync(
        HongdalContext db,
        long warehouseId,
        string shipperId,
        string reference,
        string originalReference,
        string supplierName,
        string status,
        DateTime? expectedArrivalDate,
        DateTime? completedAt,
        string memo,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var inbound = await db.입고요청
            .FirstOrDefaultAsync(x => x.주문자UserId == shipperId && x.주문참조번호 == reference, cancellationToken);
        if (inbound is null)
        {
            inbound = new 입고요청
            {
                창고Id = warehouseId,
                입고흐름유형 = "ContractBased",
                입고생성경로 = "Hongdal 1.0 개발 시드",
                계약선행여부 = true,
                자동생성여부 = true,
                주문참조번호 = reference,
                주문자UserId = shipperId,
                판매자UserId = shipperId,
                운송의뢰Id = "V1-DEV-REQ-003",
                공급처코드 = "V1-DEV-SUP-001",
                공급처명 = supplierName,
                원주문참조번호 = originalReference,
                상태 = status,
                예정도착일 = expectedArrivalDate,
                비고 = memo,
                계약번호 = "V1-DEV-3PL-CN-001",
                계약유형 = "공동구매-3PL입고",
                계약상대방명 = DevelopmentWarehouseName,
                정산방식 = "월말 정산",
                판매수수료율 = 3.5m,
                보관료일단가 = 1200m,
                통관필요여부 = true,
                계약시작일 = now.Date.AddMonths(-1),
                계약종료일 = now.Date.AddMonths(6),
                계약메모 = "공동주문 수입 화물의 국내 3PL 입고와 출고 배치 검증용 계약",
                입고완료일시 = completedAt,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.입고요청.Add(inbound);
            await db.SaveChangesAsync(cancellationToken);
            return inbound;
        }

        inbound.창고Id = warehouseId;
        inbound.입고흐름유형 = "ContractBased";
        inbound.입고생성경로 = "Hongdal 1.0 개발 시드";
        inbound.계약선행여부 = true;
        inbound.자동생성여부 = true;
        inbound.판매자UserId = shipperId;
        inbound.운송의뢰Id = "V1-DEV-REQ-003";
        inbound.공급처코드 = "V1-DEV-SUP-001";
        inbound.공급처명 = supplierName;
        inbound.원주문참조번호 = originalReference;
        inbound.상태 = status;
        inbound.예정도착일 = expectedArrivalDate;
        inbound.비고 = memo;
        inbound.계약번호 = "V1-DEV-3PL-CN-001";
        inbound.계약유형 = "공동구매-3PL입고";
        inbound.계약상대방명 = DevelopmentWarehouseName;
        inbound.정산방식 = "월말 정산";
        inbound.판매수수료율 = 3.5m;
        inbound.보관료일단가 = 1200m;
        inbound.통관필요여부 = true;
        inbound.계약시작일 = now.Date.AddMonths(-1);
        inbound.계약종료일 = now.Date.AddMonths(6);
        inbound.계약메모 = "공동주문 수입 화물의 국내 3PL 입고와 출고 배치 검증용 계약";
        inbound.입고완료일시 = completedAt;
        inbound.UpdatedAt = now;
        return inbound;
    }

    private static async Task<입고상품> EnsureInboundItemAsync(
        HongdalContext db,
        입고요청 inbound,
        string shipperId,
        string productName,
        string sku,
        string optionName,
        int inboundQuantity,
        int availableQuantity,
        int reservedQuantity,
        int defectiveQuantity,
        string storageLocation,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var item = await db.입고상품
            .FirstOrDefaultAsync(x => x.소유자UserId == shipperId && x.SKU == sku, cancellationToken);
        if (item is null)
        {
            item = new 입고상품
            {
                입고요청Id = inbound.Id,
                창고Id = inbound.창고Id,
                소유자UserId = shipperId,
                판매자UserId = shipperId,
                상품명 = productName,
                SKU = sku,
                옵션명 = optionName,
                입고수량 = inboundQuantity,
                가용수량 = availableQuantity,
                예약수량 = reservedQuantity,
                불량수량 = defectiveQuantity,
                보관위치 = storageLocation,
                계약번호 = inbound.계약번호,
                계약유형 = inbound.계약유형,
                계약상대방명 = inbound.계약상대방명,
                정산방식 = inbound.정산방식,
                판매수수료율 = inbound.판매수수료율,
                보관료일단가 = inbound.보관료일단가,
                통관필요여부 = inbound.통관필요여부,
                계약시작일 = inbound.계약시작일,
                계약종료일 = inbound.계약종료일,
                계약메모 = inbound.계약메모,
                상태 = "보관중",
                입고완료일시 = inbound.입고완료일시 ?? now.AddHours(-18),
                CreatedAt = now,
                UpdatedAt = now
            };
            db.입고상품.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            return item;
        }

        item.입고요청Id = inbound.Id;
        item.창고Id = inbound.창고Id;
        item.판매자UserId = shipperId;
        item.상품명 = productName;
        item.옵션명 = optionName;
        item.입고수량 = inboundQuantity;
        item.가용수량 = availableQuantity;
        item.예약수량 = reservedQuantity;
        item.불량수량 = defectiveQuantity;
        item.보관위치 = storageLocation;
        item.계약번호 = inbound.계약번호;
        item.계약유형 = inbound.계약유형;
        item.계약상대방명 = inbound.계약상대방명;
        item.정산방식 = inbound.정산방식;
        item.판매수수료율 = inbound.판매수수료율;
        item.보관료일단가 = inbound.보관료일단가;
        item.통관필요여부 = inbound.통관필요여부;
        item.계약시작일 = inbound.계약시작일;
        item.계약종료일 = inbound.계약종료일;
        item.계약메모 = inbound.계약메모;
        item.상태 = "보관중";
        item.입고완료일시 = inbound.입고완료일시 ?? item.입고완료일시;
        item.UpdatedAt = now;
        return item;
    }

    private static async Task EnsureInventoryEvidenceAsync(
        HongdalContext db,
        입고상품 item,
        string inboundReference,
        string userId,
        string memo,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hasHistory = await db.재고이력.AnyAsync(
            x => x.입고상품Id == item.Id && x.원인유형 == "HongdalV1DevelopmentSeed",
            cancellationToken);
        if (!hasHistory)
        {
            db.재고이력.Add(new 재고이력
            {
                입고상품Id = item.Id,
                이력유형 = 재고이동유형.입고,
                변경수량 = item.입고수량,
                변경후수량 = item.가용수량,
                원인유형 = "HongdalV1DevelopmentSeed",
                원인Id = item.입고요청Id,
                처리UserId = userId,
                메모 = memo,
                처리일시 = now.AddHours(-18)
            });
        }

        var hasMovement = await db.재고이동.AnyAsync(
            x => x.SKU == item.SKU && x.주문참조번호 == inboundReference && x.이동유형 == 재고이동유형.입고,
            cancellationToken);
        if (!hasMovement)
        {
            db.재고이동.Add(new 재고이동
            {
                창고Id = item.창고Id,
                입고상품Id = item.Id,
                상품명 = item.상품명,
                SKU = item.SKU,
                이동유형 = 재고이동유형.입고,
                수량 = item.입고수량,
                주문참조번호 = inboundReference,
                입고요청Id = item.입고요청Id,
                처리UserId = userId,
                메모 = memo,
                발생일시 = now.AddHours(-18)
            });
        }
    }

    private static void SeedDriverDevelopmentSnapshot(
        I기사개발스냅샷Provider snapshotProvider,
        string driverId,
        DateTime now,
        IReadOnlyList<V1DispatchScenario> scenarios)
    {
        var recommendations = scenarios
            .Select((scenario, index) => CreateDevelopmentRecommendation(scenario, index, now))
            .ToList();
        recommendations.Add(CreateGroupPurchaseDevelopmentRecommendation(now));

        snapshotProvider.ReplaceSnapshot(new 기사개발스냅샷응답
        {
            현재위치 = new 기사개발현재위치응답
            {
                위치명 = "서울 강서구 화곡동",
                위도 = 37.5412m,
                경도 = 126.8409m,
                갱신시각 = now.AddMinutes(-3)
            },
            근무상태 = new 기사개발근무상태응답
            {
                기사명 = "개발용 기사",
                운행상태 = 상태값.기사운행상태.운행중,
                시작모드 = "일반 운행",
                시작위치 = "서울 강서구 화곡동",
                복귀지 = "서울 양천구 목동",
                시작시각 = now.Date.AddHours(8).AddMinutes(30),
                추천콜수 = recommendations.Count,
                오늘예약수 = 1
            },
            정산요약 = new 기사개발정산요약응답
            {
                년도 = now.Year,
                월 = now.Month,
                배차건수 = 12,
                이용료 = 5000m,
                월상한 = 5000m,
                결제완료 = true,
                상세항목 =
                [
                    new 기사개발정산상세응답 { 항목명 = "이용료", 설명 = "Hongdal 1.0 개발 시드 배차 기준", 금액 = 5000m },
                    new 기사개발정산상세응답 { 항목명 = "월 상한 조정", 설명 = "월 이용료 상한 적용", 금액 = 0m }
                ]
            },
            추천의뢰목록 = recommendations,
            예약목록 =
            [
                new 기사개발예약응답
                {
                    Id = 1,
                    시작시각 = now.Date.AddHours(15),
                    시작모드 = "예약 운행",
                    시작위치 = "서울 양천구 목동",
                    복귀지 = "서울 강서구 화곡동",
                    상태 = "확정",
                    메모 = "예약 시간 20분 전 위치 확인"
                }
            ],
            운송목록 =
            [
                new 기사개발운송응답
                {
                    Id = 1,
                    의뢰Id = "V1-DEV-TRN-001",
                    화물종류 = "가구",
                    픽업지 = "서울 강서구 마곡동",
                    하차지 = "서울 양천구 목동",
                    픽업위도 = 37.5668m,
                    픽업경도 = 126.8298m,
                    하차위도 = 37.5268m,
                    하차경도 = 126.8755m,
                    현재단계 = "상차지도착",
                    예정시각 = now.AddMinutes(35),
                    운송거리Km = 12.4m,
                    예상수익 = 65000m,
                    인수증필요 = true,
                    인수증서명필수 = false,
                    결제방식 = "인수증 정산",
                    다음행동 = "상차 완료 사진 업로드"
                },
                new 기사개발운송응답
                {
                    Id = 2,
                    의뢰Id = "V1-DEV-TRN-002",
                    화물종류 = "완료 운송",
                    픽업지 = "서울 송파구 문정동",
                    하차지 = "경기 성남시 분당구",
                    픽업위도 = 37.4861m,
                    픽업경도 = 127.1226m,
                    하차위도 = 37.3946m,
                    하차경도 = 127.1115m,
                    현재단계 = "인수완료",
                    예정시각 = now.AddDays(-2).AddHours(4),
                    운송거리Km = 18.6m,
                    예상수익 = 92000m,
                    인수증필요 = true,
                    인수증서명필수 = true,
                    결제방식 = "플랫폼 정산",
                    다음행동 = "정산 상태 확인"
                }
            ],
            알림목록 =
            [
                new 기사개발알림응답
                {
                    Id = 1,
                    종류 = "추천",
                    제목 = "연계 가능한 추천콜이 도착했습니다.",
                    내용 = "현재 위치와 복귀지를 기준으로 이어 받을 수 있는 후보입니다.",
                    발생시각 = now.AddMinutes(-5),
                    읽음 = false
                },
                new 기사개발알림응답
                {
                    Id = 2,
                    종류 = "증빙",
                    제목 = "상차 완료 사진 업로드가 필요합니다.",
                    내용 = $"{driverId} 기사님의 현재 운송 건은 사진 저장 후 다음 단계로 넘어갑니다.",
                    발생시각 = now.AddMinutes(-1),
                    읽음 = false
                }
            ]
        });
    }

    private static 기사개발추천의뢰응답 CreateDevelopmentRecommendation(
        V1DispatchScenario scenario,
        int index,
        DateTime now)
    {
        var request = scenario.Request;
        var queue = scenario.Queue;
        var pickupDistance = ApproximateDistanceKm(37.5412m, 126.8409m, request.픽업_위도, request.픽업_경도);
        var lineDistance = ApproximateDistanceKm(request.픽업_위도, request.픽업_경도, request.하차_위도, request.하차_경도);
        var drivingDistance = decimal.Round(lineDistance * 1.25m, 1);
        var returnDistance = ApproximateDistanceKm(37.5268m, 126.8755m, request.하차_위도, request.하차_경도);
        var expectedProfit = request.최종운임 ?? request.결제예정금액.GetValueOrDefault();
        var expectedFuelCost = decimal.Round(drivingDistance * 750m, 0);
        var expectedToll = drivingDistance >= 25m ? 3200m : 0m;

        return new 기사개발추천의뢰응답
        {
            의뢰Id = request.의뢰Id,
            화물종류 = request.화물종류,
            운송방식 = request.운송방식,
            운송의뢰유형코드 = "GeneralCargoTransport",
            운송의뢰유형표시 = "일반 화물",
            당일상차필수 = request.픽업_시간창_시작일시.Date == now.Date,
            당일하차필수 = request.하차_시간창_종료일시?.Date == now.Date,
            차량톤수 = request.차량종류.Contains("2.5", StringComparison.Ordinal) ? "2.5톤" : "1톤",
            차량형태 = request.차량종류,
            인수증필요 = request.증빙방식.Contains("인수증", StringComparison.Ordinal),
            결제방식 = $"{request.결제수단}/{request.정산시점}",
            픽업지 = request.픽업_도로명주소,
            하차지 = request.하차_도로명주소,
            픽업_위도 = request.픽업_위도,
            픽업_경도 = request.픽업_경도,
            하차_위도 = request.하차_위도,
            하차_경도 = request.하차_경도,
            직선거리Km = lineDistance,
            픽업거리Km = pickupDistance,
            공차거리Km = pickupDistance,
            운송거리Km = drivingDistance,
            복귀예상거리Km = returnDistance,
            지금바로복귀거리Km = 8.2m,
            복귀우회증가거리Km = returnDistance - 8.2m,
            총공차거리Km = pickupDistance + returnDistance,
            주행거리Km = pickupDistance + drivingDistance,
            예상톨비 = expectedToll,
            예상연료비 = expectedFuelCost,
            예상총비용 = expectedToll + expectedFuelCost,
            예상수익 = expectedProfit,
            추천점수 = 92m - (index * 5m),
            추천사유 = queue.현재추천대상기사Id is null
                ? "공개 배차 후보이며 현재 위치에서 접근 가능한 개발 검증용 의뢰입니다."
                : "현재 기사에게 추천 잠금이 걸린 개발 검증용 의뢰입니다.",
            복귀지기준추천여부 = true,
            복귀지출처 = "오늘복귀지",
            복귀추천사유 = "오늘 복귀지와 다음 상차 동선이 크게 어긋나지 않습니다.",
            요약설명 = $"{request.화물종류} 운송, {request.픽업_도로명주소}에서 {request.하차_도로명주소}까지",
            상세설명 = $"{request.픽업_상세주소}에서 상차 후 {request.하차_상세주소}로 하차합니다. 결제와 증빙 상태까지 화면에서 함께 확인합니다.",
            상태 = queue.현재추천대상기사Id is null ? "공개중" : "추천중",
            배차상태 = request.배차상태,
            추천시작시각 = queue.추천시작시각,
            추천만료시각 = queue.추천만료시각
        };
    }

    private static 기사개발추천의뢰응답 CreateGroupPurchaseDevelopmentRecommendation(DateTime now)
    {
        var pickupDistance = ApproximateDistanceKm(37.5412m, 126.8409m, 37.4559m, 126.6243m);
        var lineDistance = ApproximateDistanceKm(37.4559m, 126.6243m, 37.5610m, 126.8370m);
        var drivingDistance = decimal.Round(lineDistance * 1.25m, 1);
        var returnDistance = ApproximateDistanceKm(37.5268m, 126.8755m, 37.5610m, 126.8370m);

        return new 기사개발추천의뢰응답
        {
            의뢰Id = "V1-DEV-GP-201",
            화물종류 = "수입 냉장식품 공동주문",
            운송방식 = "공동주문 세대배송",
            운송의뢰유형코드 = "GroupPurchaseCargoTransport",
            운송의뢰유형표시 = "공동주문 운송",
            당일상차필수 = true,
            당일하차필수 = true,
            차량톤수 = "1톤",
            차량형태 = "냉장탑",
            인수증필요 = true,
            공동주문운송여부 = true,
            세대배송포함여부 = true,
            세대배송건수 = 33,
            세대배송업무표시 = "상하차 + 세대 문앞 33건",
            결제방식 = "플랫폼 정산",
            픽업지 = "인천항 보세창고",
            하차지 = "서울 강서구 홍달아파트",
            픽업_위도 = 37.4559m,
            픽업_경도 = 126.6243m,
            하차_위도 = 37.5610m,
            하차_경도 = 126.8370m,
            직선거리Km = lineDistance,
            픽업거리Km = pickupDistance,
            공차거리Km = pickupDistance,
            운송거리Km = drivingDistance,
            복귀예상거리Km = returnDistance,
            지금바로복귀거리Km = 8.2m,
            복귀우회증가거리Km = returnDistance - 8.2m,
            총공차거리Km = pickupDistance + returnDistance,
            주행거리Km = pickupDistance + drivingDistance,
            예상톨비 = 3200m,
            예상연료비 = 18000m,
            예상총비용 = 21200m,
            예상수익 = 176000m,
            추천점수 = 94m,
            추천사유 = "공동주문 수입 화물이며 세대배송 범위와 분류 상태를 확인해야 하는 추천입니다.",
            복귀지기준추천여부 = true,
            복귀지출처 = "오늘복귀지",
            복귀추천사유 = "오늘 복귀지와 공동주문 하차지가 크게 어긋나지 않습니다.",
            요약설명 = "수입 냉장식품 공동주문 운송, 인천항 보세창고에서 서울 강서구 홍달아파트까지",
            상세설명 = "상품정보 스티커, 세대배송 건수, 분류 상태를 확인한 뒤 수락 여부를 결정합니다.",
            상태 = "공동주문추천",
            배차상태 = 상태값.배차상태.대기,
            추천시작시각 = now.AddMinutes(-3),
            추천만료시각 = now.AddMinutes(57)
        };
    }

    private static decimal ApproximateDistanceKm(decimal? fromLatitude, decimal? fromLongitude, decimal? toLatitude, decimal? toLongitude)
    {
        if (!fromLatitude.HasValue || !fromLongitude.HasValue || !toLatitude.HasValue || !toLongitude.HasValue)
        {
            return 0m;
        }

        var latitudeKm = Math.Abs(fromLatitude.Value - toLatitude.Value) * 111m;
        var longitudeKm = Math.Abs(fromLongitude.Value - toLongitude.Value) * 88m;
        return decimal.Round(latitudeKm + longitudeKm, 1);
    }

    private sealed record V1DispatchScenario(
        화주운송의뢰 Request,
        운송원장 Queue,
        화물요구조건 CargoRequirement);
}
