using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.Orderer;

namespace Hongdal.Tests.Services.Orderer;

public sealed class DomesticGroupPurchaseNegotiationServiceTests
{
    [Fact]
    public async Task AppendEvent_PublishesMaskedProcessWithoutInternalUserOrContactDetails()
    {
        var service = CreateService();
        var campaignId = Guid.NewGuid();

        await service.AppendEventAsync(
            campaignId,
            "internal-user-197",
            new DomesticGroupPurchaseNegotiationEventRequest
            {
                EventTypeCode = DomesticGroupPurchaseNegotiationEventTypeCodes.CounterProposal,
                MaskedActorDisplayName = "생산자 김○○",
                ActorRoleLabel = "생산자",
                PublicSummary = "500kg 중 420kg은 금요일, 80kg은 토요일 분할 출하를 제안합니다."
            });

        var timeline = await service.GetTimelineAsync(campaignId);

        Assert.True(timeline.CommunityVisible);
        Assert.False(timeline.ContactDetailsDisclosed);
        var item = Assert.Single(timeline.Events);
        Assert.Equal("생산자 김○○", item.MaskedActorDisplayName);
        Assert.DoesNotContain("internal-user-197", item.PublicSummary);
        Assert.False(item.ContactDetailsDisclosed);
    }

    [Theory]
    [InlineData("연락은 producer@example.com 으로 주세요")]
    [InlineData("연락처는 010-1234-5678 입니다")]
    public async Task AppendEvent_RejectsContactDetailsInPublicText(string publicSummary)
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.AppendEventAsync(
            Guid.NewGuid(),
            "user-1",
            new DomesticGroupPurchaseNegotiationEventRequest
            {
                MaskedActorDisplayName = "김○○",
                ActorRoleLabel = "생산자",
                PublicSummary = publicSummary
            }));

        Assert.Contains("공개할 수 없습니다", error.Message);
    }

    [Fact]
    public async Task OpenIssue_RejectsDeliberationOutsideOneTo168Hours()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.OpenIssueAsync(
            Guid.NewGuid(),
            "user-1",
            NewIssueRequest(deliberationHours: 169)));

        Assert.Contains("168시간", error.Message);
    }

    [Fact]
    public async Task ResolveIssue_BeforeDeliberationClose_IsRejected()
    {
        var clock = new FakeNegotiationClock(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));
        var service = CreateService(clock);
        var campaignId = Guid.NewGuid();
        var issue = await service.OpenIssueAsync(campaignId, "user-1", NewIssueRequest());
        await service.AddPositionAsync(campaignId, issue.IssueId, "user-1", NewPositionRequest("대표 박○○"));
        await service.AddPositionAsync(campaignId, issue.IssueId, "user-2", NewPositionRequest("생산자 김○○"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveIssueAsync(
            campaignId,
            issue.IssueId,
            "user-1",
            NewResolutionRequest()));

        Assert.Contains("숙고 종료 시각 전", error.Message);
    }

    [Fact]
    public async Task ResolveIssue_RequiresTwoDistinctParticipants_AndResolverParticipation()
    {
        var clock = new FakeNegotiationClock(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));
        var service = CreateService(clock);
        var campaignId = Guid.NewGuid();
        var issue = await service.OpenIssueAsync(campaignId, "reporter", NewIssueRequest(deliberationHours: 1));
        await service.AddPositionAsync(campaignId, issue.IssueId, "user-1", NewPositionRequest("대표 박○○"));
        await service.AddPositionAsync(campaignId, issue.IssueId, "user-1", NewPositionRequest("대표 박○○", "대안을 보완합니다."));
        clock.Advance(TimeSpan.FromHours(2));

        var participantError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveIssueAsync(
            campaignId,
            issue.IssueId,
            "user-1",
            NewResolutionRequest()));
        Assert.Contains("서로 다른 구성원 2명", participantError.Message);

        await service.AddPositionAsync(campaignId, issue.IssueId, "user-2", NewPositionRequest("생산자 김○○"));
        var resolverError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveIssueAsync(
            campaignId,
            issue.IssueId,
            "observer-3",
            NewResolutionRequest()));
        Assert.Contains("의견을 남긴 구성원", resolverError.Message);

        var resolved = await service.ResolveIssueAsync(
            campaignId,
            issue.IssueId,
            "user-1",
            NewResolutionRequest());

        Assert.Equal(DomesticGroupPurchaseNegotiationIssueStatusCodes.Resolved, resolved.StatusCode);
        Assert.NotNull(resolved.Resolution);
        Assert.False(resolved.ContactDetailsDisclosed);
        Assert.Contains(
            (await service.GetTimelineAsync(campaignId)).Events,
            item => item.EventTypeCode == DomesticGroupPurchaseNegotiationEventTypeCodes.Resolution);
    }

    private static DomesticGroupPurchaseNegotiationService CreateService(FakeNegotiationClock? clock = null)
        => new(
            new InMemoryDomesticGroupPurchaseNegotiationStore(),
            clock ?? new FakeNegotiationClock(DateTimeOffset.UtcNow));

    private static DomesticGroupPurchaseNegotiationIssueRequest NewIssueRequest(int deliberationHours = 24)
        => new()
        {
            Title = "거점 처리량 확인",
            PublicSummary = "중간 거점이 전체 물량을 당일 처리할 수 있는지 확인해야 합니다.",
            MaskedReporterDisplayName = "참여자 이○○",
            ReporterRoleLabel = "공동구매 참여자",
            DeliberationHours = deliberationHours
        };

    private static DomesticGroupPurchaseDeliberationPositionRequest NewPositionRequest(
        string displayName,
        string rationale = "분할 입고 대안을 함께 검토해야 합니다.")
        => new()
        {
            PositionCode = DomesticGroupPurchaseDeliberationPositionCodes.Alternative,
            MaskedParticipantDisplayName = displayName,
            ParticipantRoleLabel = "커뮤니티 구성원",
            PublicRationale = rationale
        };

    private static DomesticGroupPurchaseNegotiationResolutionRequest NewResolutionRequest()
        => new()
        {
            MaskedResolverDisplayName = "대표 박○○",
            ResolverRoleLabel = "공동구매 대표",
            ResolutionSummary = "물량을 이틀로 나누어 입고합니다.",
            DecisionRationale = "거점 일 처리량과 생산자의 분할 출하 가능량을 함께 반영했습니다."
        };

    private sealed class FakeNegotiationClock : IDomesticGroupPurchaseNegotiationClock
    {
        public FakeNegotiationClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan duration)
            => UtcNow = UtcNow.Add(duration);
    }
}
