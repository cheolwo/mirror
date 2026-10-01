using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum WorkCommunityDraftKind
{
    Question,
    WorkDone,
    Suggestion,
    InternationalCoordination
}

public sealed record CommunityDiagramDraftNode(
    string Title,
    string GroupLabel,
    string Description,
    string Kind,
    string? Condition = null);

public sealed record CommunityDiagramDraftEdge(
    string FromTitle,
    string ToTitle,
    string Label);

public sealed record CommunityComposerDraftTransition(
    string LedgerTemplateKey,
    string Category,
    string WorkflowTag,
    string RoleTag,
    string Title,
    string Body,
    bool IsReportBoardPost,
    string StatusMessage);

public sealed class PlatformCommunityDiagramWorkspaceViewModel : 조립ViewModelBase
{
    private string _selectedLedgerTemplateKey = CommunityLedgerTemplateKeys.CargoTransport;
    private string? _selectedCurrentLedgerId;
    private DiagramSnapshotDto? _sharedLedgerDiagramSnapshot;
    private bool _isApiEndpointMetadataLoading;
    private string? _ledgerSubmissionMessage;
    private CommunityComposerMessageKind _ledgerSubmissionMessageKind = CommunityComposerMessageKind.Info;

    public PlatformCommunityDiagramWorkspaceViewModel()
        : this(
            new PlatformCommunityDiagramChatViewModel(NoopDiagramCollaborationClientService.Instance),
            new PlatformCommunityDiagramCanvasViewModel())
    {
    }

    public PlatformCommunityDiagramWorkspaceViewModel(
        PlatformCommunityDiagramChatViewModel chat,
        PlatformCommunityDiagramCanvasViewModel canvas)
    {
        Chat = 하위ViewModel등록(chat, 수명소유: true);
        Canvas = 하위ViewModel등록(canvas);
    }

    public PlatformCommunityDiagramChatViewModel Chat { get; }

    public PlatformCommunityDiagramCanvasViewModel Canvas { get; }

    public string SelectedLedgerTemplateKey
    {
        get => _selectedLedgerTemplateKey;
        set => SetProperty(
            ref _selectedLedgerTemplateKey,
            string.IsNullOrWhiteSpace(value)
                ? CommunityLedgerTemplateKeys.CargoTransport
                : value.Trim());
    }

    public Dictionary<string, string> LedgerBlockValues { get; } = [];

    public Dictionary<string, string> FormValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> ApiPathParameterValues { get; } = [];

