using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OrdererApp.Services;
using RestaurantDeskApp.Services;
using FDriverApp.Services;
using SsalddelAdminApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

var baseUrl = RequireEnvironment("FOOD_OBSERVER_BASE_URL");
var password = RequireEnvironment("FOOD_OBSERVER_ACCOUNT_PASSWORD");
// 검증 전용 합성 주소에는 법정동 결속 근거가 없으므로 실제 면목동으로 승격하지 않는다.
const string observationAreaStableId = "region:kr:bjd:unclassified";
var endpoint = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

using var ordererHttp = CreateHttpClient(endpoint);
using var restaurantHttp = CreateHttpClient(endpoint);
using var driverHttp = CreateHttpClient(endpoint);
using var adminHttp = CreateHttpClient(endpoint);

var guard = new ClientSessionGuard();
var ordererSession = new ClientAuthSession(new MemoryTokenStore(), guard);
var ordererAuth = new OrdererAuthApiService(ordererHttp, ordererSession);
Require((await ordererAuth.로그인Async("food-observer-customer", password)).성공, "주문자 앱 Client 로그인 실패");
await using var encryption = new SsalddelIsmsPClientEncryptionService(new NoopJsRuntime());
var ordererApi = new 주문자음식주문Client(new SsalddelJsonApiClient(
    new SsalddelProtectedApiClient(ordererHttp, encryption, new SessionTokenProvider(ordererSession))));
var unityModule = new OperationalOsObservationModule(new FoodDeliveryOsObservationAdapter());
var unitySession = new OperationalOsWorldObservationSession(
    new OperationalWorldSceneClient(
        new HttpOperationalProjectionTransport(ordererHttp, () => ordererSession.AccessToken),
        new SystemTextJsonOperationalWorldSceneDecoder(),
        new OperationalWorldSceneInterpreter(),
        OperationalWorldScenePolicy.SchemaVersionV2),
    new OperationalOsObservationRouter(new OperationalOsModuleRegistry(new[] { unityModule })));

var restaurantSession = new ClientAuthSession(new MemoryTokenStore(), guard);
var restaurantAuth = new RestaurantAuthService(restaurantHttp, restaurantSession);
Require((await restaurantAuth.LoginAsync("food-observer-restaurant", password)).IsSuccess, "음식점 앱 Client 로그인 실패");
var restaurantApi = new Ssalddel음식주문Client(restaurantHttp, restaurantAuth, restaurantSession);
var sampleMenu = await EnsureSyntheticMenuAsync(restaurantHttp, restaurantSession.AccessToken);
var restaurantId = sampleMenu.음식점Id;
var menuId = sampleMenu.Id;

var driverSession = new MemoryFDriverSession(guard);
var driverAuth = new FDriverAuthApiService(driverHttp, driverSession);
Require(await driverAuth.LoginAsync("food-observer-driver-near", password) is null, "기사 앱 Client 로그인 실패");
var driverApi = new FoodDeliveryDriverApiService(driverHttp, driverSession, driverAuth);
var adminSession = new AdminAuthSession();
var adminAuth = new AdminAuthService(adminHttp, adminSession);
Require((await adminAuth.LoginAsync("food-observer-admin", password)).Succeeded, "운영자 앱 Client 로그인 실패");
var adminApi = new AdminAuthenticatedApiClient(adminHttp, adminSession, adminAuth);
if (args.Length == 2 && args[0] == "--confirm-ui-receipt")
{
    var uiOrderNo = args[1];
    var uiDelivered = await ordererApi.상세Async(uiOrderNo)
        ?? throw new InvalidOperationException("Android 표본 주문 재조회 없음");
    Require(uiDelivered.주문.음식점Id == restaurantId, "Android 표본 음식점 불일치");
    Require(uiDelivered.주문.상태 == 음식주문상태코드.전달완료, "Android 전달 완료가 서버에 기록되지 않음");
    await ordererApi.수령확인Async(uiOrderNo, new 주문자음식주문수령확인요청
    {
        클라이언트요청Id = Guid.NewGuid(), 확인메모 = "Android 화면 검증 뒤 합성 주문 수령 확인"
    });
    var uiCompleted = await ordererApi.상세Async(uiOrderNo)
        ?? throw new InvalidOperationException("Android 표본 수령 확인 재조회 없음");
    Require(uiCompleted.주문.상태 == 음식주문상태코드.수령확인, "Android 표본 종료 상태 불일치");
    var uiTrace = await ReadAdminTraceAsync(adminApi, uiOrderNo,
        음식주문상태코드.수령확인, 음식배달운영생명주기단계Codes.종료);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        schemaVersion = "role-app-headless-e2e.ui-receipt.r1", status = "Completed",
        orderNo = uiOrderNo, orderStatus = uiCompleted.주문.상태,
        lifecycleStage = uiTrace.생명주기조화.현재단계Code,
        serverRuntimeProof = true, databaseRoundTripProof = true, deviceUiProof = false
    }));
    return;
}
var adminLifecycleStages = new List<string>();
var projectionBaseline = await ReadProjectionIdsAsync(unitySession, observationAreaStableId);
await driverApi.StartWorkAsync("사가정 합성 기사 대기점");
await driverApi.UpdateLocationAsync(new 기사위치갱신요청
{
    AppKey = "FoodDeliveryDriverApp",
    위도 = 37.588m,
    경도 = 127.084m,
    정확도_m = 1,
    상차접근허용반경Km = 5,
    운행상태 = "운행중",
    기록시각 = DateTime.UtcNow
});

