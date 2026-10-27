using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed record 판매채널연결옵션(
    string Code,
    string Name,
    string Market,
    string IntegrationState,
    string Boundary,
    IReadOnlyList<판매채널인증필드정의> 인증필드);

public static class 판매채널연결옵션Catalog
{
    public static IReadOnlyList<판매채널연결옵션> Items { get; } =
        판매채널인증SchemaCatalog.Items
            .Select(schema => new 판매채널연결옵션(
                schema.채널종류,
                schema.표시명,
                schema.시장,
                "서버 보안 저장 지원",
                "자격증명은 서버에 암호화해 저장하며 외부 호출은 채널 모듈이 수행합니다.",
                schema.Fields))
            .ToArray();

    public static 판매채널연결옵션? 찾기(string? channelCode)
        => Items.FirstOrDefault(item =>
            string.Equals(item.Code, channelCode?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public enum 판매채널페이지접근상태
{
    확인전,
    사용가능,
    로그인필요,
    역할없음,
    기능비활성,
    오류
}

/// <summary>판매채널 페이지의 기능 플래그, 로그인과 역할 진입 조건만 담당합니다.</summary>
public sealed partial class 판매채널페이지접근ViewModel : 업무작업ViewModelBase
{
    private static readonly string[] AllowedRoles = ["화주", "판매자", "서버관리자"];
    private readonly I판매채널페이지접근Service _service;
    internal ISsalddel현재사용자변경알림? 현재사용자변경알림 { get; }

    public 판매채널페이지접근ViewModel(
        I판매채널페이지접근Service service,
        ISsalddel현재사용자Context currentUserContext)
    {
        _service = service;
        현재사용자변경알림 = currentUserContext as ISsalddel현재사용자변경알림;
        현재사용자Context연결(currentUserContext);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(사용가능))]
    [NotifyPropertyChangedFor(nameof(로그인필요))]
    [NotifyPropertyChangedFor(nameof(역할없음))]
    [NotifyPropertyChangedFor(nameof(기능비활성))]
    public partial 판매채널페이지접근상태 화면상태 { get; private set; }

    public bool 사용가능 => 화면상태 == 판매채널페이지접근상태.사용가능;
    public bool 로그인필요 => 화면상태 == 판매채널페이지접근상태.로그인필요;
    public bool 역할없음 => 화면상태 == 판매채널페이지접근상태.역할없음;
    public bool 기능비활성 => 화면상태 == 판매채널페이지접근상태.기능비활성;

    public async Task<bool> 확인Async(CancellationToken cancellationToken = default)
    {
        화면상태 = 판매채널페이지접근상태.확인전;
        var succeeded = await 작업실행Async(
            async token =>
            {
                var enabled = await _service.기능활성여부Async(token);
                화면상태 = !enabled
                    ? 판매채널페이지접근상태.기능비활성
                    : !현재사용자.인증됨
                        ? 판매채널페이지접근상태.로그인필요
                        : AllowedRoles.Any(현재사용자.역할보유)
                            ? 판매채널페이지접근상태.사용가능
                            : 판매채널페이지접근상태.역할없음;
            },
            "판매채널 페이지의 사용 가능 상태를 확인했습니다.",
            cancellationToken,
            ex => $"판매채널 기능 상태를 확인하지 못했습니다. {ex.Message}");

        if (!succeeded)
        {
            화면상태 = 취소됨
                ? 판매채널페이지접근상태.확인전
                : 판매채널페이지접근상태.오류;
        }

        return succeeded;
    }
}

/// <summary>계정 목록 조회와 화면 내 검색·채널 필터만 담당합니다.</summary>
public sealed partial class 판매채널계정목록PageViewModel(
    I판매채널계정읽기Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(표시목록))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial string 검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(표시목록))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial string? 채널종류 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(표시목록))]
    [NotifyPropertyChangedFor(nameof(계정없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial IReadOnlyList<판매채널계정항목응답> 계정목록 { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(계정없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial bool 초기화됨 { get; private set; }

    public IReadOnlyList<판매채널계정항목응답> 표시목록
        => 계정목록
            .Where(item => string.IsNullOrWhiteSpace(채널종류) ||
                string.Equals(item.채널종류, 채널종류, StringComparison.OrdinalIgnoreCase))
            .Where(item =>
                string.IsNullOrWhiteSpace(검색어) ||
                item.상점명.Contains(검색어.Trim(), StringComparison.OrdinalIgnoreCase) ||
                item.채널종류.Contains(검색어.Trim(), StringComparison.OrdinalIgnoreCase) ||
                item.연결상태.Contains(검색어.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.수정일시)
            .ThenByDescending(item => item.Id)
            .ToArray();

    public bool 계정없음 => 초기화됨 && 계정목록.Count == 0;
    public bool 검색결과없음 => 초기화됨 && 계정목록.Count > 0 && 표시목록.Count == 0;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token =>
            {
                계정목록 = await service.계정목록조회Async(token);
                초기화됨 = true;
            },
            "판매채널 계정 목록을 불러왔습니다.",
            cancellationToken,
            ex => $"판매채널 계정 목록을 불러오지 못했습니다. 잠시 뒤 다시 시도해 주세요. {ex.Message}");
}

/// <summary>주소나 목록에서 선택한 정확한 accountId 한 건만 조회합니다.</summary>
public sealed partial class 판매채널계정상세PageViewModel(
    I판매채널계정읽기Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    public partial long? 요청AccountId { get; private set; }

    [ObservableProperty]
    public partial 판매채널계정항목응답? 계정 { get; private set; }

    [ObservableProperty]
    public partial bool 찾을수없음 { get; private set; }

    public Task<bool> 조회Async(long accountId, CancellationToken cancellationToken = default)
    {
        if (accountId <= 0)
        {
            return Task.FromResult(유효성실패("조회할 판매채널 계정 ID를 확인해 주세요."));
        }

        요청AccountId = accountId;
        계정 = null;
        찾을수없음 = false;
        return 작업실행Async(
            async token =>
            {
                계정 = await service.계정상세조회Async(accountId, token);
                찾을수없음 = 계정 is null;
            },
            "판매채널 계정 상세를 불러왔습니다.",
            cancellationToken,
            ex => $"판매채널 계정 상세를 불러오지 못했습니다. 잠시 뒤 다시 시도해 주세요. {ex.Message}");
    }

    public void 선택해제()
    {
        요청AccountId = null;
        계정 = null;
        찾을수없음 = false;
        작업상태초기화();
    }
}

/// <summary>채널별 자격증명을 서버 보안 저장소에 전달하는 입력만 담당합니다.</summary>
public sealed partial class 판매채널계정연결준비ViewModel(
    I판매채널계정Service service) : 업무작업ViewModelBase
{
    private string _credentialChannel = string.Empty;
    private readonly Dictionary<string, string> _인증정보 =
        new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    public partial string 채널종류 { get; set; } = CommerceChannelKeys.SmartStore;

    [ObservableProperty]
    public partial string 상점명 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial 판매채널계정항목응답? 등록된계정 { get; private set; }

    public IReadOnlyList<판매채널인증필드정의> 인증필드목록
    {
        get
        {
            EnsureCredentialChannel();
            return 판매채널연결옵션Catalog.찾기(채널종류)?.인증필드 ?? [];
        }
    }

    public string 인증값(string key)
    {
        EnsureCredentialChannel();
        return _인증정보.GetValueOrDefault(key, string.Empty);
    }

    public void 인증값설정(string key, string? value)
    {
        EnsureCredentialChannel();
        _인증정보[key] = value ?? string.Empty;
    }

    public async Task<bool> 등록Async(CancellationToken cancellationToken = default)
    {
        if (판매채널연결옵션Catalog.찾기(채널종류) is null)
        {
            return 유효성실패("지원하는 판매채널을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(상점명))
        {
            return 유효성실패("판매채널에서 구분할 상점명을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                등록된계정 = await service.계정생성Async(new 판매채널계정저장요청
                {
                    채널종류 = 채널종류.Trim(),
                    상점명 = 상점명.Trim(),
                    인증정보 = new Dictionary<string, string>(
                        _인증정보,
                        StringComparer.OrdinalIgnoreCase)
                }, token) ?? throw new InvalidOperationException("판매채널 연결 준비 응답이 비어 있습니다.");
                상점명 = string.Empty;
                _인증정보.Clear();
            },
            "판매채널 자격증명을 서버에 암호화해 저장했습니다. 외부 연결 확인은 채널 모듈에서 수행합니다.",
            cancellationToken,
            ex => $"판매채널 연결 준비를 저장하지 못했습니다. {ex.Message}");
    }

    public void 결과초기화()
    {
        등록된계정 = null;
        작업상태초기화();
    }

    private void EnsureCredentialChannel()
    {
        if (string.Equals(_credentialChannel, 채널종류, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _credentialChannel = 채널종류;
        _인증정보.Clear();
    }
}

/// <summary>판매채널 계정 페이지의 독립 ViewModel 네 개를 조립합니다.</summary>
public sealed class 판매채널계정PageViewModel : 조립ViewModelBase
{
    public 판매채널계정PageViewModel(
        판매채널페이지접근ViewModel access,
        판매채널계정목록PageViewModel list,
        판매채널계정상세PageViewModel detail,
        판매채널계정연결준비ViewModel preparation)
    {
        접근 = 하위ViewModel등록(access);
        목록 = 하위ViewModel등록(list);
        상세 = 하위ViewModel등록(detail);
        연결준비 = 하위ViewModel등록(preparation);
    }

    public 판매채널페이지접근ViewModel 접근 { get; }
    public 판매채널계정목록PageViewModel 목록 { get; }
    public 판매채널계정상세PageViewModel 상세 { get; }
    public 판매채널계정연결준비ViewModel 연결준비 { get; }
}
