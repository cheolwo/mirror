using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Models;

namespace Hongdal.Tests.Services.Community;

public sealed class PlatformHomeWorkspaceCatalogTests
{
    [Fact]
    public void DefaultWorkspaces_MapToLedgerTemplates()
    {
        foreach (var workspace in PlatformHomeWorkspaceCatalog.DefaultWorkspaces)
        {
            var template = CommunityLedgerTemplateCatalog.Find(workspace.LedgerTemplateKey);

            Assert.Equal(workspace.LedgerTemplateKey, template.Key);
            Assert.False(string.IsNullOrWhiteSpace(workspace.OperatingSystemName));
            Assert.False(string.IsNullOrWhiteSpace(workspace.EntryHref));
        }
    }

    [Fact]
    public void DefaultWorkspaces_CoverCommunityCenteredLifeWork()
    {
        var keys = PlatformHomeWorkspaceCatalog.DefaultWorkspaces
            .Select(workspace => workspace.LedgerTemplateKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(CommunityLedgerTemplateKeys.CargoTransport, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.FoodOrder, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.FoodDelivery, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.WarehouseOutbound, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.WarehouseInbound, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.LocalSale, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.GroupPurchase, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.GroupImport, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.Errand, keys);
    }

    [Fact]
    public void DefaultWorkspaces_CanBeGroupedByTargetOperatingSystem()
    {
        var osNames = PlatformHomeWorkspaceCatalog.DefaultWorkspaces
            .Select(workspace => workspace.OperatingSystemName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Contains("국내 화물 운송 OS", osNames);
        Assert.Contains("음식 배달 OS", osNames);
        Assert.Contains("창고·커머스 이행 OS", osNames);
        Assert.Contains("공동수입 OS", osNames);
        Assert.Contains("커뮤니티 신뢰 OS", osNames);
    }
}
