namespace Ssalddel.Tests.Architecture;

public sealed class OperationalServerClientBoundaryCompositionTests
{
    public static TheoryData<string, string> OperationalClientStartups => new()
    {
        { "Ssalddel.WebApp", "Program.cs" },
        { "OrdererApp", "MauiProgram.cs" },
        { "DriverApp", "MauiProgram.cs" },
        { "RestaurantDeskApp", "MauiProgram.cs" },
        { "SsalddelApp", "MauiProgram.cs" },
        { "FDriverApp", "MauiProgram.cs" },
        { "WarehouseManagerApp", "MauiProgram.cs" },
        { "HumanResourcesManagerApp", "MauiProgram.cs" },
        { "SellerApp", "MauiProgram.cs" },
        { "SsalddelAdminApp", "MauiProgram.cs" },
        { "SsalddelAdmin", "Program.cs" },
        { "Ssalddel.Web.UnityReviewApp", "Program.cs" },
        { "eng/web-role-app", "Program.cs" },
    };

    [Theory]
    [MemberData(nameof(OperationalClientStartups))]
    public void 운영Client는_명시적운영Api를사용하고_SimulationRuntime을등록하지않는다(
        string project,
        string startupFile)
    {
        var startup = Read(project, startupFile);

        Assert.Contains("AddSsalddelOperationalApiHttpClient", startup);
        Assert.DoesNotContain("AddRemoteBusinessWorkflowRuntime", startup);
        Assert.DoesNotContain("AddRemoteSimulationBusinessWorkflowRuntime", startup);
    }

    [Fact]
    public void 운영과Simulation논리Client는_한서버주소설정Key를공유한다()
    {
        Assert.Equal(
            "SsalddelEndpoints:ServerBaseAddress",
            global::Ssalddel.Ui.Common.Areas.App.Services
                .SsalddelServerEndpoint.ConfigurationKey);
        Assert.Equal(
            "SsalddelEndpoints:ServerBaseAddress",
            global::Ssalddel.Client.Infrastructure.Simulation
                .SsalddelSimulationApiEndpoint.ConfigurationKey);
        Assert.Equal(
            global::Ssalddel.Ui.Common.Areas.App.Services
                .SsalddelServerEndpoint.ConfigurationKey,
            global::Ssalddel.Client.Infrastructure.Simulation
                .SsalddelSimulationApiEndpoint.ConfigurationKey);

        var simulation = Read(
            "Ssalddel.Client.Infrastructure",
            "Simulation/SsalddelSimulationApiEndpoint.cs");
        Assert.Contains("단일 살뜰 서버 주소", simulation);
    }

    [Fact]
    public void SimulationHttp는_별도실행서버가아닌_Ssalddel내부Hosting모듈이다()
    {
        var hostingProject = Read(
            "Ssalddel.Simulation.Hosting",
            "Ssalddel.Simulation.Hosting.csproj");
        var mainProject = Read("Ssalddel", "Ssalddel.csproj");
        var mainProgram = Read("Ssalddel", "Program.cs");
        var composeOverlay = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "docker-compose.simulation.yml"));

        Assert.Contains("<Project Sdk=\"Microsoft.NET.Sdk\">", hostingProject);
        Assert.DoesNotContain("Microsoft.NET.Sdk.Web", hostingProject);
        Assert.Contains("Ssalddel.Simulation.Hosting.csproj", mainProject);
        Assert.Contains("AddSsalddelSimulationModule", mainProgram);
        Assert.Contains("MapHub<SimulationOnlineWorldHub>", mainProgram);
        Assert.False(File.Exists(Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel.Simulation.Server",
            "Ssalddel.Simulation.Server.csproj")));
        Assert.DoesNotContain("\n  simulation:\n", composeOverlay
            .Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Contains("\n  app:\n", composeOverlay
            .Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static string Read(string project, string relativePath)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), project, relativePath));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }
}
