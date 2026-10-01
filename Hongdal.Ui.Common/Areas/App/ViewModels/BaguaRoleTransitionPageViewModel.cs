using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record BaguaTargetWorkspace(string Name, string Href);

public sealed record BaguaTargetWorkspaceContext(
    BaguaActorRoleDefinition Role,
    BaguaBusinessAreaDefinition SourceArea,
    BaguaBusinessAreaDefinition TargetArea,
    BaguaTransitionDefinition Transition,
    BaguaRoleTransitionPerspectiveDefinition Perspective);

public enum Bagua서버권한상태
{
    대상선택필요,
    확인전,
    확인중,
    확인됨,
    거부됨,
    실패
}

/// <summary>
/// 역할 관점과 실제 서버 권한이 섞이지 않도록 별도로 유지하는 상태입니다.
/// 원장이나 업무 대상이 선택된 뒤 권한 API 결과를 이 ViewModel에 적용합니다.
/// </summary>
public sealed class Bagua서버권한ViewModel : ObservableObject
{
    private readonly HashSet<string> _allowedFeatures = new(StringComparer.OrdinalIgnoreCase);

    public Bagua서버권한상태 상태 { get; private set; } = Bagua서버권한상태.대상선택필요;
    public string? PerspectiveKey { get; private set; }
    public string? 메시지 { get; private set; }
    public IReadOnlyCollection<string> 허용기능 => _allowedFeatures;
    public bool 확인중 => 상태 == Bagua서버권한상태.확인중;
    public bool 실행허용 => 상태 == Bagua서버권한상태.확인됨 && _allowedFeatures.Count > 0;

    public void 화면초기화(string? perspectiveKey)
    {
        PerspectiveKey = perspectiveKey;
        상태 = perspectiveKey is null
            ? Bagua서버권한상태.대상선택필요
            : Bagua서버권한상태.확인전;
        메시지 = null;
        _allowedFeatures.Clear();
        OnPropertyChanged(string.Empty);
    }

    public void 확인시작()
    {
        if (PerspectiveKey is null)
        {
            return;
        }

        상태 = Bagua서버권한상태.확인중;
        메시지 = null;
        _allowedFeatures.Clear();
        OnPropertyChanged(string.Empty);
    }

    public void 권한적용(IEnumerable<string> allowedFeatures)
    {
        ArgumentNullException.ThrowIfNull(allowedFeatures);
        if (PerspectiveKey is null)
        {
            throw new InvalidOperationException("역할 화면을 선택한 뒤 서버 권한을 적용해야 합니다.");
        }

        _allowedFeatures.Clear();
        _allowedFeatures.UnionWith(allowedFeatures.Where(feature => !string.IsNullOrWhiteSpace(feature)));
        상태 = Bagua서버권한상태.확인됨;
        메시지 = null;
        OnPropertyChanged(string.Empty);
    }

    public void 거부(string message)
    {
        _allowedFeatures.Clear();
        상태 = Bagua서버권한상태.거부됨;
        메시지 = message;
        OnPropertyChanged(string.Empty);
    }

    public void 실패(string message)
    {
        _allowedFeatures.Clear();
        상태 = Bagua서버권한상태.실패;
        메시지 = message;
        OnPropertyChanged(string.Empty);
    }

    public bool 허용됨(string featureKey)
        => 상태 == Bagua서버권한상태.확인됨 && _allowedFeatures.Contains(featureKey);
}

/// <summary>
/// 클라이언트별 역할·라우트 차이를 공통 Bagua 화면에서 분리합니다.
/// 앱은 이 서비스를 먼저 등록하여 기본 경로를 교체할 수 있습니다.
/// </summary>
public interface IBaguaTargetWorkspaceResolver
{
    BaguaTargetWorkspace Resolve(BaguaTargetWorkspaceContext context);
}

public sealed class DefaultBaguaTargetWorkspaceResolver : IBaguaTargetWorkspaceResolver
{
    public BaguaTargetWorkspace Resolve(BaguaTargetWorkspaceContext context)
    {
        var workspace = BaguaRoleTransitionPageCatalog.ResolveDefaultTargetWorkspace(
            context.TargetArea.BusinessCode);
        return new BaguaTargetWorkspace(workspace.Name, workspace.Href);
    }
}

