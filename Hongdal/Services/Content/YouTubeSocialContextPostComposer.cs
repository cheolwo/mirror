using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Community;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.Content;

public interface IYouTubeSocialContextPostComposer
{
    YouTubeSocialContextPostDraftDto Compose(
        YouTubeSocialContextVideoDto video,
        IReadOnlyList<string> searchTerms,
        IReadOnlyList<string> adjacentTopics,
        IReadOnlyList<CommunityInformationCandidateDto> items);
}

public sealed class YouTubeSocialContextPostComposer : IYouTubeSocialContextPostComposer
{
    private readonly int _maxItemsPerSource;

    public YouTubeSocialContextPostComposer(IOptions<ApifySocialMediaOptions> options)
    {
        _maxItemsPerSource = Math.Clamp(options.Value.MaxDraftItemsPerSource, 1, 10);
    }

    public YouTubeSocialContextPostDraftDto Compose(
        YouTubeSocialContextVideoDto video,
        IReadOnlyList<string> searchTerms,
        IReadOnlyList<string> adjacentTopics,
        IReadOnlyList<CommunityInformationCandidateDto> items)
    {
        ArgumentNullException.ThrowIfNull(video);
        var lines = new List<string>
        {
            "영상에서 시작한 이야기",
            string.Empty,
            video.Title,
            video.OriginalUrl
        };

        var videoSummary = Normalize(video.Summary, 500);
        if (videoSummary is not null)
        {
            lines.Add(string.Empty);
            lines.Add(videoSummary);
        }

        if (searchTerms.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"함께 살펴본 핵심어: {string.Join(", ", searchTerms)}");
        }

        if (adjacentTopics.Count > 0)
        {
            lines.Add($"인접 주제: {string.Join(", ", adjacentTopics)}");
            lines.Add("아래 자료에는 영상을 직접 언급하지 않지만 인접 주제를 이해하는 데 도움이 되는 공개 게시물도 포함되었습니다.");
        }

        lines.Add(string.Empty);
        lines.Add("이 영상을 보고 품은 서원");
        lines.Add("영상을 보며 같이 알아보거나 이루고 싶어진 일을 작성자가 직접 적어 주세요.");

        lines.Add(string.Empty);
        lines.Add("공개 커뮤니티에서 함께 본 이야기");
        if (items.Count == 0)
        {
            lines.Add(string.Empty);
            lines.Add("아직 검토할 SNS 공개 자료가 없습니다. 영상을 본 느낌과 확인하고 싶은 점을 직접 보완해 주세요.");
        }
        else
        {
            foreach (var group in items
                         .GroupBy(item => item.SourceKey, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.First().Provider, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add(string.Empty);
                lines.Add($"[{ProviderName(group.First())}]");
                foreach (var item in group
                             .OrderByDescending(candidate => candidate.PublishedAtUtc)
                             .Take(_maxItemsPerSource))
                {
                    lines.Add($"- {item.Title}");
                    lines.Add($"  {Normalize(item.Summary, 180)}");
                    lines.Add($"  원문: {item.OriginalUrl}");
                }
            }
        }

        lines.Add(string.Empty);
        lines.Add("이 자료들을 엮어 보며");
        lines.Add("각 게시물은 작성자 개인의 경험과 견해입니다. 반복되는 질문, 서로 다른 관점, 우리 커뮤니티에서 더 확인할 점을 작성자가 직접 정리해 주세요.");
        lines.Add(string.Empty);
        lines.Add("함께 알아차리고 싶은 사람·업체");
        lines.Add("판매자·생산자·구매자·수출입 관계자·운송·통관·창고 중 이 일을 함께 살펴볼 사람과 아직 확인해야 할 업체를 적어 주세요.");
        lines.Add(string.Empty);
        lines.Add("같이 해보고 싶다면 이 글의 ‘함께하기’에서 구매자·공급자·운송·통관·창고 역할 중 가능한 것을 표시해 주세요.");
        lines.Add("관심이 모이면 공동구매 또는 공동수입 검토를 위한 비구속적 가원장으로 조건을 함께 살펴볼 수 있습니다.");
        lines.Add("참여 표시는 주문·계약·결제·배차·운송 주선을 확정하지 않습니다.");
        lines.Add("다음 단계는 별도 확인과 동의로 진행합니다.");
        lines.Add(string.Empty);
        lines.Add("※ 원문 링크에서 내용·작성일·출처를 다시 확인한 뒤 게시해 주세요. 이 초안은 여론 조사, 사실 확정, 상품 추천이 아닙니다.");

        return new YouTubeSocialContextPostDraftDto(
            Normalize($"[서원·함께 보기] {video.Title}", 160) ?? "[서원·함께 보기] YouTube 영상",
            string.Join(Environment.NewLine, lines),
            new YouTubeSocialContextCollectiveActionDraftDto(
                "공동구매",
                CommunityCollectiveIntentTypeCodes.GroupPurchase,
                [
                    CommunityCollectiveIntentTypeCodes.GroupPurchase,
                    CommunityCollectiveIntentTypeCodes.GroupImportCandidate
                ],
                "관심이 있으면 게시글의 함께하기에서 역할과 수요를 표시합니다.",
                "비구속적 관심 표시이며 주문·계약·결제·배차·운송 주선을 확정하지 않습니다.",
                "/api/v1/community/posts/{postId}/opportunities"));
    }

    private static string ProviderName(CommunityInformationCandidateDto candidate)
        => candidate.Provider.Split('·', 2, StringSplitOptions.TrimEntries)[0];

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = string.Join(
            ' ',
            (value ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
