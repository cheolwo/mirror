using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using 홍달.Services.Images;
using 홍달.도메인.공통;

namespace Hongdal.Tests.Services.Content;

public sealed class CommunityAuthoringImagePromptPlannerTests
{
    private readonly CommunityAuthoringImagePromptPlanner _sut = new();

    [Fact]
    public void 소제목과문단을_연속된이미지문맥으로묶는다()
    {
        var result = _sut.Plan(new CommunityAuthoringImagePromptPlanRequest
        {
            Title = "지역 식재료 공동구매를 준비합니다",
            Body = """
                   수요를 알아보는 이유
                   이웃이 원하는 품목과 수량을 공개적으로 확인합니다.

                   공급 조건을 비교하는 단계
                   공개 가격 자료를 모아 부담과 이익을 함께 비교합니다.

                   실행 전에 확인할 일
                   참여자와 공급자가 다음 행동을 합의하기 전에 질문을 남깁니다.
                   """,
            MaxImages = 2,
            AspectRatio = CommunityAuthoringImageAspectRatios.Landscape
        });

        Assert.Equal(3, result.SourceSectionCount);
        Assert.Equal(2, result.Segments.Count);
        Assert.Contains("수요를 알아보는 이유", result.Segments[0].Context, StringComparison.Ordinal);
        Assert.Contains("공급 조건을 비교하는 단계", result.Segments[0].Context, StringComparison.Ordinal);
        Assert.DoesNotContain("실행 전에 확인할 일", result.Segments[0].Context, StringComparison.Ordinal);
        Assert.Contains("실행 전에 확인할 일", result.Segments[1].Context, StringComparison.Ordinal);
        Assert.All(result.Segments, segment =>
        {
            Assert.Contains(result.ArticleTitle, segment.Prompt, StringComparison.Ordinal);
            Assert.Contains(segment.Context, segment.Prompt, StringComparison.Ordinal);
            Assert.Contains("실제 현장", segment.Prompt, StringComparison.Ordinal);
            Assert.True(segment.IsSelectedByDefault);
        });
    }

    [Fact]
    public void 소제목이없으면_빈줄로구분된문단을사용한다()
    {
        var result = _sut.Plan(new CommunityAuthoringImagePromptPlanRequest
        {
            Title = "함께 살펴볼 자료",
            Body = "첫 문단에서는 공개 자료를 확인합니다.\n\n두 번째 문단에서는 참여 조건을 질문합니다.",
            MaxImages = 4
        });

        Assert.Equal(2, result.SourceSectionCount);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal("도입", result.Segments[0].Title);
        Assert.Equal("문맥 2", result.Segments[1].Title);
    }

    [Fact]
    public void 물류문맥에는_관계자와인계절차를시각적초점으로넣는다()
    {
        var result = _sut.Plan(new CommunityAuthoringImagePromptPlanRequest
        {
            Title = "공동주문 물류 준비",
            Body = "창고 입고 이후 지역 배송 관계자가 출고 순서를 함께 확인합니다.",
            MaxImages = 1,
            AspectRatio = CommunityAuthoringImageAspectRatios.Portrait
        });

        var segment = Assert.Single(result.Segments);
        Assert.Contains("물류 인계 절차", segment.Prompt, StringComparison.Ordinal);
        Assert.Contains("세로형 모바일 글", segment.Prompt, StringComparison.Ordinal);
        Assert.Equal(CommunityAuthoringImageAspectRatios.Portrait, segment.AspectRatio);
    }

    [Fact]
    public void 게시글첨부한도를넘는계획은_거부한다()
    {
        var exception = Assert.Throws<ArgumentException>(() => _sut.Plan(
            new CommunityAuthoringImagePromptPlanRequest
            {
                Title = "이미지 계획",
                MaxImages = 6
            }));

        Assert.Contains("5개 이하", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 외부프롬프트에는_링크와연락처를자동으로넣지않는다()
    {
        var result = _sut.Plan(new CommunityAuthoringImagePromptPlanRequest
        {
            Title = "trade@example.com 업체와 함께 준비할 일",
            Body = "원문 https://example.com/contact 와 trade@example.com, 010-1234-5678을 확인합니다.",
            MaxImages = 1
        });

        var segment = Assert.Single(result.Segments);
        Assert.Contains("https://example.com/contact", segment.Context, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/contact", segment.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("trade@example.com", segment.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("010-1234-5678", segment.Prompt, StringComparison.Ordinal);
        Assert.Contains("[링크 생략]", segment.Prompt, StringComparison.Ordinal);
        Assert.Contains("[이메일 생략]", segment.Prompt, StringComparison.Ordinal);
        Assert.Contains("[전화번호 생략]", segment.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void 문맥프롬프트는_Kie요청용최종프롬프트에포함된다()
    {
        var plan = _sut.Plan(new CommunityAuthoringImagePromptPlanRequest
        {
            Title = "지역 공동주문",
            Body = "주민과 공급자가 필요한 수량을 함께 확인합니다.",
            MaxImages = 1
        });
        var segment = Assert.Single(plan.Segments);
        var generator = new 커뮤니티글쓰기이미지프롬프트생성기();

        var outboundPrompt = generator.CreatePrompt(new 이미지생성요청
        {
            이미지용도 = 생성이미지용도.커뮤니티글쓰기이미지,
            제목 = segment.Title,
            설명 = segment.Prompt,
            추가맥락 = "AI 생성 이미지는 실제 업무 증빙이 아닙니다."
        });

        Assert.Contains(segment.Prompt, outboundPrompt, StringComparison.Ordinal);
        Assert.Contains("Do not present invented", outboundPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void 긴원문은_내부문맥에보존하고_외부프롬프트에서생략을표시한다()
    {
        var body = $"앞부분 {new string('가', 2_800)} 뒷부분";

        var result = _sut.Plan(new CommunityAuthoringImagePromptPlanRequest
        {
            Title = "긴 문맥을 확인하는 글",
            Body = body,
            MaxImages = 1
        });

        var segment = Assert.Single(result.Segments);
        Assert.Contains(body, segment.Context, StringComparison.Ordinal);
        Assert.Contains("앞부분", segment.Prompt, StringComparison.Ordinal);
        Assert.Contains("뒷부분", segment.Prompt, StringComparison.Ordinal);
        Assert.Contains("[중간 문맥 일부 생략]", segment.Prompt, StringComparison.Ordinal);
        Assert.True(segment.Prompt.Length <= CommunityAuthoringImageLimits.MaximumPromptLength);
    }
}
