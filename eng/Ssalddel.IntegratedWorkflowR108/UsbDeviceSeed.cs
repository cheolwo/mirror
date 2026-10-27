using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Development.MobileFieldTest;
using 살뜰.Data;
using 살뜰.도메인.기사;
using 살뜰.도메인.사용자;

internal static class UsbDeviceSeed
{
    private static readonly string[] CargoNames =
    [CargoJourneySeed.ShipperId, CargoJourneySeed.DriverId, CargoJourneySeed.WarehouseUserId, CargoJourneySeed.AdminId];
    private static readonly string[] DevelopmentNames =
    ["qkrcjfdn79", "driver1@ssalddel.local", "restaurant101", "field-orderer", "shipper1"];

    internal static async Task<IReadOnlyList<UsbTestAccount>> PrepareAsync(IServiceProvider services, CargoJourneySettings settings)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
        // Latest model only, on the freshly isolated r67 database. No migration or reset command.
        await db.Database.EnsureCreatedAsync();
        var names = await db.Users.AsNoTracking().Select(user => user.UserName).ToListAsync();
        var allowedNames = CargoNames.Concat(DevelopmentNames).ToHashSet(StringComparer.Ordinal);
        if (names.Any(name => name is null || !allowedNames.Contains(name)))
            throw new InvalidOperationException("UsbR67ForeignIdentityFound");

        // The original cargo seed's strict cargo-only guard is preserved. It runs before food identities.
        // On subsequent r67 boots validate its stable bindings instead of replaying/rewinding the journey.
        if (names.All(name => name is not null && CargoNames.Contains(name, StringComparer.Ordinal)))
            await CargoJourneySeed.PrepareAsync(scope.ServiceProvider, settings);
        else
            await ValidateCargoFixtureAsync(db);

        await IdentityDataSeeder.SeedAsync(services, includeDevelopmentAccounts: true);
        await 모바일현장검증자료Seeder.SeedAsync(db, DateTime.UtcNow);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var foodDriver = await users.FindByNameAsync("driver1@ssalddel.local")
                         ?? throw new InvalidOperationException("UsbR67FoodDriverMissing");
        // IdentityDataSeeder supplies the shared vehicle profile. The food profile is a synthetic identity
        // fixture only: no shift, GPS point, receiving intent, recommendation, or completion is fabricated.
        var existing = await db.Set<배달기사>().SingleOrDefaultAsync(driver => driver.기사Id == foodDriver.Id);
        if (existing is null)
        {
            var now = DateTime.UtcNow;
            db.Set<배달기사>().Add(new 배달기사
            {
                기사Id = foodDriver.Id, 기사명 = "USB r67 합성 음식 기사", 상태 = "활동중", 차량 = "오토바이",
                메모 = "합성 계정 fixture · 실제 기사/운행/GPS 증거 아님 · usb-r67", CreatedAt = now, UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }
        else if (existing.차량 != "오토바이" || !existing.메모.Contains("usb-r67", StringComparison.Ordinal))
            throw new InvalidOperationException("UsbR67FoodProfileConflict");

        var accounts = new List<UsbTestAccount>();
        foreach (var name in CargoNames.Concat(DevelopmentNames))
        {
            var user = await users.FindByNameAsync(name) ?? throw new InvalidOperationException("UsbR67IdentityMissing");
            if (!await users.CheckPasswordAsync(user, settings.AccountPassword))
                throw new InvalidOperationException("UsbR67IdentityPasswordConflict");
            accounts.Add(new(user.Id, name, (await users.GetRolesAsync(user)).ToArray(), settings.AccountPassword));
        }
        var restaurant = await users.FindByNameAsync("restaurant101") ?? throw new InvalidOperationException("UsbR67RestaurantMissing");
        var restaurantClaims = await users.GetClaimsAsync(restaurant);
        if (!restaurantClaims.Any(claim => claim.Type == 음식점접근ClaimTypes.음식점Id && claim.Value == "101"))
            throw new InvalidOperationException("UsbR67RestaurantClaimMissing");
        var privatePath = Path.Combine(settings.ArtifactRoot, ".private", "accounts.json");
        await File.WriteAllTextAsync(privatePath, JsonSerializer.Serialize(new
        {
            schemaVersion = "usb-r67.synthetic-accounts.r1", synthetic = true,
            database = CargoJourneySettings.DatabaseName, accounts
        }, new JsonSerializerOptions { WriteIndented = true }));
        return accounts;
    }

    private static async Task ValidateCargoFixtureAsync(SsalddelContext db)
    {
        if (await db.Users.CountAsync(user => Enumerable.Contains(CargoNames, user.UserName!)) != CargoNames.Length)
            throw new InvalidOperationException("UsbR67CargoIdentityIncomplete");
        var plan = await db.출고예정.AsNoTracking().SingleOrDefaultAsync(row => row.Id == CargoJourneySeed.OutboundPlanId);
        var item = await db.입고상품.AsNoTracking().SingleOrDefaultAsync(row => row.Id == CargoJourneySeed.InboundItemId);
        var warehouse = await db.창고.AsNoTracking().SingleOrDefaultAsync(row => row.Id == CargoJourneySeed.WarehouseId);
        if (plan is null || item is null || warehouse is null
            || warehouse.소유자UserId != CargoJourneySeed.ShipperId
            || plan.입고상품Id != CargoJourneySeed.InboundItemId || plan.출고창고Id != CargoJourneySeed.WarehouseId
            || plan.수량 != CargoJourneySeed.Quantity || item.창고Id != CargoJourneySeed.WarehouseId
            || item.입고요청Id != CargoJourneySeed.InboundRequestId || item.SKU != "CARGO-JOURNEY-BOX"
            || (!string.IsNullOrEmpty(plan.운송의뢰Id) && plan.운송의뢰Id != CargoJourneySeed.ExpectedRequestId))
            throw new InvalidOperationException("UsbR67CargoFixtureBindingConflict");
    }
}

internal sealed record UsbTestAccount(string UserId, string UserName, IReadOnlyList<string> Roles, string Password);