var created = await ordererApi.등록Async(new 음식주문등록요청
{
    클라이언트요청Id = Guid.NewGuid(),
    음식점Id = restaurantId,
    수령인정보 = new 음식주문수령인정보Dto
    {
        수령인명 = "합성 주문자",
        연락처 = "000-0000-0000",
        주소 = "검증 표본 주택",
        주문자본인수령여부 = true
    },
    상품목록 = [new 음식주문상품Dto { 메뉴Id = menuId, 수량 = 1 }]
});
RequireAction(created.AvailableActions, 음식배달가능행동Ids.주문취소, "주문자 주문대기");
adminLifecycleStages.Add((await ReadAdminTraceAsync(
    adminApi,
    created.주문번호,
    음식주문상태코드.주문대기,
    음식배달운영생명주기단계Codes.음식점응답대기)).생명주기조화.현재단계Code);
var projectionWorkId = (await ReadActiveProjectionAsync(
    unitySession,
    observationAreaStableId,
    음식주문상태코드.주문대기,
    excludedWorkIds: projectionBaseline)).WorkStableId;

// Android 검증은 같은 앱 Client로 주문만 준비하고 실제 화면에서 이후 행동을 수행한다.
if (args.Contains("--prepare-ui", StringComparer.Ordinal))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        schemaVersion = "role-app-headless-e2e.ui-preparation.r1",
        status = "Prepared",
        orderNo = created.주문번호,
        restaurantId,
        menuId,
        workStableId = projectionWorkId,
        synthetic = true,
        deviceUiProof = false
    }));
    return;
}

var inbox = await restaurantApi.주문목록조회Async(new 음식점주문수신함조회요청 { Page = 1, PageSize = 20 });
Require(inbox.Items.Any(x => x.주문번호 == created.주문번호), "음식점 앱 Client 수신함에 주문 없음");
var accepted = await restaurantApi.음식점수락Async(created.주문번호, new 음식점주문수락요청
{
    클라이언트요청Id = Guid.NewGuid(),
    음식점명 = "관찰 검증 음식점",
    음식점주소 = "검증 표본 음식점",
    음식점위도 = 37.588m,
    음식점경도 = 127.085m,
    조리예상분 = 1
}) ?? throw new InvalidOperationException("음식점 앱 Client 수락 응답 없음");
Require(accepted.상태 == 음식주문상태코드.주문확인, "음식점 확인 뒤 배차 대기 상태 불일치");
Require(!업무가능행동목록.포함(accepted.AvailableActions, 음식배달가능행동Ids.음식점조리시작)
    && !업무가능행동목록.포함(accepted.AvailableActions, 음식배달가능행동Ids.음식점픽업준비완료),
    "기사 배정 전에 조리 또는 준비 완료 행동이 열렸음");
adminLifecycleStages.Add((await ReadAdminTraceAsync(
    adminApi,
    created.주문번호,
    음식주문상태코드.주문확인,
    음식배달운영생명주기단계Codes.기사확보대기)).생명주기조화.현재단계Code);
RequireSameProjection(await ReadActiveProjectionAsync(
    unitySession,
    observationAreaStableId,
    음식주문상태코드.주문확인,
    expectedWorkStableId: projectionWorkId), projectionWorkId);

