using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using 살뜰.Data;
using 살뜰.도메인.기사;
using 살뜰.도메인.사용자;
using 살뜰.도메인.차량;

namespace Ssalddel.DispatchTraining;

/// <summary>격리 DB의 합성 계정과 차량만 준비한다. 주문·운행·추천·수락 상태는 정상 API가 소유한다.</summary>
internal static class DispatchTrainingSeed
{
    internal const string Prefix = "dispatch-training-";
    internal const string AdminId = Prefix + "admin";
    internal const string ShipperId = Prefix + "shipper";
    internal const string VehicleCode = Prefix + "motorcycle";
    internal const string VehicleName = "오토바이";
    internal const int InitialDriverCount = 6;
    internal const int SupportDriverCount = 2;
    internal static IReadOnlyList<string> DriverIds { get; } = Enumerable.Range(1, InitialDriverCount + SupportDriverCount)
        .Select(i => $"{Prefix}driver-{i:00}").ToArray();
    internal static IReadOnlyList<string> Drivers => DriverIds;

    internal static async Task PrepareAsync(IServiceProvider services, DispatchTrainingSettings settings)
    {
        var db = services.GetRequiredService<SsalddelContext>();
        await db.Database.EnsureCreatedAsync();
        // 환경 선택은 settings.Validate가 확인하며 재사용 DB의 다른 사용자도 허용하지 않는다.
        if (await db.Users.AnyAsync(user => !user.Id.StartsWith(Prefix)))
            throw new InvalidOperationException("DispatchTrainingForeignUserFound");
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var accounts = new[] { (AdminId, 역할명.서버관리자), (ShipperId, 역할명.화주) }
            .Concat(DriverIds.Select(id => (id, 역할명.기사)));
        foreach (var (id, role) in accounts)
        {
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)));
            var user = await users.FindByIdAsync(id);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = id, UserName = id, Email = id + "@example.invalid", EmailConfirmed = true,
                    PrivacyConsentVersion = "dispatch-training-synthetic.r58", PrivacyConsentedAtUtc = DateTime.UtcNow
                };
                Check(await users.CreateAsync(user, settings.AccountPassword));
            }
            else if (user.UserName != id || user.Email != id + "@example.invalid"
                || !await users.CheckPasswordAsync(user, settings.AccountPassword))
                throw new InvalidOperationException("DispatchTrainingIdentityConflict");
            var assigned = await users.GetRolesAsync(user);
            if (assigned.Any(x => x != role)) throw new InvalidOperationException("DispatchTrainingRoleConflict");
            if (!assigned.Contains(role, StringComparer.Ordinal)) Check(await users.AddToRoleAsync(user, role));
        }

        var vehicle = await db.차량제원.SingleOrDefaultAsync(x => x.차량코드 == VehicleCode);
        if (vehicle is null)
        {
            db.차량제원.Add(new 차량제원
            {
                차량코드 = VehicleCode, 차량명 = VehicleName, 제조사 = "합성 검증", 모델명 = "합성 소형 배송함",
                차급 = "오토바이", 차체형태 = "배송함", 적재함길이Mm = 500, 적재함폭Mm = 450, 적재함높이Mm = 400,
                최대적재중량Kg = 20, 운영권장중량Kg = 20, 권장최대CBM = 0.09m, 팔레트적재개수 = 0,
                비눈보호가능 = true, 추천사용여부 = true, 기준연비KmPerLiter = 40m,
                장점메모 = "격리 합성 제원 · 실제 차량 성능·적재 허가 근거 아님"
            });
        }
        else if (vehicle.차량명 != VehicleName || vehicle.제조사 != "합성 검증"
            || vehicle.적재함길이Mm != 500 || vehicle.적재함폭Mm != 450 || vehicle.적재함높이Mm != 400
            || vehicle.최대적재중량Kg != 20 || vehicle.권장최대CBM != 0.09m)
            throw new InvalidOperationException("DispatchTrainingVehicleConflict");

        var now = DateTime.UtcNow;
        foreach (var id in DriverIds)
        {
            var driver = await db.용달기사.SingleOrDefaultAsync(x => x.기사Id == id);
            if (driver is null)
                db.용달기사.Add(new 용달기사
                {
                    기사Id = id, 기사명 = $"합성 기사 {id[^2..]}", 차량 = VehicleCode,
                    상태 = "활동중", 운행상태 = "대기", 연락처 = "000-0000-0000", 주_활동지역 = "격리 합성 지역",
                    메모 = "DispatchTrainingSynthetic.r58 · 실제 기사 등록 아님", CreatedAt = now, UpdatedAt = now
                });
            else if (driver.차량 != VehicleCode || driver.메모 != "DispatchTrainingSynthetic.r58 · 실제 기사 등록 아님")
                throw new InvalidOperationException("DispatchTrainingDriverConflict");
        }
        await db.SaveChangesAsync();
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("DispatchTrainingIdentitySeedFailed");
    }
}
