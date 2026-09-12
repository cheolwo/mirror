using MySqlConnector;

namespace Ssalddel.Services.Development.ObservableOperations;

/// <summary>실제 운영 효과 없이 세 저장소의 인계만 확인하는 opt-in 로컬 검증 설정입니다.</summary>
public sealed class 관찰운영검증Options
{
    public const string Section = "ObservableOperationsVerification";
    public const string DatabaseName = "ssalddel_observable_operations";
    public const int RequiredDurationSeconds = 600;

    public bool Enabled { get; set; }
    public string AccessKey { get; set; } = string.Empty;
    public int DurationSeconds { get; set; } = RequiredDurationSeconds;
    public int PollIntervalMilliseconds { get; set; } = 1000;

    public void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!Enabled) return;
        var mysql = new MySqlConnectionStringBuilder(
            configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
        if (!environment.IsDevelopment()
            || !string.Equals(configuration["SsalddelExecution:Mode"], "Simulation", StringComparison.Ordinal)
            || !string.Equals(configuration["DOTNET_RUNNING_IN_CONTAINER"], "true", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(mysql.Server, "mysql", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(mysql.Database, DatabaseName, StringComparison.Ordinal)
            || !string.Equals(configuration["MongoDb:ConnectionString"], "mongodb://mongo:27017", StringComparison.Ordinal)
            || !string.Equals(configuration["MongoDb:Database"], DatabaseName, StringComparison.Ordinal)
            || !string.Equals(configuration["TransientState:Provider"], "Redis", StringComparison.Ordinal)
            || !string.Equals(configuration["Redis:ConnectionString"], "redis:6379,abortConnect=false", StringComparison.Ordinal)
            || configuration.GetValue<bool>("DatabaseInitialization:RunAtStartup")
            || AccessKey.Length < 32
            || DurationSeconds != RequiredDurationSeconds
            || PollIntervalMilliseconds is < 250 or > 5000)
        {
            throw new InvalidOperationException(
                "Observable operations verification requires an isolated Development/Simulation container, dedicated MySQL/MongoDB/Redis, a 600-second duration, and external effects disabled.");
        }
    }
}

public static class 관찰운영검증상태Codes
{
    public const string Idle = "Idle";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Completed = "Completed";
    public const string Scheduled = "Scheduled";
    public const string Published = "Published";
    public const string Pending = "Pending";
    public const string Failed = "Failed";
}

public sealed record 관찰운영검증시작요청(string? RunStableId = null);
public sealed record 관찰운영검증사례상태(
    string CaseCode,
    string OperatingSystemId,
    string WorkStableId,
    string LifecycleStageCode,
    string AttentionStateCode,
    string StateCode,
    int ScheduledOffsetSeconds,
    long Revision);
public sealed record 관찰운영검증상태(
    string RunStableId,
    string StatusCode,
    string AreaStableId,
    int ElapsedSeconds,
    int DurationSeconds,
    int PublishedCaseCount,
    int PendingOutboxCount,
    int FailedOutboxCount,
    IReadOnlyList<관찰운영검증사례상태> Cases);
