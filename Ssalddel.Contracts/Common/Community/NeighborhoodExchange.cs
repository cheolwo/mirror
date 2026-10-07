namespace Ssalddel.Contracts.Common.Community;

/// <summary>생활 교류 글의 분류와 라우트. 업무 역할이나 거래 인증을 뜻하지 않습니다.</summary>
public static class NeighborhoodExchange
{
    public const string Home = "/community/exchange";
    public const string Write = Home + "/write";
    public const string WorkflowTag = "생활 교류";
    public const string Offer = "제공해요";
    public const string Need = "필요해요";
    public const int PageSize = 20;
    public static string Detail(long id) => $"{Home}/posts/{id}";
    public static bool IsIntent(string? value) => value is Offer or Need;
    public static bool IsExchange(PlatformCommunityPostResponse post)
        => post.WorkflowTag == WorkflowTag && IsIntent(post.RoleTag)
           && post.Category == PlatformCommunityPostCategories.General && !post.IsReportBoardPost;
}
