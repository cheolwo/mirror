using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using 홍달.Services.Options;
using Microsoft.Extensions.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTubeSocialContextPostComposerTests
{
    [Fact]
    public void Compose_ProvidesExplicitCollectiveActionPathWithoutCreatingExecution()
    {
        var composer = new YouTubeSocialContextPostComposer(
            Options.Create(new ApifySocialMediaOptions { MaxDraftItemsPerSource = 3 }));

        var draft = composer.Compose(
            new YouTubeSocialContextVideoDto(
                "video-1",
                "Food channel",
                "Video title",
                "Video summary",
                "https://www.youtube.com/watch?v=video-1",
                null,
                new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc),
                "US",
                "en"),
            ["group order"],
            ["local food"],
            []);

        Assert.Equal("공동구매", draft.CollectiveAction.WorkflowTag);
        Assert.Equal(
            CommunityCollectiveIntentTypeCodes.GroupPurchase,
            draft.CollectiveAction.PrimaryIntentTypeCode);
        Assert.Contains(
            CommunityCollectiveIntentTypeCodes.GroupImportCandidate,
            draft.CollectiveAction.IntentTypeCodes);
        Assert.Contains("함께하기", draft.Body, StringComparison.Ordinal);
        Assert.Contains("이 영상을 보고 품은 서원", draft.Body, StringComparison.Ordinal);
        Assert.Contains("함께 알아차리고 싶은 사람·업체", draft.Body, StringComparison.Ordinal);
        Assert.Contains("비구속적 가원장", draft.Body, StringComparison.Ordinal);
        Assert.Contains("주문·계약·결제·배차·운송 주선을 확정하지 않습니다", draft.Body, StringComparison.Ordinal);
        Assert.Equal(
            "/api/v1/community/posts/{postId}/opportunities",
            draft.CollectiveAction.ParticipationEndpointTemplate);
    }
}
