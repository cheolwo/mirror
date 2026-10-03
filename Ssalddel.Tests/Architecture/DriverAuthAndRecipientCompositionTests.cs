namespace Ssalddel.Tests.Architecture;

public sealed class DriverAuthAndRecipientCompositionTests
{
    [Fact]
    public void 기사Api는_토큰선갱신과_401후한번재시도를지원한다()
    {
        var auth = Read("DriverApp/Services/AuthApiService.cs");
        var api = Read("DriverApp/Services/DriverApiClient.cs");
        var session = Read("DriverApp/Services/AuthSession.cs");

        Assert.Contains("api/v1/auth/refresh", auth);
        Assert.Contains("EnsureAccessTokenAsync", auth);
        Assert.Contains("forceRefresh: true", api);
        Assert.Contains("response.StatusCode != HttpStatusCode.Unauthorized", api);
        Assert.Contains("CloneRequestAsync", api);
        Assert.Contains("AccessTokenExpiresAtUtc", session);
        Assert.Contains("RefreshTokenExpiresAtUtc", session);
    }

    [Fact]
    public void 진행중운송화면은_고정전화번호대신_서버수령자정보를사용한다()
    {
        var contract = Read("Ssalddel.Contracts/Driver/Transport/기사운송Dtos.cs");
        var adapter = Read("DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs");
        var mapper = Read("DriverApp/Services/기사운송표시Mapper.cs");
        var page = Read("DriverApp/Components/Pages/Driver/03_Progress/진행중운송Page.razor");

        Assert.Contains("수령자연락처", contract);
        Assert.Contains("기사운송표시Mapper.Map(source)", adapter);
        Assert.Contains("수령자연락처 = source.수령자연락처", mapper);
        Assert.Contains("MaskPhone(현재운송?.수령자연락처)", page);
        Assert.DoesNotContain("010-****-2401", page);
        Assert.DoesNotContain("상세 API 연결 시 표시", page);
    }

    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
