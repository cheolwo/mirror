using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Admin.Management;
using 살뜰.Data;

namespace Ssalddel.Application.Admin.Management;

public sealed record 기사실차적재제원조회Query(string 기사Id) : IRequest<기사실차적재제원응답?>;
public sealed record 기사실차적재제원수정Command(string 기사Id, string 검토자Id, 기사실차적재제원수정요청 요청) : IRequest<기사실차적재제원응답?>;

/// <summary>관리자의 물리 적재 확인 기록. 공식기관 자격이나 운송 수행 상태는 변경하지 않는다.</summary>
public sealed class 기사실차적재제원UseCase(SsalddelContext db, TimeProvider? clock = null) :
    IRequestHandler<기사실차적재제원조회Query, 기사실차적재제원응답?>,
    IRequestHandler<기사실차적재제원수정Command, 기사실차적재제원응답?>
{
    public async Task<기사실차적재제원응답?> Handle(기사실차적재제원조회Query request, CancellationToken ct)
    {
        var driver = await db.용달기사.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == request.기사Id, ct);
        return driver is null ? null : new(driver.기사Id, driver.실차적재판본,
            string.IsNullOrWhiteSpace(driver.실차적재제원Json) ? null : JsonSerializer.Deserialize<기사실차적재제원>(driver.실차적재제원Json))
        { 현재실차참조 = driver.현재실차참조, 현재차량종류 = driver.차량 };
    }

    public async Task<기사실차적재제원응답?> Handle(기사실차적재제원수정Command command, CancellationToken ct)
    {
        var request = command.요청;
        var value = request?.제원;
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        static bool Ref(string? s) => s is not null && Regex.IsMatch(s, "^[A-Za-z][A-Za-z0-9._:-]{2,159}$");
        if (request is null || value is null || string.IsNullOrWhiteSpace(command.검토자Id)
            || string.IsNullOrWhiteSpace(value.VehicleProfileValue) || value.VehicleProfileValue.Length > 150
            || !Ref(value.ActualVehicleReference) || !Ref(value.PrivateEvidenceReference)
            || value.EquipmentReferences is null || value.EquipmentReferences.Any(x => !Ref(x))
            || value.EntranceKind is not ("Unknown" or "Open" or "Enclosed")
            || value.ConfirmedAtUtc.Kind != DateTimeKind.Utc || value.ValidUntilUtc.Kind != DateTimeKind.Utc
            || value.ConfirmedAtUtc > now || value.ValidUntilUtc <= now || value.ValidUntilUtc <= value.ConfirmedAtUtc
            || new[] { value.적재함길이Mm, value.적재함폭Mm, value.적재함높이Mm, value.최대적재중량Kg }.Any(x => x is <= 0)
            || value.최대부피Cbm is <= 0 || value.팔레트적재개수 is < 0
            || (value.냉장가능 == true || value.냉동가능 == true || value.리프트가능 == true) && value.EquipmentReferences.Count == 0)
            throw new ArgumentException("실차·장비의 비공개 근거 참조, 확인 기간, 양수 실측 한도를 확인해 주세요.");
        var driver = await db.용달기사.SingleOrDefaultAsync(x => x.기사Id == command.기사Id, ct);
        if (driver is null) return null;
        if (driver.실차적재판본 != request.ExpectedRevision)
            throw new DbUpdateConcurrencyException("실차 적재 확인이 변경됐습니다. 다시 조회해 주세요.");
        if (driver.현재실차참조 != value.ActualVehicleReference || driver.차량 != value.VehicleProfileValue)
            throw new ArgumentException("기사 본인이 선택한 현재 실차·차종과 확인 자료가 일치해야 합니다.");
        var saved = value with { ReviewerId = command.검토자Id };
        driver.실차적재제원Json = JsonSerializer.Serialize(saved);
        driver.실차적재판본 = checked((driver.실차적재판본 ?? 0) + 1);
        await db.SaveChangesAsync(ct);
        return new(driver.기사Id, driver.실차적재판본, saved)
        { 현재실차참조 = driver.현재실차참조, 현재차량종류 = driver.차량 };
    }
}
