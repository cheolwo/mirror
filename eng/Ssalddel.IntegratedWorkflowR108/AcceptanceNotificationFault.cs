using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>명시적 ARM 이후 현재 r108 DB의 수락 알림 INSERT 한 번만 실패시킨다. SQL/제품 데이터는 바꾸지 않는다.</summary>
internal sealed class AcceptanceNotificationFault : DbCommandInterceptor
{
    private int armed;
    private int injected;
    public int Injected => Volatile.Read(ref injected);
    public void Arm()
    {
        if(Interlocked.CompareExchange(ref armed,1,0)!=0 || Injected!=0)
            throw new InvalidOperationException("FaultAlreadyArmedOrInjected");
    }
    private void Inspect(DbCommand command)
    {
        if(Volatile.Read(ref armed)!=1 || command.Connection?.Database!=CargoJourneySettings.DatabaseName
            || !command.CommandText.Contains("Command_알림_Outbox",StringComparison.Ordinal)
            || !command.CommandText.Contains("INSERT",StringComparison.OrdinalIgnoreCase)
            || !command.Parameters.Cast<DbParameter>().Any(x=>Equals(x.Value,"DispatchAccepted")))return;
        if(Interlocked.CompareExchange(ref armed,2,1)==1)
        {
            Interlocked.Increment(ref injected);
            throw new InvalidOperationException("R108ExplicitAcceptanceNotificationWriteFailure");
        }
    }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
    {Inspect(command);return ValueTask.FromResult(result);}
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
    {Inspect(command);return ValueTask.FromResult(result);}
}
