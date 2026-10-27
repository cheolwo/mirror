using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ssalddel.Application.Driver.DispatchAction;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.도메인.공통;

internal sealed class RuntimeProofControl(CargoJourneySettings settings) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async(context,nextMiddleware) =>
        {
            if(!context.Request.Path.StartsWithSegments("/verification/r108")){await nextMiddleware(context);return;}
            var supplied=context.Request.Headers["x-r108-key"].ToString();
            if(context.Connection.RemoteIpAddress is not {} remote || !IPAddress.IsLoopback(remote)
                || context.Request.Host.Port!=CargoJourneySettings.HostPort || context.Request.ContentLength is >1024
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied),Encoding.UTF8.GetBytes(settings.AccessKey)))
            {context.Response.StatusCode=401;return;}
            using var scope=context.RequestServices.CreateScope();var sp=scope.ServiceProvider;var ct=context.RequestAborted;
            settings.ValidateEffectiveConfiguration(sp.GetRequiredService<IConfiguration>(),sp.GetRequiredService<IHostEnvironment>());
            var db=sp.GetRequiredService<SsalddelContext>();settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
            if(context.Request.Path=="/verification/r108/proof" && HttpMethods.IsGet(context.Request.Method))
            {
                var rows=await db.운송이벤트.AsNoTracking().Where(x=>x.이벤트타입==CargoAcceptanceRecoveryMarker.EventType)
                    .OrderBy(x=>x.Id).Select(x=>new{x.Id,x.의뢰Id,x.메타데이터}).ToArrayAsync(ct);
                var jobs=rows.Select(x=>
                {
                    using var json=JsonDocument.Parse(x.메타데이터);
                    var job=json.RootElement.GetProperty("cargoAcceptanceFollowups");
                    return new{eventId=x.Id,requestId=x.의뢰Id,state=job.GetProperty("state").GetString(),attempts=job.GetProperty("attempts").GetInt32(),
                        notificationPrepared=job.GetProperty("notificationPrepared").GetBoolean(),promisePrepared=job.GetProperty("promisePrepared").GetBoolean(),
                        nextAttemptAtUtc=job.GetProperty("nextAttemptAtUtc").ValueKind==JsonValueKind.Null?(DateTime?)null:job.GetProperty("nextAttemptAtUtc").GetDateTime()};
                }).ToArray();
                await context.Response.WriteAsJsonAsync(new{settings.RunStableId,database=CargoJourneySettings.DatabaseName,
                    financial=new{payments=await db.결제.CountAsync(ct),payoutRequests=await db.기사운송대금지급요청.CountAsync(ct),payoutOutbox=await db.기사지급Outbox.CountAsync(ct)},
                    recommendationOutbox=await db.배차추천알림Outbox.AsNoTracking().OrderBy(x=>x.Id).Select(x=>new{x.Id,requestId=x.의뢰Id,state=x.발송상태,attempts=x.시도횟수}).ToArrayAsync(ct),
                    acceptanceJobs=jobs,acceptanceNotifications=await db.Command알림Outbox.CountAsync(x=>x.FeatureName=="DispatchAccepted",ct),
                    acceptanceNotificationFaultsInjected=sp.GetRequiredService<AcceptanceNotificationFault>().Injected,
                    simulatedFcmOnly=true,googleFcmSent=false,actualRouteVerified=false,memberProviderGuardEmulated=true},ct);return;
            }
            if(context.Request.Path=="/verification/r108/acceptance/fault-arm" && HttpMethods.IsPost(context.Request.Method))
            {
                sp.GetRequiredService<AcceptanceNotificationFault>().Arm();
                await context.Response.WriteAsJsonAsync(new{armed=true,fault="ExplicitOneAcceptanceNotificationInsertFailure",existingSqlChanged=false},ct);return;
            }
            if(context.Request.Path=="/verification/r108/notifications/send" && HttpMethods.IsPost(context.Request.Method))
            {
                var sender=sp.GetRequiredService<I배차추천알림Service>();
                var food=await sender.업무유형별대기알림발송Async(상태값.배차업무유형.음식배달,100,ct);
                var cargo=await sender.업무유형별대기알림발송Async(상태값.배차업무유형.용달운송,100,ct);
                await context.Response.WriteAsJsonAsync(new{settings.RunStableId,food,cargo,transport="ExplicitLocalFake",googleFcmSent=false,deviceReceived=false},ct);return;
            }
            if(context.Request.Path=="/verification/r108/acceptance/recover" && HttpMethods.IsPost(context.Request.Method))
            {
                var input=await context.Request.ReadFromJsonAsync<RecoverInput>(ct);
                var queue=input is null?null:await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x=>x.의뢰Id==input.RequestId,ct);
                if(queue is null || queue.일반회원배송전용 || string.IsNullOrWhiteSpace(queue.확정기사Id)){context.Response.StatusCode=409;return;}
                var service=sp.GetRequiredService<ICargoAcceptanceRecoveryService>();
                var before=await db.Command알림Outbox.CountAsync(x=>x.FeatureName=="DispatchAccepted",ct);
                var first=await service.ProcessRequestAsync(queue.의뢰Id,queue.확정기사Id,ct);
                var afterFirst=await db.Command알림Outbox.CountAsync(x=>x.FeatureName=="DispatchAccepted",ct);
                var again=await service.ProcessRequestAsync(queue.의뢰Id,queue.확정기사Id,ct);
                var afterAgain=await db.Command알림Outbox.CountAsync(x=>x.FeatureName=="DispatchAccepted",ct);
                await context.Response.WriteAsJsonAsync(new{requestId=queue.의뢰Id,first,again,before,afterFirst,afterAgain,
                    duplicateNotificationsAdded=afterAgain!=afterFirst,explicitRecoveryCall=true,externalNotificationSent=false},ct);return;
            }
            context.Response.StatusCode=404;
        });
        next(app);
    };
    private sealed record RecoverInput(string RequestId);
}
