using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Domain.Notifications;
using 살뜰.Data;

namespace Ssalddel.Services.OperationsAlerts;
public sealed class OperationsBudgetOptions
{
    public bool Enabled { get; set; }
    public int NewPhotoUploadsPerDay { get; set; } = 1000;
    public int ExternalRouteRequestsPerDay { get; set; } = 1000;
}
public sealed class OperationsBudgetGate(SsalddelContext db, IOptionsMonitor<OperationsBudgetOptions> options, TimeProvider clock)
{
    public async Task<bool> ConsumeAsync(string category, bool essential, CancellationToken ct)
    {
        if (!options.CurrentValue.Enabled) return true;
        var now=clock.GetUtcNow().UtcDateTime; var day=now.AddHours(9).Date.AddHours(-9);
        var limit=category=="routes"?options.CurrentValue.ExternalRouteRequestsPerDay:options.CurrentValue.NewPhotoUploadsPerDay;
        var id=OperationsAlertCoordinator.Id(category,day.ToString("O"));
        if (!await db.Set<OperationsBudgetUsage>().AnyAsync(x=>x.Id==id,ct))
        {
            var row=new OperationsBudgetUsage { Id=id,Category=category,DayUtc=day,UpdatedAtUtc=now };
            db.Add(row);
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.Entry(row).State=EntityState.Detached; }
        }
        return await db.Set<OperationsBudgetUsage>().Where(x=>x.Id==id && (essential || x.Count < Math.Max(0,limit)))
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Count,x=>x.Count+1).SetProperty(x=>x.UpdatedAtUtc,now),ct)==1;
    }
}
/// <summary>신규 경로 견적만 제한. 이미 수행 중인 업무·배차 worker의 경로는 중단하지 않는다.</summary>
public sealed class OperationsRouteBudgetHandler(IServiceScopeFactory scopes, IHttpContextAccessor context) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        using var scope=scopes.CreateScope();
        // HTTP client 등록만 사용하는 도구/시험에도 기존 동작을 보존한다. 예산 OFF에서는 DB가 필요 없다.
        if (scope.ServiceProvider.GetService<IOptionsMonitor<OperationsBudgetOptions>>()?.CurrentValue.Enabled != true)
            return await base.SendAsync(request,ct);
        var path=context.HttpContext?.Request.Path.Value ?? "";
        var quote=path.Contains("estimate",StringComparison.OrdinalIgnoreCase)||path.Contains("quote",StringComparison.OrdinalIgnoreCase);
        var essential=!quote || path.Contains("/work/",StringComparison.OrdinalIgnoreCase)||path.Contains("/dispatch/",StringComparison.OrdinalIgnoreCase);
        if (!await scope.ServiceProvider.GetRequiredService<OperationsBudgetGate>().ConsumeAsync("routes",essential,ct))
            throw new HttpRequestException("OperationsRouteQuotaReached");
        return await base.SendAsync(request,ct);
    }
}
public sealed class OperationsBudgetAlertSource(SsalddelContext db,IOptionsMonitor<OperationsBudgetOptions> options,TimeProvider clock) : IOperationsAlertSource
{
    public string Key=>"budget";
    public bool RepeatActiveSweep=>true;
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime at,string id,int take,CancellationToken ct)
    {
        if(!options.CurrentValue.Enabled)return [];
        var day=clock.GetUtcNow().UtcDateTime.AddHours(9).Date.AddHours(-9);
        var rows=await db.Set<OperationsBudgetUsage>().AsNoTracking().Where(x=>x.DayUtc>=day && (x.UpdatedAtUtc>at||(x.UpdatedAtUtc==at&&string.Compare(x.Id,id)>0)))
            .OrderBy(x=>x.UpdatedAtUtc).ThenBy(x=>x.Id).Take(take).ToArrayAsync(ct);
        return rows.Select(Observe).ToArray();
    }
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id,CancellationToken ct)
        => await db.Set<OperationsBudgetUsage>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct) is {} row ? [Observe(row)] : throw new InvalidOperationException("SourceUnavailable");
    private OperationsObservation Observe(OperationsBudgetUsage row)
    {
        var day=clock.GetUtcNow().UtcDateTime.AddHours(9).Date.AddHours(-9);
        var limit=row.Category=="routes"?options.CurrentValue.ExternalRouteRequestsPerDay:options.CurrentValue.NewPhotoUploadsPerDay;
        return new(Key,row.Id,"UsageQuotaReached","Warning",row.DayUtc==day&&row.Count>=Math.Max(0,limit),row.UpdatedAtUtc);
    }
}