public sealed record Bagua전환흐름ViewModel(
    string WorkflowKind,
    string 표시명,
    string 제목,
    string 설명,
    bool 원본선택필요,
    bool 합의흐름)
{
    public static Bagua전환흐름ViewModel Create(BaguaTransitionDefinition transition)
    {
        var (label, description) = transition.WorkflowKind switch
        {
            BaguaTransitionWorkflowKinds.Home => ("업무 홈", "한 업무 영역의 현황과 다음 행동을 정리합니다."),
            BaguaTransitionWorkflowKinds.Conversion => ("업무 변환", "출발 기록을 대상 업무의 새 기록으로 변환합니다."),
            BaguaTransitionWorkflowKinds.Handoff => ("업무 인계", "출발 업무의 조건과 증빙을 대상 담당자에게 인계합니다."),
            BaguaTransitionWorkflowKinds.Governance => ("합의·의결", "안건, 투표, 이의, 결의문과 서명을 관리합니다."),
            BaguaTransitionWorkflowKinds.Execution => ("확정안 실행", "확정된 결의를 대상 업무의 실행 기록으로 만듭니다."),
            BaguaTransitionWorkflowKinds.Result => ("결과 반영", "완료 결과와 증빙을 앞선 업무에 되돌려 반영합니다."),
            BaguaTransitionWorkflowKinds.Return => ("반품·회수", "회수, 검수, 재입고와 정산을 연결합니다."),
            _ => ("업무 전환", "두 업무 영역 사이의 상태와 책임을 연결합니다.")
        };

        var heading = transition.WorkflowKind == BaguaTransitionWorkflowKinds.Governance
            ? "제안부터 전자서명과 실행까지"
            : $"{label} 절차";

        return new Bagua전환흐름ViewModel(
            transition.WorkflowKind,
            label,
            heading,
            description,
            transition.RequiresSourceSelection,
            transition.OpensAgreementFlow);
    }
}

/// <summary>
/// 역할 관점은 화면의 행동 후보만 정하며 실제 권한을 부여하지 않습니다.
/// 실행 가능 여부는 원장 참여와 서버 권한 응답으로 다시 판정해야 합니다.
/// </summary>
public sealed record Bagua역할관점ViewModel(
    BaguaActorRoleDefinition 역할,
    BaguaRoleTransitionPerspectiveDefinition 관점,
    string 아이콘,
    string 모드표시명,
    string 모드설명,
    string CssClass,
    bool 조회중심,
    bool 행동후보표시)
{
    public static Bagua역할관점ViewModel Create(
        BaguaActorRoleDefinition role,
        BaguaRoleTransitionPerspectiveDefinition perspective)
    {
        var icon = role.RoleCode switch
        {
            BaguaActorRoleCodes.Orderer => "🧾",
            BaguaActorRoleCodes.Seller => "🏪",
            BaguaActorRoleCodes.WarehouseManager => "📦",
            BaguaActorRoleCodes.TransportOperator => "🚚",
            _ => "🤝"
        };
        var (label, description, cssClass) = perspective.PerspectiveMode switch
        {
            BaguaRolePerspectiveModes.Owner =>
                ("주관", "내 업무를 직접 관리", "bagua-transition-mode--owner"),
            BaguaRolePerspectiveModes.Initiator =>
                ("발신", "조건을 정해 요청·인계", "bagua-transition-mode--initiator"),
            BaguaRolePerspectiveModes.Receiver =>
                ("접수", "요청을 받아 확인·실행", "bagua-transition-mode--receiver"),
            BaguaRolePerspectiveModes.Governor =>
                ("의결", "안건·투표·서명 관리", "bagua-transition-mode--governor"),
            _ =>
                ("참조", "진행과 업무 영향 확인", "bagua-transition-mode--observer")
        };
        var readOnly = perspective.PerspectiveMode == BaguaRolePerspectiveModes.Observer;

        return new Bagua역할관점ViewModel(
            role,
            perspective,
            icon,
            label,
            description,
            cssClass,
            readOnly,
            !readOnly);
    }
}

public sealed record Bagua전환MatrixCellViewModel(
    BaguaBusinessAreaDefinition TargetArea,
    Bagua역할관점ViewModel 역할관점,
    bool 현재셀,
    string 짧은제목)
{
    public string CssClass => 현재셀 ? "is-current" : string.Empty;
}

public sealed record Bagua전환MatrixRowViewModel(
    BaguaBusinessAreaDefinition SourceArea,
    IReadOnlyList<Bagua전환MatrixCellViewModel> Cells);

