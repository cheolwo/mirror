using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Common.Hr;
using 살뜰.Data;
using 살뜰.도메인.기사;
using 살뜰.도메인.사용자;
using 살뜰.도메인.창고;

internal static class CargoJourneySeed
{
    internal const string ShipperId = "cargo-journey-shipper";
    internal const string DriverId = "cargo-journey-driver";
    internal const string WarehouseUserId = "cargo-journey-warehouse";
    internal const string AdminId = "cargo-journey-admin";
    internal const long WarehouseId = 910001;
    internal const long InboundRequestId = 910002;
    internal const long InboundItemId = 910003;
    internal const long OutboundPlanId = 910004;
    internal const int Quantity = 9;
    internal const string ExpectedRequestId = "warehouse-outbound-910004";

    internal static async Task PrepareAsync(IServiceProvider services, CargoJourneySettings settings)
    {
        var db = services.GetRequiredService<SsalddelContext>();
        await db.Database.EnsureCreatedAsync();
        // A reused dedicated database must never acquire accounts from another environment.
        if (await db.Users.AnyAsync(user => !user.Id.StartsWith("cargo-journey-")))
            throw new InvalidOperationException("CargoJourneyForeignUserFound");
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var (id, role) in new[]
        {
            (ShipperId, 역할명.화주), (DriverId, 역할명.기사),
            (WarehouseUserId, 역할명.창고관리자), (AdminId, 역할명.서버관리자)
        })
        {
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)));
            var user = await users.FindByIdAsync(id);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = id, UserName = id, Email = id + "@example.invalid", EmailConfirmed = true,
                    PrivacyConsentVersion = "cargo-journey-synthetic.r1", PrivacyConsentedAtUtc = DateTime.UtcNow
                };
                Check(await users.CreateAsync(user, settings.AccountPassword));
            }
            else if (user.UserName != id || !await users.CheckPasswordAsync(user, settings.AccountPassword))
                throw new InvalidOperationException("CargoJourneyIdentityConflict");
            if (!await users.IsInRoleAsync(user, role)) Check(await users.AddToRoleAsync(user, role));
        }

        var now = DateTime.UtcNow;
        if (!await db.용달기사.AnyAsync(driver => driver.기사Id == DriverId))
            db.용달기사.Add(new 용달기사
            {
                기사Id = DriverId, 기사명 = "합성 화물 기사", 차량 = "1톤 카고",
                상태 = "활동중", 운행상태 = "대기", 연락처 = "000-0000-0000",
                주_활동지역 = "격리 검증", 메모 = "합성 fixture · 실제 기사 등록 아님",
                CreatedAt = now, UpdatedAt = now
            });
        if (!await db.창고.AnyAsync(warehouse => warehouse.Id == WarehouseId))
            db.창고.Add(new 창고
            {
                Id = WarehouseId, 소유자UserId = ShipperId, 소유자유형 = 창고소유자유형.주문자,
                창고명 = "합성 출고 검증 창고", 주소 = "격리 검증 상차 창고",
                담당자명 = "합성 상차 담당자", 연락처 = "000-0000-0000",
                위도 = 37.588m, 경도 = 127.085m, IsActive = true,
                CreatedAt = now, UpdatedAt = now
            });
        await db.SaveChangesAsync();
        if (!await db.창고사용자.AnyAsync(user => user.창고Id == WarehouseId && user.UserId == WarehouseUserId))
            db.창고사용자.Add(new 창고사용자
            {
                창고Id = WarehouseId, UserId = WarehouseUserId, 역할명 = "출고", IsPrimary = true,
                CreatedAt = now, UpdatedAt = now
            });
        foreach (var roleCode in new[] { HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseDispatchOperator })
            if (!await db.HrRoleAssignments.AnyAsync(role => role.UserId == WarehouseUserId && role.RoleCode == roleCode))
                db.HrRoleAssignments.Add(new HrRoleAssignmentRecord
                {
                    Id = Guid.NewGuid(), UserId = WarehouseUserId,
                    ScopeType = HrScopeTypes.Platform, ScopeId = HrScopeIds.Global,
                    ParticipantCategory = HrParticipantCategoryCodes.InternalProjectOperator,
                    RoleCode = roleCode, RoleName = "격리 합성 창고 출고 담당자",
                    IsActive = true, AssignedByUserId = AdminId, AssignedAtUtc = now,
                    CreatedAt = now, UpdatedAt = now
                });
        if (!await db.입고요청.AnyAsync(inbound => inbound.Id == InboundRequestId))
            db.입고요청.Add(new 입고요청
            {
                Id = InboundRequestId, 창고Id = WarehouseId,
                주문참조번호 = "CARGO-JOURNEY-OUTBOUND-001",
                주문자UserId = ShipperId, 판매자UserId = ShipperId,
                상태 = "입고완료", 입고생성경로 = "CargoJourneySyntheticFixture",
                계약선행여부 = false, 자동생성여부 = false, 보관조건 = "상온",
                공급처명 = "합성 공급자", 입고완료일시 = now,
                CreatedAt = now, UpdatedAt = now
            });
        await db.SaveChangesAsync();
        if (!await db.입고상품.AnyAsync(item => item.Id == InboundItemId))
            db.입고상품.Add(new 입고상품
            {
                Id = InboundItemId, 입고요청Id = InboundRequestId, 창고Id = WarehouseId,
                소유자UserId = ShipperId, 판매자UserId = ShipperId,
                상품명 = "합성 검증 박스", SKU = "CARGO-JOURNEY-BOX",
                입고수량 = Quantity, 가용수량 = Quantity, 예약수량 = 0,
                보관위치 = "합성 도크 A-01", 상태 = "포장완료", 입고완료일시 = now,
                CreatedAt = now, UpdatedAt = now
            });
        await db.SaveChangesAsync();
        if (!await db.출고예정.AnyAsync(plan => plan.Id == OutboundPlanId))
            db.출고예정.Add(new 출고예정
            {
                Id = OutboundPlanId, 입고상품Id = InboundItemId, 입고요청Id = InboundRequestId,
                출고창고Id = WarehouseId, 상품명 = "합성 검증 박스", SKU = "CARGO-JOURNEY-BOX",
                주문참조번호 = "CARGO-JOURNEY-OUTBOUND-001", 수량 = Quantity,
                주문자UserId = ShipperId, 판매자UserId = ShipperId,
                상태 = 출고상태.준비중, CreatedAt = now, UpdatedAt = now
            });
        if (!await db.재고이력.AnyAsync(history => history.입고상품Id == InboundItemId && history.이력유형 == "포장"))
            db.재고이력.Add(new 재고이력
            {
                입고상품Id = InboundItemId, 이력유형 = "포장", 변경수량 = 0, 변경후수량 = Quantity,
                원인유형 = "CargoJourneySyntheticFixture", 처리UserId = WarehouseUserId,
                메모 = "합성 fixture: 포장 9개 / 상온포장 · 실제 포장 작업 증거 아님", 처리일시 = now
            });
        await db.SaveChangesAsync();
        // Preserve previous journey state on a host restart, and reject mismatched fixture bindings.
        var planCheck = await db.출고예정.AsNoTracking().SingleAsync(plan => plan.Id == OutboundPlanId);
        var inventoryCheck = await db.입고상품.AsNoTracking().SingleAsync(item => item.Id == InboundItemId);
        if (planCheck.입고상품Id != InboundItemId || planCheck.출고창고Id != WarehouseId
            || planCheck.수량 != Quantity || inventoryCheck.창고Id != WarehouseId
            || inventoryCheck.입고요청Id != InboundRequestId
            || inventoryCheck.SKU != "CARGO-JOURNEY-BOX"
            || (!string.IsNullOrEmpty(planCheck.운송의뢰Id) && planCheck.운송의뢰Id != ExpectedRequestId))
            throw new InvalidOperationException("CargoJourneyFixtureBindingConflict");
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("CargoJourneyIdentitySeedFailed:" + string.Join(',', result.Errors.Select(error => error.Code)));
    }
}