var recommendation = await PollAsync(
    async () => (await driverApi.GetWorkspaceAsync()).Recommendations.SingleOrDefault(x => x.OfferId == created.주문번호),
    value => value is not null,
    TimeSpan.FromSeconds(30),
    "기사 앱 Client 추천 대기 초과");
RequireAction(recommendation!.AvailableActions, 음식배달가능행동Ids.기사제안수락, "기사 추천");
const string routeFixtureSource = "FoodObserverSimulationRouteFixture";
Require(recommendation.PricingPolicyRevision.Contains($"distance:{routeFixtureSource}:r1", StringComparison.Ordinal),
    "격리 검증의 명시적 합성 거리 판본 누락");
await driverApi.AcceptAsync(created.주문번호);
adminLifecycleStages.Add((await ReadAdminTraceAsync(
    adminApi,
    created.주문번호,
    음식주문상태코드.기사배정,
    음식배달운영생명주기단계Codes.픽업인계)).생명주기조화.현재단계Code);
RequireSameProjection(await ReadActiveProjectionAsync(
    unitySession,
    observationAreaStableId,
    음식주문상태코드.기사배정,
    expectedWorkStableId: projectionWorkId), projectionWorkId);

var restaurantCurrent = await restaurantApi.주문상세조회Async(created.주문번호)
    ?? throw new InvalidOperationException("음식점 앱 Client 기사 수락 뒤 재조회 없음");
RequireAction(restaurantCurrent.AvailableActions, 음식배달가능행동Ids.음식점조리시작, "기사 배정 뒤 조리 대기");
var cooking = await restaurantApi.음식점진행변경Async(created.주문번호, new 음식점주문진행변경요청
{
    클라이언트요청Id = Guid.NewGuid(),
    예상Revision = restaurantCurrent.Revision,
    작업 = 음식점주문진행작업코드.조리시작
}) ?? throw new InvalidOperationException("음식점 앱 Client 조리 시작 응답 없음");
Require(cooking.조리시작시각Utc is not null, "조리 시작 시각 미기록");
RequireAction(cooking.AvailableActions, 음식배달가능행동Ids.음식점픽업준비완료, "조리 시작 뒤 준비 완료");
adminLifecycleStages.Add((await ReadAdminTraceAsync(
    adminApi, created.주문번호, 음식주문상태코드.조리중,
    음식배달운영생명주기단계Codes.조리배차병행)).생명주기조화.현재단계Code);
RequireSameProjection(await ReadActiveProjectionAsync(
    unitySession, observationAreaStableId, 음식주문상태코드.조리중,
    expectedWorkStableId: projectionWorkId), projectionWorkId);

var ready = await restaurantApi.음식점진행변경Async(created.주문번호, new 음식점주문진행변경요청
{
    클라이언트요청Id = Guid.NewGuid(),
    예상Revision = cooking.Revision,
    작업 = 음식점주문진행작업코드.픽업준비
}) ?? throw new InvalidOperationException("음식점 앱 Client 픽업 준비 응답 없음");
Require(
    ready.상태 is 음식주문상태코드.픽업대기 or 음식주문상태코드.기사배정,
    "음식점 앱 Client 픽업 준비 상태 불일치");
var readyTrace = await ReadAdminTraceAsync(
    adminApi,
    created.주문번호,
    ready.상태,
    음식배달운영생명주기단계Codes.픽업인계);
RequireSameProjection(await ReadActiveProjectionAsync(
    unitySession, observationAreaStableId, ready.상태,
    expectedWorkStableId: projectionWorkId), projectionWorkId);
Require(
    readyTrace.생명주기조화.현재책임주체Codes.Contains(
        음식배달운영책임주체Codes.음식배달기사,
        StringComparer.Ordinal),
    "픽업 인계 단계의 기사 책임 주체 누락");

await driverApi.ConfirmPickupAsync(created.주문번호);
adminLifecycleStages.Add((await ReadAdminTraceAsync(
    adminApi,
    created.주문번호,
    음식주문상태코드.픽업완료,
    음식배달운영생명주기단계Codes.배송)).생명주기조화.현재단계Code);
RequireSameProjection(await ReadActiveProjectionAsync(
    unitySession,
    observationAreaStableId,
    음식주문상태코드.픽업완료,
    expectedWorkStableId: projectionWorkId), projectionWorkId);
