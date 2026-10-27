using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Development.MobileFieldTest;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;

/// <summary>r67 전용 합성 주문에만 제품 추천 전환을 요청합니다. 운영 API·자동 배차·기사 수락을 만들지 않습니다.</summary>
internal sealed class UsbFoodControlStartupFilter(CargoJourneySettings settings) : IStartupFilter
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (!context.Request.Path.StartsWithSegments("/verification/food")) { await nextMiddleware(context); return; }
            var supplied = context.Request.Headers["x-usb-r67-key"].ToString();
            if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
                || context.Request.Host.Port != CargoJourneySettings.HostPort
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(settings.AccessKey)))
            { context.Response.StatusCode = 401; return; }
            using var scope = context.RequestServices.CreateScope();
            var services = scope.ServiceProvider;
            settings.ValidateEffectiveConfiguration(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>());
            if (!services.GetRequiredService<ISsalddelExecutionModePolicy>().IsSimulation)
            { await Error(context, 409, "SimulationRequired"); return; }
            var db = services.GetRequiredService<SsalddelContext>();
            settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
            if (context.Request.Path == "/verification/food/driver-proof" && HttpMethods.IsGet(context.Request.Method))
            {
                var id = await db.Users.AsNoTracking().Where(x => x.UserName == "driver1@ssalddel.local")
                    .Select(x => x.Id).SingleOrDefaultAsync(context.RequestAborted);
                if (id is null) { await Error(context, 409, "SyntheticDriverNotPrepared"); return; }
                await context.Response.WriteAsJsonAsync(await DriverProofAsync(services, id, context.RequestAborted), context.RequestAborted);
                return;
            }
            string? orderNo;
            if (context.Request.Path == "/verification/food/offer" && HttpMethods.IsPost(context.Request.Method))
            {
                if (context.Request.ContentLength is > 1024) { await Error(context, 400, "InputTooLarge"); return; }
                var input = await context.Request.ReadFromJsonAsync<OfferInput>(context.RequestAborted);
                orderNo = input?.OrderNo;
            }
            else if (context.Request.Path == "/verification/food/proof" && HttpMethods.IsGet(context.Request.Method))
                orderNo = context.Request.Query["orderNo"].ToString();
            else { await Error(context, 404, "ControlRouteNotFound"); return; }
            if (string.IsNullOrWhiteSpace(orderNo) || orderNo.Length > 128 || orderNo != orderNo.Trim())
            { await Error(context, 400, "OrderNoRequired"); return; }

            var gate = Gates.GetOrAdd(orderNo, _ => new(1, 1));
            await gate.WaitAsync(context.RequestAborted);
            try
            {
                var orderer = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == "field-orderer", context.RequestAborted);
                var restaurant = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == "restaurant101", context.RequestAborted);
                var driver = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == "driver1@ssalddel.local", context.RequestAborted);
                var order = await db.음식주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문번호 == orderNo, context.RequestAborted);
                var profile = await db.음식점공개프로필.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 101, context.RequestAborted);
                if (orderer is null || restaurant is null || driver is null || order is null || profile is null
                    || order.음식점Id != 101 || order.주문자UserId != orderer.Id || order.모의처리처수행
                    || order.클라이언트요청Id is null || order.클라이언트요청Id == Guid.Empty
                    || order.수령지주소 != 모바일음식배차시험자료.DropoffAddress
                    || profile.상호명 != 모바일음식배차시험자료.RestaurantName
                    || profile.공개주소 != 모바일음식배차시험자료.PickupAddress
                    || profile.위도 != 37.580450m || profile.경도 != 127.087950m
                    || !profile.소개.Contains(모바일현장검증자료Seeder.FixtureRevision, StringComparison.Ordinal)
                    || !await db.UserClaims.AsNoTracking().AnyAsync(x => x.UserId == restaurant.Id
                        && x.ClaimType == 음식점접근ClaimTypes.음식점Id && x.ClaimValue == "101", context.RequestAborted)
                    || !await db.배달기사.AsNoTracking().AnyAsync(x => x.기사Id == driver.Id && x.차량 == "오토바이"
                        && x.메모.Contains("usb-r67"), context.RequestAborted))
                { await Error(context, 409, "SyntheticFoodBindingMismatch"); return; }
                var queue = order.배차대기Id is { } queueId
                    ? await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == queueId, context.RequestAborted)
                    : null;
                if (queue is not null && (queue.원본의뢰Id != orderNo || queue.배차업무유형 != 상태값.배차업무유형.음식배달
                    || queue.원본의뢰유형 != 운송의뢰배차원천유형.음식점주문 || queue.화주Id != "restaurant:101"
                    || queue.픽업_도로명주소 != profile.공개주소 || queue.하차_도로명주소 != 모바일음식배차시험자료.DropoffAddress
                    || queue.픽업_위도 != profile.위도 || queue.픽업_경도 != profile.경도))
                { await Error(context, 409, "SyntheticFoodQueueMismatch"); return; }
                if (HttpMethods.IsPost(context.Request.Method))
                {
                    if (queue is null || !order.음식점수락시각Utc.HasValue)
                    { await Error(context, 409, "RestaurantAcceptanceAndQueueRequired"); return; }
                    if (!string.IsNullOrWhiteSpace(queue.확정기사Id))
                    {
                        if (queue.확정기사Id != driver.Id) { await Error(context, 409, "OtherDriverAssignmentFound"); return; }
                        // 확정/완료 이후 재전송은 정본만 돌려주며 이전 제안을 다시 만들지 않습니다.
                    }
                    else if (!string.IsNullOrWhiteSpace(queue.현재추천대상기사Id))
                    {
                        if (queue.현재추천대상기사Id != driver.Id || queue.추천만료시각 is null || queue.추천만료시각 <= DateTime.UtcNow)
                        { await Error(context, 409, "OfferExpiredOrOtherDriverRequiresReview"); return; }
                    }
                    else
                    {
                        if (queue.추천라운드 != 0 || order.상태 is not (음식주문상태코드.주문확인 or 음식주문상태코드.조리중 or 음식주문상태코드.픽업대기))
                        { await Error(context, 409, "OrderOrPriorOfferRequiresReview"); return; }
                        var transition = services.GetRequiredService<I배차대기원장전환Service>();
                        var policy = services.GetServices<I배차업무정책>().OfType<음식배달배차업무정책>().Single();
                        var candidateReview = await policy.후보검토Async(queue, cancellationToken: context.RequestAborted);
                        await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, ".private", "food-candidate-review-" + queue.Id + ".json"),
                            System.Text.Json.JsonSerializer.Serialize(new { orderNo, atUtc = DateTime.UtcNow,
                                source = "ProductCandidatePolicyBeforeControlledInvocation", synthetic = true, candidateReview }), context.RequestAborted);
                        var result = queue.배차큐단계 == 상태값.배차큐단계.계획배차
                            ? await transition.계획배차에서추천으로전환Async(queue.의뢰Id, context.RequestAborted)
                            : await transition.추천대기처리Async(queue.의뢰Id, context.RequestAborted);
                        if (!result.전환여부 && result.결과코드 != 배차대기원장전환결과코드.이미추천중)
                        { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { errorCode = result.결과코드, synthetic = true, candidateReview }, context.RequestAborted); return; }
                    }
                }
                db.ChangeTracker.Clear();
                order = await db.음식주문.AsNoTracking().SingleAsync(x => x.주문번호 == orderNo, context.RequestAborted);
                queue = order.배차대기Id is { } currentQueueId
                    ? await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentQueueId, context.RequestAborted) : null;
                await context.Response.WriteAsJsonAsync(new
                {
                    schemaVersion = "usb-r67.food-control.r1", runStableId = settings.RunStableId,
                    synthetic = true, executionMode = "Simulation", productionDispatcherProof = false,
                    automaticDispatchVerified = false, actualGpsVerified = false, realFcmVerified = false,
                    orderNo, orderStatus = order.상태, dispatchStatus = order.배차상태, acceptedAtUtc = order.음식점수락시각Utc,
                    totalOrderKrw = order.총주문금액, orderUpdatedAtUtc = order.UpdatedAt,
                    driver = await DriverProofAsync(services, driver.Id, context.RequestAborted),
                    offerId = queue?.의뢰Id, queueStage = queue?.배차큐단계, exposureStatus = queue?.배차노출상태,
                    recommendationRound = queue?.추천라운드, expiresAtUtc = queue?.추천만료시각,
                    recommendedSyntheticDriver = queue?.현재추천대상기사Id == driver.Id,
                    confirmedSyntheticDriver = queue?.확정기사Id == driver.Id,
                    frozenDriverPayout = queue?.기사지급예정액, pricingPolicyRevision = queue?.기사제안요금정책판본,
                    pricingEvidencePresent = !string.IsNullOrWhiteSpace(queue?.기사제안요금계산근거Json),
                    chosenDriverId = queue?.현재추천대상기사Id ?? queue?.확정기사Id,
                    candidateEngine = "ProductQueueTransitionAndBusinessPolicy", actualRouteVerified = false,
                    note = "Controlled invocation of product candidate engine with synthetic adapters. Driver acceptance, cooking, pickup, delivery and receipt are separate actions. Device GPS may fail proximity checks against synthetic coordinates."
                }, context.RequestAborted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var detail = exception.ToString();
                foreach (var name in CargoJourneySettings.SecretEnvironmentNames)
                    if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } secret)
                        detail = detail.Replace(secret, "[omitted]", StringComparison.Ordinal);
                await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, ".private", "food-control-failure.txt"), detail);
                // 개발 오류 응답의 원문·연결 문자열·비밀을 제어 API에 내보내지 않습니다.
                await Error(context, 500, "ControlledFoodPreparationFailed");
            }
            finally { gate.Release(); }
        });
        next(app);
    };
    private static Task Error(HttpContext context, int status, string code)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { errorCode = code, synthetic = true, productionDispatcherProof = false });
    }
    private static async Task<object> DriverProofAsync(IServiceProvider services, string id, CancellationToken ct)
    {
        var state = await services.GetRequiredService<I국내화물운송기사상태Store>().GetAsync(id, ct);
        var receiving = await services.GetRequiredService<I운영배차공통UseCase>().수신상태조회Async(id, ct);
        return new { synthetic = true, driverId = id, shiftId = state?.ShiftId, drivingState = state?.운행상태,
            appKey = state?.AppKey, locationPresent = state?.Latitude.HasValue == true && state?.Longitude.HasValue == true,
            locationReceivedAtUtc = state?.위치수신시각Utc,
            locationFreshWithinTwoMinutes = state?.위치수신시각Utc is { } at && DateTime.UtcNow - at < TimeSpan.FromMinutes(2),
            receptionIntent = receiving.수신의사Code, effectiveState = receiving.실효상태Code,
            effectiveReason = receiving.실효사유Code, automaticallyPaused = receiving.미응답자동중지여부,
            consecutiveNoResponse = receiving.연속미응답횟수, exactGpsCoordinatesIncluded = false };
    }
    private sealed record OfferInput(string OrderNo);
}