    public Dictionary<string, WorkflowApiEndpointDto> ApiEndpointMetadata { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, bool> FeatureFlagStates { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? SelectedCurrentLedgerId
    {
        get => _selectedCurrentLedgerId;
        set => SetProperty(ref _selectedCurrentLedgerId, value);
    }

    public DiagramSnapshotDto? SharedLedgerDiagramSnapshot
    {
        get => _sharedLedgerDiagramSnapshot;
        set => SetProperty(ref _sharedLedgerDiagramSnapshot, value);
    }

    public bool IsApiEndpointMetadataLoading
    {
        get => _isApiEndpointMetadataLoading;
        set => SetProperty(ref _isApiEndpointMetadataLoading, value);
    }

    public string? LedgerSubmissionMessage
    {
        get => _ledgerSubmissionMessage;
        set => SetProperty(ref _ledgerSubmissionMessage, value);
    }

    public CommunityComposerMessageKind LedgerSubmissionMessageKind
    {
        get => _ledgerSubmissionMessageKind;
        set => SetProperty(ref _ledgerSubmissionMessageKind, value);
    }

    public CommunityComposerDraftTransition CreateDiagramShareDraft(
        string? currentWorkflowTag,
        string? currentTitle,
        string? currentBody,
        string roleTag)
        => new(
            SelectedLedgerTemplateKey,
            "시스템 다이어그램",
            string.IsNullOrWhiteSpace(currentWorkflowTag) ? "국내 화물 운송" : currentWorkflowTag,
            roleTag,
            string.IsNullOrWhiteSpace(currentTitle) ? "업무 흐름 다이어그램 공유" : currentTitle,
            string.IsNullOrWhiteSpace(currentBody)
                ? "요청 접수 -> 참여자 확인 -> 업무 처리 -> 결과 공유\n\n각 단계에서 함께 확인할 내용과 개선 의견을 적어주세요."
                : currentBody,
            false,
            "다이어그램 게시글 초안을 준비했습니다. 화살표로 연결한 단계는 게시판에서 바로 미리보기 됩니다.");

    public CommunityComposerDraftTransition CreateWorkDraft(
        WorkCommunityDraftKind kind,
        string appName,
        string roleLabel,
        string roleTag)
    {
        var contextLine = $"앱: {appName}\n역할: {roleLabel}";
        var category = kind switch
        {
            WorkCommunityDraftKind.Question => "업무 질문",
            WorkCommunityDraftKind.WorkDone => "업무 기록",
            WorkCommunityDraftKind.Suggestion => "개선 제안",
            WorkCommunityDraftKind.InternationalCoordination => "국제 소통",
            _ => "업무 공유"
        };
        var workflowTag = kind == WorkCommunityDraftKind.InternationalCoordination
            ? "통관·무역 데이터"
            : "국내 화물 운송";
        var title = kind switch
        {
            WorkCommunityDraftKind.Question => $"[{roleLabel}] 업무 중 궁금한 점",
            WorkCommunityDraftKind.WorkDone => $"[{roleLabel}] 오늘 처리한 업무 공유",
            WorkCommunityDraftKind.Suggestion => $"[{roleLabel}] 업무 개선 제안",
            WorkCommunityDraftKind.InternationalCoordination => $"[{roleLabel}] 국제 소통 확인 요청",
            _ => $"[{roleLabel}] 업무 공유"
        };
        var body = kind switch
        {
            WorkCommunityDraftKind.Question =>
                $"{contextLine}\n\n궁금한 점:\n- \n\n확인한 상황:\n- \n\n도움이 필요한 부분:\n- ",
            WorkCommunityDraftKind.WorkDone =>
                $"{contextLine}\n\n처리한 일:\n- \n\n관련된 업무/화면:\n- \n\n다음에 이어서 볼 점:\n- ",
            WorkCommunityDraftKind.Suggestion =>
                $"{contextLine}\n\n불편했던 점:\n- \n\n제안하는 개선 방향:\n- \n\n기대 효과:\n- ",
            WorkCommunityDraftKind.InternationalCoordination =>
                $"{contextLine}\n\n관련 국가/지역:\n- \n\n관련 업무:\n- 통관 / 해외 창고 / 해외 파트너 / 외국인 참여자 / 언어 확인 중 해당 항목을 남겨주세요.\n\n소통 대상:\n- \n\n확인이 필요한 내용:\n- \n\n언어/문화상 조심할 점:\n- \n\n다음에 이어질 업무:\n- ",
            _ => $"{contextLine}\n\n공유할 내용:\n- "
        };

        return new(
            SelectedLedgerTemplateKey,
            category,
            workflowTag,
            roleTag,
            title,
            body,
            false,
            "업무 맥락을 커뮤니티 글 초안으로 옮겼습니다. 닉네임과 비밀번호를 입력한 뒤 필요한 내용을 보완해 등록하세요.");
    }

    public CommunityComposerDraftTransition CreateCommunityDraft(
        IReadOnlyList<CommunityDiagramDraftNode> nodes,
        IReadOnlyList<CommunityDiagramDraftEdge> edges,
        IReadOnlyList<string> boardCategories,
        string appName,
        string roleLabel)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(boardCategories);

        var analysis = CommunityLedgerFlowClassifier.Analyze(new()
        {
            Title = string.Join(" ", nodes.Select(node => node.Title)),
            Body = string.Join(Environment.NewLine, edges.Select(edge => $"{edge.FromTitle} -> {edge.ToTitle}: {edge.Label}")),
            UiSectionHints = nodes
                .SelectMany(node => new[] { node.Title, node.GroupLabel, node.Description, node.Kind })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray(),
            ActionHints = edges
                .Select(edge => edge.Label)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray(),
            Attributes = new Dictionary<string, string>
            {
                ["source"] = "diagram",
                ["app"] = appName,
                ["role"] = roleLabel,
                ["nodes"] = string.Join(", ", nodes.Select(node => node.Title)),
                ["edges"] = string.Join(", ", edges.Select(edge => $"{edge.FromTitle}->{edge.ToTitle}"))
            }
        });

        var templateKey = string.IsNullOrWhiteSpace(analysis.PrimaryCandidate.TemplateKey)
            ? SelectedLedgerTemplateKey
            : analysis.PrimaryCandidate.TemplateKey;
        var template = CommunityLedgerTemplateCatalog.Find(templateKey);
        var category = ResolveBoardCategory(template, nodes, edges, boardCategories);
        var reason = ResolveRecommendationReason(template, nodes.Count, edges.Count, analysis);
        var isReportPost = ContainsDiagramSignal(nodes, edges, "신고", "분쟁", "report", "dispute");
        var transition = new CommunityComposerDraftTransition(
            template.Key,
            category,
            string.Empty,
            string.Empty,
            BuildTitle(template, nodes),
            BuildBody(template, category, reason, nodes, edges, appName, roleLabel),
            isReportPost,
            $"다이어그램을 바탕으로 '{category}' 대분류까지만 추천했습니다. 워크플로우 태그와 역할 태그는 직접 선택해 주세요. {reason}");

        SelectedLedgerTemplateKey = transition.LedgerTemplateKey;
        return transition;
    }

    private static string ResolveBoardCategory(
        CommunityLedgerTemplateResponse template,
        IReadOnlyList<CommunityDiagramDraftNode> nodes,
        IReadOnlyList<CommunityDiagramDraftEdge> edges,
        IReadOnlyList<string> boardCategories)
    {
        string? Find(params string[] keywords)
            => boardCategories.FirstOrDefault(category =>
                keywords.Any(keyword => category.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        if (ContainsDiagramSignal(nodes, edges, "신고", "분쟁", "report", "dispute"))
        {
            return Find("신고", "분쟁", "report", "dispute") ?? "신고/분쟁";
        }

        if (template.Key is CommunityLedgerTemplateKeys.CargoTransport
            or CommunityLedgerTemplateKeys.FoodDelivery)
        {
            return Find("운송", "배송", "배달", "용달") ?? template.Category;
        }

        if (template.Key is CommunityLedgerTemplateKeys.HongdalMart
            or CommunityLedgerTemplateKeys.WarehouseOutbound
            or CommunityLedgerTemplateKeys.WarehouseInbound)
        {
            return Find("창고", "재고", "입고", "출고", "업무", "기록") ?? template.Category;
        }

        if (template.Key is CommunityLedgerTemplateKeys.LocalSale
            or CommunityLedgerTemplateKeys.GroupPurchase
            or CommunityLedgerTemplateKeys.GroupImport
            or CommunityLedgerTemplateKeys.FoodOrder
            or CommunityLedgerTemplateKeys.Errand)
        {
            return Find("생활", "원장") ?? template.Category;
        }

        if (nodes.Any(node => node.Kind.Equals("sales-channel", StringComparison.OrdinalIgnoreCase)) ||
            ContainsDiagramSignal(nodes, edges, "판매", "판매채널", "스마트스토어", "쿠팡", "마켓", "commerce", "sales-channel"))
        {
            return Find("판매", "채널", "커머스", "마켓", "스마트스토어", "쿠팡") ?? template.Category;
        }

        if (nodes.Any(node => node.Kind.Equals("warehouse", StringComparison.OrdinalIgnoreCase) ||
                              node.Kind.Equals("work", StringComparison.OrdinalIgnoreCase)) ||
            ContainsDiagramSignal(nodes, edges, "창고", "재고", "입고", "출고", "피킹", "포장", "warehouse", "inventory"))
        {
            return Find("창고", "재고", "입고", "출고", "업무", "기록") ?? template.Category;
        }

        if (nodes.Any(node => node.Kind.Equals("delivery", StringComparison.OrdinalIgnoreCase)) ||
            ContainsDiagramSignal(nodes, edges, "배송", "운송", "배달", "용달", "상차", "하차", "transport", "delivery"))
        {
            return Find("운송", "배송", "배달", "용달") ?? template.Category;
        }

        return Find(template.Category) ?? template.Category;
    }

    private static string ResolveRecommendationReason(
        CommunityLedgerTemplateResponse template,
        int nodeCount,
        int edgeCount,
        CommunityLedgerFlowAnalysisResponse analysis)
    {
        var reviewSuffix = analysis.RequiresHumanReview
            ? " 다만 신호가 약해서 대분류도 작성 전에 한 번 확인하는 편이 좋습니다."
            : string.Empty;
        return $"{nodeCount}개 블록과 {edgeCount}개 연결을 기준으로 '{template.DisplayName}' 흐름을 참고했습니다.{reviewSuffix}";
    }

    private static string BuildTitle(
        CommunityLedgerTemplateResponse template,
        IReadOnlyList<CommunityDiagramDraftNode> nodes)
    {
        var path = string.Join(" -> ", nodes.Take(3).Select(node => node.Title));
        return $"[{template.DisplayName}] {(string.IsNullOrWhiteSpace(path) ? "다이어그램" : path)} 공유";
    }

    private static string BuildBody(
        CommunityLedgerTemplateResponse template,
        string boardCategory,
        string reason,
        IReadOnlyList<CommunityDiagramDraftNode> nodes,
        IReadOnlyList<CommunityDiagramDraftEdge> edges,
        string appName,
        string roleLabel)
    {
        var lines = new List<string>
        {
            "다이어그램에서 시작한 커뮤니티 글입니다.",
            $"- 추천 대분류: {boardCategory}",
            $"- 참고 원장 흐름: {template.DisplayName}",
            $"- 추천 기준: {reason}",
            "- 세부 분류: 워크플로우 태그와 역할 태그는 작성자가 직접 선택해 주세요.",
            string.Empty,
            "다이어그램 블록"
        };

        lines.AddRange(nodes.Count == 0
            ? ["- 아직 배치된 블록이 없습니다."]
            : nodes.Select(node => $"- {node.Title} / {node.GroupLabel} / {node.Kind}"));
        lines.Add(string.Empty);
        lines.Add("연결");
        if (edges.Count == 0)
        {
            lines.Add("- 아직 연결선이 없습니다.");
        }
        else
        {
            lines.AddRange(edges.Take(20).Select(edge => $"- {edge.FromTitle} -> {edge.ToTitle}: {edge.Label}"));
            if (edges.Count > 20)
            {
                lines.Add($"- 외 {edges.Count - 20}개 연결");
            }
        }

        lines.AddRange(
        [
            string.Empty,
            "보완하면 좋은 내용",
            "- 실제 참여자, 장소, 시간, 수량, 비용 조건을 확인해 주세요.",
            "- 결제나 증빙 이미지는 선택 사항으로 두고, 필요한 경우에만 첨부해 주세요.",
            string.Empty,
            "기본 원장 초안",
            CommunityLedgerTemplateCatalog.BuildDraftBody(template.Key, appName, roleLabel)
        ]);

        var body = string.Join(Environment.NewLine, lines);
        const int maxBodyLength = 3900;
        return body.Length <= maxBodyLength ? body : body[..maxBodyLength];
    }

    private static bool ContainsDiagramSignal(
        IReadOnlyList<CommunityDiagramDraftNode> nodes,
        IReadOnlyList<CommunityDiagramDraftEdge> edges,
        params string[] keywords)
    {
        var text = string.Join(
            " ",
            nodes.SelectMany(node => new[] { node.Title, node.GroupLabel, node.Description, node.Kind, node.Condition ?? string.Empty })
                .Concat(edges.SelectMany(edge => new[] { edge.FromTitle, edge.ToTitle, edge.Label })));
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
