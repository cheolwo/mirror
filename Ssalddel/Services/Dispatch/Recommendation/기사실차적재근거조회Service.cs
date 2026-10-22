using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Admin.Management;
using 살뜰.Data;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;

namespace 살뜰.Services.Dispatch.Recommendation;

public sealed record 기사실차적재조회결과
{
    public 차량제원? 유효제원 { get; init; }
    public bool 실차확인됨 { get; init; }
    public long? 판본 { get; init; }
    public string? 근거지문 { get; init; }
    public string 사유Code { get; init; } = "ActualVehicle.Unknown";
    public IReadOnlyList<string> 경고 { get; init; } = [];
}

public interface I기사실차적재근거조회Service
{
    Task<기사실차적재조회결과> 조회Async(string 기사Id, CancellationToken cancellationToken = default);
}

public sealed class 기사실차적재근거조회Service(SsalddelContext db, TimeProvider? clock = null) : I기사실차적재근거조회Service
{
    public async Task<기사실차적재조회결과> 조회Async(string 기사Id, CancellationToken cancellationToken = default)
    {
        var driver = await db.용달기사.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == 기사Id, cancellationToken);
        List<차량제원> specs = driver is null ? [] : await db.차량제원.AsNoTracking()
            .Where(x => x.차량코드 == driver.차량 || x.차량명 == driver.차량).ToListAsync(cancellationToken);
        // 모호한 별칭을 임의의 첫 행으로 확정하지 않는다.
        var exact = specs.Where(x => x.차량코드 == driver?.차량).ToArray();
        var spec = exact.Length == 1 ? exact[0] : specs.Count == 1 ? specs[0] : null;
        return 해석(driver, spec, (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
    }

    public static 기사실차적재조회결과 해석(용달기사? driver, 차량제원? spec, DateTime now)
    {
        if (driver is null) return new() { 사유Code = "ActualVehicle.DriverMissing" };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { driver.기사Id, driver.차량, driver.현재실차참조, driver.실차적재판본, driver.실차적재제원Json, Class = spec })))).ToLowerInvariant();
        기사실차적재조회결과 Missing(string code, string message) => new()
        { 판본 = driver.실차적재판본, 근거지문 = fingerprint, 사유Code = code, 경고 = [message] };
        if (string.IsNullOrWhiteSpace(driver.현재실차참조) && string.IsNullOrWhiteSpace(driver.실차적재제원Json))
            return new() { 유효제원 = spec, 판본 = driver.실차적재판본, 근거지문 = fingerprint,
                사유Code = "ActualVehicle.ClassOnly", 경고 = ["차종 기준만 확인했습니다. 기사별 실차·장비 확인 자료는 없습니다."] };
        기사실차적재제원? actual;
        try { actual = JsonSerializer.Deserialize<기사실차적재제원>(driver.실차적재제원Json ?? "null"); }
        catch (JsonException) { return Missing("ActualVehicle.Unreadable", "실차 적재 확인 자료를 읽을 수 없습니다."); }
        if (actual is null) return Missing("ActualVehicle.Unconfirmed", "선택 실차의 적재 확인 자료가 없습니다.");
        if (string.IsNullOrWhiteSpace(driver.현재실차참조) || actual.ActualVehicleReference != driver.현재실차참조
            || actual.VehicleProfileValue != driver.차량)
            return Missing("ActualVehicle.Changed", "현재 실차·차종과 적재 확인 자료가 다릅니다.");
        if (actual.Withdrawn) return Missing("ActualVehicle.Withdrawn", "실차 적재 확인이 철회됐습니다.");
        if (new[] { actual.적재함길이Mm, actual.적재함폭Mm, actual.적재함높이Mm, actual.최대적재중량Kg }.Any(x => x is <= 0)
            || actual.최대부피Cbm is <= 0 || actual.팔레트적재개수 is < 0
            || actual.EntranceKind is not ("Unknown" or "Open" or "Enclosed")
            || actual.EquipmentReferences is null
            || (actual.냉장가능 == true || actual.냉동가능 == true || actual.리프트가능 == true) && actual.EquipmentReferences.Count == 0)
            return Missing("ActualVehicle.InvalidEvidence", "실차 적재 값·장비 확인 근거가 유효하지 않습니다.");
        if (actual.ConfirmedAtUtc.Kind != DateTimeKind.Utc || actual.ValidUntilUtc.Kind != DateTimeKind.Utc
            || actual.ConfirmedAtUtc > now || actual.ValidUntilUtc <= now || actual.ValidUntilUtc <= actual.ConfirmedAtUtc
            || string.IsNullOrWhiteSpace(actual.PrivateEvidenceReference) || string.IsNullOrWhiteSpace(actual.ReviewerId))
            return Missing("ActualVehicle.ExpiredOrUnconfirmed", "유효한 실차 적재 확인 시각·근거가 필요합니다.");
        if (spec is null || !spec.추천사용여부) return Missing("ActualVehicle.ClassMissing", "사용 가능한 공통 차종 기준이 없습니다.");
        int Axis(int basis, int? measured) => basis > 0 && measured is > 0 ? Math.Min(basis, measured.Value) : 0;
        int? NullableAxis(int? basis, int? measured) => basis is > 0 && measured is > 0 ? Math.Min(basis.Value, measured.Value) : null;
        decimal? cbm = actual.최대부피Cbm is > 0 ? actual.최대부피Cbm : spec.권장최대CBM;
        if (spec.권장최대CBM is > 0 && cbm is > 0) cbm = Math.Min(spec.권장최대CBM.Value, cbm.Value);
        var resolved = new 차량제원
        {
            차량코드 = spec.차량코드, 차량명 = spec.차량명, 제조사 = spec.제조사, 모델명 = spec.모델명,
            차급 = spec.차급, 차체형태 = actual.EntranceKind == "Open" ? "개방형" : actual.EntranceKind == "Enclosed" ? "밀폐형" : "미확인",
            적재함길이Mm = Axis(spec.적재함길이Mm, actual.적재함길이Mm), 적재함폭Mm = Axis(spec.적재함폭Mm, actual.적재함폭Mm),
            적재함높이Mm = NullableAxis(spec.적재함높이Mm, actual.적재함높이Mm),
            최대적재중량Kg = Axis(spec.최대적재중량Kg, actual.최대적재중량Kg),
            운영권장중량Kg = NullableAxis(spec.운영권장중량Kg ?? spec.최대적재중량Kg, actual.최대적재중량Kg),
            권장최대CBM = cbm, 팔레트적재개수 = spec.팔레트적재개수.HasValue && actual.팔레트적재개수.HasValue
                ? Math.Min(spec.팔레트적재개수.Value, actual.팔레트적재개수.Value) : null,
            비눈보호가능 = spec.비눈보호가능 && actual.비눈보호가능 == true,
            냉장가능 = spec.냉장가능 && actual.냉장가능 == true, 냉동가능 = spec.냉동가능 && actual.냉동가능 == true,
            측면상하차가능 = spec.측면상하차가능 && actual.측면상하차가능 == true,
            장재물유리 = spec.장재물유리 && actual.장재물유리 == true,
            추천사용여부 = spec.추천사용여부, 추천우선순위 = spec.추천우선순위,
            CreatedAt = spec.CreatedAt, UpdatedAt = spec.UpdatedAt
        };
        resolved.리프트가능 = spec.리프트가능 && actual.리프트가능 == true;
        return new() { 유효제원 = resolved, 실차확인됨 = true, 판본 = driver.실차적재판본,
            근거지문 = fingerprint, 사유Code = "ActualVehicle.Confirmed",
            경고 = actual.EntranceKind == "Unknown" ? ["실차 적재 입구 형태는 미확인입니다."] : [] };
    }
}
