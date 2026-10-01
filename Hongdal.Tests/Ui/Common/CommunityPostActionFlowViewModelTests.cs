using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class CommunityPostActionFlowViewModelTests
{
    [Fact]
    public void 아직시작하지않은글은_마음모으기를대표행동으로제시한다()
    {
        var flow = CommunityPostActionFlowViewModel.Create(new CommunityPostOpportunityListResponse
        {
            PostId = 42,
            Participation = new CommunityPostParticipationEntryResponse
            {
                CanStart = true
            },
            Journey = new CommunityActionJourneyResponse
            {
                PostId = 42,
                CurrentStageCode = CommunityActionJourneyStageCodes.Conversation,
                CurrentStageLabel = "이야기 나누는 중"
            }
        });

        Assert.Equal(CommunityPostPrimaryActionKind.StartGathering, flow.PrimaryActionKind);
        Assert.Equal("이 글에서 마음 모으기", flow.PrimaryActionLabel);
        Assert.True(flow.Stages.Single(stage => stage.Code == CommunityActionJourneyStageCodes.Conversation).IsCurrent);
    }

    [Fact]
    public void 관심투표가열리면_역할선택을다음행동으로제시한다()
    {
        var flow = CommunityPostActionFlowViewModel.Create(new CommunityPostOpportunityListResponse
        {
            PostId = 51,
            Participation = new CommunityPostParticipationEntryResponse
            {
                CanStart = false,
                CanJoin = true,
                InterestVoteId = Guid.NewGuid()
            },
            Journey = new CommunityActionJourneyResponse
            {
                PostId = 51,
                CurrentStageCode = CommunityActionJourneyStageCodes.Gathering,
                CurrentStageLabel = "마음 모으는 중",
                InterestVoteId = Guid.NewGuid()
            }
        });

        Assert.Equal(CommunityPostPrimaryActionKind.ShowParticipationDetails, flow.PrimaryActionKind);
        Assert.Equal("가능한 역할 고르기", flow.PrimaryActionLabel);
        Assert.True(flow.HasParticipationDetails);
    }

    [Fact]
    public void 역할구성단계는_원문에서시작된정확한Campaign으로이동한다()
    {
        var campaignId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var flow = CommunityPostActionFlowViewModel.Create(new CommunityPostOpportunityListResponse
        {
            PostId = 77,
            Participation = new CommunityPostParticipationEntryResponse
            {
                InterestVoteId = campaignId,
                ProvisionalLedgerId = "ledger-77"
            },
            Journey = new CommunityActionJourneyResponse
            {
                PostId = 77,
                CurrentStageCode = CommunityActionJourneyStageCodes.Party,
                CurrentStageLabel = "함께할 사람을 찾는 중",
                InterestVoteId = campaignId,
                ProvisionalLedgerId = "ledger-77",
                RequiredRoleCount = 4,
                FilledRequiredRoleCount = 2
            }
        });

        Assert.Equal(CommunityPostPrimaryActionKind.OpenJourney, flow.PrimaryActionKind);
        Assert.Equal(
            $"/community/actions/party?campaignId={campaignId:D}",
            flow.PrimaryActionRoute);
        Assert.Equal(4, flow.RequiredRoleCount);
        Assert.Equal(2, flow.FilledRequiredRoleCount);
    }

    [Fact]
    public void 완료된일은_완료기록으로이어진다()
    {
        var campaignId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var flow = CommunityPostActionFlowViewModel.Create(new CommunityPostOpportunityListResponse
        {
            PostId = 88,
            Participation = new CommunityPostParticipationEntryResponse
            {
                InterestVoteId = campaignId
            },
            Journey = new CommunityActionJourneyResponse
            {
                PostId = 88,
                CurrentStageCode = CommunityActionJourneyStageCodes.Completed,
                CurrentStageLabel = "함께 완료함",
                InterestVoteId = campaignId
            }
        });

        Assert.Equal("완료 기록 보기", flow.PrimaryActionLabel);
        Assert.Equal(
            $"/community/actions/completed?campaignId={campaignId:D}",
            flow.PrimaryActionRoute);
        Assert.True(flow.Stages.Last().IsCurrent);
        Assert.All(flow.Stages.Take(flow.Stages.Count - 1), stage => Assert.True(stage.IsComplete));
    }
}
