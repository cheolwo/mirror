namespace Ssalddel.Tests.Architecture;

public sealed class OrdererGroupImportReadinessPageCompositionTests
{
    private static readonly (string FileName, string Route, string Screen)[] Pages =
    [
        ("GroupImportReadinessOverview.razor", "/group-purchase/imports/{LedgerId}", "<GroupImportReadinessOverviewScreen"),
        ("GroupImportReadinessSuppliers.razor", "/group-purchase/imports/{LedgerId}/suppliers", "<GroupImportReadinessSuppliersScreen"),
        ("GroupImportReadinessCosts.razor", "/group-purchase/imports/{LedgerId}/costs", "<GroupImportReadinessCostsScreen"),
        ("GroupImportReadinessClassification.razor", "/group-purchase/imports/{LedgerId}/classification", "<GroupImportReadinessClassificationScreen"),
        ("GroupImportReadinessLogisticsReview.razor", "/group-purchase/imports/{LedgerId}/logistics-review", "<GroupImportReadinessLogisticsReviewScreen"),
        ("GroupImportReadinessHandoff.razor", "/group-purchase/imports/{LedgerId}/handoff", "<GroupImportReadinessHandoffScreen"),
        ("GroupImportReadinessConsent.razor", "/group-purchase/imports/{LedgerId}/consent", "<GroupImportReadinessConsentScreen")
    ];

    [Fact]
    public void 같이수입1_5조회는_원장기준의얇은책임별Route로분리된다()
    {
        var pagesRoot = Path.Combine(FindRepositoryRoot(), "OrdererApp", "Components", "Pages");

        foreach (var (fileName, route, screen) in Pages)
        {
            var path = Path.Combine(pagesRoot, fileName);
            var source = File.ReadAllText(path);

            Assert.True(File.ReadLines(path).Count() <= 25, $"{fileName}이 route 조립 책임을 넘어섰습니다.");
            Assert.Contains($"@page \"{route}\"", source);
            Assert.Contains("<GroupImportReadinessRouteFrame", source);
            Assert.Contains(screen, source);
            Assert.Contains("SupplyParameterFromQuery", source);
            Assert.DoesNotContain("ProductId", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void 주문자조회화면은_관리자실행과외부거래동작을포함하지않는다()
    {
        var root = FindRepositoryRoot();
        var componentRoot = Path.Combine(root, "OrdererApp", "Components", "GroupPurchase");
        var files = Directory.GetFiles(componentRoot, "GroupImportReadiness*.razor");
        var source = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.Equal(8, files.Length);
        Assert.DoesNotContain("I같이수입준비관리Client", source, StringComparison.Ordinal);
        Assert.DoesNotContain("준비Os작업실행Async", source, StringComparison.Ordinal);
        Assert.DoesNotContain("전문검토인계Async", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpMethod.Post", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpMethod.Put", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpMethod.Delete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("같이수입준비원장응답", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".준비자료", source, StringComparison.Ordinal);
        Assert.DoesNotContain("정보제공동의근거참조", source, StringComparison.Ordinal);
        Assert.DoesNotContain("기록자표시명", source, StringComparison.Ordinal);
        Assert.DoesNotContain("검토자표시명", source, StringComparison.Ordinal);
    }

    [Fact]
    public void 준비현황딥링크는_인증후에만_보호ApiLoader를생성하고_책임별화면종류를사용한다()
    {
        var framePath = Path.Combine(
            FindRepositoryRoot(),
            "OrdererApp",
            "Components",
            "GroupPurchase",
            "GroupImportReadinessRouteFrame.razor");
        var source = File.ReadAllText(framePath);

        Assert.Contains("<GroupPurchaseOrdererAccessGate", source);
        Assert.Contains("<Authorized>", source);
        Assert.Contains("<OrdererGroupImportReadinessLoader", source);
        Assert.True(
            source.IndexOf("<Authorized>", StringComparison.Ordinal)
            < source.IndexOf("<OrdererGroupImportReadinessLoader", StringComparison.Ordinal));
        Assert.Contains("GroupPurchaseScreenKind.ImportOverview", source);
        Assert.Contains("GroupPurchaseScreenKind.ImportSuppliers", source);
        Assert.Contains("GroupPurchaseScreenKind.ImportCosts", source);
        Assert.Contains("GroupPurchaseScreenKind.ImportClassification", source);
        Assert.Contains("GroupPurchaseScreenKind.ImportLogisticsReview", source);
        Assert.Contains("GroupPurchaseScreenKind.ImportHandoff", source);
        Assert.Contains("GroupPurchaseScreenKind.ImportConsent", source);
    }

    [Fact]
    public void 같이수입준비Loader는_기존내용을유지하며_30초마다서버원장을다시읽는다()
    {
        var loaderPath = Path.Combine(
            FindRepositoryRoot(),
            "OrdererApp",
            "Components",
            "GroupPurchase",
            "OrdererGroupImportReadinessLoader.razor");
        var source = File.ReadAllText(loaderPath);

        Assert.Contains("TimeSpan.FromSeconds(30)", source);
        Assert.Contains("PeriodicTimer", source);
        Assert.Contains("preserveCurrentContent: true", source);
        Assert.Contains("OrdererGroupImportReadinessState", source);
        Assert.Contains("이전에 확인한 내용을 유지합니다.", File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "Ssalddel.Ui.Common", "Areas", "App", "ViewModels", "OrdererGroupImportReadinessState.cs")));
        Assert.Contains("@implements IAsyncDisposable", source);
    }

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
