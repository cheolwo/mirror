using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record BaguaApi실행요청(
    HttpMethod Method,
    string Path,
    string 작업명,
    string? 요청Json);

/// <summary>
/// 125개 역할·전환 화면에서 공통으로 사용하는 업무 대상과 전환 메모입니다.
/// API DTO와 분리해 수정 여부와 입력 검증을 화면 단위로 관리합니다.
/// </summary>
public sealed class Bagua전환초안ViewModel : 업무입력ViewModelBase
{
    private string? _원본참조Id;
    private string? _대상참조Id;
    private long? _예상Revision;
    private string? _제목;
    private string? _메모;

    [MaxLength(200)]
    public string? 원본참조Id
    {
        get => _원본참조Id;
        set => 입력값설정(ref _원본참조Id, 정규화(value));
    }

    [MaxLength(200)]
    public string? 대상참조Id
    {
        get => _대상참조Id;
        set => 입력값설정(ref _대상참조Id, 정규화(value));
    }

    [Range(0, long.MaxValue)]
    public long? 예상Revision
    {
        get => _예상Revision;
        set => 입력값설정(ref _예상Revision, value);
    }

    [MaxLength(200)]
    public string? 제목
    {
        get => _제목;
        set => 입력값설정(ref _제목, 정규화(value));
    }

    [MaxLength(2000)]
    public string? 메모
    {
        get => _메모;
        set => 입력값설정(ref _메모, 정규화(value));
    }

    public void 초기화()
    {
        _원본참조Id = null;
        _대상참조Id = null;
        _예상Revision = null;
        _제목 = null;
        _메모 = null;
        검증초기화();
        변경확정();
        OnPropertyChanged(string.Empty);
    }

