using Hongdal.Contracts.Common.Community;

namespace Hongdal.Tests.Contracts.Common.Community;

public sealed class CommunityMutualBenefitAssessmentEvaluatorTests
{
    [Fact]
    public void MissingSharedRulesAndRoleDetails_RequiresInformation()
    {
        var result = CommunityMutualBenefitAssessmentEvaluator.Evaluate(new()
        {
            Roles =
            [
                new CommunityMutualBenefitRoleInput { RoleKey = "buyer", RoleLabel = "구매자" }
            ]
        });

        Assert.Equal(CommunityMutualBenefitAssessmentStatusCodes.NeedsInformation, result.StatusCode);
        Assert.Contains(result.Issues, issue => issue.Contains("함께 이루려는 목적", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Contains("2개 이상", StringComparison.Ordinal));
        Assert.Equal(
            CommunityMutualBenefitRoleStatusCodes.NeedsInformation,
            Assert.Single(result.Roles).StatusCode);
    }

    [Fact]
    public void KnownNonPositiveNetBenefit_RequiresAdjustment()
    {
        var request = CompleteRequest(reviewed: false);
        request.Roles[0].ExpectedBenefitAmount = 10_000m;
        request.Roles[0].ExpectedBurdenAmount = 12_000m;

        var result = CommunityMutualBenefitAssessmentEvaluator.Evaluate(request);

        Assert.Equal(CommunityMutualBenefitAssessmentStatusCodes.NeedsAdjustment, result.StatusCode);
        Assert.True(result.HasKnownImbalance);
        Assert.Equal(-2_000m, result.Roles[0].NetBenefitAmount);
        Assert.Contains(result.Roles[0].Issues, issue => issue.Contains("크지 않습니다", StringComparison.Ordinal));
    }

    [Fact]
    public void CompleteAuthorEstimateWithoutParticipantReview_IsReadyForConversation()
    {
        var result = CommunityMutualBenefitAssessmentEvaluator.Evaluate(
            CompleteRequest(reviewed: false));

        Assert.Equal(CommunityMutualBenefitAssessmentStatusCodes.ReadyForConversation, result.StatusCode);
        Assert.False(result.AllRolesReviewed);
        Assert.Contains(result.Warnings, warning => warning.Contains("작성자의 가정", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryRoleReviewedWithPositiveOrQualitativeBenefit_IsCandidateNotExecutionApproval()
    {
        var request = CompleteRequest(reviewed: true);
        request.Roles[0].ExpectedBenefitAmount = 15_000m;
        request.Roles[0].ExpectedBurdenAmount = 10_000m;
        request.Roles[1].ExpectedBenefitAmount = 20_000m;
        request.Roles[1].ExpectedBurdenAmount = 12_000m;

        var result = CommunityMutualBenefitAssessmentEvaluator.Evaluate(request);

        Assert.Equal(CommunityMutualBenefitAssessmentStatusCodes.MutualBenefitCandidate, result.StatusCode);
        Assert.True(result.IsMutualBenefitCandidate);
        Assert.True(result.AllRolesReviewed);
        Assert.Equal(2, result.QuantifiedRoleCount);
        Assert.Contains("거래 실행을 대신하지 않습니다", CommunityMutualBenefitAssessmentResult.BoundaryNotice, StringComparison.Ordinal);
        Assert.Contains("공동조달 경제성 계획", CommunityMutualBenefitAssessmentResult.EconomicValidationNotice, StringComparison.Ordinal);
    }

    private static CommunityMutualBenefitAssessmentRequest CompleteRequest(bool reviewed)
        => new()
        {
            SharedPurpose = "같은 품질의 식재료를 예측 가능한 조건으로 함께 확보합니다.",
            AllocationRule = "실제 주문 수량과 맡은 작업 범위에 따라 비용과 편익을 나눕니다.",
            ExitRule = "목표 단가나 품질 근거가 맞지 않으면 계약 전 다시 협의합니다.",
            EvidenceNote = "2026-07-19 공개 가격 자료와 공급자 예비 견적",
            CurrencyCode = "KRW",
            Roles =
            [
                Role("buyer", "구매자", reviewed),
                Role("supplier", "공급자", reviewed)
            ]
        };

    private static CommunityMutualBenefitRoleInput Role(
        string roleKey,
        string roleLabel,
        bool reviewed)
        => new()
        {
            RoleKey = roleKey,
            RoleLabel = roleLabel,
            ExpectedBenefit = "예측 가능한 가격과 수량을 확보합니다.",
            ContributionOrBurden = "수량과 이행 조건을 확인합니다.",
            RiskOrCondition = "가격, 품질과 취소 조건을 다시 확인해야 합니다.",
            ParticipantReviewed = reviewed
        };
}
