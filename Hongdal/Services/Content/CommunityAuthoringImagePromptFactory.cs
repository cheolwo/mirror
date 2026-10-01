using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Metadata;

namespace Hongdal.Services.Content;

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.Application,
    "문맥 그룹을 Kie.ai용 편집 이미지 프롬프트로 변환하고 연락처를 제거",
    FlowOrder = 42,
    Boundary = "URL, 이메일, 한국·북미 전화번호를 외부 프롬프트에서 제거합니다.")]
internal static class CommunityAuthoringImagePromptFactory
{
    private const int MaximumExternalContextLength = 2_200;
    private const string OmissionMarker = "\n[중간 문맥 일부 생략]\n";
    private static readonly Regex UrlPattern = new(
        @"https?://\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EmailPattern = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex KoreanMobilePattern = new(
        @"(?<!\d)(?:\+?82[-\s]?)?0?1[016789][ -]?\d{3,4}[ -]?\d{4}(?!\d)",
        RegexOptions.CultureInvariant);
    private static readonly Regex NorthAmericanPhonePattern = new(
        @"(?<!\d)(?:\+?1[ .-])?\(?\d{3}\)?[ .-]+\d{3}[ .-]+\d{4}(?!\d)",
        RegexOptions.CultureInvariant);

    public static CommunityAuthoringImagePromptSegmentDto Create(
        string articleTitle,
        CommunityAuthoringImageContextGroup group,
        int sequence,
        int total,
        string aspectRatio)
    {
        var visualFocus = ResolveVisualFocus($"{group.Title}\n{group.Context}");
        var externalArticleTitle = SanitizeForExternalPrompt(articleTitle);
        var externalGroupTitle = SanitizeForExternalPrompt(group.Title);
        var externalContext = CompactForExternalPrompt(SanitizeForExternalPrompt(group.Context));
        var composition = ResolveComposition(aspectRatio);
        var prompt = $$"""
            커뮤니티 게시글의 {{sequence}}/{{total}} 문맥을 표현하는 편집용 이미지를 한 장 생성한다.

            게시글 제목: {{externalArticleTitle}}
            문맥 제목: {{externalGroupTitle}}
            문맥 내용:
            {{externalContext}}

            시각적 초점: {{visualFocus}}
            시리즈 일관성: 같은 게시글에 이어지는 이미지처럼 현실적인 다큐멘터리형 편집 사진, 자연광, 실제 생활 공간, 차분하고 신뢰할 수 있는 색감으로 표현한다.
            구도: {{composition}}
            제약: 이미지 안에 읽을 수 있는 글자, 숫자, 브랜드 로고, 공식 인장, 증명서 또는 UI를 넣지 않는다. 계약이나 거래가 확정된 것처럼 묘사하지 않으며 실제 현장, 상품, 통계 또는 업무 증빙으로 오인될 연출을 피한다.
            """;

        return new CommunityAuthoringImagePromptSegmentDto(
            $"context-{sequence:00}",
            sequence,
            group.Title,
            group.Context,
            prompt.Trim(),
            aspectRatio,
            true);
    }

    private static string CompactForExternalPrompt(string context)
    {
        if (context.Length <= MaximumExternalContextLength)
        {
            return context;
        }

        var available = MaximumExternalContextLength - OmissionMarker.Length;
        var leadingLength = available * 2 / 3;
        var trailingLength = available - leadingLength;
        return $"{context[..leadingLength].TrimEnd()}{OmissionMarker}{context[^trailingLength..].TrimStart()}";
    }

    private static string SanitizeForExternalPrompt(string context)
    {
        var sanitized = UrlPattern.Replace(context, "[링크 생략]");
        sanitized = EmailPattern.Replace(sanitized, "[이메일 생략]");
        sanitized = KoreanMobilePattern.Replace(sanitized, "[전화번호 생략]");
        return NorthAmericanPhonePattern.Replace(sanitized, "[전화번호 생략]");
    }

    private static string ResolveVisualFocus(string context)
    {
        if (ContainsAny(context, "가격", "통계", "자료", "근거", "비교", "cost", "price", "data"))
        {
            return "여러 사람이 출처 자료와 읽을 수 없는 추상적 차트를 함께 검토하며 조건을 비교하는 장면";
        }

        if (ContainsAny(context, "운송", "창고", "입고", "출고", "배송", "물류", "warehouse", "delivery", "logistics"))
        {
            return "공급자, 창고 담당자와 배송 관계자가 물류 인계 절차를 함께 확인하는 현실적인 장면";
        }

        if (ContainsAny(context, "수입", "수출", "통관", "관세", "해외", "import", "export", "customs"))
        {
            return "국경 간 공급 과정을 준비하는 구매자와 실무 전문가가 서류 없는 시각 자료를 놓고 협의하는 장면";
        }

        if (ContainsAny(context, "이익", "부담", "조건", "협상", "합의", "win-win", "benefit"))
        {
            return "참여자들이 각자의 부담과 이익이 균형을 이루는 조건을 차분하게 협의하는 장면";
        }

        if (ContainsAny(context, "원장", "다이어그램", "여정", "행동", "실행", "단계", "ledger", "diagram", "journey"))
        {
            return "참여자들이 전체 여정과 다음 행동을 함께 계획하되 화면 글자는 보이지 않는 장면";
        }

        if (ContainsAny(context, "참여", "함께", "사람", "업체", "역할", "마음", "공동", "community", "together"))
        {
            return "서로 다른 역할의 이웃과 관계자가 원형으로 모여 정보를 나누고 참여 의사를 확인하는 장면";
        }

        return "본문의 핵심 대상과 그 일을 함께 준비하는 사람들의 관계가 한눈에 드러나는 생활 밀착형 장면";
    }

    private static bool ContainsAny(string source, params string[] keywords)
        => keywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static string ResolveComposition(string aspectRatio)
        => aspectRatio switch
        {
            CommunityAuthoringImageAspectRatios.Square => "정사각형 피드에 맞는 안정적인 중심 구도와 충분한 여백",
            CommunityAuthoringImageAspectRatios.Portrait => "세로형 모바일 글에 맞는 전경, 중경, 배경의 깊이가 있는 구도",
            CommunityAuthoringImageAspectRatios.Auto => "핵심 대상과 참여 관계가 가장 명확하게 보이는 자연스러운 구도",
            _ => "가로형 게시글 본문에 맞는 좌우 흐름과 다음 문맥을 암시하는 구도"
        };
}
