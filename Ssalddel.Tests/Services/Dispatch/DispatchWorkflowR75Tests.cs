using System.Net;
using System.Text.Json;
using FluentResults;
using MediatR;
using 살뜰.Data;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using Ssalddel.Services.Notifications;
using Ssalddel.Services.Community;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Operations;
using Ssalddel.Services.Privacy;
using Ssalddel.Tests.Clients;
using Ssalddel.Tests.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.External.Google;
using 살뜰.Services.Options;
using 살뜰.도메인.운송;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.설정;
using 살뜰.도메인.차량;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch
{
    /// <summary>
    /// 실제 배차 정책/엔진, SQLite, loopback HTTP와 제품 모바일 PageModel을 재사용한다.
    /// 합성 입력과 외부 포트 대체는 결과에 기록하며 기기/FCM/Mongo 검증으로 승격하지 않는다.
    /// </summary>
    public sealed class DispatchWorkflowR75Tests
    {
        public static readonly string[] ScenarioIds =
        [
            "food-normal-same-region", "food-normal-adjacent-region", "food-demand-capacity",
            "food-ineligible-and-queue-recovery", "food-duplicate-hint", "food-expired-offer",
            "food-lost-accept-response", "cargo-engine-recommendation", "small-goods-normal-accept"
        ];

        [Theory]
        [InlineData("food-normal-same-region")]
        [InlineData("food-normal-adjacent-region")]
        [InlineData("food-demand-capacity")]
        [InlineData("food-ineligible-and-queue-recovery")]
        [InlineData("food-duplicate-hint")]
        [InlineData("food-expired-offer")]
        [InlineData("food-lost-accept-response")]
        [InlineData("cargo-engine-recommendation")]
        [InlineData("small-goods-normal-accept")]
        public async Task 실제정책과원장및모바일경계는_시나리오별완료근거를보존한다(string scenarioId)
        {
            var result = await RunScenarioAsync(scenarioId);
            Assert.True(result.Passed);
            Assert.False(result.RealFcmDelivered || result.RealOllamaInference || result.DeviceUiRendered
                || result.RealMongoVerified || result.AzureVerified || result.RealSoundHeard);
        }

        // Console runner와 시험이 같은 검증 본문을 실행한다. 실제 모델을 켜는 옵션은 없다.
        public static async Task<DispatchWorkflowR75Evidence> RunScenarioAsync(string scenarioId, string? databasePath = null)
        {
            if (!ScenarioIds.Contains(scenarioId, StringComparer.Ordinal))
                throw new ArgumentException("등록된 r75 합성 시나리오만 실행할 수 있습니다.", nameof(scenarioId));
            using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler());
            if (scenarioId is "food-normal-same-region" or "food-normal-adjacent-region" or "food-demand-capacity")
            {
                var existingId = scenarioId switch
                {
                    "food-normal-adjacent-region" => "adjacent-region",
                    "food-demand-capacity" => "demand-capacity",
                    _ => "same-region"
                };
                var result = await FDriverServerDispatchScenarioTests.RunScenarioAsync(existingId, modelHttp, databasePath);
                Assert.NotEqual("scenario-off", result.SelectedDriverId);
                Assert.True(result.FrozenFee > 0m);
                Assert.True(result.Accepted && result.Delivered && result.CustomerReceived);
                return Evidence(scenarioId, "FoodOrderToSeparateReceipt", result.ModelRequests, result,
                    [.. result.SubstitutedPorts,
                        "Shared-driver-admission:not-configured-in-reused-fixture-legacy-intent-only",
                        "Straight-line-r72-pricing:not-enabled-in-reused-fixture"],
                    ["ActualEngineCandidateSelection", "FrozenFee", "MobileStageActions", "SeparateCustomerReceipt"]);
            }

            if (scenarioId == "food-ineligible-and-queue-recovery")
            {
                await using var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", modelHttp, databasePath);
                await fixture.r75수신의사변경Async("scenario-older", false);
                await fixture.r75수신의사변경Async("scenario-near", false);
                await fixture.r75자동추천처리Async();
                var waiting = await fixture.ReadSnapshotAsync();
                Assert.Null(waiting.RecommendedDriverId);
                Assert.Null(waiting.ConfirmedDriverId);
                Assert.Equal(0, waiting.AttemptCount);
                Assert.Equal(0, waiting.NotificationOutboxCount);
                Assert.Equal(0, fixture.ModelRequests);

                await fixture.r75수신의사변경Async("scenario-older", true);
                await fixture.r75자동추천처리Async();
                var recovered = await fixture.ReadSnapshotAsync();
                Assert.Equal("scenario-older", recovered.RecommendedDriverId);
                Assert.Equal(1, recovered.RecommendationRound);
                Assert.Equal(1, recovered.NotificationOutboxCount);
                Assert.Equal(0, recovered.AttemptCount);
                Assert.Equal(0, fixture.ModelRequests); // 적격 한 명이면 추론 생략.
                using var wrongDriver = fixture.CreateActorClient("fixture-driver:scenario-off");
                using var rejected = await wrongDriver.PostAsync($"api/v1/driver/food-deliveries/offers/{fixture.OfferId}/accept", null);
                Assert.False(rejected.IsSuccessStatusCode);
                var unchanged = await fixture.ReadSnapshotAsync();
                Assert.Equal(0, unchanged.AttemptCount);
                Assert.Null(unchanged.ConfirmedDriverId);
                Assert.Equal(recovered.FrozenFee, unchanged.FrozenFee);
                return Evidence(scenarioId, "FoodQueueHeldThenActualEngineResumed", fixture.ModelRequests,
                    new { waiting, recovered, unauthorizedAcceptStatus = (int)rejected.StatusCode, unchanged }, FoodPorts,
                    ["NoEligibleDriverHold", "IntentRecovery", "NoDirectOffer", "DifferentDriverCannotAccept"]);
            }

            if (scenarioId is "cargo-engine-recommendation" or "small-goods-normal-accept")
            {
                await using var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", modelHttp, databasePath);
                var smallGoods = scenarioId == "small-goods-normal-accept";
                var result = await fixture.r75화물엔진추천Async(smallGoods);
                Assert.Equal(0, fixture.ModelRequests); // 현재 화물 정책은 Ollama 후보 어댑터를 호출하지 않는다.
                return Evidence(scenarioId, smallGoods ? "SmallGoodsRegistrationToEngineAndActualAcceptCommand" : "CargoSeededSourceToQuotePostpayEngineAndActualAcceptCommand", 0, result,
                    [.. FoodPorts.Where(x => !x.StartsWith("Straight-line-r72-pricing:", StringComparison.Ordinal)),
                        smallGoods ? "Straight-line-r72-pricing:production-policy-enabled-explicit-synthetic-policy-input" : "Straight-line-r72-pricing:not-applicable-to-general-cargo",
                        "Cargo-route:explicit-synthetic-8km-10minute-zero-toll-provider-not-road-observation",
                        smallGoods ? "Cargo-source:production-neighborhood-registration-and-create-commands-no-HTTP" : "Cargo-source:synthetic-SQL-seed-not-shipper-HTTP",
                        "Cargo-Mongo:recording-ack-SQL-bridge-no-actual-Mongo",
                        "Cargo-actor:explicit-synthetic-current-user-not-production-JWT",
                        "Cargo-market-policy:fixture-allow-not-real-provider-qualification",
                        "Cargo-privacy-consent:fixture-validation-not-real-stored-consent",
                        "Cargo-OS-handoff:recording-only", "Cargo-continuity:production-UseCase-disabled-no-reservations",
                        "Cargo-acceptance:production-handler-direct-call-not-mobile-HTTP",
                        "Cargo-completion:not-executed", "Fuel:not-invoked-by-this-candidate-path"],
                    ["CargoEngineCatalog", "ProductionVehicleSuitability", "FoodAppExcluded", "IncompatibleVehicleExcluded",
                        "StoredRecommendation", "NoDirectOffer", "ActualAcceptCommand", "AcceptanceReplay",
                        smallGoods ? "ActualSmallGoodsDriverPayoutPolicy" : "CustomerQuoteNotDriverPayout"]);
            }

            await using var mobile = await FDriverWorkflowMobileSession.CreateAsync("same-region", modelHttp, databasePath);
            var before = await mobile.Fixture.ReadSnapshotAsync();
            Assert.NotEqual("scenario-off", before.RecommendedDriverId);
            if (scenarioId == "food-duplicate-hint")
            {
                var hint = Assert.Single(mobile.Fixture.PushMessages).Data;
                Assert.True(await mobile.Receiver.ReceiveAsync(hint));
                Assert.True(await mobile.Receiver.ReceiveAsync(hint));
                await mobile.Fixture.RetryNotificationRequestAsync();
                await mobile.Fixture.DispatchPendingNotificationsAsync();
                await mobile.Model.InitializeAsync();
                await FDriverLifecycleTestSupport.RefreshBackground(mobile.Model);
                var after = await mobile.Fixture.ReadSnapshotAsync();
                Assert.Single(mobile.Fixture.PushMessages);
                Assert.Single(mobile.Model.RecommendedTicketItems);
                Assert.Equal(before.RecommendationRound, after.RecommendationRound);
                Assert.Equal(before.NotificationOutboxCount, after.NotificationOutboxCount);
                Assert.Equal(before.FrozenFee, after.FrozenFee);
                Assert.Equal(0, after.AttemptCount);
                Assert.DoesNotContain(mobile.Transport.Requests, x => x.Method == "POST");
                return Evidence(scenarioId, "FoodOutboxAndMobileHintDeduplication", mobile.Fixture.ModelRequests,
                    new { before, after, pushTransportCalls = mobile.Fixture.PushMessages.Count,
                        devicePortRequests = mobile.Notifications.PublishRequests, nativeDedupVerified = false,
                        mobileOffers = mobile.Model.RecommendedTicketItems.Count }, FoodPorts,
                    ["SingleCanonicalOffer", "SingleOutbox", "DuplicateHintsCannotAccept", "NoNativeSoundClaim"]);
            }

            await mobile.Model.InitializeAsync();
            var offeredPayout = Assert.IsType<DeliveryTicketPreview>(mobile.Model.SelectedTicket).DriverPayout;
            Assert.Equal(before.FrozenFee, offeredPayout);
            if (scenarioId == "food-expired-offer")
            {
                // 시간 경계만 주입한다. 기사 지정·금액·확정 상태는 직접 변경하지 않는다.
                await using (var db = mobile.Fixture.CreateContext())
                {
                    var queue = await db.운송원장.SingleAsync();
                    queue.추천만료시각 = DateTime.UtcNow.AddSeconds(-1);
                    await db.SaveChangesAsync();
                }
                await Assert.ThrowsAsync<FDriverApiException>(() => mobile.Api.AcceptAsync(mobile.Fixture.OfferId));
                await FDriverLifecycleTestSupport.RefreshBackground(mobile.Model);
                var after = await mobile.Fixture.ReadSnapshotAsync();
                Assert.Equal(0, after.AttemptCount);
                Assert.Null(after.ConfirmedDriverId);
                Assert.Equal(before.FrozenFee, after.FrozenFee);
                Assert.Empty(mobile.Model.RecommendedTicketItems);
                Assert.False(mobile.Model.CanAcceptSelectedTicket);
                var expiredHint = new Dictionary<string, string>(Assert.Single(mobile.Fixture.PushMessages).Data)
                    { ["expiresAtUtc"] = "2000-01-01T00:00:00Z" };
                var publications = mobile.Notifications.PublishRequests;
                Assert.False(await mobile.Receiver.ReceiveAsync(expiredHint));
                Assert.Equal(publications, mobile.Notifications.PublishRequests);
                return Evidence(scenarioId, "FoodExpiredCanonicalOfferAndHint", mobile.Fixture.ModelRequests,
                    new { before, after, offeredPayout, http = mobile.Transport.Requests.ToArray(),
                        fault = "Synthetic expiry timestamp in isolated SQL; no driver/fare/allocation injection" }, FoodPorts,
                    ["ExpiredServerAcceptRejected", "CanonicalBannerRemoved", "ExpiredHintIgnored", "FeeUnchanged"]);
            }

            mobile.Transport.LoseNextAcceptResponse = true;
            await mobile.Model.AcceptSelectedTicketCommand.ExecuteAsync(null);
            var accepted = await mobile.Fixture.ReadSnapshotAsync();
            Assert.Equal(1, accepted.AttemptCount);
            Assert.Equal(mobile.Fixture.DriverId, accepted.ConfirmedDriverId);
            Assert.Equal(offeredPayout, mobile.Model.ActiveDelivery?.DriverPayout);
            Assert.Equal(before.FrozenFee, accepted.FrozenFee);
            Assert.Equal(FDriverFoodStage.PickupTravel, mobile.Model.CurrentDeliveryStage);
            Assert.False(mobile.Model.HasWorkspaceWarning);
            await mobile.Model.AcceptSelectedTicketCommand.ExecuteAsync(null);
            Assert.Single(mobile.Transport.Requests, x => x.Method == "POST" && x.Path.EndsWith("/accept", StringComparison.Ordinal));
            Assert.Single(mobile.Transport.Requests, x => x.Boundary == "injected-response-loss-after-server");
            Assert.Contains(mobile.Transport.Requests, x => x.Method == "GET" && x.Path.EndsWith("/workspace/current", StringComparison.Ordinal));
            return Evidence(scenarioId, "FoodMobileRecoveryAfterCommittedAcceptResponseLost", mobile.Fixture.ModelRequests,
                new { before, accepted, offeredPayout, activePayout = mobile.Model.ActiveDelivery?.DriverPayout,
                    mobileStage = mobile.Model.CurrentDeliveryStage.ToString(), http = mobile.Transport.Requests.ToArray() }, FoodPorts,
                ["SingleCommittedAttempt", "NoSecondAcceptPost", "CanonicalRecovery", "FrozenPayoutPreserved"]);
        }

        private static DispatchWorkflowR75Evidence Evidence(string id, string scope, int modelRequests, object detail,
            IReadOnlyList<string> ports, IReadOnlyList<string> checks)
            => new(id, true, scope, "Production-policy-engine-and-transition; no designated-driver offer endpoint",
                "SQLite relational fixture; not MySQL/Mongo operational-host verification", modelRequests,
                "Synthetic HTTP response through production Ollama adapter; not actual inference", ports, checks, detail);

        private static readonly string[] FoodPorts =
        [
            "Coordinates-and-geocoder:synthetic-fixed", "Route:synthetic-fixed-production-interface",
            "Weather:fixture-MissingServiceKey-null-observation-not-measured-weather",
            "Menu:fixture-pass-through-no-catalog-revalidation", "SQL:isolated-SQLite",
            "Driver-location-area-and-availability-projection:in-memory-synthetic",
            "Mongo-and-food-ledger-synchronization:no-op", "Settlement:no-op", "Restaurant-notification:no-op",
            "Domain-events:recording-publisher-no-automatic-handlers-explicit-Outbox-drain", "FCM:recording-only",
            "Native-notification:recording-only-no-sound", "Push-callback:actual-receiver-payload-injection",
            "Authentication:fixture-Bearer-not-production-JWT", "SecureStorage:in-memory-fixture",
            "Database-encryption:pass-through", "GPS-and-map-lifetime:disabled-synthetic-server-location",
            "Shared-driver-admission:not-configured-in-reused-fixture-legacy-intent-only",
            "Straight-line-r72-pricing:not-enabled-in-reused-fixture"
        ];
    }

    public sealed record DispatchWorkflowR75Evidence(string ScenarioId, bool Passed, string Scope, string SelectionEntry,
        string DatabaseBoundary, int ModelRequests, string ModelBoundary, IReadOnlyList<string> SubstitutedPorts,
        IReadOnlyList<string> Checks, object Detail)
    {
        public bool RealFcmDelivered => false;
        public bool RealOllamaInference => false;
        public bool DeviceUiRendered => false;
        public bool RealMongoVerified => false;
        public bool AzureVerified => false;
        public bool RealSoundHeard => false;
        public bool ActualGpsVerified => false;
        public bool OperationalPaymentsExecuted => false;
    }
}

