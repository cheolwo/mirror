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
                    || context.Request.Host.Port != CargoJourneySettings.HostPort
                    || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(settings.AccessKey)))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                using var scope = context.RequestServices.CreateScope();
                settings.ValidateEffectiveConfiguration(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
                    scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>());
                var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
                settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
                if (context.Request.Path == "/verification/cargo/standalone-proof" || context.Request.Path == "/verification/cargo/standalone-offer")
                {
                    await HandleStandaloneAsync(context, db, scope.ServiceProvider);
                    return;
                }
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
                    var current = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == input.RequestId, context.RequestAborted);
                    if (current is null) { context.Response.StatusCode = 409; return; }
                    if (!string.IsNullOrWhiteSpace(current.확정기사Id) || !string.IsNullOrWhiteSpace(current.현재추천대상기사Id))
                    {
                        await WriteProofAsync(context, db, scope.ServiceProvider);
                        return;
                    }
                    var candidateReview = await scope.ServiceProvider.GetRequiredService<용달운송배차업무정책>()
                        .후보검토Async(current, cancellationToken: context.RequestAborted);
                    await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, ".private", "cargo-candidate-review-" + current.Id + ".json"),
                        System.Text.Json.JsonSerializer.Serialize(new { input.RequestId, atUtc = DateTime.UtcNow,
                            source = "ProductCandidatePolicyBeforeControlledInvocation", synthetic = true, candidateReview }), context.RequestAborted);
                    var result = current.배차큐단계 == 상태값.배차큐단계.계획배차
                        ? await transition.계획배차에서추천으로전환Async(input.RequestId, context.RequestAborted)
                        : await transition.추천대기처리Async(input.RequestId, context.RequestAborted);
                    context.Response.StatusCode = result.전환여부 || result.결과코드 == 배차대기원장전환결과코드.이미추천중 ? 200 : 409;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        scheduler = "ControlledProductCandidateEngineWithSyntheticAdapters",
                        productionCandidateAlgorithmProof = false,
                        candidateEngine = "ProductQueueTransitionAndBusinessPolicy", synthetic = true, candidateReview,
                        requestId = result.의뢰Id, driverId = result.기사Id,
                        transitioned = result.전환여부, resultCode = result.결과코드, message = result.메시지
                    });
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
            next(app);
        };

    private async Task HandleStandaloneAsync(HttpContext context, SsalddelContext db, IServiceProvider services)
    {
        const string markerPrefix = "USB r67 standalone cargo; case=";
        const string markerSuffix = "; ActualMapRequest=false";
        var ct = context.RequestAborted;
        var offer = context.Request.Path == "/verification/cargo/standalone-offer";
        if (offer ? !HttpMethods.IsPost(context.Request.Method) : !HttpMethods.IsGet(context.Request.Method))
        { context.Response.StatusCode = 405; return; }
        if (context.Request.ContentLength is > 1024) { context.Response.StatusCode = 400; return; }
        var id = offer ? (await context.Request.ReadFromJsonAsync<CargoOfferInput>(ct))?.RequestId
            : context.Request.Query["requestId"].ToString();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100) { context.Response.StatusCode = 400; return; }
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == id, ct);
        var caseKey = request?.요청사항 switch
        {
            markerPrefix + "cargo-phone-quick-02" + markerSuffix => "cargo-phone-quick-02",
            markerPrefix + "cargo-phone-quick-03" + markerSuffix => "cargo-phone-quick-03",
            markerPrefix + "cargo-phone-quick-04" + markerSuffix => "cargo-phone-quick-04",
            markerPrefix + "cargo-phone-quick-05" + markerSuffix => "cargo-phone-quick-05",
            "추천 예시 A" => "cargo-map-r69-a",
            "추천 예시 B" => "cargo-map-r69-b",
            "추천 예시 C" => "cargo-map-r69-c",
            "추천 예시 D" => "cargo-map-r69-d",
            "추천 예시 E" => "cargo-map-r69-e",
            "추천 예시 F" => "cargo-map-r69-f",
            "추천 예시 G" => "cargo-map-r69-g",
            "추천 예시 H" => "cargo-map-r69-h",
            "추천 예시 I" => "cargo-map-r69-i",
            "추천 예시 J" => "cargo-map-r69-j",
            "추천 예시 K" => "cargo-map-r69-k",
            "추천 예시 L" => "cargo-map-r69-l",
            "추천 예시 M" => "cargo-map-r69-m",
            "추천 예시 N" => "cargo-map-r69-n",
            "추천 예시 O" => "cargo-map-r69-o",
            "추천 예시 P" => "cargo-map-r69-p",
            "추천 예시 Q" => "cargo-map-r69-q",
            "추천 예시 R" => "cargo-map-r69-r",
            "추천 예시 S" => "cargo-map-r69-s",
            "추천 예시 T" => "cargo-map-r69-t",
            "추천 예시 U" => "cargo-map-r69-u",
            "추천 예시 V" => "cargo-map-r69-v",
            "추천 예시 W" => "cargo-map-r69-w",
            "추천 예시 X" => "cargo-map-r69-x",
            _ => null
        };
        (string PickupAddress, decimal PickupLatitude, decimal PickupLongitude,
            string DropoffAddress, decimal DropoffLatitude, decimal DropoffLongitude, decimal Fare)? r187Fixture = caseKey switch
        {
            "cargo-map-r69-m" or "cargo-map-r69-p" or "cargo-map-r69-s" or "cargo-map-r69-v" => ("픽업 장소 4", 37.583m, 127.086m, "전달 장소 4", 37.585m, 127.089m, 6000m),
            "cargo-map-r69-n" or "cargo-map-r69-q" or "cargo-map-r69-t" or "cargo-map-r69-w" => ("픽업 장소 5", 37.588m, 127.092m, "전달 장소 5", 37.590m, 127.095m, 6200m),
            "cargo-map-r69-o" or "cargo-map-r69-r" or "cargo-map-r69-u" or "cargo-map-r69-x" => ("픽업 장소 6", 37.594m, 127.082m, "전달 장소 6", 37.597m, 127.085m, 6400m),
            _ => null
        };
        var isR69MapCase = caseKey is "cargo-map-r69-a" or "cargo-map-r69-b" or "cargo-map-r69-c" or "cargo-map-r69-d" or "cargo-map-r69-e" or "cargo-map-r69-f" or "cargo-map-r69-g" or "cargo-map-r69-h" or "cargo-map-r69-i" or "cargo-map-r69-j" or "cargo-map-r69-k" or "cargo-map-r69-l";
        var isR69SecondMapCase = caseKey is "cargo-map-r69-b" or "cargo-map-r69-d" or "cargo-map-r69-f" or "cargo-map-r69-h" or "cargo-map-r69-k";
        var isR185ThirdMapCase = caseKey is "cargo-map-r69-i" or "cargo-map-r69-l";
        var isR183MapCase = caseKey is "cargo-map-r69-c" or "cargo-map-r69-d" or "cargo-map-r69-e" or "cargo-map-r69-f" or "cargo-map-r69-g" or "cargo-map-r69-h" or "cargo-map-r69-i" or "cargo-map-r69-j" or "cargo-map-r69-k" or "cargo-map-r69-l";
        var expectedPickupAddress = r187Fixture?.PickupAddress ?? (isR69MapCase ? "픽업 장소 1" : "격리 검증 상차 창고");
        var expectedPickupLatitude = r187Fixture?.PickupLatitude ?? 37.588m;
        var expectedPickupLongitude = r187Fixture?.PickupLongitude ?? 127.085m;
        var expectedDropoffAddress = r187Fixture?.DropoffAddress ?? (isR185ThirdMapCase ? "전달 장소 3" : isR69SecondMapCase ? "전달 장소 2"
            : isR69MapCase ? "전달 장소 1" : "합성 검증 하차지 도로명 주소");
        var expectedCargoType = r187Fixture.HasValue || isR69MapCase ? "소형 상자" : "USB r67 독립 소형 상자";
        var expectedDropoffLatitude = r187Fixture?.DropoffLatitude ?? (isR185ThirdMapCase ? 37.592m : isR69SecondMapCase ? 37.584m : 37.590m);
        var expectedDropoffLongitude = r187Fixture?.DropoffLongitude ?? (isR185ThirdMapCase ? 127.092m : isR69SecondMapCase ? 127.094m : 127.087m);
        var expectedR183Fare = r187Fixture?.Fare ?? (isR185ThirdMapCase ? 6400m : isR69SecondMapCase ? 6200m : 6000m);
        if (request is null || request.화주Id != CargoJourneySeed.ShipperId || request.주문자UserId != CargoJourneySeed.ShipperId
            || caseKey is null || request.모의처리처수행 || request.차량종류 != "1톤 카고"
            || request.화물종류 != expectedCargoType
            || request.픽업_도로명주소 != expectedPickupAddress || request.하차_도로명주소 != expectedDropoffAddress
            || ((isR183MapCase || r187Fixture.HasValue) && request.최종운임 != expectedR183Fare)
            || request.클라이언트요청Id?.StartsWith("shipper-app-", StringComparison.Ordinal) != true)
        {
            context.Response.StatusCode = 409;
            await context.Response.WriteAsJsonAsync(new { errorCode = "StandaloneSyntheticBindingMismatch", synthetic = true }, ct);
            return;
        }
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == id, ct);
        var coordinatesBound = request.픽업_위도 == expectedPickupLatitude && request.픽업_경도 == expectedPickupLongitude
            && request.하차_위도 == expectedDropoffLatitude && request.하차_경도 == expectedDropoffLongitude;
        string? resultCode = null;
        object? candidateReview = null;
        if (offer)
        {
            if (!coordinatesBound || request.정산상태 != 운임정산상태.후불승인완료.ToString() || queue is null)
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new { errorCode = "StandaloneApprovedCoordinatesAndQueueRequired", coordinatesBound,
                    settlementState = request.정산상태, queuePresent = queue is not null, synthetic = true }, ct);
                return;
            }
            if (queue.현재추천대상기사Id is not null && queue.추천만료시각 <= DateTime.UtcNow)
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new { errorCode = "StandaloneExpiredOfferRequiresReview", synthetic = true }, ct);
                return;
            }
            if (queue.배차큐단계 == 상태값.배차큐단계.공개배차)
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new { errorCode = "StandalonePublicQueuePreserved", caseKey,
                    queueStage = queue.배차큐단계, exposureStatus = queue.배차노출상태,
                    recommendationRound = queue.추천라운드, synthetic = true }, ct);
                return;
            }
            if (string.IsNullOrWhiteSpace(queue.현재추천대상기사Id) && string.IsNullOrWhiteSpace(queue.확정기사Id))
            {
                candidateReview = await services.GetRequiredService<용달운송배차업무정책>().후보검토Async(queue, cancellationToken: ct);
                await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, ".private", "standalone-cargo-candidate-review-" + queue.Id + ".json"),
                    System.Text.Json.JsonSerializer.Serialize(new { requestId = id, caseKey, atUtc = DateTime.UtcNow, synthetic = true, candidateReview }), ct);
                var transition = services.GetRequiredService<I배차대기원장전환Service>();
                var result = queue.배차큐단계 == 상태값.배차큐단계.계획배차
                    ? await transition.계획배차에서추천으로전환Async(id, ct) : await transition.추천대기처리Async(id, ct);
                resultCode = result.결과코드;
                if (!result.전환여부 && result.결과코드 != 배차대기원장전환결과코드.이미추천중)
                { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { resultCode, synthetic = true, candidateReview }, ct); return; }
                db.ChangeTracker.Clear();
                queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == id, ct);
            }
        }
        await context.Response.WriteAsJsonAsync(new
        {
            schemaVersion = "usb-r67.standalone-cargo-proof.r1", settings.RunStableId, database = CargoJourneySettings.DatabaseName,
            caseKey, synthetic = true, actualMapRequest = false, actualGpsVerified = false,
            recommendationTtlSeconds = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<배차큐정책Options>>().Value.추천유지시간초,
            recommendationTtlScope = "ArtifactHostOnly_NativeScreenshotAndCameraVerification",
            productionDefaultTtlAcceptanceVerified = false,
            sourceCoordinates = caseKey is "cargo-map-r69-v" or "cargo-map-r69-w" or "cargo-map-r69-x"
                ? "DeclaredR190SyntheticGeocodingFixture"
                : caseKey is "cargo-map-r69-s" or "cargo-map-r69-t" or "cargo-map-r69-u"
                ? "DeclaredR189SyntheticGeocodingFixture"
                : caseKey is "cargo-map-r69-p" or "cargo-map-r69-q" or "cargo-map-r69-r"
                ? "DeclaredR188SyntheticGeocodingFixture"
                : r187Fixture.HasValue ? "DeclaredR187SyntheticGeocodingFixture"
                : caseKey is "cargo-map-r69-j" or "cargo-map-r69-k" or "cargo-map-r69-l"
                ? "DeclaredR186SyntheticGeocodingFixture"
                : caseKey is "cargo-map-r69-g" or "cargo-map-r69-h" or "cargo-map-r69-i"
                    ? "DeclaredR185SyntheticGeocodingFixture" : "DeclaredR67SyntheticGeocodingFixture", coordinatesBound,
            requestId = request.의뢰Id, requestState = request.상태, paymentState = request.결제상태, settlementState = request.정산상태,
            quotedFareKrw = request.최종운임, transportId = queue?.Id, transportState = queue?.상태,
            queueStage = queue?.배차큐단계, exposureStatus = queue?.배차노출상태, recommendationRound = queue?.추천라운드,
            chosenDriverId = queue?.현재추천대상기사Id, confirmedDriverId = queue?.확정기사Id,
            expiresAtUtc = queue?.추천만료시각, frozenDriverPayout = queue?.기사지급예정액,
            queueCoordinatesPresent = queue?.픽업_위도.HasValue == true && queue?.픽업_경도.HasValue == true
                && queue?.하차_위도.HasValue == true && queue?.하차_경도.HasValue == true,
            resultCode, candidateReview, warehouseStockUsed = false, actualPaymentVerified = false
        }, ct);
    }

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
