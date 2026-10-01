namespace Hongdal.Contracts.Common.Community;

public sealed record CommunityBoardDefinition(
    string Key,
    string DisplayName,
    string Description,
    string GroupCode,
    string GroupDisplayName,
    bool IsUserCreatable,
    bool IsPublic,
    string PostingAccessCode,
    IReadOnlyList<string> LegacyCategoryNames)
{
    public bool AllowsAnonymousPosting
        => PostingAccessCode == CommunityBoardPostingAccessCodes.Anonymous;

    public bool RequiresAuthenticatedPosting
        => PostingAccessCode == CommunityBoardPostingAccessCodes.Authenticated;

    public string PostingAccessDisplayName
        => CommunityBoardPostingAccessCodes.DisplayName(PostingAccessCode);
}

public sealed class CommunityBoardSummaryResponse
{
    public string BoardKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string GroupDisplayName { get; set; } = string.Empty;
    public bool IsUserCreatable { get; set; }
    public bool IsCustom { get; set; }
    public string PostingAccessCode { get; set; } = CommunityBoardPostingAccessCodes.Authenticated;
    public string PostingAccessDisplayName { get; set; } = CommunityBoardPostingAccessCodes.DisplayName(
        CommunityBoardPostingAccessCodes.Authenticated);
    public bool AllowsAnonymousPosting { get; set; }
    public int PostCount { get; set; }
    public DateTime? LatestPostAtUtc { get; set; }
}

public static class CommunityBoardKeys
{
    public const string NoticeGuide = "notice-guide";
    public const string Vow = "vow";
    public const string FreeLife = "free-life";
    public const string QuestionHelp = "question-help";
    public const string InformationPrices = "information-prices";
    public const string Food = "food";
    public const string Cargo = "cargo";
    public const string Prajna = "prajna";
    public const string Participation = "participation";
    public const string SalesSupply = "sales-supply";
    public const string LedgerProgress = "ledger-progress";
    public const string CompletionReview = "completion-review";
    public const string ProductFeedback = "product-feedback";
    public const string SafetyReport = "safety-report";
}

public static class CommunityBoardGroupCodes
{
    public const string PeopleAndInformation = "people-information";
    public const string CollectiveWork = "collective-work";
    public const string ServiceOperation = "service-operation";
    public const string Safety = "safety";
}

public static class CommunityBoardPostingAccessCodes
{
    public const string Anonymous = "anonymous";
    public const string Authenticated = "authenticated";
    public const string OperatorOnly = "operator-only";
    public const string Mixed = "mixed";

    public static string DisplayName(string? code)
        => code switch
        {
            Anonymous => "비로그인 작성 가능",
            Authenticated => "로그인 후 작성",
            OperatorOnly => "운영자 작성",
            Mixed => "게시판별 작성 조건",
            _ => "로그인 후 작성"
        };
}

public static class CommunityAnonymousNicknameCatalog
{
    public static string ResolveBaseName(string? category)
        => CommunityBoardCatalog.Find(category)?.Key switch
        {
            CommunityBoardKeys.Vow => "서원 적는 이웃",
            CommunityBoardKeys.FreeLife => "지나가는 이웃",
            CommunityBoardKeys.QuestionHelp => "궁금한 이웃",
            CommunityBoardKeys.InformationPrices => "시세 살피는 이웃",
            CommunityBoardKeys.Food => "골목 미식가",
            CommunityBoardKeys.SafetyReport => "익명 신고자",
            _ => "익명 이웃"
        };

    public static string Create(string? category, string? discriminator)
    {
        var baseName = ResolveBaseName(category);
        if (CommunityBoardCatalog.Find(category)?.Key == CommunityBoardKeys.SafetyReport)
        {
            return baseName;
        }

        var suffix = new string((discriminator ?? string.Empty)
            .Where(char.IsAsciiLetterOrDigit)
            .Take(4)
            .Select(char.ToUpperInvariant)
            .ToArray());
        return string.IsNullOrWhiteSpace(suffix)
            ? baseName
            : $"{baseName}-{suffix}";
    }

    public static string Preview(string? category)
        => CommunityBoardCatalog.Find(category)?.Key == CommunityBoardKeys.SafetyReport
            ? ResolveBaseName(category)
            : $"{ResolveBaseName(category)}-****";
}

