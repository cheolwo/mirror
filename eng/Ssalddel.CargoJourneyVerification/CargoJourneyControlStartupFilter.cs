using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

internal sealed class CargoJourneyControlStartupFilter(CargoJourneySettings settings) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (!context.Request.Path.StartsWithSegments("/verification/cargo"))
                {
                    await nextMiddleware(context);
                    return;
                }
                var supplied = context.Request.Headers["x-cargo-journey-key"].ToString();
                if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
                    || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(settings.AccessKey)))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                using var scope = context.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
                settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
                if (context.Request.Path == "/verification/cargo/database-proof" && HttpMethods.IsGet(context.Request.Method))
                {
                    await WriteProofAsync(context, db, scope.ServiceProvider);
                    return;
                }
                if (context.Request.Path == "/verification/cargo/offer" && HttpMethods.IsPost(context.Request.Method))
                {
                    var input = await context.Request.ReadFromJsonAsync<CargoOfferInput>(context.RequestAborted);
                    var plan = await db.출고예정.AsNoTracking().SingleAsync(item => item.Id == CargoJourneySeed.OutboundPlanId, context.RequestAborted);
                    if (input?.RequestId != CargoJourneySeed.ExpectedRequestId || plan.운송의뢰Id != input.RequestId
                        || plan.입고상품Id != CargoJourneySeed.InboundItemId || plan.수량 != CargoJourneySeed.Quantity)
                    {
                        context.Response.StatusCode = StatusCodes.Status409Conflict;
                        await context.Response.WriteAsJsonAsync(new { errorCode = "CargoJourneyRequestBindingMismatch" });
                        return;
                    }
                    var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(item => item.의뢰Id == input.RequestId, context.RequestAborted);
                    var allocation = await db.운송의뢰상품연결.AsNoTracking().SingleOrDefaultAsync(item => item.운송의뢰Id == input.RequestId && item.입고상품Id == CargoJourneySeed.InboundItemId, context.RequestAborted);
                    if (request is null || request.화주Id != CargoJourneySeed.ShipperId
                        || allocation?.할당수량 != CargoJourneySeed.Quantity
                        || (request.정산상태 != 운임정산상태.후불승인완료.ToString() && request.결제상태 != 상태값.결제상태.결제완료))
                    {
                        context.Response.StatusCode = StatusCodes.Status409Conflict;
                        await context.Response.WriteAsJsonAsync(new { errorCode = "CargoJourneyPaymentApprovalRequired" });
                        return;
                    }
                    var transition = scope.ServiceProvider.GetRequiredService<I배차대기원장전환Service>();
                    var result = await transition.추천시작Async(input.RequestId, CargoJourneySeed.DriverId, 600, context.RequestAborted);
                    context.Response.StatusCode = result.전환여부 || result.결과코드 == 배차대기원장전환결과코드.이미추천중 ? 200 : 409;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        scheduler = "ControlledSyntheticDriverSelection",
                        productionCandidateAlgorithmProof = false,
                        requestId = result.의뢰Id, driverId = result.기사Id,
                        transitioned = result.전환여부, resultCode = result.결과코드, message = result.메시지
                    });
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
            next(app);
        };

    private async Task WriteProofAsync(HttpContext context, SsalddelContext db, IServiceProvider services)
    {
        var ct = context.RequestAborted;
        var plan = await db.출고예정.AsNoTracking().SingleAsync(item => item.Id == CargoJourneySeed.OutboundPlanId, ct);
        var inventory = await db.입고상품.AsNoTracking().SingleAsync(item => item.Id == CargoJourneySeed.InboundItemId, ct);
        var requestId = plan.운송의뢰Id ?? "";
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(item => item.의뢰Id == requestId, ct);
        var transport = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(item => item.의뢰Id == requestId, ct);
        var pricing = request?.운임구성Id is { } pricingId
            ? await db.운임구성.AsNoTracking().SingleOrDefaultAsync(item => item.Id == pricingId && item.의뢰Id == requestId, ct)
            : null;
        object? mongoProof = null;
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            var mongoState = await services.GetRequiredService<I운송원장Mongo동기화Service>().상태조회Async(requestId, ct);
            var collection = services.GetRequiredService<IMongoClient>().GetDatabase(CargoJourneySettings.DatabaseName).GetCollection<BsonDocument>("community_ledgers");
            var documentCount = await collection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("원장Id", mongoState.원장Id), cancellationToken: ct);
            mongoProof = new { documentCount, state = mongoState };
        }
        await context.Response.WriteAsJsonAsync(new
        {
            schemaVersion = "cargo-journey-verification.database-proof.r1",
            runStableId = settings.RunStableId,
            database = CargoJourneySettings.DatabaseName,
            fixture = new { warehouseId = CargoJourneySeed.WarehouseId, inboundItemId = inventory.Id, outboundPlanId = plan.Id },
            stock = new { received = inventory.입고수량, available = inventory.가용수량, reserved = inventory.예약수량, state = inventory.상태 },
            outbound = new { quantity = plan.수량, state = plan.상태, requestId = plan.운송의뢰Id, completedAtUtc = plan.출고처리일시 },
            request = request is null ? null : new { id = request.의뢰Id, state = request.상태, paymentState = request.결제상태, settlementState = request.정산상태, dispatchState = request.배차상태 },
            transport = transport is null ? null : new { id = transport.Id, requestId = transport.의뢰Id, state = transport.상태, assignedDriverId = transport.확정기사Id, currentDriverId = transport.기사_운송자, ledgerId = transport.커뮤니티원장Id },
            requestCount = await db.화주운송의뢰.CountAsync(item => item.의뢰Id == requestId, ct),
            pricingCount = await db.운임구성.CountAsync(item => item.의뢰Id == requestId, ct),
            pricing = pricing is null ? null : new { id = pricing.Id, requestId = pricing.의뢰Id,
                finalFare = pricing.최종운임, expectedDistanceKm = pricing.예상거리Km, perKmRate = pricing.Km당단가,
                minimumFare = pricing.최소운임, distanceBasis = pricing.거리계산방식, rateSource = pricing.단가출처 },
            transportCount = await db.운송원장.CountAsync(item => item.의뢰Id == requestId, ct),
            allocationCount = await db.운송의뢰상품연결.CountAsync(item => item.운송의뢰Id == requestId && item.입고상품Id == inventory.Id, ct),
            outboundMovementCount = await db.재고이동.CountAsync(item => item.입고상품Id == inventory.Id && item.이동유형 == 재고이동유형.출고, ct),
            outboundHistoryCount = await db.재고이력.CountAsync(item => item.입고상품Id == inventory.Id && item.이력유형 == 재고이동유형.출고, ct),
            mongo = mongoProof
        }, cancellationToken: ct);
    }

    private sealed record CargoOfferInput(string RequestId);
}