var pickup = await driverApi.GetWorkspaceAsync();
RequireAction(
    pickup.ActiveDeliveries.Single(x => x.OfferId == created.주문번호).AvailableActions,
    음식배달가능행동Ids.기사전달완료,
    "기사 픽업완료");
await driverApi.CompleteAsync(created.주문번호);
var awaitingReceiptSettlement = (await driverApi.GetWorkspaceAsync()).OrderSettlements
    .Single(x => x.OrderNo == created.주문번호);
Require(awaitingReceiptSettlement.SettlementStatusCode == "AwaitingReceipt"
    && awaitingReceiptSettlement.GrossAmount == recommendation.DriverPayout
    && awaitingReceiptSettlement.NetAmount is null
    && !awaitingReceiptSettlement.IsActualTransferCompleted,
    "전달 완료 정산의 동결 금액 또는 수령 확인 대기 불일치");
adminLifecycleStages.Add((await ReadAdminTraceAsync(
    adminApi,
    created.주문번호,
    음식주문상태코드.전달완료,
    음식배달운영생명주기단계Codes.수령확인대기)).생명주기조화.현재단계Code);
RequireSameProjection(await ReadActiveProjectionAsync(
    unitySession,
    observationAreaStableId,
    음식주문상태코드.전달완료,
    expectedWorkStableId: projectionWorkId), projectionWorkId);

var delivered = await ordererApi.상세Async(created.주문번호)
    ?? throw new InvalidOperationException("주문자 앱 Client 전달완료 재조회 없음");
RequireAction(delivered.AvailableActions, 음식배달가능행동Ids.주문수령확인, "주문자 전달완료");
await ordererApi.수령확인Async(created.주문번호, new 주문자음식주문수령확인요청
{
    클라이언트요청Id = Guid.NewGuid(),
    확인메모 = "Windows 역할 앱 headless 검증 수령"
});
var completed = await ordererApi.상세Async(created.주문번호)
    ?? throw new InvalidOperationException("주문자 앱 Client 완료 재조회 없음");
Require(completed.주문.상태 == 음식주문상태코드.수령확인, "완료 상태 불일치");
Require(completed.AvailableActions.Count == 0, "완료 뒤 주문자 행동 목록이 비어 있지 않음");
var completedTrace = await ReadAdminTraceAsync(
    adminApi,
    created.주문번호,
    음식주문상태코드.수령확인,
    음식배달운영생명주기단계Codes.종료);
adminLifecycleStages.Add(completedTrace.생명주기조화.현재단계Code);
Require(completedTrace.전체상태 == 음식주문운영추적상태코드.완료, "운영자 앱 완료 판정 불일치");
var settlementBeforePayout = await PollAsync(
    async () => (await adminApi.GetAsync<음식주문운영추적응답>(
        $"api/v1/admin/food-orders/{created.주문번호}/operations-trace")).DriverSettlement,
    value => value?.ReceiptConfirmedAtUtc is not null,
    TimeSpan.FromSeconds(20), "수령 확인 정산 투영 대기 초과");
Require(settlementBeforePayout!.SettlementStatusCode == "AwaitingDeductions"
    && settlementBeforePayout.DeductionAmount is null && settlementBeforePayout.NetAmount is null,
    "공제 근거 없이 수령액을 확정함");