/// <summary>
/// 게시판은 글의 목적을 나타내고, 공동구매·창고·운송 같은 업무 영역은 WorkflowTag로 분리합니다.
/// 기존 Category 값은 별칭으로 유지해 데이터 이관 없이 새 게시판에서 함께 조회합니다.
/// </summary>
public static class CommunityBoardCatalog
{
    public static CommunityBoardDefinition NoticeGuide { get; } = Board(
        CommunityBoardKeys.NoticeGuide,
        "공지·이용안내",
        "운영 공지와 이용·개인정보·거래 안전 안내",
        CommunityBoardGroupCodes.ServiceOperation,
        "서비스 운영",
        isUserCreatable: false,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.OperatorOnly,
        "공지",
        "이용안내");

    public static CommunityBoardDefinition Vow { get; } = Board(
        CommunityBoardKeys.Vow,
        "서원",
        "이루고 싶은 일과 함께 알아차릴 사람·업체를 가볍게 적고 마음을 모으는 공간",
        CommunityBoardGroupCodes.PeopleAndInformation,
        "사람과 정보",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Anonymous,
        "발원");

    public static CommunityBoardDefinition FreeLife { get; } = Board(
        CommunityBoardKeys.FreeLife,
        "자유·생활",
        "지역 이야기, 음식, 생활 정보와 일상 대화",
        CommunityBoardGroupCodes.PeopleAndInformation,
        "사람과 정보",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Anonymous,
        "자유");

    public static CommunityBoardDefinition QuestionHelp { get; } = Board(
        CommunityBoardKeys.QuestionHelp,
        "질문·도움",
        "생활과 업무의 궁금한 점을 함께 해결하는 공간",
        CommunityBoardGroupCodes.PeopleAndInformation,
        "사람과 정보",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Anonymous,
        "업무 질문",
        "운송 실무");

    public static CommunityBoardDefinition InformationPrices { get; } = Board(
        CommunityBoardKeys.InformationPrices,
        "정보·시세",
        "농수산물 가격, HS 코드, 전통시장과 공공데이터",
        CommunityBoardGroupCodes.PeopleAndInformation,
        "사람과 정보",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Anonymous,
        "정보 협업");

    public static CommunityBoardDefinition Food { get; } = Board(
        CommunityBoardKeys.Food,
        "음식",
        "음식 이야기와 조리·식재료 정보, 위치 동의 시 반경 7km 음식점 후보를 함께 보는 공간",
        CommunityBoardGroupCodes.PeopleAndInformation,
        "사람과 정보",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Anonymous,
        "맛집",
        "음식 정보");

    public static CommunityBoardDefinition Cargo { get; } = Board(
        CommunityBoardKeys.Cargo,
        "화물",
        "화물 정보와 운송 조건을 나누고 자격 역할·공개 화물 후보를 비구속적으로 살펴보는 공간",
        CommunityBoardGroupCodes.CollectiveWork,
        "함께하는 일",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Authenticated,
        "화물 운송",
        "운송 정보");

    public static CommunityBoardDefinition Prajna { get; } = Board(
        CommunityBoardKeys.Prajna,
        "반야",
        "관리자가 선별한 배움·철학·홍익학당 카드와 영상",
        CommunityBoardGroupCodes.PeopleAndInformation,
        "사람과 정보",
        isUserCreatable: false,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.OperatorOnly);

    public static CommunityBoardDefinition Participation { get; } = Board(
        CommunityBoardKeys.Participation,
        "모집·함께하기",
        "공동구매·공동주문 수요와 필요한 역할을 모으는 공간",
        CommunityBoardGroupCodes.CollectiveWork,
        "함께하는 일",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Authenticated,
        "공동구매",
        "모집");

    public static CommunityBoardDefinition SalesSupply { get; } = Board(
        CommunityBoardKeys.SalesSupply,
        "판매·공급",
        "생산자와 판매자의 상품·수량·공급 조건 제안",
        CommunityBoardGroupCodes.CollectiveWork,
        "함께하는 일",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Authenticated,
        "판매");

    public static CommunityBoardDefinition LedgerProgress { get; } = Board(
        CommunityBoardKeys.LedgerProgress,
        "진행·공동 원장",
        "합의된 업무의 단계, 변경 이력과 다이어그램 공유",
        CommunityBoardGroupCodes.CollectiveWork,
        "함께하는 일",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Authenticated,
        "생활 원장",
        "업무 기록",
        "시스템 다이어그램");

