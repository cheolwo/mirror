using MongoDB.Driver;
using MySqlConnector;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Development.MobileFieldTest;

/// <summary>실행 모드를 추가하지 않는, 전용 Staging/Simulation 음식 배차 시험의 opt-in.</summary>
public sealed class 모바일음식배차시험Options
{
    public const string Section = "MobileFieldTest:FoodDispatch";
    public const string DatabaseName = "ssalddel_food_mobile_test";
    public bool Enabled { get; set; }
    public bool BootstrapSyntheticDriverState { get; set; }
    public bool RefreshSyntheticDriverLocation { get; set; }
    public string FixtureRevision { get; set; } = 모바일음식배차시험자료.FixtureRevision;
    public int PollIntervalSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 20;

    public void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!Enabled) return;
        bool isolated;
        try
        {
            var mysql = new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection") ?? "");
            var mongo = new MongoUrl(configuration["MongoDb:ConnectionString"] ?? "");
            isolated = mysql.Server == "mysql" && mysql.Database == DatabaseName
                       && configuration["MongoDb:Database"] == DatabaseName
                       && mongo.Servers.Count() == 1 && mongo.Servers.Single().Host == "mongo";
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or MongoConfigurationException)
        {
            // 연결 문자열/비밀번호를 오류나 로그에 포함하지 않는다.
            isolated = false;
        }

        if (!environment.IsStaging() || configuration["SsalddelExecution:Mode"] != "Simulation"
            || !configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER") || !isolated
            || !configuration.GetValue<bool>("MobileFieldTest:Enabled")
            || !configuration.GetValue<bool>("IdentitySeed:DevelopmentAccounts:Enabled")
            || !configuration.GetValue<bool>("IdentitySeed:DevelopmentAccounts:AllowInSimulation")
            || configuration.GetValue<bool>("IdentitySeed:BootstrapAdmin:Enabled")
            || configuration.GetValue<bool>("DatabaseInitialization:RunAtStartup")
            || configuration.GetValue<bool>("FoodObserverVerification:Enabled")
            || configuration.GetValue<bool>("ObservableOperationsVerification:Enabled")
            || !configuration.GetValue<bool>("VersionFeatureFlags:FoodDeliveryWorkflow")
            || FixtureRevision != 모바일음식배차시험자료.FixtureRevision
            || PollIntervalSeconds is < 1 or > 30 || BatchSize is < 1 or > 100)
            throw new InvalidOperationException("MobileFoodDispatchRequiresIsolatedStagingSimulation");
    }
}

public sealed class 모바일음식배차시험Guard(
    모바일음식배차시험Options options, IConfiguration configuration,
    IHostEnvironment environment, ISsalddelExecutionModePolicy executionMode)
{
    public void EnsureEnabled()
    {
        if (!options.Enabled || !executionMode.IsSimulation)
            throw new InvalidOperationException("MobileFoodDispatchRequiresIsolatedStagingSimulation");
        options.Validate(configuration, environment);
    }
}