var payoutPath = $"api/v1/admin/food-orders/{created.주문번호}/simulate-driver-payout";
using (var anonymousHttp = CreateHttpClient(endpoint))
using (var anonymousResponse = await anonymousHttp.PostAsJsonAsync(payoutPath, new FoodDeliverySimulatedPayoutRequest()))
{
    Require(anonymousResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized, "익명 모의 지급 요청이 차단되지 않음");
}
using (var forbiddenRequest = new HttpRequestMessage(HttpMethod.Post, payoutPath))
{
    forbiddenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ordererSession.AccessToken);
    forbiddenRequest.Content = JsonContent.Create(new FoodDeliverySimulatedPayoutRequest());
    using var forbiddenResponse = await ordererHttp.SendAsync(forbiddenRequest);
    Require(forbiddenResponse.StatusCode == System.Net.HttpStatusCode.Forbidden, "주문자 모의 지급 요청이 차단되지 않음");
}
try
{
    await adminApi.PostAsync<FoodDeliverySimulatedPayoutRequest, FoodDeliveryOrderSettlementDto>(payoutPath, new()
    {
        IdempotencyKey = Guid.NewGuid().ToString("N"), ExpectedSettlementRevision = settlementBeforePayout.Revision,
        ConfirmedGrossAmount = settlementBeforePayout.GrossAmount!.Value
    });
    throw new InvalidOperationException("공제 근거 없는 모의 지급이 허용됨");
}
catch (SsalddelAdminApp.Services.AdminApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict) { }
// 아래 100원은 이 실행 전용 시험 입력이며 법정 공제액을 계산하거나 확정한 값이 아니다.
var failedPayout = await adminApi.PostAsync<FoodDeliverySimulatedPayoutRequest, FoodDeliveryOrderSettlementDto>(payoutPath, new()
{
    IdempotencyKey = Guid.NewGuid().ToString("N"), ExpectedSettlementRevision = settlementBeforePayout.Revision,
    ConfirmedGrossAmount = settlementBeforePayout.GrossAmount!.Value, ConfirmedDeductionAmount = 100m,
    DeductionEvidenceReference = $"role-app-headless-e2e:{created.주문번호}:simulation-fixture", OutcomeCode = "Failed"
});
Require(failedPayout.PayoutStatusCode == "SimulationFailed" && !failedPayout.IsActualTransferCompleted,
    "모의 지급 실패를 실입금 완료로 표시함");
var retryRequest = new FoodDeliverySimulatedPayoutRequest
{
    IdempotencyKey = Guid.NewGuid().ToString("N"), ExpectedSettlementRevision = failedPayout.Revision,
    ConfirmedGrossAmount = failedPayout.GrossAmount!.Value, ConfirmedDeductionAmount = 100m,
    DeductionEvidenceReference = failedPayout.DeductionEvidenceReference
};
var successfulPayout = await adminApi.PostAsync<FoodDeliverySimulatedPayoutRequest, FoodDeliveryOrderSettlementDto>(payoutPath, retryRequest);
var replayPayout = await adminApi.PostAsync<FoodDeliverySimulatedPayoutRequest, FoodDeliveryOrderSettlementDto>(payoutPath, retryRequest);
var driverSettlement = (await driverApi.GetWorkspaceAsync()).OrderSettlements.Single(x => x.OrderNo == created.주문번호);
var adminSettlement = (await adminApi.GetAsync<음식주문운영추적응답>(
    $"api/v1/admin/food-orders/{created.주문번호}/operations-trace")).DriverSettlement;
Require(successfulPayout.PayoutStatusCode == "SimulationSucceeded"
    && replayPayout.IsIdempotentReplay
    && successfulPayout.SimulationPaymentId == replayPayout.SimulationPaymentId
    && driverSettlement.SimulationPayments.Count == 2
    && adminSettlement?.SimulationPaymentId == driverSettlement.SimulationPaymentId
    && driverSettlement.NetAmount == recommendation.DriverPayout - 100m
    && driverSettlement.ServerExecutionModeCode == "Simulation"
    && adminSettlement.ServerExecutionModeCode == "Simulation"
    && !driverSettlement.IsActualTransferCompleted && !adminSettlement.IsActualTransferCompleted,
    "모의 지급 재시도·멱등성 또는 기사/관리자 재조회 불일치");
var finalScene = await unitySession.RefreshAsync(observationAreaStableId, 0, DateTime.UtcNow);
Require(finalScene.SceneResult.Accepted, "Unity Interpreter가 완료 뒤 장면 사본을 거절함");
Require(finalScene.OsObservationResult.Accepted, "Unity OS Router가 완료 뒤 장면 사본을 거절함");
Require(
    finalScene.OsObservationResult.CurrentStates.All(item => item.WorkStableId != projectionWorkId),
    "수령 확인 뒤 Unity 메모리에서 진행 중 지역 투영이 제거되지 않음");