/// <summary>
/// 한 화면에서 사용하는 출발 업무, 전환 방식, 도착 업무와 역할 정책을 조립합니다.
/// 같은 업무의 홈 전환에서는 동일한 업무 ViewModel 인스턴스를 재사용합니다.
/// </summary>
public sealed class Bagua전환업무조립ViewModel
{
    public Bagua전환업무조립ViewModel(
        Bagua업무영역ViewModel source,
        Bagua업무영역ViewModel target,
        Bagua전환흐름ViewModel workflow,
        Bagua역할관점ViewModel rolePerspective)
    {
        Source = source;
        Target = target;
        Workflow = workflow;
        RolePerspective = rolePerspective;
        ActiveDomains = ReferenceEquals(source, target) ? [source] : [source, target];
        Controllers = ActiveDomains
            .SelectMany(domain => domain.Controllers.Values)
            .DistinctBy(controller => controller.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Api기능 = ActiveDomains
            .SelectMany(domain => domain.Api기능)
            .DistinctBy(
                feature => $"{feature.ControllerKey}:{feature.Key}",
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Bagua업무영역ViewModel Source { get; }
    public Bagua업무영역ViewModel Target { get; }
    public Bagua전환흐름ViewModel Workflow { get; }
    public Bagua역할관점ViewModel RolePerspective { get; }
    public IReadOnlyList<Bagua업무영역ViewModel> ActiveDomains { get; }
    public IReadOnlyList<Controller기능ViewModel> Controllers { get; }
    public IReadOnlyList<BaguaApi기능정의> Api기능 { get; }
}

/// <summary>
/// 5개 업무 × 5개 전환 × 5개 역할 화면을 한 종류의 PageViewModel로 제어합니다.
/// 정적 화면 정의와 변경 가능한 화면 상태, API 하위 ViewModel 조립을 분리합니다.
/// </summary>
public sealed class BaguaRoleTransitionPageViewModel : 조립ViewModelBase
{
    private readonly IBaguaTargetWorkspaceResolver _workspaceResolver;
    private readonly IReadOnlyDictionary<string, Bagua업무영역ViewModel> _domains;

    public BaguaRoleTransitionPageViewModel(
        IBagua업무영역ViewModelFactory domainFactory,
        IBaguaTargetWorkspaceResolver workspaceResolver,
        IHongdalJsonApiClient apiClient)
    {
        _workspaceResolver = workspaceResolver;
        서버권한 = 하위ViewModel등록(new Bagua서버권한ViewModel(), 수명소유: true);
        전환Runtime = 하위ViewModel등록(
            new Bagua전환RuntimeViewModel(apiClient, 서버권한),
            수명소유: true);
        _domains = domainFactory.CreateAll();
        foreach (var domain in _domains.Values)
        {
            하위ViewModel등록(domain, 수명소유: true);
        }
    }

    public string? RoleCode { get; private set; }
    public string SourceTrigramKey { get; private set; } = string.Empty;
    public string TargetTrigramKey { get; private set; } = string.Empty;
    public BaguaRoleTransitionPageModel? 페이지 { get; private set; }
    public Bagua서버권한ViewModel 서버권한 { get; }
    public Bagua전환RuntimeViewModel 전환Runtime { get; }
    public BaguaTransitionDefinition? 전환 { get; private set; }
    public BaguaBusinessAreaDefinition? 출발영역 { get; private set; }
    public BaguaBusinessAreaDefinition? 도착영역 { get; private set; }
    public Bagua역할관점ViewModel? 현재역할관점 { get; private set; }
    public Bagua전환흐름ViewModel? 전환흐름 { get; private set; }
    public Bagua전환업무조립ViewModel? 업무조립 { get; private set; }
    public string? 오류메시지 { get; private set; }
    public IReadOnlyList<Bagua역할관점ViewModel> 역할선택지 { get; private set; } = [];
    public IReadOnlyList<Bagua전환MatrixRowViewModel> 전환행 { get; private set; } = [];
    public IReadOnlyList<BaguaBusinessAreaDefinition> 업무영역 => BaguaTransitionCatalog.Areas;
    public IReadOnlyDictionary<string, Bagua업무영역ViewModel> 업무모듈 => _domains;
    public string PageTitleText => 페이지?.Perspective.ViewTitle ?? 전환?.PageTitle ?? "팔괘 업무 전환";
    public string 커뮤니티경로 => "/community";
    public string? 목표업무경로 => 페이지?.TargetWorkspaceHref;
    public bool 역할선택화면 => 오류메시지 is null && 전환 is not null && 페이지 is null;
    public bool 역할화면 => 오류메시지 is null && 페이지 is not null;

    public void 초기화(
        string? roleCode,
        string sourceTrigramKey,
        string targetTrigramKey)
    {
        RoleCode = string.IsNullOrWhiteSpace(roleCode) ? null : roleCode.Trim();
        SourceTrigramKey = sourceTrigramKey?.Trim() ?? string.Empty;
        TargetTrigramKey = targetTrigramKey?.Trim() ?? string.Empty;
        페이지 = null;
        전환 = null;
        출발영역 = null;
        도착영역 = null;
        현재역할관점 = null;
        전환흐름 = null;
        업무조립 = null;
        오류메시지 = null;
        역할선택지 = [];
        전환행 = [];
        서버권한.화면초기화(null);
        전환Runtime.초기화(null);

        try
        {
            전환 = BaguaTransitionCatalog.Find(SourceTrigramKey, TargetTrigramKey);
            출발영역 = BaguaTransitionCatalog.FindArea(전환.SourceTrigramKey);
            도착영역 = BaguaTransitionCatalog.FindArea(전환.TargetTrigramKey);
            전환흐름 = Bagua전환흐름ViewModel.Create(전환);
            역할선택지 = BuildRoleChoices(전환);

            if (RoleCode is null)
            {
                return;
            }

            var page = BaguaRoleTransitionPageCatalog.Build(
                RoleCode,
                SourceTrigramKey,
                TargetTrigramKey);
            var context = new BaguaTargetWorkspaceContext(
                page.Role,
                page.SourceArea,
                page.TargetArea,
                page.Transition,
                page.Perspective);
            var workspace = _workspaceResolver.Resolve(context);
            페이지 = page with
            {
                TargetWorkspaceName = workspace.Name,
                TargetWorkspaceHref = workspace.Href
            };
            서버권한.화면초기화(page.Perspective.PerspectiveKey);
            현재역할관점 = Bagua역할관점ViewModel.Create(page.Role, page.Perspective);
            업무조립 = new Bagua전환업무조립ViewModel(
                _domains[page.SourceArea.BusinessCode],
                _domains[page.TargetArea.BusinessCode],
                전환흐름,
                현재역할관점);
            전환Runtime.초기화(업무조립);
            전환행 = BuildMatrix(page.Role.RoleCode);
        }
        catch (KeyNotFoundException)
        {
            오류메시지 = "요청한 역할 또는 업무 전환을 찾을 수 없습니다. 역할과 출발·도착 영역을 다시 선택해 주세요.";
        }
        finally
        {
            OnPropertyChanged(string.Empty);
        }
    }

    public string 역할경로(string roleCode)
    {
        if (전환 is null)
        {
            throw new InvalidOperationException("역할을 선택하기 전에 전환 화면을 초기화해야 합니다.");
        }

        return BaguaRoleTransitionRoutes.Build(
            roleCode,
            전환.SourceTrigramKey,
            전환.TargetTrigramKey);
    }

    public string? 전환경로(string sourceTrigramKey, string targetTrigramKey)
        => 페이지 is null
            ? null
            : BaguaRoleTransitionRoutes.Build(
                페이지.Role.RoleCode,
                sourceTrigramKey,
                targetTrigramKey);

    private static IReadOnlyList<Bagua역할관점ViewModel> BuildRoleChoices(
        BaguaTransitionDefinition transition)
        => BaguaTransitionCatalog.Roles
            .Select(role => Bagua역할관점ViewModel.Create(
                role,
                BaguaTransitionCatalog.FindPerspective(
                    role.RoleCode,
                    transition.SourceTrigramKey,
                    transition.TargetTrigramKey)))
            .ToArray();

    private IReadOnlyList<Bagua전환MatrixRowViewModel> BuildMatrix(string roleCode)
    {
        var role = BaguaTransitionCatalog.FindRole(roleCode);
        return BaguaTransitionCatalog.Areas
            .Select(source => new Bagua전환MatrixRowViewModel(
                source,
                BaguaTransitionCatalog.Areas
                    .Select(target =>
                    {
                        var perspective = BaguaTransitionCatalog.FindPerspective(
                            roleCode,
                            source.TrigramKey,
                            target.TrigramKey);
                        return new Bagua전환MatrixCellViewModel(
                            target,
                            Bagua역할관점ViewModel.Create(role, perspective),
                            string.Equals(
                                source.TrigramKey,
                                SourceTrigramKey,
                                StringComparison.OrdinalIgnoreCase)
                            && string.Equals(
                                target.TrigramKey,
                                TargetTrigramKey,
                                StringComparison.OrdinalIgnoreCase),
                            ShortenTitle(perspective.ViewTitle));
                    })
                    .ToArray()))
            .ToArray();
    }

    private static string ShortenTitle(string viewTitle)
    {
        var separator = viewTitle.IndexOf(" · ", StringComparison.Ordinal);
        return separator > 0 ? viewTitle[..separator] : viewTitle;
    }
}
