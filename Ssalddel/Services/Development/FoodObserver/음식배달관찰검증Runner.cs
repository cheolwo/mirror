using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.기사;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Services.Development.FoodObserver;

/// <summary>격리된 표본만 정상 역할 API로 실행한다. 조회 사본은 업무 권위가 아니다.</summary>
public sealed class 음식배달관찰검증Runner(
    IServiceScopeFactory scopes, IHttpClientFactory clients, 음식배달관찰검증Options options,
    ILogger<음식배달관찰검증Runner> logger) : BackgroundService
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _start = new(0, 1);
    private readonly Stopwatch _clock = new();
    private readonly List<음식배달관찰Event> _events = [];
    private readonly Dictionary<string, HttpClient> _roles = [];
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private 음식배달관찰Actor[] _actors = InitialActors();
    private string _status = "Idle", _runId = "", _orderNo = "", _orderStatus = "", _dispatchStatus = "";
    private string _message = "검증 DB 준비 중";
    private long _revision;
    private bool _ready;
    private bool _executedThisHost;
    private double _restoredElapsed;
    private long _restaurantId, _menuId;
    private 음식자료사본 _menuData = null!;
    private string _menuHash = "";
    private readonly Dictionary<long, long> _menuBindings = [];
    private 음식자료선택결과? _menuSelection;
    private static string UserId(string id) => "food-observer-" + id;
    public static Ssalddel.Contracts.Driver.Work.기사운행시작요청 운행시작입력() => new()
        { 시작모드 = "바로시작", 시작위치 = "검증 표본 대기점", 커뮤니티운행공개 = false };
    public bool IsReady { get { lock (_gate) return _ready && _status != "Failed"; } }

    public 음식배달관찰Snapshot Read()
    {
        lock (_gate) return new("food-delivery-observer.r1", _runId, _revision, _status,
            _restoredElapsed + _clock.Elapsed.TotalSeconds, options.DurationSeconds, _orderNo, _orderStatus, _dispatchStatus,
            _message, _actors.ToArray(), _events.ToArray(), _menuSelection);
    }

    public bool Start()
    {
        lock (_gate)
        {
            // 새 실행은 기존 진행/오류를 자동 초기화하지 않는다. 재실행은 컨테이너 재기동 후 명시한다.
            if (!_ready || _executedThisHost || _status is not ("Idle" or "Completed")) return false;
            _executedThisHost = true;
            _restoredElapsed = 0;
            _actors = InitialActors();
            _events.Clear();
            _menuSelection = null;
            _orderNo = _orderStatus = _dispatchStatus = "";
            _status = "Running";
            _runId = Guid.NewGuid().ToString("N");
            _message = "정상 업무 API 실행 시작 · 합성 데이터 / 외부 효과 차단";
            _revision++;
            _clock.Restart();
            _start.Release();
            return true;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            (_menuData, _menuHash) = 음식자료선택Policy.Read(options.MenuSnapshotPath);
            await SeedAsync(stoppingToken);
            await RestoreResultAsync(stoppingToken);
            lock (_gate) { _ready = true; if (_status == "Idle") _message = "격리 DB 준비됨 · 명시적으로 5분 검증을 시작하세요"; _revision++; }
            await _start.WaitAsync(stoppingToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.DurationSeconds - _clock.Elapsed.TotalSeconds)));
            await WorkflowAsync(deadline.Token);
            // 조기 완료 뒤에도 정해진 관찰 시간 동안 동일 주문을 재조회한다.
            while (_clock.Elapsed.TotalSeconds < options.DurationSeconds - 1)
            {
                await RequeryAsync(deadline.Token);
                await Task.Delay(1000, deadline.Token);
            }
            lock (_gate) { _status = "Completed"; _message = "5분 검증 종료 · 수령 확인/역할 재조회 완료 · 실제 영업 증거 아님"; }
        }
        catch (메뉴대기Exception)
        {
            lock (_gate) { _status = "Waiting"; _message = _menuSelection!.Reason; }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            lock (_gate) { _status = "TimedOut"; _message = "검증 시간 종료 · 미완료 단계를 확인하세요. 자동 연장하지 않습니다."; }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            lock (_gate) { if (_status == "Running") { _status = "Failed"; _message = "호스트 종료로 검증 중단 · 자동 재개하지 않습니다."; } }
        }
        catch (Exception ex)
        {
            // HTTP 본문/자격 증명을 화면 또는 로그에 출력하지 않는다.
            logger.LogError("Food observer failed: {ExceptionType}", ex.GetType().Name);
            lock (_gate) { _status = "Failed"; _message = ex is 검증실패Exception ? ex.Message : "검증 실패: " + ex.GetType().Name; }
        }
        finally
        {
            _clock.Stop();
            lock (_gate) _revision++;
            foreach (var client in _roles.Values) client.Dispose();
            Directory.CreateDirectory("App_Data/food-observer");
            var serialized = JsonSerializer.Serialize(Read(), _json);
            if (!string.IsNullOrEmpty(_runId))
            {
                await File.WriteAllTextAsync("App_Data/food-observer/result.json", serialized, CancellationToken.None);
                await File.WriteAllTextAsync($"App_Data/food-observer/{_runId}.json", serialized, CancellationToken.None);
            }
        }
    }

    private async Task RestoreResultAsync(CancellationToken ct)
    {
        const string path = "App_Data/food-observer/result.json";
        if (!File.Exists(path)) return;
        var prior = JsonSerializer.Deserialize<음식배달관찰Snapshot>(await File.ReadAllTextAsync(path, ct), _json);
        if (prior is null || string.IsNullOrEmpty(prior.OrderNo)) return;
        using var scope = scopes.CreateScope();
        var canonical = scope.ServiceProvider.GetRequiredService<ISsalddelFoodOrderStore>().GetOrder(prior.OrderNo);
        Require(canonical is not null, "저장된 관찰 결과의 주문을 DB에서 찾지 못했습니다.");
        lock (_gate)
        {
            _runId = prior.RunId; _revision = prior.Revision + 1; _restoredElapsed = prior.ElapsedSeconds;
            _orderNo = canonical!.주문번호; _orderStatus = canonical.상태; _dispatchStatus = canonical.배차상태;
            _actors = prior.Actors.ToArray(); _events.AddRange(prior.Events);
            _menuSelection = prior.MenuSelection;
            _status = prior.Status == "Completed" && canonical.상태 == 음식주문상태코드.수령확인 ? "Completed" : "Failed";
            _message = "이전 검증 결과 · 서버 DB 재조회 완료 · NPC 자동 재개 없음";
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        // 오직 전용 DB의 현재 모델 스키마 검사. 운영 DB migration 검증과 구분한다.
        await db.Database.EnsureCreatedAsync(ct);
        await ValidateSchemaAsync(db, ct);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var restaurant = await db.음식점공개프로필.FirstOrDefaultAsync(x => x.상호명 == "관찰 검증 음식점", ct);
        if (restaurant is null)
        {
            restaurant = new 음식점공개프로필 { 상호명 = "관찰 검증 음식점", 공개주소 = 검증표본좌표Service.RestaurantAddress,
                위도 = 37.5880m, 경도 = 127.0850m, 공개여부 = true, 주문가능여부 = true, 예상조리분 = 1 };
            db.음식점공개프로필.Add(restaurant);
            await db.SaveChangesAsync(ct);
        }
        _restaurantId = restaurant.Id;
        foreach (var actor in _actors)
        {
            var role = actor.Role switch { "restaurant" => 역할명.음식점, "driver" => 역할명.기사, _ => 역할명.커뮤니티회원 };
            if (!await roles.RoleExistsAsync(role)) CheckIdentity(await roles.CreateAsync(new IdentityRole(role)));
            var id = UserId(actor.Id);
            var user = await users.FindByIdAsync(id);
            if (user is null)
            {
                user = new ApplicationUser { Id = id, UserName = id, Email = id + "@example.invalid", EmailConfirmed = true };
                CheckIdentity(await users.CreateAsync(user, options.AccountPassword));
            }
            if (!await users.IsInRoleAsync(user, role)) CheckIdentity(await users.AddToRoleAsync(user, role));
            if (actor.Role == "restaurant" && !(await users.GetClaimsAsync(user)).Any(x => x.Type == 음식점접근ClaimTypes.음식점Id))
                CheckIdentity(await users.AddClaimAsync(user, new Claim(음식점접근ClaimTypes.음식점Id, restaurant.Id.ToString())));
            if (actor.Role == "driver" && !await db.용달기사.AnyAsync(x => x.기사Id == id, ct))
                db.용달기사.Add(new 용달기사 { 기사Id = id, 기사명 = actor.Name, 차량 = "검증 오토바이", 주_활동지역 = "서울특별시 중랑구 면목동" });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task ValidateSchemaAsync(SsalddelContext db, CancellationToken ct)
    {
        // MySQL DDL은 원자적이지 않다. 중간 생성 실패 뒤 EnsureCreated=false를 성공으로 숨기지 않는다.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT TABLE_NAME, INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = @database";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@database";
            parameter.Value = 음식배달관찰검증Options.DatabaseName;
            command.Parameters.Add(parameter);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) actual.Add(reader.GetString(0) + "/" + reader.GetString(1));
            foreach (var table in db.Model.GetRelationalModel().Tables)
                foreach (var index in table.Indexes)
                    Require(actual.Contains(table.Name + "/" + index.Name), "검증 DB 생성이 불완전합니다. 누락 인덱스: " + table.Name + "/" + index.Name);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private static void CheckIdentity(IdentityResult result)
    {
        if (!result.Succeeded) throw new 검증실패Exception("검증 계정 구성 실패: " + string.Join(",", result.Errors.Select(x => x.Code)));
    }

    private async Task WorkflowAsync(CancellationToken ct)
    {
        foreach (var actor in _actors)
        {
            var client = clients.CreateClient();
            client.BaseAddress = new Uri("http://127.0.0.1:8080/");
            client.Timeout = TimeSpan.FromSeconds(12);
            _roles.Add(actor.Id, client);
            var token = await PostAsync<토큰응답>(actor.Id, "api/v1/auth/login", new 로그인요청 { UserNameOrEmail = UserId(actor.Id), Password = options.AccountPassword }, ct);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            Record(actor.Id, "정상 로그인", "역할 인증 성공");
        }
        // 메뉴는 음식점 운영자 API로만 만들고 다시 조회한다. 전용 검증 DB의 음식점/계정 seed와 구분한다.
        foreach (var source in _menuData.Menus)
        {
            var binding = $"food-observer:{source.SourceKey}:{source.RecipeId}:{source.Checksum}";
            var created = await PostAsync<음식점메뉴관리응답>("restaurant", "api/v1/restaurant/menus", new 음식점메뉴등록요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                메뉴명 = source.Title,
                설명 = binding,
                판매가 = 5000,
                공개여부 = true
            }, ct);
            _menuBindings[source.RecipeId] = created.Id;
        }
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
            var restaurant = await db.음식점공개프로필.AsNoTracking().SingleAsync(x => x.Id == _restaurantId, ct);
            var menus = await GetAsync<IReadOnlyList<음식점메뉴관리응답>>("restaurant", "api/v1/restaurant/menus", ct);
            var candidates = _menuData.Menus.Select(source => {
                var menu = menus.SingleOrDefault(x => x.Id == _menuBindings[source.RecipeId]);
                return new 음식자료판매후보(source, menu?.Id ?? 0, menu?.판매가 ?? 0,
                    restaurant.공개여부 && restaurant.주문가능여부 && menu is { 공개여부: true, 품절여부: false });
            }).ToArray();
            var choice = 음식자료선택Policy.Select(candidates, _menuHash, options.MealNeeded,
                options.MealBudget, options.PreferredRecipeId, DateOnly.FromDateTime(DateTime.UtcNow));
            lock (_gate) { _menuSelection = choice; _menuId = choice.MenuId; _revision++; }
            Record("customer", "메뉴 선택", choice.Title + " · " + choice.Reason);
            Actor("customer", choice.MenuId > 0 ? "메뉴 선택 완료" : "메뉴 선택 대기", "주문 등록", choice.Reason);
            if (choice.MenuId == 0) throw new 메뉴대기Exception();
        }
        foreach (var id in new[] { "driver-near", "driver-far" })
        {
            await PostAsync<JsonElement>(id, "api/v1/driver/food-deliveries/work/start", 운행시작입력(), ct);
            await LocationAsync(id, id == "driver-near" ? 127.084m : 127.075m, ct);
            Actor(id, "콜 대기", "제안 조회", "음식점 수락 후 서버 추천 대기");
        }
        var register = new 음식주문등록요청 { 클라이언트요청Id = Guid.NewGuid(), 음식점Id = _restaurantId,
            상품목록 = [new 음식주문상품Dto { 메뉴Id = _menuId, 수량 = 1 }],
            수령인정보 = new() { 수령인명 = "합성 주문자", 연락처 = "000-0000-0000", 주소 = 검증표본좌표Service.CustomerAddress, 주문자본인수령여부 = true } };
        var order = await PostAsync<음식주문응답>("customer", "api/v1/food-orders", register, ct);
        RequireAction(order.AvailableActions, 음식배달가능행동Ids.주문취소, "주문자 주문대기");
        lock (_gate) _orderNo = order.주문번호;
        var duplicate = await PostAsync<음식주문응답>("customer", "api/v1/food-orders", register, ct);
        Require(duplicate.주문번호 == _orderNo, "중복 등록이 다른 주문을 만들었습니다.");
        Require(음식자료선택Policy.OrderMatches(_menuSelection!, order.상품목록)
            && 음식자료선택Policy.OrderMatches(_menuSelection!, duplicate.상품목록), "선택 메뉴와 등록 주문 상품이 다릅니다.");
        Record("customer", "자료·주문 결속", $"자료 {_menuSelection!.RecipeId} / 메뉴 {_menuId} / 주문 {_orderNo} / 사본 {_menuHash}");
        Record("customer", "주문 등록", "같은 요청 ID 재전송 / 동일 주문번호 확인");
        Actor("customer", "주문 접수", "수령 확인", "음식점 처리 대기");
        var acceptance = new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 음식점명 = "관찰 검증 음식점",
            음식점주소 = 검증표본좌표Service.RestaurantAddress, 음식점위도 = 37.588m, 음식점경도 = 127.085m, 조리예상분 = 1 };
        await ExpectDeniedAsync("customer", $"api/v1/food-orders/{_orderNo}/restaurant-acceptance", acceptance, ct);
        var accepted = await PostAsync<음식주문응답>("restaurant", $"api/v1/food-orders/{_orderNo}/restaurant-acceptance", acceptance, ct);
        RequireAction(accepted.AvailableActions, 음식배달가능행동Ids.음식점조리시간변경, "음식점 조리중");
        RequireAction(accepted.AvailableActions, 음식배달가능행동Ids.음식점픽업준비완료, "음식점 조리중");
        await PostAsync<음식주문응답>("restaurant", $"api/v1/food-orders/{_orderNo}/restaurant-acceptance", acceptance, ct);
        var readyAt = DateTime.UtcNow.AddMinutes(1);
        Record("restaurant", "수락·조리 시작", "수락 중복 요청 / 동일 배차대기 확인");
        Actor("restaurant", "조리중", "픽업 준비", "기존 계약의 1분 조리 대기");
        await RequeryAsync(ct);
        var dispatchDeadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < dispatchDeadline)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
            var queues = await db.운송원장.AsNoTracking().Where(x => x.의뢰Id == _orderNo).ToListAsync(ct);
            Require(queues.Count <= 1, "동일 주문의 배차 원장이 중복되었습니다.");
            if (queues.SingleOrDefault()?.현재추천대상기사Id == UserId("driver-near")) break;
            await Task.Delay(1000, ct);
        }
        var offers = await GetAsync<JsonElement>("driver-near", "api/v1/driver/food-deliveries/offers", ct);
        var offeredWorkspace = await GetAsync<FoodDeliveryDriverWorkspaceDto>(
            "driver-near",
            "api/v1/driver/food-deliveries/workspace",
            ct);
        var offered = offeredWorkspace.Recommendations.Single(x => x.OfferId == _orderNo);
        RequireAction(offered.AvailableActions, 음식배달가능행동Ids.기사제안수락, "기사 추천");
        RequireAction(offered.AvailableActions, 음식배달가능행동Ids.기사제안거절, "기사 추천");
        // 정상 제안 조회 결과와 DB의 추천 대상을 함께 확인. 기사 ID를 임의로 확정하지 않는다.
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
            var queue = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == _orderNo, ct);
            Require(queue.현재추천대상기사Id == UserId("driver-near"), "근거리 기사 추천을 확인하지 못했습니다.");
        }
        Require(offers.ToString().Contains(_orderNo, StringComparison.Ordinal), "기사 제안 조회에 동일 주문이 없습니다.");
        var offerPath = $"api/v1/driver/food-deliveries/offers/{_orderNo}";
        await ExpectDeniedAsync("driver-far", offerPath + "/accept", null, ct);
        await PostAsync<JsonElement>("driver-near", offerPath + "/accept", null, ct);
        Record("driver-near", "제안 수락", "서버 거리 평가·권한 확인 뒤 기사 배정");
        Actor("driver-near", "음식점 이동", "픽업", "조리 완료와 도착 대기");
        for (var step = 1; step <= 10; step++)
        {
            await LocationAsync("driver-near", 127.084m + .001m * step / 10, ct);
            Position("driver-near", -8 + .8f * step, 0);
            await RequeryAsync(ct);
            await Task.Delay(1000, ct);
        }
        Actor("driver-near", "음식점 대기", "픽업", "음식점 준비 완료 대기");
        while (DateTime.UtcNow < readyAt)
        {
            await RequeryAsync(ct);
            await Task.Delay(1000, ct);
        }
        await PostAsync<음식주문응답>("restaurant", $"api/v1/food-orders/{_orderNo}/restaurant-progress",
            new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.픽업준비 }, ct);
        Record("restaurant", "픽업 준비", "조리 대기 후 준비 완료 API / 기사배정 보존");
        Actor("restaurant", "인계 준비", "기사 픽업 확인", "");
        await PostAsync<JsonElement>("driver-near", offerPath + "/pickup-complete", null, ct);
        var pickupWorkspace = await GetAsync<FoodDeliveryDriverWorkspaceDto>(
            "driver-near",
            "api/v1/driver/food-deliveries/workspace",
            ct);
        RequireAction(
            pickupWorkspace.ActiveDeliveries.Single(x => x.OfferId == _orderNo).AvailableActions,
            음식배달가능행동Ids.기사전달완료,
            "기사 픽업완료");
        Record("driver-near", "음식 픽업", "서버 픽업 완료 재조회");
        Actor("driver-near", "주문자 이동", "전달 완료", "주택 도착 대기");
        for (var step = 1; step <= 16; step++)
        {
            await LocationAsync("driver-near", 127.085m + .002m * step / 16, ct);
            Position("driver-near", step, 0);
            await RequeryAsync(ct);
            await Task.Delay(1000, ct);
        }
        await PostAsync<JsonElement>("driver-near", offerPath + "/delivery-complete", null, ct);
        Record("driver-near", "고객 전달", "서버 전달 완료");
        var delivered = await GetAsync<주문자음식주문상세응답>("customer", $"api/v1/food-orders/{_orderNo}", ct);
        RequireAction(delivered.AvailableActions, 음식배달가능행동Ids.주문수령확인, "주문자 전달완료");
        await PostAsync<음식주문응답>("customer", $"api/v1/food-orders/{_orderNo}/receipt-confirmation",
            new 주문자음식주문수령확인요청 { 클라이언트요청Id = Guid.NewGuid(), 확인메모 = "격리 합성 표본 수령" }, ct);
        await RequeryAsync(ct);
        Require(Read().OrderStatus == 음식주문상태코드.수령확인, "서버 수령 확인 상태가 아닙니다.");
        var completed = await GetAsync<주문자음식주문상세응답>("customer", $"api/v1/food-orders/{_orderNo}", ct);
        Require(completed.AvailableActions.Count == 0, "수령확인 완료 뒤 주문자 행동 목록이 비어 있지 않습니다.");
        Record("customer", "수령 확인", "정상 역할 API와 독립 DB 재조회 일치");
        Actor("customer", "수령 확인", "관찰", "");
        Actor("restaurant", "처리 완료", "새 주문 대기", "검증은 주문 1건만 생성");
        Actor("driver-near", "전달 완료", "근무 종료", "");
        foreach (var id in new[] { "driver-near", "driver-far" })
            await PostAsync<JsonElement>(id, "api/v1/driver/food-deliveries/work/stop", null, ct);
        lock (_gate) _message = "주문 폐루프 완료 · 남은 5분 관찰 구간에서 같은 주문 재조회 중";
    }

    private async Task RequeryAsync(CancellationToken ct)
    {
        var customer = await GetAsync<주문자음식주문상세응답>("customer", $"api/v1/food-orders/{_orderNo}", ct);
        var restaurant = await GetAsync<음식주문응답>("restaurant", $"api/v1/food-orders/restaurant/inbox/{_orderNo}", ct);
        using var scope = scopes.CreateScope();
        var saved = scope.ServiceProvider.GetRequiredService<ISsalddelFoodOrderStore>().GetOrder(_orderNo);
        Require(saved is not null && saved.주문번호 == customer.주문.주문번호 && saved.상태 == customer.주문.상태
            && saved.상태 == restaurant.상태, "역할별 재조회와 영속 원장 상태가 일치하지 않습니다.");
        Require(음식자료선택Policy.OrderMatches(_menuSelection!, saved!.상품목록)
            && 음식자료선택Policy.OrderMatches(_menuSelection!, customer.상품목록)
            && 음식자료선택Policy.OrderMatches(_menuSelection!, restaurant.상품목록), "재조회에서 메뉴·수량·가격 결속이 달라졌습니다.");
        lock (_gate) { _orderStatus = saved!.상태; _dispatchStatus = saved.배차상태; _revision++; }
    }
    private Task<JsonElement> LocationAsync(string id, decimal longitude, CancellationToken ct) =>
        PostAsync<JsonElement>(id, "api/v1/driver/food-deliveries/work/location",
            new { AppKey = "FoodDeliveryDriverApp", 위도 = 37.588m, 경도 = longitude, 정확도_m = 1, 상차접근허용반경Km = 5, 운행상태 = "운행중", 기록시각 = DateTime.UtcNow }, ct);
    private static void RequireAction(
        IReadOnlyList<업무가능행동Dto> actions,
        string actionId,
        string stateLabel)
        => Require(
            업무가능행동목록.포함(actions, actionId),
            $"{stateLabel} 상태 사본에 필요한 행동이 없습니다: {actionId}");
    private async Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
    {
        using var response = await _roles[role].GetAsync(path, ct);
        return await DecodeAsync<T>(response, path, ct);
    }
    private async Task<T> PostAsync<T>(string role, string path, object? payload, CancellationToken ct)
    {
        using var response = await _roles[role].PostAsJsonAsync(path, payload, _json, ct);
        return await DecodeAsync<T>(response, path, ct);
    }
    private async Task<T> DecodeAsync<T>(HttpResponseMessage response, string path, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode) throw new 검증실패Exception($"업무 API 실패: {path} HTTP {(int)response.StatusCode}");
        if (response.StatusCode == HttpStatusCode.NoContent) return default!;
        return (await response.Content.ReadFromJsonAsync<T>(_json, ct))!;
    }
    private async Task ExpectDeniedAsync(string role, string path, object? payload, CancellationToken ct)
    {
        using var response = await _roles[role].PostAsJsonAsync(path, payload, _json, ct);
        Require(role == "customer" ? response.StatusCode == HttpStatusCode.Forbidden
            : response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            "권한 밖 업무 요청이 예상대로 거절되지 않았습니다: " + path);
        Record(role, "권한 검증", "업무 API가 허용되지 않은 전이를 거절함");
    }
    private void Record(string id, string action, string result)
    {
        lock (_gate)
        {
            _events.Add(new(_events.Count + 1, id, action, result, DateTime.UtcNow));
            _actors = _actors.Select(x => x.Id == id ? x with { LastAction = action } : x).ToArray();
            _revision++;
        }
    }
    private void Actor(string id, string state, string next, string wait)
    {
        lock (_gate) { _actors = _actors.Select(x => x.Id == id ? x with { State = state, NextAction = next, WaitReason = wait } : x).ToArray(); _revision++; }
    }
    private void Position(string id, float x, float z)
    {
        lock (_gate) { _actors = _actors.Select(a => a.Id == id ? a with { X = x, Z = z } : a).ToArray(); _revision++; }
    }
    private static void Require(bool valid, string message) { if (!valid) throw new 검증실패Exception(message); }
    private sealed class 검증실패Exception(string message) : Exception(message);
    private sealed class 메뉴대기Exception : Exception;
    private static 음식배달관찰Actor[] InitialActors() =>
    [
        new("customer", "주문자", "orderer", "대기", "주문 등록", "검증 시작 대기", "", 16, 0),
        new("restaurant", "음식점 주인", "restaurant", "대기", "수신함 조회", "새 주문 대기", "", 0, 0),
        new("driver-near", "가까운 기사", "driver", "대기", "운행 시작", "검증 시작 대기", "", -8, 0),
        new("driver-far", "먼 기사", "driver", "대기", "운행 시작", "검증 시작 대기", "", -20, 8)
    ];
}