    private static string? 정규화(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public enum Bagua기능가용상태
{
    확인전,
    확인불가,
    사용가능,
    비활성,
    지원하지않는요청형식
}

/// <summary>
/// 정적으로 조립된 팔괘 화면을 실제 업무 대상, 서버 권한, API 실행 상태와 연결합니다.
/// 도메인별 요청 DTO는 각 하위 ViewModel이 계속 담당하고, 이 런타임은 공통 JSON 작업만 실행합니다.
/// </summary>
public sealed class Bagua전환RuntimeViewModel : 조립ViewModelBase
{
    private const string VersionFeatureFlagsPath = "api/v1/version-feature-flags";
    private static readonly Regex RouteParameterRegex = new(@"\{[^}]+\}", RegexOptions.Compiled);
    private static readonly Regex PlaceholderRegex = new(@"\{(?<key>[^}:]+)(?::[^}]+)?\}", RegexOptions.Compiled);

    private readonly IHongdalJsonApiClient _client;
    private readonly Bagua서버권한ViewModel _서버권한;
    private readonly Dictionary<string, string> _경로값 = new(StringComparer.OrdinalIgnoreCase);
    private readonly 업무선택ContextViewModel _원본선택Context;
    private Bagua전환업무조립ViewModel? _업무조립;
    private BaguaApi기능정의? _선택기능;
    private VersionFeatureFlagsResponse? _기능메타데이터;
    private string? _요청Json;
    private string? _입력오류메시지;

    public Bagua전환RuntimeViewModel(
        IHongdalJsonApiClient client,
        Bagua서버권한ViewModel 서버권한)
    {
        _client = client;
        _서버권한 = 서버권한;
        초안 = 하위ViewModel등록(new Bagua전환초안ViewModel(), 수명소유: true);
        _원본선택Context = 하위ViewModel등록(new 업무선택ContextViewModel(), 수명소유: true);
        기능메타데이터조회 = 하위ViewModel등록(new Api작업ViewModel<VersionFeatureFlagsResponse?>(
            cancellationToken => _client.GetAsync<VersionFeatureFlagsResponse>(
                VersionFeatureFlagsPath,
                "버전 기능 메타데이터 조회",
                allowNotFound: false,
                cancellationToken)), 수명소유: true);
        실행작업 = 하위ViewModel등록(
            new Api작업ViewModel<BaguaApi실행요청, string?>(실행CoreAsync),
            수명소유: true);
    }

    public Bagua전환초안ViewModel 초안 { get; }
    public Api작업ViewModel<VersionFeatureFlagsResponse?> 기능메타데이터조회 { get; }
    public Api작업ViewModel<BaguaApi실행요청, string?> 실행작업 { get; }
    public Bagua전환업무조립ViewModel? 업무조립 => _업무조립;
    public BaguaApi기능정의? 선택기능 => _선택기능;
    public VersionFeatureFlagsResponse? 기능메타데이터 => _기능메타데이터;
    public IReadOnlyDictionary<string, string> 경로값 => _경로값;
    public string? 요청Json => _요청Json;
    public string? 입력오류메시지 => _입력오류메시지;
    public string? 오류메시지 => 입력오류메시지 ?? 실행작업.오류메시지;
    public Api작업오류? 오류 => 실행작업.오류;
    public string? 응답Json => 실행작업.결과;
    public bool 처리중 => 실행작업.처리중 || 기능메타데이터조회.처리중;
    public bool 원본선택됨 => _원본선택Context.선택됨;
    public bool 변경기능 => _선택기능 is not null && _선택기능.Method != HttpMethod.Get;
    public bool 실행가능 => 실행불가사유 is null;

    public Bagua기능가용상태 기능가용상태
    {
        get
        {
            if (_선택기능 is null || _기능메타데이터 is null)
            {
                return Bagua기능가용상태.확인전;
            }

            if (!_선택기능.JsonClient호출가능)
            {
                return Bagua기능가용상태.지원하지않는요청형식;
            }

            var endpoint = 일치Endpoint(_선택기능);
            if (endpoint is null)
            {
                return Bagua기능가용상태.확인불가;
            }

            if (endpoint.WorkflowCodes.Count == 0)
            {
                return Bagua기능가용상태.사용가능;
            }

            var enabled = _기능메타데이터.Workflows.Any(workflow =>
                workflow.IsEnabled
                && endpoint.WorkflowCodes.Contains(workflow.WorkflowCode, StringComparer.OrdinalIgnoreCase));
            return enabled ? Bagua기능가용상태.사용가능 : Bagua기능가용상태.비활성;
        }
    }

    public string? 실행불가사유
    {
        get
        {
            if (_업무조립 is null)
            {
                return "역할과 업무 전환 화면을 먼저 선택해야 합니다.";
            }

            if (_선택기능 is null)
            {
                return "실행할 API 기능을 선택해야 합니다.";
            }

            if (!_선택기능.JsonClient호출가능)
            {
                return "파일 업로드 기능은 해당 도메인의 전용 ViewModel에서 실행해야 합니다.";
            }

            if (!_선택기능.Method.Equals(HttpMethod.Get)
                && _업무조립.RolePerspective.조회중심)
            {
                return "현재 역할 관점은 조회 전용입니다.";
            }

            if (_업무조립.Workflow.원본선택필요 && !_원본선택Context.선택됨)
            {
                return "전환할 원본 업무를 선택해야 합니다.";
            }

            if (기능가용상태 == Bagua기능가용상태.비활성)
            {
                return "현재 버전에서 비활성화된 업무 기능입니다.";
            }

            if (!_선택기능.Method.Equals(HttpMethod.Get)
                && !서버에서허용됨(_선택기능))
            {
                return "변경 작업은 서버 권한 확인이 필요합니다.";
            }

            try
            {
                _ = 선택기능경로();
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }

            if (!초안.전체검증())
            {
                return "전환 초안의 입력값을 확인해 주세요.";
            }

            return null;
        }
    }

    public void 초기화(Bagua전환업무조립ViewModel? 업무조립)
    {
        실행작업.취소();
        if (!실행작업.처리중)
        {
            실행작업.초기화();
        }

        _업무조립 = 업무조립;
        _선택기능 = null;
        _경로값.Clear();
        _요청Json = null;
        _입력오류메시지 = null;
        _원본선택Context.선택(null);
        초안.초기화();
        OnPropertyChanged(string.Empty);
    }

    public void 기능선택(string controllerKey, string featureKey)
    {
        if (_업무조립 is null)
        {
            throw new InvalidOperationException("업무 화면을 먼저 초기화해야 합니다.");
        }

        _선택기능 = _업무조립.Api기능.FirstOrDefault(feature =>
            string.Equals(feature.ControllerKey, controllerKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(feature.Key, featureKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"현재 업무 화면에 없는 API 기능입니다: {controllerKey}:{featureKey}");
        _경로값.Clear();
        _요청Json = null;
        _입력오류메시지 = null;
        if (!실행작업.처리중)
        {
            실행작업.초기화();
        }

        원본경로자동적용();
        OnPropertyChanged(string.Empty);
    }

    public void 원본선택(string referenceId, long? expectedRevision = null)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            throw new ArgumentException("원본 업무 식별자가 필요합니다.", nameof(referenceId));
        }

        var normalized = referenceId.Trim();
        _원본선택Context.선택(normalized);
        초안.원본참조Id = normalized;
        초안.예상Revision = expectedRevision;
        원본경로자동적용();
        _입력오류메시지 = null;
        OnPropertyChanged(string.Empty);
    }

    public void 대상선택(string? referenceId)
    {
        초안.대상참조Id = referenceId;
        _입력오류메시지 = null;
        OnPropertyChanged(string.Empty);
    }

    public void 경로값설정(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("API 경로 이름과 값이 모두 필요합니다.");
        }

        _경로값[key.Trim()] = value.Trim();
        _입력오류메시지 = null;
        OnPropertyChanged(string.Empty);
    }

    public void 요청Json설정(string? json)
    {
        _요청Json = string.IsNullOrWhiteSpace(json) ? null : json.Trim();
        _입력오류메시지 = null;
        OnPropertyChanged(string.Empty);
    }

    public async Task<bool> 기능메타데이터조회Async(CancellationToken cancellationToken = default)
    {
        await 기능메타데이터조회.실행Async(cancellationToken);
        if (!기능메타데이터조회.성공함)
        {
            OnPropertyChanged(string.Empty);
            return false;
        }

        _기능메타데이터 = 기능메타데이터조회.결과;
        OnPropertyChanged(string.Empty);
        return true;
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        _입력오류메시지 = 실행불가사유;
        OnPropertyChanged(string.Empty);
        if (_입력오류메시지 is not null || _선택기능 is null)
        {
            return false;
        }

        var request = new BaguaApi실행요청(
            _선택기능.Method,
            선택기능경로(),
            _선택기능.표시명,
            _요청Json);
        await 실행작업.실행Async(request, cancellationToken);
        if (실행작업.성공함)
        {
            초안.변경확정();
        }

        OnPropertyChanged(string.Empty);
        return 실행작업.성공함;
    }

    private async Task<string?> 실행CoreAsync(
        BaguaApi실행요청 request,
        CancellationToken cancellationToken)
    {
        JsonElement? response;
        if (request.Method.Equals(HttpMethod.Get))
        {
            response = await _client.GetAsync<JsonElement>(
                request.Path,
                request.작업명,
                allowNotFound: false,
                cancellationToken);
        }
        else if (request.요청Json is null)
        {
            response = await _client.SendAsync<JsonElement>(
                request.Method,
                request.Path,
                request.작업명,
                cancellationToken: cancellationToken);
        }
        else
        {
            using var document = JsonDocument.Parse(request.요청Json);
            response = await _client.SendAsync<JsonElement, JsonElement>(
                request.Method,
                request.Path,
                document.RootElement.Clone(),
                request.작업명,
                cancellationToken: cancellationToken);
        }

        return response?.GetRawText();
    }

    private string 선택기능경로()
    {
        if (_업무조립 is null || _선택기능 is null)
        {
            throw new InvalidOperationException("실행할 API 기능을 먼저 선택해야 합니다.");
        }

        var controller = _업무조립.Controllers.Single(candidate =>
            string.Equals(candidate.Key, _선택기능.ControllerKey, StringComparison.OrdinalIgnoreCase));
        return controller.경로(_선택기능.RelativePath, _경로값);
    }

    private bool 서버에서허용됨(BaguaApi기능정의 feature)
        => _서버권한.허용됨(feature.Key)
           || _서버권한.허용됨($"{feature.ControllerKey}:{feature.Key}");

    private void 원본경로자동적용()
    {
        if (_선택기능 is null || string.IsNullOrWhiteSpace(초안.원본참조Id))
        {
            return;
        }

        var placeholders = PlaceholderRegex.Matches(_선택기능.RelativePath)
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (placeholders.Length == 1)
        {
            _경로값[placeholders[0]] = 초안.원본참조Id;
        }
    }

    private WorkflowApiEndpointDto? 일치Endpoint(BaguaApi기능정의 feature)
    {
        if (_업무조립 is null || _기능메타데이터 is null)
        {
            return null;
        }

        var controller = _업무조립.Controllers.First(candidate =>
            string.Equals(candidate.Key, feature.ControllerKey, StringComparison.OrdinalIgnoreCase));
        var route = 정규화경로($"{controller.BasePath}/{feature.RelativePath}");
        return _기능메타데이터.ApiEndpoints.FirstOrDefault(endpoint =>
            string.Equals(endpoint.Method, feature.Method.Method, StringComparison.OrdinalIgnoreCase)
            && string.Equals(정규화경로(endpoint.RoutePattern), route, StringComparison.OrdinalIgnoreCase));
    }

    private static string 정규화경로(string route)
        => RouteParameterRegex.Replace(route.Split('?', 2)[0].Trim('/'), "{}");
}