await driverApi.StopWorkAsync();

Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
{
    schemaVersion = "role-app-headless-e2e.r4",
    status = "Completed",
    orderNo = created.주문번호,
    orderStatus = completed.주문.상태,
    operationalProjection = new
    {
        schemaVersion = OperationalWorldScenePolicy.SchemaVersionV2,
        workStableId = projectionWorkId,
        stagesVerified = new[]
        {
            음식주문상태코드.주문대기,
            음식주문상태코드.주문확인,
            음식주문상태코드.기사배정,
            음식주문상태코드.조리중,
            음식주문상태코드.픽업대기,
            음식주문상태코드.픽업완료,
            음식주문상태코드.전달완료
        },
        removedAfterReceiptConfirmation = true,
        containsOperationalIdentifiers = false,
        unityClientInterpreterRouterApplied = true
    },
    osLifecycles = new
    {
        order = new[] { 음식주문상태코드.주문대기, 음식주문상태코드.전달완료, 음식주문상태코드.수령확인 },
        restaurantSales = new[] { 음식주문상태코드.주문확인, cooking.상태, ready.상태 },
        foodDelivery = new[] { 음식주문상태코드.기사배정, 음식주문상태코드.픽업완료, 음식주문상태코드.전달완료 },
        platformOperations = adminLifecycleStages
    },
    clients = new[]
    {
        "OrdererApp.SharedClient",
        "RestaurantDeskApp.Client",
        "FDriverApp.Client",
        "SsalddelAdminApp.AdminAuthenticatedApiClient"
    },
    sampleData = new { restaurantId, menuId, synthetic = true },
    routePricing = new
    {
        sourceCode = routeFixtureSource, fixtureId = "observer-restaurant-to-customer.r1",
        fixtureInputDistanceKm = 0.250m, actualMapRequest = false, isEstimated = true,
        pricingPolicyRevision = recommendation.PricingPolicyRevision
    },
    driverBeforeCookingVerified = true,
    orderSettlement = new
    {
        grossAmount = driverSettlement.GrossAmount, simulatedDeductionAmount = driverSettlement.DeductionAmount,
        simulatedNetAmount = driverSettlement.NetAmount, missingDeductionsBlocked = true,
        failedPayoutThenRetryVerified = true, idempotentReplayVerified = true,
        driverAdminReadbackVerified = true, actualTransferCompleted = false,
        anonymousAndNonAdminBlocked = true,
        currentServerMode = driverSettlement.ServerExecutionModeCode,
        deductionEvidenceScope = driverSettlement.DeductionEvidenceScopeCode
    },
    serverRuntimeProof = true,
    databaseRoundTripProof = true,
    deviceUiProof = false
}));

static HttpClient CreateHttpClient(Uri endpoint) => new() { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(20) };
static string RequireEnvironment(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"환경 변수 누락: {name}");
static void Require(bool valid, string message)
{
    if (!valid) throw new InvalidOperationException(message);
}
static void RequireAction(IReadOnlyList<업무가능행동Dto> actions, string actionId, string state)
    => Require(업무가능행동목록.포함(actions, actionId), $"{state} 행동 누락: {actionId}");
