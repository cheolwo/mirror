using MySqlConnector;

namespace Ssalddel.Services.Development.FoodObserver;

/// <summary>실행 모드가 아닌, 외부 효과가 차단된 로컬 검증 호스트의 opt-in 설정.</summary>
public sealed class 음식배달관찰검증Options
{
    public const string Section = "FoodObserverVerification";
    public const int 기본검증초 = 300;
    public const string DatabaseName = "ssalddel_food_observer";
    public bool Enabled { get; set; }
    public string AccessKey { get; set; } = "";
    public string AccountPassword { get; set; } = "";
    public int DurationSeconds { get; set; } = 기본검증초;
    public string MenuSnapshotPath { get; set; } = "/app/observer-input/menus.json";
    public decimal MealBudget { get; set; } = 5000;
    public long PreferredRecipeId { get; set; } = 7;
    public bool MealNeeded { get; set; } = true;

    public void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!Enabled) return;
        var db = new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection") ?? "");
        if (!environment.IsDevelopment()
            || configuration["SsalddelExecution:Mode"] != "Simulation"
            || configuration["DOTNET_RUNNING_IN_CONTAINER"] != "true"
            || db.Server != "mysql" || db.Database != DatabaseName
            || configuration["MongoDb:Database"] != DatabaseName
            || configuration["MongoDb:ConnectionString"] != "mongodb://mongo:27017"
            || configuration.GetValue<bool>("DatabaseInitialization:RunAtStartup")
            || AccessKey.Length < 32 || AccountPassword.Length < 16
            || DurationSeconds < 90 || DurationSeconds > 기본검증초)
            throw new InvalidOperationException("Food observer requires an isolated Development/Simulation container and dedicated databases; duration must be 90..300 seconds.");
    }
}

public sealed record 음식배달관찰Actor(string Id, string Name, string Role, string State,
    string NextAction, string WaitReason, string LastAction, float X, float Z);
public sealed record 음식배달관찰Event(long Sequence, string ActorId, string Action, string Result, DateTime OccurredAtUtc);
public sealed record 음식배달관찰Snapshot(string SchemaVersion, string RunId, long Revision, string Status,
    double ElapsedSeconds, int DurationSeconds, string OrderNo, string OrderStatus, string DispatchStatus,
    string Message, IReadOnlyList<음식배달관찰Actor> Actors, IReadOnlyList<음식배달관찰Event> Events,
    음식자료선택결과? MenuSelection = null,
    음식배달관찰RouteFixture? ConfiguredRouteFixture = null);

/// <summary>등록된 검증 입력의 범위다. 실제 지도 조회 또는 실제 주행 확인 결과가 아니다.</summary>
public sealed record 음식배달관찰RouteFixture(string ExecutionModeCode, string SourceCode, string FixtureId,
    string PickupAddress, string DropoffAddress, decimal PickupLatitude, decimal PickupLongitude,
    decimal DropoffLatitude, decimal DropoffLongitude, decimal DistanceKm,
    bool IsEstimated, bool ActualMapRequest, IReadOnlyList<string> AllowedDriverIds);
