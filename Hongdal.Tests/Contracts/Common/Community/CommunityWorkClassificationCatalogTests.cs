using Hongdal.Contracts.Common.Community;

namespace Hongdal.Tests.Contracts.Common.Community;

public sealed class CommunityWorkClassificationCatalogTests
{
    [Fact]
    public void 업무_분류는_중복되지_않는_원장_종류와_기능_설정을_가진다()
    {
        var classifications = CommunityWorkClassificationCatalog.All;
        var ledgerTemplateKeys = classifications.SelectMany(x => x.LedgerTemplateKeys).ToArray();

        Assert.Equal(classifications.Count, classifications.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(ledgerTemplateKeys.Length, ledgerTemplateKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(classifications, x => Assert.False(string.IsNullOrWhiteSpace(x.FeatureFlagKey)));
    }

    [Fact]
    public void 화물_운송_분류는_운송_원장과_국내_운송_기능에_연결된다()
    {
        var classification = CommunityWorkClassificationCatalog.FindByWorkflowTag("국내 화물 운송");

        Assert.NotNull(classification);
        Assert.Equal("DomesticTransportWorkflow", classification.FeatureFlagKey);
        Assert.Equal([CommunityLedgerTemplateKeys.CargoTransport], classification.LedgerTemplateKeys);
    }

    [Fact]
    public void 공동구매와_공동수입은_서로_다른_업무_분류와_원장을_가진다()
    {
        var groupPurchase = CommunityWorkClassificationCatalog.FindByWorkflowTag("공동구매");
        var groupImport = CommunityWorkClassificationCatalog.FindByWorkflowTag("공동수입");

        Assert.NotNull(groupPurchase);
        Assert.NotNull(groupImport);
        Assert.Contains(CommunityLedgerTemplateKeys.GroupPurchase, groupPurchase.LedgerTemplateKeys);
        Assert.DoesNotContain(CommunityLedgerTemplateKeys.GroupImport, groupPurchase.LedgerTemplateKeys);
        Assert.Equal([CommunityLedgerTemplateKeys.GroupImport], groupImport.LedgerTemplateKeys);
        Assert.Equal("CommunityTrustWorkflow", groupPurchase.FeatureFlagKey);
        Assert.Equal("GroupPurchaseImportWorkflow", groupImport.FeatureFlagKey);
    }

    [Fact]
    public void 육류수입준비정보는_주선업이아닌_커뮤니티신뢰기능으로_분류한다()
    {
        var classification = CommunityWorkClassificationCatalog.FindByLedgerTemplate(
            CommunityLedgerTemplateKeys.MeatImportReadiness);

        Assert.NotNull(classification);
        Assert.Equal("meat-import-readiness", classification.Code);
        Assert.Equal("CommunityTrustWorkflow", classification.FeatureFlagKey);
    }
}