static async Task<음식점메뉴관리응답> EnsureSyntheticMenuAsync(
    HttpClient http,
    string? accessToken)
{
    const string binding = "role-app-headless-e2e:synthetic-menu:v2";
    Require(!string.IsNullOrWhiteSpace(accessToken), "음식점 메뉴 준비용 접근 토큰 누락");
    using var listRequest = new HttpRequestMessage(HttpMethod.Get, "api/v1/restaurant/menus");
    listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    using var listResponse = await http.SendAsync(listRequest);
    listResponse.EnsureSuccessStatusCode();
    var menus = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<음식점메뉴관리응답>>() ?? [];
    var existing = menus.FirstOrDefault(item => string.Equals(item.설명, binding, StringComparison.Ordinal));
    if (existing is not null)
    {
        return existing;
    }

    using var createRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/restaurant/menus")
    {
        Content = JsonContent.Create(new 음식점메뉴등록요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            메뉴명 = "역할 앱 E2E 합성 한상",
            설명 = binding,
            판매가 = 9000,
            공개여부 = true,
            품절여부 = false,
            표시순서 = 900
        })
    };
    createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    using var createResponse = await http.SendAsync(createRequest);
    createResponse.EnsureSuccessStatusCode();
    return await createResponse.Content.ReadFromJsonAsync<음식점메뉴관리응답>()
           ?? throw new InvalidOperationException("합성 메뉴 등록 응답이 비어 있습니다.");
}
static async Task<음식주문운영추적응답> ReadAdminTraceAsync(
    AdminAuthenticatedApiClient adminApi,
    string orderNo,
    string expectedOrderState,
    string expectedLifecycleStage)
{
    var trace = await PollAsync(
        async () => (음식주문운영추적응답?)await adminApi.GetAsync<음식주문운영추적응답>(
            $"api/v1/admin/food-orders/{Uri.EscapeDataString(orderNo)}/operations-trace"),
        value => value is not null
                 && value.주문상태 == expectedOrderState
                 && value.생명주기조화.현재단계Code == expectedLifecycleStage,
        TimeSpan.FromSeconds(15),
        $"운영자 생명주기 사본 대기 초과: {expectedOrderState}/{expectedLifecycleStage}")
        ?? throw new InvalidOperationException("운영자 생명주기 사본 없음");
    Require(trace.주문번호 == orderNo, "운영자 추적 주문번호 불일치");
    Require(trace.생명주기조화.정상경로조화여부, "정상 표본 생명주기 조화 실패");
    Require(!trace.생명주기조화.운영자확인필요여부, "정상 표본에 운영자 확인 필요 판정 발생");
    Require(trace.생명주기조화.기술이상Codes.Count == 0, "정상 표본에 기술 이상 발생");
    return trace;
}
static void RequireSameProjection(OperationalOsObservationState item, string expectedWorkStableId)
{
    Require(item.WorkStableId == expectedWorkStableId, "운영 지역 투영 가명이 단계 사이에 변경됨");
    Require(item.Revision > 0, "운영 지역 투영 revision 누락");
    Require(item.RepresentationDataJson.Contains("\"personalDataIncluded\":false", StringComparison.Ordinal), "개인정보 제외 표식 누락");
    Require(item.RepresentationDataJson.Contains("\"exactLocationIncluded\":false", StringComparison.Ordinal), "정확 위치 제외 표식 누락");
    Require(item.RepresentationDataJson.Contains("projectionHashSha256", StringComparison.Ordinal), "투영 hash 누락");
}
static async Task<OperationalOsObservationState> ReadActiveProjectionAsync(
    OperationalOsWorldObservationSession session,
    string areaStableId,
    string expectedStage,
    IReadOnlySet<string>? excludedWorkIds = null,
    string? expectedWorkStableId = null)
{
    var applied = await PollAsync(
        async () => (OperationalOsWorldObservationRefreshResult?)await session.RefreshAsync(
            areaStableId,
            0,
            DateTime.UtcNow),
        value => value?.OsObservationResult.CurrentStates.Any(item =>
            item.OperatingSystemId == OperationalWorldOperatingSystemIds.FoodDelivery
            && item.LifecycleStageId == expectedStage
            && (expectedWorkStableId is null || item.WorkStableId == expectedWorkStableId)
            && (excludedWorkIds is null || !excludedWorkIds.Contains(item.WorkStableId))) == true,
        TimeSpan.FromSeconds(15),
        $"진행 중 지역 투영 단계 대기 초과: {expectedStage}")
        ?? throw new InvalidOperationException("Unity 운영 지역 장면 적용 결과 없음");
    Require(applied.SceneResult.Accepted, "Unity Interpreter가 진행 사본을 거절함");
    Require(applied.OsObservationResult.Accepted, "Unity OS Router가 진행 사본을 거절함");
    Require(applied.SceneResult.Diagnostics.Length == 0, "Unity Interpreter 진행 사본 진단 발생");
    Require(applied.OsObservationResult.Diagnostics.Length == 0, "Unity OS Router 진행 사본 진단 발생");
    var candidates = applied.OsObservationResult.CurrentStates.Where(state =>
            state.OperatingSystemId == OperationalWorldOperatingSystemIds.FoodDelivery
            && state.LifecycleStageId == expectedStage
            && (expectedWorkStableId is null || state.WorkStableId == expectedWorkStableId)
            && (excludedWorkIds is null || !excludedWorkIds.Contains(state.WorkStableId)))
        .ToArray();
    Require(candidates.Length == 1, $"대상 운영 지역 투영을 하나로 식별하지 못함: {expectedStage}");
    var item = candidates[0];
    RequireSameProjection(item, item.WorkStableId);
    return item;
}
static async Task<IReadOnlySet<string>> ReadProjectionIdsAsync(
    OperationalOsWorldObservationSession session,
    string areaStableId)
{
    var result = await session.RefreshAsync(areaStableId, 0, DateTime.UtcNow);
    Require(result.SceneResult.Accepted, "Unity Interpreter가 기준선 사본을 거절함");
    Require(result.OsObservationResult.Accepted, "Unity OS Router가 기준선 사본을 거절함");
    return result.OsObservationResult.CurrentStates
        .Where(item => item.OperatingSystemId == OperationalWorldOperatingSystemIds.FoodDelivery)
        .Select(item => item.WorkStableId)
        .ToHashSet(StringComparer.Ordinal);
}
static async Task<T?> PollAsync<T>(Func<Task<T?>> read, Func<T?, bool> ready, TimeSpan timeout, string error)
{
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        var value = await read();
        if (ready(value)) return value;
        await Task.Delay(500);
    }
    throw new TimeoutException(error);
}