    public static CommunityBoardDefinition CompletionReview { get; } = Board(
        CommunityBoardKeys.CompletionReview,
        "완료 사례·후기",
        "개인정보를 줄인 완료 사례와 참여자 경험·신뢰 기록",
        CommunityBoardGroupCodes.CollectiveWork,
        "함께하는 일",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Authenticated,
        "성립 사례",
        "완료 사례",
        "후기");

    public static CommunityBoardDefinition ProductFeedback { get; } = Board(
        CommunityBoardKeys.ProductFeedback,
        "개선 제안",
        "홍달 기능, 화면과 업무 절차를 바꾸는 의견",
        CommunityBoardGroupCodes.ServiceOperation,
        "서비스 운영",
        isUserCreatable: true,
        isPublic: true,
        postingAccessCode: CommunityBoardPostingAccessCodes.Authenticated);

    public static CommunityBoardDefinition SafetyReport { get; } = Board(
        CommunityBoardKeys.SafetyReport,
        "신고·분쟁",
        "당사자와 운영자만 원문을 확인하는 보호 기록",
        CommunityBoardGroupCodes.Safety,
        "안전센터",
        isUserCreatable: false,
        isPublic: false,
        postingAccessCode: CommunityBoardPostingAccessCodes.Anonymous,
        "신고/분쟁",
        "신고",
        "분쟁");

    public static IReadOnlyList<CommunityBoardDefinition> All { get; } =
    [
        Vow,
        FreeLife,
        QuestionHelp,
        InformationPrices,
        Food,
        Cargo,
        Prajna,
        Participation,
        SalesSupply,
        LedgerProgress,
        CompletionReview,
        NoticeGuide,
        ProductFeedback,
        SafetyReport
    ];

    public static IReadOnlyList<CommunityBoardDefinition> PublicBoards { get; } =
        All.Where(board => board.IsPublic).ToArray();

    public static IReadOnlyList<CommunityBoardDefinition> UserCreatableBoards { get; } =
        PublicBoards.Where(board => board.IsUserCreatable).ToArray();

    public static CommunityBoardDefinition? Find(string? keyOrName)
        => string.IsNullOrWhiteSpace(keyOrName)
            ? null
            : All.FirstOrDefault(board =>
                IsSame(board.Key, keyOrName)
                || IsSame(board.DisplayName, keyOrName)
                || board.LegacyCategoryNames.Any(alias => IsSame(alias, keyOrName)));

    public static bool MatchesCategory(string? boardKeyOrName, string? category)
    {
        if (string.IsNullOrWhiteSpace(boardKeyOrName)
            || string.Equals(boardKeyOrName.Trim(), "전체", StringComparison.OrdinalIgnoreCase))
        {
            return !IsProtectedCategory(category);
        }

        var board = Find(boardKeyOrName);
        return board is null
            ? IsSame(boardKeyOrName, category)
            : IsSame(board.DisplayName, category)
              || board.LegacyCategoryNames.Any(alias => IsSame(alias, category));
    }

    public static IReadOnlyList<string> CategoryNamesFor(string keyOrName)
    {
        var board = Find(keyOrName);
        return board is null
            ? [keyOrName.Trim()]
            : new[] { board.DisplayName }
                .Concat(board.LegacyCategoryNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    public static string ResolveCanonicalCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return FreeLife.DisplayName;
        }

        return Find(category)?.DisplayName ?? category.Trim();
    }

    public static bool IsProtectedCategory(string? category)
        => IsSame(SafetyReport.Key, category)
           || IsSame(SafetyReport.DisplayName, category)
           || SafetyReport.LegacyCategoryNames.Any(alias => IsSame(alias, category));

    private static CommunityBoardDefinition Board(
        string key,
        string displayName,
        string description,
        string groupCode,
        string groupDisplayName,
        bool isUserCreatable,
        bool isPublic,
        string postingAccessCode,
        params string[] legacyCategoryNames)
        => new(
            key,
            displayName,
            description,
            groupCode,
            groupDisplayName,
            isUserCreatable,
            isPublic,
            postingAccessCode,
            legacyCategoryNames
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

    private static bool IsSame(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
