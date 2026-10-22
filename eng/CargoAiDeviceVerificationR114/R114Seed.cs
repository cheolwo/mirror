using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using 살뜰.Data;
using 살뜰.도메인.기사;
using 살뜰.도메인.사용자;
using 살뜰.도메인.차량;

internal static class R114Seed
{
    internal const string SecondDriverId = "cargo-journey-driver-b";
    private static readonly string[] AccountIds =
    [CargoJourneySeed.ShipperId, CargoJourneySeed.DriverId, CargoJourneySeed.WarehouseUserId, CargoJourneySeed.AdminId, SecondDriverId];

    internal static async Task<IReadOnlyList<R114TestAccount>> PrepareAsync(IServiceProvider services, CargoJourneySettings settings)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
        await db.Database.EnsureCreatedAsync();
        var existing = await db.Users.AsNoTracking().Select(x => new { x.Id, x.UserName }).ToListAsync();
        if (existing.Any(x => !AccountIds.Contains(x.Id, StringComparer.Ordinal) || x.UserName != x.Id))
            throw new InvalidOperationException("R114ForeignIdentityFound");
        // 기존 seed의 시작 이력·재고·출고 안정 참조를 재사용하며 진행 상태를 되돌리지 않는다.
        await CargoJourneySeed.PrepareAsync(scope.ServiceProvider, settings);
        if (settings.CargoPrimaryAI) await PreparePrimaryVehicleAsync(db, settings);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var second = await users.FindByIdAsync(SecondDriverId);
        if (second is null)
        {
            second = new ApplicationUser
            {
                Id = SecondDriverId, UserName = SecondDriverId, Email = SecondDriverId + "@example.invalid", EmailConfirmed = true,
                PrivacyConsentVersion = "cargo-ai-device-r114.synthetic.r1", PrivacyConsentedAtUtc = DateTime.UtcNow
            };
            Check(await users.CreateAsync(second, settings.AccountPassword));
        }
        if (second.UserName != SecondDriverId || !await users.CheckPasswordAsync(second, settings.AccountPassword))
            throw new InvalidOperationException("R114SecondDriverIdentityConflict");
        if (!await users.IsInRoleAsync(second, 역할명.기사)) Check(await users.AddToRoleAsync(second, 역할명.기사));
        var profile = await db.용달기사.SingleOrDefaultAsync(x => x.기사Id == SecondDriverId);
        if (profile is null)
        {
            var now = DateTime.UtcNow;
            db.용달기사.Add(new 용달기사
            {
                기사Id = SecondDriverId, 기사명 = "합성 화물 기사 B", 차량 = "1톤 카고", 상태 = "활동중", 운행상태 = "대기",
                연락처 = "000-0000-0000", 주_활동지역 = "격리 검증", 메모 = "합성 fixture · 실제 기사 등록 아님 · r114",
                CreatedAt = now, UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }
        else if (profile.차량 != "1톤 카고" || !profile.메모.Contains("r114", StringComparison.Ordinal))
            throw new InvalidOperationException("R114SecondDriverProfileConflict");
        var first = await db.용달기사.AsNoTracking().SingleAsync(x => x.기사Id == CargoJourneySeed.DriverId);
        if (first.차량 != "1톤 카고") throw new InvalidOperationException("R114FirstDriverProfileConflict");
        var accounts = new List<R114TestAccount>();
        foreach (var id in AccountIds)
        {
            var user = await users.FindByIdAsync(id) ?? throw new InvalidOperationException("R114AccountMissing");
            if (user.UserName != id || !await users.CheckPasswordAsync(user, settings.AccountPassword))
                throw new InvalidOperationException("R114AccountConflict");
            accounts.Add(new(user.Id, id, (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToArray(), settings.AccountPassword));
        }
        if (await db.Users.CountAsync() != AccountIds.Length) throw new InvalidOperationException("R114UnexpectedAccountCount");
        var path = Path.Combine(settings.ArtifactRoot, ".private", "accounts.json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "cargo-ai-device-r114.synthetic-accounts.v1", synthetic = true,
            database = CargoJourneySettings.DatabaseName, runStableId = settings.RunStableId, accounts
        }, new JsonSerializerOptions { WriteIndented = true });
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new InvalidOperationException("R114PrivateAccountManifestConflict");
        }
        else
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await file.WriteAsync(bytes); file.Flush(true);
        }
        return accounts;
    }

    private static async Task PreparePrimaryVehicleAsync(SsalddelContext db, CargoJourneySettings settings)
    {
        // 기존 IdentityDataSeeder의 1톤 카고 수치를 시험 입력으로 재사용한다. 기사 실차 측정/승인이 아니다.
        var specification = await db.차량제원.SingleOrDefaultAsync(x => x.차량코드 == "1톤 카고");
        var created = specification is null;
        if (specification is null)
        {
            if (await db.차량제원.AnyAsync(x => x.차량명 == "1톤 카고")) throw new InvalidOperationException("R114SyntheticVehicleAliasConflict");
            specification = new 차량제원
            {
                차량코드 = "1톤 카고", 차량명 = "1톤 카고", 제조사 = "합성 검증", 모델명 = "R114 휴대폰 검증 제원",
                차급 = "1톤", 차체형태 = "카고", 적재함길이Mm = 3110, 적재함폭Mm = 1630, 적재함높이Mm = 355,
                최대적재중량Kg = 1000, 운영권장중량Kg = 850, 차량전체높이Mm = 1990, 바닥높이Mm = 840,
                비눈보호가능 = false, 냉장가능 = false, 냉동가능 = false, 측면상하차가능 = false,
                리프트가능 = false, 장재물유리 = true, 팔레트적재개수 = 1, 기준연비KmPerLiter = 8m,
                추천사용여부 = true, 추천우선순위 = 100,
                장점메모 = "R114 합성 검증: 기존 IdentityDataSeeder 1톤 카고 수치 재사용. 기사 실차 측정·승인 아님.",
                단점메모 = "시험 fixture. 현실 적재·장비·보험·운영 운임 근거로 사용하지 않음."
            };
            db.차량제원.Add(specification);
            await db.SaveChangesAsync();
        }
        if (!PrimaryVehicleMatches(specification)) throw new InvalidOperationException("R114SyntheticVehicleConflict");
        var source = "Ssalddel.Infrastructure/Persistence/IdentityDataSeeder.cs";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "r114-synthetic-vehicle-fixture.v1", atUtc = DateTime.UtcNow, database = CargoJourneySettings.DatabaseName,
            created, vehicleCode = specification.차량코드, synthetic = true, actualVehicleMeasured = false,
            source, sourceSha256 = CargoAiSelector.Hash(File.ReadAllBytes(Path.Combine(settings.RepositoryRoot, source))),
            specification
        }, new JsonSerializerOptions { WriteIndented = true });
        var directory = Path.Combine(settings.ArtifactRoot, ".private", "vehicle-fixture");
        Directory.CreateDirectory(directory);
        await using var file = new FileStream(Path.Combine(directory, CargoAiSelector.Hash(bytes) + ".json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await file.WriteAsync(bytes); file.Flush(true);
    }

    internal static bool PrimaryVehicleMatches(차량제원 item)
        => item.차량코드 == "1톤 카고" && item.차량명 == "1톤 카고" && item.제조사 == "합성 검증"
            && item.모델명 == "R114 휴대폰 검증 제원" && item.차급 == "1톤" && item.차체형태 == "카고"
            && item.적재함길이Mm == 3110 && item.적재함폭Mm == 1630 && item.적재함높이Mm == 355
            && item.최대적재중량Kg == 1000 && item.운영권장중량Kg == 850 && item.차량전체높이Mm == 1990
            && item.바닥높이Mm == 840 && !item.비눈보호가능 && !item.냉장가능 && !item.냉동가능
            && !item.측면상하차가능 && !item.리프트가능 && item.장재물유리 && item.팔레트적재개수 == 1
            && item.기준연비KmPerLiter == 8m && item.추천사용여부 && item.권장최대CBM is null
            && item.장점메모.Contains("R114 합성 검증", StringComparison.Ordinal);
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("R114IdentitySeedFailed:" + string.Join(',', result.Errors.Select(x => x.Code)));
    }
}

internal sealed record R114TestAccount(string UserId, string UserName, IReadOnlyList<string> Roles, string Password);