sealed class MemoryTokenStore : IClientSecureTokenStore
{
    private ClientAuthTokenSnapshot? _value;
    public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_value);
    public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) { _value = snapshot; return Task.CompletedTask; }
    public Task ClearAsync(CancellationToken cancellationToken = default) { _value = null; return Task.CompletedTask; }
}

sealed class SessionTokenProvider(ClientAuthSession session) : ISsalddelAccessTokenProvider
{
    public string? AccessToken => session.AccessToken;
}

namespace FDriverApp.Services
{
    public interface IFDriverAuthSession : ISsalddelAccessTokenProvider
    {
        DateTime AccessTokenExpiresAtUtc { get; }
        string? RefreshToken { get; }
        DateTime RefreshTokenExpiresAtUtc { get; }
        string? UserId { get; }
        string? UserName { get; }
        IReadOnlyList<string> Roles { get; }
        bool IsAuthenticated { get; }
        ClientAuthSessionRestoreState CurrentState { get; }
        Task<ClientAuthSessionRestoreState> RestoreAsync(CancellationToken cancellationToken = default);
        Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}

sealed class MemoryFDriverSession(IClientSessionGuard guard) : IFDriverAuthSession
{
    private ClientAuthTokenSnapshot? _snapshot;
    public string? AccessToken => _snapshot?.AccessToken;
    public DateTime AccessTokenExpiresAtUtc => _snapshot?.AccessTokenExpiresAtUtc ?? default;
    public string? RefreshToken => _snapshot?.RefreshToken;
    public DateTime RefreshTokenExpiresAtUtc => _snapshot?.RefreshTokenExpiresAtUtc ?? default;
    public string? UserId => _snapshot?.UserId;
    public string? UserName => _snapshot?.UserName;
    public IReadOnlyList<string> Roles => _snapshot?.Roles ?? [];
    public ClientAuthSessionRestoreState CurrentState => guard.IsAccessTokenUsable(_snapshot, DateTime.UtcNow)
        ? ClientAuthSessionRestoreState.Authenticated
        : guard.IsRefreshTokenUsable(_snapshot, DateTime.UtcNow)
            ? ClientAuthSessionRestoreState.RefreshRequired
            : ClientAuthSessionRestoreState.Anonymous;
    public bool IsAuthenticated => CurrentState == ClientAuthSessionRestoreState.Authenticated;
    public Task<ClientAuthSessionRestoreState> RestoreAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentState);
    public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) { _snapshot = snapshot; return Task.CompletedTask; }
    public Task ClearAsync(CancellationToken cancellationToken = default) { _snapshot = null; return Task.CompletedTask; }
}

sealed class NoopJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => throw new InvalidOperationException($"headless 검증에서 JavaScript 호출은 허용되지 않습니다: {identifier}");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => throw new InvalidOperationException($"headless 검증에서 JavaScript 호출은 허용되지 않습니다: {identifier}");
}

sealed class HttpOperationalProjectionTransport(
    HttpClient http,
    Func<string?> accessToken) : IOperationalWorldProjectionTransport
{
    public async Task<string?> GetAsync(
        string relativeRoute,
        bool allowNotFound,
        bool requiresAuthentication,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeRoute);
        if (requiresAuthentication)
        {
            var token = accessToken();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Unity 운영 투영 접근 토큰 없음");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        using var response = await http.SendAsync(request, cancellationToken);
        if (allowNotFound && response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

sealed class SystemTextJsonOperationalWorldSceneDecoder : IOperationalWorldSceneDecoder
{
    private static readonly System.Text.Json.JsonSerializerOptions Options =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    public OperationalWorldSceneResponse Decode(string json)
        => System.Text.Json.JsonSerializer.Deserialize<OperationalWorldSceneResponse>(json, Options)
           ?? throw new FormatException("OperationalWorldSceneJsonInvalid");
}
