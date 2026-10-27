namespace Ssalddel.Tests.Architecture;

public sealed class ApplicationPrivacyConsentCompositionTests
{
    [Fact]
    public void 동의Panel은_필수동의를기본미선택으로두고_두확인뒤에만계속한다()
    {
        var source = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Components",
            "ApplicationPrivacyConsentPanel.razor");

        Assert.Contains("개인정보 동의 · 필수", source);
        Assert.Contains("수집·이용 목적", source);
        Assert.Contains("보유·이용 기간", source);
        Assert.Contains("거부 권리와 영향", source);
        Assert.Contains("법적 안내 전문", source);
        Assert.Contains("<textarea", source);
        Assert.Contains("readonly", source);
        Assert.Contains("@LegalNoticeText", source);
        Assert.Contains("[제3자 제공]", source);
        Assert.Contains("[국외 이전]", source);
        Assert.Contains("[운영 전 확인]", source);
        Assert.Contains("WebLocalePreferenceService", source);
        Assert.Contains("EnglishLegalNoticeText", source);
        Assert.Contains("FTC Act", source);
        Assert.Contains("COPPA", source);
        Assert.Contains("California Consumer Privacy Act", source);
        Assert.Contains("Selecting English does not determine jurisdiction", source);
        Assert.DoesNotContain("<details>", source);
        Assert.Contains("type=\"checkbox\"", source);
        Assert.Contains("disabled=\"@(!CanContinue || _isSaving)\"", source);
        Assert.Contains("_collectionUseAccepted && _ageRequirementConfirmed", source);
        Assert.Contains("ConsentClient.RecordAsync", source);
        Assert.Contains("신청개인정보동의정책.현재버전", source);
        Assert.Contains("신청개인정보출처Codes.커뮤니티지도", source);
    }

    [Theory]
    [InlineData("ShipperInboundRequestCreatePage.razor", "신청개인정보업무Codes.물류대행")]
    [InlineData("ShipperRequestPage.razor", "신청개인정보업무Codes.운송대행")]
    [InlineData("OrdererMartOrderRequestPage.razor", "신청개인정보업무Codes.개별주문")]
    public void 지도출발신청은_동의전양식을렌더하지않는다(
        string pageName,
        string applicationKind)
    {
        var source = ReadRepositoryFile("Ssalddel.WebApp", "Pages", pageName);

        if (pageName == "ShipperRequestPage.razor")
        {
            // 완료된 bool만으로 다음 의뢰의 동의를 재사용하지 않고 증적과 복구 경계도 검사합니다.
            Assert.Contains("RequiresMapPrivacyConsent && (!_privacyConsentAccepted || !PageModel.ApplicationPrivacyConsentEvidenceId.HasValue) && !PageModel.HasSubmission", source);
            Assert.Contains("@if (!PageModel.HasSubmission && !PageModel.IsBusy)", source);
        }
        else
        {
            Assert.Contains("RequiresMapPrivacyConsent && !_privacyConsentAccepted", source);
        }
        Assert.Contains("<ApplicationPrivacyConsentPanel", source);
        Assert.Contains(applicationKind, source);
        Assert.Contains("Accepted=\"AcceptPrivacyAsync\"", source);
        Assert.Contains("CommunityMapApplicationRoutes.SourceCode", source);
        Assert.Contains("_privacyConsentAccepted = true", source);
    }

    [Fact]
    public void 화주지도신청은_동의증적전에는동의Panel만_미확정이면복구만조립한다()
    {
        var source = ReadRepositoryFile("Ssalddel.WebApp", "Pages", "ShipperRequestPage.razor");
        var consentGuard = source.IndexOf("@if (RequiresMapPrivacyConsent &&", StringComparison.Ordinal);
        var consentPanel = source.IndexOf("<ApplicationPrivacyConsentPanel", StringComparison.Ordinal);
        var alternate = source.IndexOf("else", consentPanel, StringComparison.Ordinal);
        var recovery = source.IndexOf("<WebShipperSubmissionRecoveryPanel", StringComparison.Ordinal);
        var editingGuard = source.IndexOf("@if (!PageModel.HasSubmission && !PageModel.IsBusy)", StringComparison.Ordinal);
        var editor = source.IndexOf("<ShipperRequestAuthoringScreen", StringComparison.Ordinal);

        Assert.True(consentGuard >= 0 && consentGuard < consentPanel);
        Assert.True(consentPanel < alternate && alternate < recovery);
        Assert.True(recovery < editingGuard && editingGuard < editor);
        Assert.Contains("PageModel.SetApplicationPrivacyConsent(evidence)", source);
        Assert.Contains("NewRequestStarted=\"ResetSubmissionContext\"", source);
        Assert.Contains("_privacyConsentAccepted = false", source);
    }

    [Theory]
    [InlineData("Common", "창고작업Controller.cs", "신청개인정보업무Codes.물류대행")]
    [InlineData("Shipper/01_Request", "화주운송의뢰Controller.cs", "신청개인정보업무Codes.운송대행")]
    [InlineData("Orderer", "마트주문요청Controller.cs", "신청개인정보업무Codes.개별주문")]
    public void 지도출발신청Command는_서버에서유효동의증적을요구한다(
        string controllerFolder,
        string controllerName,
        string applicationKind)
    {
        var source = ReadRepositoryFile(
            ["Ssalddel", "Controllers", .. controllerFolder.Split('/'), controllerName]);

        Assert.Contains("유효한동의요구Async", source);
        Assert.Contains("신청개인정보동의증적Id", source);
        Assert.Contains("신청출처Code", source);
        Assert.Contains(applicationKind, source);
    }

    [Fact]
    public void 지도출발신청은_Main메뉴와운영Bar없는단독화면으로조립한다()
    {
        var layout = ReadRepositoryFile("Ssalddel.WebApp", "Layout", "MainLayout.razor");
        var css = ReadRepositoryFile("Ssalddel.WebApp", "wwwroot", "css", "app.css");

        Assert.Contains("IsMapApplicationStandalone", layout);
        Assert.Contains("CommunityMapApplicationRoutes.UsesStandaloneApplicationLayout", layout);
        Assert.Contains("map-application-standalone__main", layout);
        Assert.Contains("<NavMenu />", layout);
        Assert.Contains("<PreviewPortalBar />", layout);
        Assert.Contains(".map-application-standalone__main", css);
    }

    [Fact]
    public void 지도신청상태변경은_Command성공뒤_같은원장을다시조회한다()
    {
        var client = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Services",
            "CommunityMapApplicationLedgerClient.cs");
        var withdrawal = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Components",
            "ApplicationPrivacyWithdrawalPanel.razor");
        var cancellation = ReadRepositoryFile(
            "Ssalddel.WebApp",
            "Components",
            "MapApplicationOperationalCancellationPanel.razor");

        Assert.Contains("await MarkSubmittedAsync", client);
        Assert.Contains("var refreshed = await FindByOperationalSourceAsync", client);
        Assert.Contains("await LedgerClient.MarkConsentWithdrawnAsync", withdrawal);
        Assert.Contains("_ledger = await LedgerClient.FindByOperationalSourceAsync", withdrawal);
        Assert.Contains("await LedgerClient.RequestTransportCancellationReviewAsync", cancellation);
        Assert.Contains("await LedgerClient.MarkOperationalCancelledAsync", cancellation);
        Assert.Equal(3, CountOccurrences(cancellation, "_ledger = await LedgerClient.FindByOperationalSourceAsync"));
    }

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;

    private static string ReadRepositoryFile(params string[] relativePath)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. relativePath]));

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
