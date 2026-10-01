using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Components.Community;

namespace Hongdal.Tests.Ui.Common;

public sealed class CommunitySystemPostDisplayTests
{
    [Theory]
    [InlineData(PlatformCommunitySystemPostKinds.LedgerCompletion, "성립", "자동 성립 기록")]
    [InlineData(PlatformCommunitySystemPostKinds.KamisPriceBrief, "정보", "자동 가격 정보")]
    [InlineData(PlatformCommunitySystemPostKinds.Reflection, "성찰", "자동 성찰문")]
    [InlineData(PlatformCommunitySystemPostKinds.ActivityDigest, "요약", "자동 활동 요약")]
    [InlineData(PlatformCommunitySystemPostKinds.PrajnaContent, "반야", "관리자 선별 반야 자료")]
    public void SystemPostKind_UsesDistinctPublicLabels(
        string kind,
        string expectedShortLabel,
        string expectedBadgeLabel)
    {
        Assert.Equal(expectedShortLabel, CommunitySystemPostDisplay.ShortLabel(kind));
        Assert.Equal(expectedBadgeLabel, CommunitySystemPostDisplay.BadgeLabel(kind));
    }
}