// 기존 fixture 파일을 수정하지 않고 격리 시험의 입력 제어만 확장한다.
namespace Ssalddel.Tests.Services.Dispatch.AI
{
    public sealed partial class FoodDispatchWorkflowScenarioFixture
    {
        internal async Task r75수신의사변경Async(string driverId, bool enabled)
        {
            await using var db = CreateContext();
            await Availability(db).기사의사변경Async(driverId, new()
            {
                클라이언트요청Id = Guid.NewGuid(),
                수신의사Code = enabled ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off
            });
        }

        internal async Task r75자동추천처리Async()
        {
            await using var db = CreateContext();
            var queue = await db.운송원장.SingleAsync();
            if (queue.배차큐단계 == 상태값.배차큐단계.계획배차)
                await Transition(db).계획배차에서추천으로전환Async(queue.의뢰Id);
            else
                await Transition(db).추천대기처리Async(queue.의뢰Id);
        }

        internal async Task<object> r75화물엔진추천Async(bool smallGoods)
        {
            var requestId = "r75-synthetic-cargo-source";
            var now = DateTime.UtcNow;
            await using var db = CreateContext();
            var shipper = new TestCurrentUserAccessor("r75-synthetic-shipper", "화주");
            var operators = new 화주운송업무담당자UseCase(db, shipper, TimeProvider.System);
            var routes = new r75합성경로Service(_routes);
            var fareService = new 화주운송기준운임Service(db, routes);
            var sync = new r75기록형운송동기화(db);
            var queueFactory = new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(),
                new SyntheticAreaProjection(), _spaces, _cargoSpaces);
            var smallOptions = Options.Create(new StraightLineDriverPricingOptions
            {
                Enabled = smallGoods, SmallGoodsPolicyRevision = "r75-explicit-synthetic-test-policy",
                SmallGoodsEffectiveFromUtc = now.AddHours(-1), SmallGoodsEffectiveUntilUtc = now.AddHours(1),
                SmallGoodsBaseFeeKrw = 2000m, SmallGoodsMinimumPayoutKrw = 2000m,
                SmallGoodsWorkFeeKrw = 0m, SmallGoodsSurchargeKrw = 0m
            });
            var smallPricing = new 소형배송기사제안요금Service(db, smallOptions);
            object quoteEvidence;
            // 화주 원본 등록은 이 probe의 합성 입력이며 화주 HTTP/결제 완료를 뜻하지 않는다.
            if (!smallGoods)
            {
                db.화주운송의뢰.Add(new 화주운송의뢰
                {
                의뢰Id = requestId, 화주Id = "r75-synthetic-shipper", 주문자UserId = "r75-synthetic-shipper",
                화물종류 = "합성 상자", 화물수량 = 1, 화물중량Kg = 50m,
                화물길이Mm = 300, 화물폭Mm = 300, 화물높이Mm = 300, 운송방식 = "혼적",
                차량종류 = "1톤", 정산시점 = "운송완료후정산", 정산상태 = "후불승인대기",
                픽업_도로명주소 = "합성 상차 도로", 픽업_위도 = PickupLatitude, 픽업_경도 = PickupLongitude,
                하차_도로명주소 = "합성 하차 도로", 하차_위도 = DropoffLatitude, 하차_경도 = DropoffLongitude,
                픽업_시간창_시작일시 = now.AddMinutes(-5), 픽업_시간창_종료일시 = now.AddHours(1)
                });
                await db.SaveChangesAsync();
                var quoted = await fareService.견적Async(new() { 차량종류 = "1톤", 예상거리Km = 8m });
                Assert.True(quoted.IsSuccess, string.Join(";", quoted.Errors));
                var update = await new 의뢰수정CommandHandler(db, operators, fareService, sync, shipper)
                    .Handle(new 의뢰수정Command(requestId, new(null, null, null), new(null, null, null, null, null, null, null),
                        new(null, null, null, null, null, null, null, null), new(null, null, null, null, null, null, null, null),
                        new(null), null, new() { 예상거리Km = 8m }), CancellationToken.None);
                Assert.True(update.IsSuccess, string.Join(";", update.Errors));
                Assert.Equal(quoted.Value.최종운임, update.Value.최종운임);
                var approved = await new 화주운송의뢰후불승인CommandHandler(db, shipper, queueFactory, sync, operators)
                    .Handle(new(requestId, "r75 합성 후불 승인; 실제 수금 아님"), CancellationToken.None);
                Assert.True(approved.IsSuccess, string.Join(";", approved.Errors));
                quoteEvidence = new { quoted = quoted.Value, stored = update.Value.최종운임,
                    settlement = approved.Value.정산상태, realPaymentReceived = false, sourceRegisteredByCommand = false };
            }
            else
            {
                var registration = await r75생활소형등록Async(db, shipper, operators, fareService, routes, sync, queueFactory, smallPricing, now);
                requestId = registration.RequestId;
                quoteEvidence = registration;
            }
            db.차량제원.AddRange(
                new 차량제원 { 차량코드 = "r75-cargo-fit", 차량명 = smallGoods ? "합성 오토바이" : "합성 1톤 차량",
                    최대적재중량Kg = smallGoods ? 20 : 1000, 운영권장중량Kg = smallGoods ? 20 : 800,
                    적재함길이Mm = 2000, 적재함폭Mm = 1500, 적재함높이Mm = 1500 },
                new 차량제원 { 차량코드 = "r75-cargo-small", 차량명 = "합성 적재 불가 차량", 최대적재중량Kg = 1,
                    운영권장중량Kg = 1, 적재함길이Mm = 100, 적재함폭Mm = 100, 적재함높이Mm = 100 });
            foreach (var item in new[]
            {
                (Id: "r75-cargo-older", Age: 30, Vehicle: "r75-cargo-fit"),
                (Id: "r75-cargo-near", Age: 1, Vehicle: "r75-cargo-fit"),
                (Id: "r75-cargo-incompatible", Age: 120, Vehicle: "r75-cargo-small")
            })
            {
                db.용달기사.Add(new 용달기사 { 기사Id = item.Id, 상태 = "활동중", 차량 = item.Vehicle,
                    운행상태 = 상태값.기사운행상태.운행중 });
                await _states.UpsertAsync(new(item.Id, null, 상태값.기사운행상태.운행중,
                    now.AddMinutes(-item.Age), now.AddMinutes(-item.Age), 0m,
                    PickupLatitude, PickupLongitude, 5m, now, now, null, null, 0,
                    "immediate", "synthetic-r75", null, AppKey: 기사앱식별자.CargoYongdalDriverApp));
                await new SsalddelMobilePushInstallationService(db).UpsertAsync(item.Id,
                    new($"r75-installation-{item.Id}", 기사앱식별자.CargoYongdalDriverApp,
                        "Android", $"synthetic-token-{item.Id}"), default);
            }
            await db.SaveChangesAsync();
            var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == requestId);
            Assert.Null(queue.현재추천대상기사Id);
            var policy = new 용달운송배차업무정책(db, _locations, _states, Options.Create(_queueOptions), _routes,
                new 차량화물적합성Service(new 차량적재추천Engine()), new 배차추천판정Service(),
                new 배차추천평가Service(), _rejected);
            var engine = new 화물용달배차엔진([policy], new 운송의뢰배차원천분류Service(),
                new 화물용달배차흐름Resolver(), NullLogger<화물용달배차엔진>.Instance);
            var selection = new 배차추천후보선정Service(db, new 운영체제배차EngineCatalog([engine]));
            var candidate = await selection.다음후보선정Async(requestId);
            Assert.Equal(배차추천후보선정상태.선정됨, candidate.상태값);
            Assert.Equal(EngineImplementationIds.CargoYongdalDispatch, candidate.감사Context?.EngineImplementationId);
            Assert.Equal("r75-cargo-older", candidate.후보?.DriverId);
            var nextCandidate = await selection.다음후보선정Async(requestId, "r75-cargo-older");
            Assert.Equal("r75-cargo-near", nextCandidate.후보?.DriverId);
            var noCandidate = await selection.다음후보선정Async(requestId, "r75-cargo-incompatible");
            Assert.Equal("r75-cargo-older", noCandidate.후보?.DriverId);
            var transition = new 배차대기원장전환Service(db, Options.Create(_queueOptions), selection, Notifications(db),
                new 국내화물운송기사상태Service(_states), new 음식배달배차흐름Resolver(), smallGoodsPricing: smallPricing);
            var changed = await transition.계획배차에서추천으로전환Async(requestId);
            Assert.True(changed.전환여부);
            await db.Entry(queue).ReloadAsync();
            Assert.Equal(candidate.후보?.DriverId, queue.현재추천대상기사Id);
            Assert.Equal(1, queue.추천라운드);
            Assert.Null(queue.확정기사Id);
            if (smallGoods) Assert.True(queue.기사지급예정액 > 0m);
            else Assert.Null(queue.기사지급예정액); // 일반 화주 운임을 기사 지급액으로 복사하지 않는다.
            Assert.Single(await db.배차추천알림Outbox.Where(x => x.의뢰Id == requestId).ToArrayAsync());
            var audit = await db.운송이벤트.Where(x => x.의뢰Id == requestId).OrderByDescending(x => x.Id).FirstAsync();
            using var auditJson = JsonDocument.Parse(audit.메타데이터);
            Assert.Equal(EngineImplementationIds.CargoYongdalDispatch,
                auditJson.RootElement.GetProperty("EngineImplementationId").GetString());
            var offeredPayout = queue.기사지급예정액;
            var driver = candidate.후보!.DriverId;
            var continuity = new 화물연속배차UseCase(db, Availability(db), new InMemory화물연속배차ProjectionStore(),
                new PilotUnusedCargoRecommendations(), routes, _locations, new 화물연속배차Policy(),
                Options.Create(new 화물연속배차Options { Enabled = false }), TimeProvider.System,
                NullLogger<화물연속배차UseCase>.Instance);
            var eligibility = new 화물배차수락적격성Service(db, _locations,
                new 차량화물적합성Service(new 차량적재추천Engine()), new 기사운송일정구성Service(db),
                new 운송일정삽입평가Service(routes));
            var accept = new 배차수락CommandHandler(db, _publisher, new TestCurrentUserAccessor(driver, "기사"),
                new 참여자실행권한검사(), new WorkRelationshipSnapshotCollector(), eligibility, continuity,
                NullLogger<배차수락CommandHandler>.Instance, smallGoodsPricing: smallPricing);
            var acceptCommand = new 배차수락Command(driver, requestId, queue.추천라운드,
                [화물배차수락경고코드.차량주의사항]);
            var accepted = await accept.Handle(acceptCommand, CancellationToken.None);
            if (!smallGoods)
            {
                Assert.True(accepted.IsFailed);
                Assert.Equal("FreightDriverPayoutAgreementRequired", Assert.Single(accepted.Errors).Metadata["ErrorCode"]);
                Assert.Null((await db.운송원장.SingleAsync(x => x.의뢰Id == requestId)).확정기사Id);
                // 일반 화물은 고객 견적과 별도로 합의한 기사 보수의 명시 시험 입력이다.
                // 실제 당사자 합의·송금 또는 새로운 요율 산정의 증거로 표시하지 않는다.
                var workForAgreement = await db.화주운송의뢰.SingleAsync(x => x.의뢰Id == requestId);
                var fareForAgreement = await db.운임구성.SingleAsync(x => x.Id == workForAgreement.운임구성Id);
                fareForAgreement.기사지급예정운임 = 18000m;
                await db.SaveChangesAsync();
                offeredPayout = 18000m;
                accepted = await accept.Handle(acceptCommand, CancellationToken.None);
            }
            Assert.True(accepted.IsSuccess, string.Join(";", accepted.Errors));
            var replay = await accept.Handle(acceptCommand, CancellationToken.None);
            Assert.True(replay.IsSuccess, string.Join(";", replay.Errors));
            db.ChangeTracker.Clear();
            var acceptedQueue = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == requestId);
            var source = await db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == requestId);
            var fare = await db.운임구성.AsNoTracking().SingleAsync(x => x.Id == source.운임구성Id);
            Assert.Equal(driver, acceptedQueue.확정기사Id);
            Assert.Equal(상태값.배차대기상태.확정, acceptedQueue.상태);
            Assert.Equal(source.최종운임, acceptedQueue.운임);
            Assert.Equal(offeredPayout, acceptedQueue.기사지급예정액);
            if (smallGoods)
            {
                Assert.Equal(offeredPayout, fare.기사지급예정운임);
                Assert.NotEqual(source.최종운임, fare.기사지급예정운임);
            }
            else Assert.Equal(18000m, fare.기사지급예정운임);
            Assert.Equal(상태값.결제상태.결제대기, source.결제상태);
            var projection = 기사제안표시근거Service.요금근거(acceptedQueue);
            if (smallGoods) Assert.Equal(offeredPayout, projection?.TotalPayoutKrw);
            return new { requestId, sourceBoundary = smallGoods ? "Production registration/commands with explicit synthetic ports" : "Synthetic SQL seed then production quote/update/postpay commands",
                quoteEvidence, candidateId = candidate.후보!.DriverId,
                candidate.후보.추천점수, candidate.후보.추천사유, candidate.감사Context!.EngineImplementationId,
                acceptedQueue.확정기사Id, acceptedQueue.추천라운드, offeredPayout, confirmedPayout = acceptedQueue.기사지급예정액,
                customerFare = source.최종운임, storedDriverPayout = fare.기사지급예정운임, pricing = projection,
                compatibleCandidates = new[] { "r75-cargo-older", "r75-cargo-near" },
                incompatibleCandidate = "r75-cargo-incompatible", foodAppCandidatesExcluded = true,
                cargoShipperHttpVerified = false, cargoAccepted = true, cargoCompleted = false,
                cargoDriverPayoutFrozen = smallGoods, acceptanceReplay = replay.IsSuccess,
                generalPayoutAgreementIsSyntheticFixture = !smallGoods, preAgreementAcceptanceRejected = !smallGoods,
                realPaymentReceived = false, notificationTransportDelivered = false };
        }

        private async Task<r75생활소형등록근거> r75생활소형등록Async(SsalddelContext db, ICurrentUserAccessor actor,
            I화주운송업무담당자UseCase operators, I화주운송기준운임Service fareService,
            I배차추천경로Service routes, r75기록형운송동기화 sync, I운송의뢰배차대기Service queueFactory,
            I소형배송기사제안요금Service pricing, DateTime now)
        {
            var geo = new r75합성주소Service(PickupLatitude, PickupLongitude, DropoffLatitude, DropoffLongitude);
            var sender = new r75생활배송Sender(
                new 의뢰생성CommandHandler(db, geo, actor, sync, new r75모의시장Policy(), new r75기록형인계()),
                new 화주운송의뢰현장지급처리CommandHandler(db, actor, queueFactory, sync, operators),
                new 의뢰단건조회QueryHandler(db, operators));
            var transport = new 화주운송의뢰UseCase(sender, null!, null!, fareService,
                new 화주운송요금정책검토Service(), operators, null!);
            var useCase = new 생활배송의뢰UseCase(db, actor, transport, new r75시험동의Service(), geo,
                new r75작업자비활성Policy(), smallGoodsPricing: pricing);
            var input = new NeighborhoodDeliveryRequest
            {
                ClientRequestId = Guid.NewGuid(), CargoName = "합성 생활 상자", Quantity = 1, WeightKg = 5m,
                VehicleType = "오토바이", PrivacyConsentEvidenceId = Guid.NewGuid(),
                PrivacyConsentSourceCode = NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode,
                DispatchRequested = true, DirectPaymentAgreed = true, DispatchMode = NeighborhoodDispatchModes.Automatic,
                Pickup = new() { 주소 = new() { 도로명주소 = "합성 상차 도로" },
                    연락처 = new() { 이름 = "합성 상차 담당", 전화번호 = "010-0000-0000" },
                    시간창 = new() { 시작일시 = now.AddMinutes(5), 종료일시 = now.AddHours(1) } },
                Dropoff = new() { 주소 = new() { 도로명주소 = "합성 하차 도로" },
                    연락처 = new() { 이름 = "합성 하차 담당", 전화번호 = "010-0000-0001" } }
            };
            var quote = await useCase.견적Async(input);
            Assert.True(quote.IsSuccess, string.Join(";", quote.Errors));
            Assert.True(quote.Value.Fare.최종운임 > 0m);
            input.AgreedFareKrw = quote.Value.Fare.최종운임; // 실제 고객 견적의 명시 확인 입력이다.
            var created = await useCase.등록Async(input);
            Assert.True(created.IsSuccess, string.Join(";", created.Errors));
            var requestId = created.Value.RequestId;
            var source = await db.화주운송의뢰.SingleAsync(x => x.의뢰Id == requestId);
            var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == requestId);
            Assert.Equal(NeighborhoodDispatchModes.Automatic, queue.생활배송배차방식);
            Assert.True(queue.생활배송수락준비완료); // 제품 UseCase가 완료하며 시험은 이 값을 쓰지 않는다.
            Assert.True(queue.기사지급예정액 > 0m);
            Assert.Equal(quote.Value.Fare.최종운임, source.최종운임);
            Assert.Equal("현장수금예정", source.정산상태);
            Assert.Null(source.현장수금확인일시);
            var projected = 기사제안표시근거Service.요금근거(queue);
            Assert.NotNull(projected);
            Assert.Equal(2000m, projected.BaseFeeKrw);
            Assert.Equal(1000m, projected.RatePerKmKrw);
            Assert.Equal(0m, projected.IncludedDistanceMeters);
            Assert.Equal(queue.기사지급예정액, projected.TotalPayoutKrw);
            Assert.NotEqual(source.최종운임, projected.TotalPayoutKrw);
            return new(requestId, quote.Value.Fare.최종운임, projected.TotalPayoutKrw,
                source.정산상태, "Explicit test policy: base/minimum 2000, work/surcharge 0, Haversine 1000/km; not customer-price copy");
        }

        private sealed record r75생활소형등록근거(string RequestId, decimal CustomerFare, decimal DriverPayout,
            string SettlementStatus, string PolicyInput);

        private sealed class r75합성경로Service(I배차추천경로Service inner) : I배차추천경로Service
        {
            private static Task<배차경로예상결과?> Route(string? option = null) => Task.FromResult<배차경로예상결과?>(
                new(8m, TimeSpan.FromMinutes(10), 0m, "r75-explicit-synthetic-route", true) { RouteOption = option });
            public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination) => Route();
            public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string option, CancellationToken ct) => Route(option);
            public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria)
                => inner.ResolveOriginLocationAsync(id, driver, location, criteria);
            public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location)
                => inner.ResolveRouteAnchorLocationAsync(id, driver, location);
            public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default)
                => inner.EstimateOrderedRouteAsync(origin, stops, ct);
            public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
                => inner.EstimateInsertionDelayAsync(origin, anchor, pickup, dropoff);
            public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => inner.CalculateDistanceKm(source, target);
        }

        private sealed class r75합성주소Service(decimal pickupLat, decimal pickupLon, decimal dropoffLat, decimal dropoffLon) : IGeocodingService
        {
            public Task<(decimal lat, decimal lng)?> GeocodeAsync(string address)
                => Task.FromResult<(decimal lat, decimal lng)?>(address.Contains("상차", StringComparison.Ordinal)
                    ? (pickupLat, pickupLon) : (dropoffLat, dropoffLon));
        }

        private sealed class r75기록형운송동기화(SsalddelContext db) : I운송원장Mongo동기화Service
        {
            public async Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 source, string actor, CancellationToken ct = default)
            {
                // 실제 Mongo 대신 기존 fixture와 같은 SQL persistence bridge를 사용한다.
                if (!await db.화주운송의뢰.AnyAsync(x => x.의뢰Id == source.의뢰Id, ct)) db.화주운송의뢰.Add(source);
                await db.SaveChangesAsync(ct);
                return new() { 원장Id = source.의뢰Id };
            }
            public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 source, string actor, CancellationToken ct = default)
                => Task.FromResult<커뮤니티원장Dto?>(new() { 원장Id = source.의뢰Id });
            public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken ct = default)
                => Task.FromResult(운송원장Mongo동기화상태.Empty(id, string.Empty));
        }

        private sealed class r75모의시장Policy : IOperatingMarketFreightWorkflowPolicy
        {
            public string MarketCode => "KR";
            public OperatingMarketFreightWorkflowDecision Evaluate(OperatingMarketFreightWorkflowRequest request)
                => new() { CanProceed = true };
        }

        private sealed class r75기록형인계 : I화주운송의뢰화물운송인계Service
        {
            public Task<운영체제업무인계Dto> 인계Async(string requestId, CancellationToken ct = default)
                => Task.FromResult(new 운영체제업무인계Dto());
        }

        private sealed class r75작업자비활성Policy : ISsalddelBackgroundJobActivationPolicy
        {
            public SsalddelBackgroundWorkloadActivation Evaluate(string workloadKey)
                => new(false, SsalddelBackgroundWorkloadActivationCodes.OperationalModeRequired, "DomesticTransportWorkflow");
        }

        private sealed class r75시험동의Service : I신청개인정보동의증적Service
        {
            public Task 유효한동의요구Async(Guid? id, string work, string source, string user, CancellationToken ct = default)
            {
                Assert.NotNull(id);
                Assert.Equal(신청개인정보업무Codes.운송대행, work);
                Assert.Equal(NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, source);
                Assert.Equal("r75-synthetic-shipper", user);
                return Task.CompletedTask;
            }
            public Task<신청개인정보동의증적Response> 동의기록Async(신청개인정보동의기록Request request, string user, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<신청개인정보동의증적Response?> 내증적조회Async(Guid id, string user, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<신청개인정보동의증적Response> 철회Async(Guid id, 신청개인정보동의철회Request request, string user, CancellationToken ct = default) => throw new NotSupportedException();
        }

        private sealed class r75생활배송Sender(의뢰생성CommandHandler create,
            화주운송의뢰현장지급처리CommandHandler onsite, 의뢰단건조회QueryHandler read) : ISender
        {
            public async Task<T> Send<T>(IRequest<T> request, CancellationToken ct = default)
            {
                object? result = request switch
                {
                    의뢰생성Command command => (object)await create.Handle(command, ct),
                    화주운송의뢰현장지급처리Command command => (object)await onsite.Handle(command, ct),
                    의뢰단건조회Query query => (object?)await read.Handle(query, ct),
                    _ => throw new NotSupportedException(request.GetType().Name)
                };
                return (T)result!;
            }
            public Task Send<T>(T request, CancellationToken ct = default) where T : IRequest => throw new NotSupportedException();
            public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
            public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request, CancellationToken ct = default) => throw new NotSupportedException();
            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        }
    }
}
