namespace Ssalddel.Tests.Ui.Common;

public sealed class DriverMobileFigma04PresentationTests
{
    [Theory]
    [InlineData("Home/기사홈Page.razor", "/driver/home/summary")]
    [InlineData("01_Work/운행시작Page.razor", "/driver/work/start")]
    [InlineData("01_Work/커뮤니티개별의뢰Page.razor", "/driver/work/community-inquiries")]
    [InlineData("02_Recommendation/추천목록Page.razor", "/driver/recommendations")]
    [InlineData("02_Recommendation/추천상세Page.razor", "/driver/recommendations/{의뢰Id}")]
    [InlineData("02_Recommendation/배차처리Page.razor", "/driver/recommendations/{의뢰Id}/decision")]
    [InlineData("02_Recommendation/탐색캠페인Page.razor", "/driver/exploration/campaigns")]
    [InlineData("03_Progress/진행중운송Page.razor", "/driver/transports/current")]
    [InlineData("03_Progress/상차Page.razor", "/driver/transports/{운송Id:long}/pickup")]
    [InlineData("03_Progress/하차Page.razor", "/driver/transports/{운송Id:long}/dropoff")]
    [InlineData("03_Progress/배달내역Page.razor", "/driver/transports/history")]
    [InlineData("04_Reservation/예약Page.razor", "/driver/reservations")]
    [InlineData("05_Settlement/월정산Page.razor", "/driver/settlements/current-month")]
    [InlineData("05_Settlement/계좌정보Page.razor", "/driver/account/bank")]
    [InlineData("06_Notification/알림함Page.razor", "/driver/notifications")]
    public void Figma04의_열다섯화면은_기존기사Route와업무화면을재사용한다(
        string relativePath,
        string route)
    {
        var source = ReadDriverPage(relativePath);

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains("driver-page-legacy-header", source);
    }

    [Theory]
    [InlineData("04.01", "기사 홈 요약")]
    [InlineData("04.02", "운행 시작")]
    [InlineData("04.03", "커뮤니티 개별 의뢰")]
    [InlineData("04.04", "운송 추천")]
    [InlineData("04.05", "추천 상세")]
    [InlineData("04.06", "운송 참여 결정")]
    [InlineData("04.07", "보낸 탐색 문의함")]
    [InlineData("04.08", "진행 중 운송")]
    [InlineData("04.09", "상차 기록")]
    [InlineData("04.10", "하차 기록")]
    [InlineData("04.11", "배달 내역")]
    [InlineData("04.12", "운행 예약")]
    [InlineData("04.13", "월 정산")]
    [InlineData("04.14", "계좌 정보")]
    [InlineData("04.15", "알림함")]
    public void 모바일ScreenCatalog는_Figma04책임코드와제목을고정한다(
        string screenCode,
        string title)
    {
        var source = Read("DriverApp", "Services", "DriverMobileScreenCatalog.cs");

        Assert.Contains($"\"{screenCode}\"", source);
        Assert.Contains($"\"{title}\"", source);
    }

    [Fact]
    public void 기사MauiShell은_FigmaAppBar와네개하단Navigation을제공한다()
    {
        var source = Read("DriverApp", "Components", "Layout", "MainLayout.razor");
        var styles = Read("DriverApp", "wwwroot", "driver-mobile.css");

        Assert.Contains("driver-mobile-shell__appbar", source);
        Assert.Contains("driver-mobile-shell__bottom-nav", source);
        Assert.Contains("살뜰 기사", source);
        Assert.Contains(">홈</span>", source);
        Assert.Contains(">추천</span>", source);
        Assert.Contains(">운송</span>", source);
        Assert.Contains(">정산</span>", source);
        Assert.Contains("--mud-palette-primary: #009688", styles);
        Assert.Contains("width: min(100%, 520px)", styles);
    }

    [Fact]
    public void 앱은_Figma업무Shell로시작하고_기존네이티브지도를보존한다()
    {
        var app = Read("DriverApp", "App.xaml.cs");
        var layout = Read("DriverApp", "Components", "Layout", "MainLayout.razor");
        var navigator = Read("DriverApp", "Services", "DriverNativeMapNavigator.cs");

        Assert.Contains("private readonly MainPage _mainPage", app);
        Assert.Contains("new NavigationPage(_mainPage)", app);
        Assert.Contains("<span>운행 지도</span>", layout);
        Assert.Contains("@onclick=\"OpenNativeMapAsync\"", layout);
        Assert.Contains("GetRequiredService<NativeDriverHomePage>()", navigator);
    }

    [Fact]
    public void 기존기사홈주소는_실제요약화면으로호환이동한다()
    {
        var source = Read("DriverApp", "Components", "Pages", "Home.razor");

        Assert.Contains("@page \"/driver/home\"", source);
        Assert.Contains("DriverRoutes.HomeSummary", source);
        Assert.Contains("replace: true", source);
    }

    private static string ReadDriverPage(string relativePath)
        => Read(
            new[] { "DriverApp", "Components", "Pages", "Driver" }
                .Concat(relativePath.Split('/'))
                .ToArray());

    private static string Read(params string[] segments)
        => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(segments).ToArray()));

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
