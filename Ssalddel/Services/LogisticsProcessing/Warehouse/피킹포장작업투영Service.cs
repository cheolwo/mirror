using Ssalddel.Contracts.Common.Warehouse;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

public interface I피킹포장작업투영Service
{
    Task 투영Async(
        string 출고참조번호,
        피킹배치계획결과 계획,
        string? 커뮤니티원장Id = null,
        CancellationToken cancellationToken = default);
}

public sealed class 피킹포장작업투영Service : I피킹포장작업투영Service
{
    private readonly SsalddelContext _db;

    public 피킹포장작업투영Service(SsalddelContext db)
    {
        _db = db;
    }

    public async Task 투영Async(
        string 출고참조번호,
        피킹배치계획결과 계획,
        string? 커뮤니티원장Id = null,
        CancellationToken cancellationToken = default)
    {
        var 작업Keys = 계획.피킹작업목록.Select(x => x.TaskKey)
            .Concat(계획.포장작업목록.Select(x => x.TaskKey))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (작업Keys.Count == 0)
        {
            return;
        }

        var 기존작업 = await _db.피킹포장작업
            .Where(x => 작업Keys.Contains(x.작업Key))
            .ToDictionaryAsync(x => x.작업Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var now = DateTime.UtcNow;
        var 피킹By포장Key = 계획.피킹작업목록
            .Where(x => !string.IsNullOrWhiteSpace(x.포장작업Key))
            .GroupBy(x => x.포장작업Key!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var 피킹작업 in 계획.피킹작업목록.Where(x => !string.IsNullOrWhiteSpace(x.TaskKey)))
        {
            var entity = GetOrCreate(기존작업, 피킹작업.TaskKey, now);
            ApplyPicking(entity, 출고참조번호, 피킹작업, 커뮤니티원장Id, now);
        }

        foreach (var 포장작업 in 계획.포장작업목록.Where(x => !string.IsNullOrWhiteSpace(x.TaskKey)))
        {
            var entity = GetOrCreate(기존작업, 포장작업.TaskKey, now);
            피킹By포장Key.TryGetValue(포장작업.TaskKey, out var 이전피킹작업);
            ApplyPacking(entity, 출고참조번호, 포장작업, 이전피킹작업, 커뮤니티원장Id, now);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private 피킹포장작업 GetOrCreate(
        IDictionary<string, 피킹포장작업> 기존작업,
        string 작업Key,
        DateTime now)
    {
        if (기존작업.TryGetValue(작업Key, out var entity))
        {
            return entity;
        }

        entity = new 피킹포장작업
        {
            작업Key = 작업Key,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.피킹포장작업.Add(entity);
        기존작업[작업Key] = entity;
        return entity;
    }

    private static void ApplyPicking(
        피킹포장작업 entity,
        string 출고참조번호,
        피킹작업배정 작업,
        string? 커뮤니티원장Id,
        DateTime now)
    {
        entity.작업유형 = 피킹포장작업유형.피킹;
        entity.처리방식 = 작업.처리방식.ToString();
        entity.상태 = ResolveState(entity);
        entity.출고예정Id = 작업.출고예정Id;
        entity.입고상품Id = 작업.InboundProductId;
        entity.창고Id = 작업.WarehouseId;
        entity.창고명 = 작업.WarehouseName;
        entity.작업자UserId = 작업.WorkerUserId;
        entity.작업자표시명 = 작업.WorkerName;
        entity.상대작업자UserId = null;
        entity.이전작업Key = null;
        entity.다음작업Key = 작업.포장작업Key;
        entity.주문참조번호 = 출고참조번호;
        entity.라인Key = 작업.LineKey;
        entity.상품명 = 작업.ProductName;
        entity.SKU = 작업.Sku;
        entity.수량 = 작업.Quantity;
        entity.적재대코드 = 작업.적재대코드;
        entity.보관위치코드 = 작업.보관위치코드;
        entity.묶음바코드 = 작업.WorkerBundleBarcode;
        entity.할당사유 = 작업.AssignmentReason;
        entity.커뮤니티원장Id = CleanNullable(커뮤니티원장Id);
        entity.커뮤니티원장블록Id = CleanNullable(작업.LineKey);
        entity.UpdatedAt = now;
    }

    private static void ApplyPacking(
        피킹포장작업 entity,
        string 출고참조번호,
        포장작업배정 작업,
        피킹작업배정? 이전피킹작업,
        string? 커뮤니티원장Id,
        DateTime now)
    {
        entity.작업유형 = 피킹포장작업유형.포장;
        entity.처리방식 = 피킹포장처리방식.피킹포장분리.ToString();
        entity.상태 = ResolveState(entity);
        entity.출고예정Id = 작업.출고예정Id;
        entity.입고상품Id = 작업.InboundProductId;
        entity.창고Id = 작업.WarehouseId;
        entity.창고명 = 작업.WarehouseName;
        entity.작업자UserId = 작업.PackerUserId;
        entity.작업자표시명 = 작업.PackerName;
        entity.상대작업자UserId = 작업.PickerUserId;
        entity.이전작업Key = 이전피킹작업?.TaskKey;
        entity.다음작업Key = null;
        entity.주문참조번호 = 출고참조번호;
        entity.라인Key = 작업.LineKey;
        entity.상품명 = 작업.ProductName;
        entity.SKU = 작업.Sku;
        entity.수량 = 작업.Quantity;
        entity.적재대코드 = null;
        entity.보관위치코드 = null;
        entity.묶음바코드 = 작업.PackerBundleBarcode;
        entity.할당사유 = 작업.AssignmentReason;
        entity.커뮤니티원장Id = CleanNullable(커뮤니티원장Id);
        entity.커뮤니티원장블록Id = CleanNullable(작업.LineKey);
        entity.UpdatedAt = now;
    }

    private static string ResolveState(피킹포장작업 entity)
        => string.IsNullOrWhiteSpace(entity.상태) ? 피킹포장작업상태.대기 : entity.상태;

    private static string? CleanNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
