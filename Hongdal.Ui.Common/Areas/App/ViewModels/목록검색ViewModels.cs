namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum 목록정렬방향
{
    오름차순,
    내림차순
}

/// <summary>UI 라이브러리와 무관한 목록 정렬 조건입니다.</summary>
public sealed record 목록정렬조건(
    string 필드,
    목록정렬방향 방향,
    int 우선순위 = 0);

/// <summary>서버가 지원하는 필터만 선택적으로 해석할 수 있는 목록 필터 조건입니다.</summary>
public sealed record 목록필터조건(
    string 필드,
    string 연산자,
    string? 값);

/// <summary>페이지 번호는 0부터 시작합니다.</summary>
public sealed record 목록조회요청
{
    public int 페이지 { get; init; }
    public int 페이지크기 { get; init; } = 25;
    public string? 검색어 { get; init; }
    public IReadOnlyList<목록정렬조건> 정렬조건 { get; init; } = [];
    public IReadOnlyList<목록필터조건> 필터조건 { get; init; } = [];

    public 목록조회요청 정규화(int 최대페이지크기 = 200)
        => this with
        {
            페이지 = Math.Max(0, 페이지),
            페이지크기 = Math.Clamp(페이지크기, 1, Math.Max(1, 최대페이지크기)),
            검색어 = string.IsNullOrWhiteSpace(검색어) ? null : 검색어.Trim(),
            정렬조건 = 정렬조건
                .Where(item => !string.IsNullOrWhiteSpace(item.필드))
                .OrderBy(item => item.우선순위)
                .ToArray(),
            필터조건 = 필터조건
                .Where(item => !string.IsNullOrWhiteSpace(item.필드)
                               && !string.IsNullOrWhiteSpace(item.값))
                .ToArray()
        };
}

public sealed record 목록조회결과<TItem>(
    IReadOnlyList<TItem> 항목,
    int 전체건수)
{
    public static 목록조회결과<TItem> 비어있음 { get; } = new([], 0);
}

/// <summary>MudDataGrid 같은 서버 목록 UI가 주입받는 프레임워크 독립 계약입니다.</summary>
public interface I서버목록조회ViewModel<TItem> : I조회ViewModel
{
    목록조회결과<TItem> 결과 { get; }
    목록조회요청? 최근요청 { get; }
    Task<bool> 조회Async(목록조회요청 요청, CancellationToken cancellationToken = default);
}

/// <summary>검색 입력 UI가 항목 형식과 무관하게 사용할 수 있는 비동기 검색 계약입니다.</summary>
public interface I비동기검색ViewModel<TItem> : I조회ViewModel
{
    Task<IReadOnlyList<TItem>> 검색Async(
        string? 검색어,
        CancellationToken cancellationToken = default);
}

/// <summary>기존 서비스 함수를 서버 목록 ViewModel로 점진적으로 전환하는 얇은 어댑터입니다.</summary>
public sealed class 위임서버목록조회ViewModel<TItem> : 업무조각ViewModelBase, I서버목록조회ViewModel<TItem>
{
    private readonly Func<목록조회요청, CancellationToken, Task<목록조회결과<TItem>>> _조회;
    private 목록조회결과<TItem> _결과 = 목록조회결과<TItem>.비어있음;
    private 목록조회요청? _최근요청;

    public 위임서버목록조회ViewModel(
        string 업무코드,
        string 업무명,
        Func<목록조회요청, CancellationToken, Task<목록조회결과<TItem>>> 조회)
        : base(업무코드, 업무명, 업무조각유형.목록조회)
    {
        ArgumentNullException.ThrowIfNull(조회);
        _조회 = 조회;
    }

    public 목록조회결과<TItem> 결과
    {
        get => _결과;
        private set => SetProperty(ref _결과, value);
    }

    public 목록조회요청? 최근요청
    {
        get => _최근요청;
        private set => SetProperty(ref _최근요청, value);
    }

    public Task<bool> 조회Async(
        목록조회요청 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var normalized = 요청.정규화();
        최근요청 = normalized;

        return 작업실행Async(
            async token => 결과 = await _조회(normalized, token),
            $"{업무명} 결과를 조회했습니다.",
            cancellationToken);
    }
}

/// <summary>기존 검색 함수를 Autocomplete용 ViewModel로 점진적으로 전환하는 얇은 어댑터입니다.</summary>
public sealed class 위임비동기검색ViewModel<TItem> : 업무조각ViewModelBase, I비동기검색ViewModel<TItem>
{
    private readonly Func<string?, CancellationToken, Task<IReadOnlyList<TItem>>> _검색;

    public 위임비동기검색ViewModel(
        string 업무코드,
        string 업무명,
        Func<string?, CancellationToken, Task<IReadOnlyList<TItem>>> 검색)
        : base(업무코드, 업무명, 업무조각유형.목록조회)
    {
        ArgumentNullException.ThrowIfNull(검색);
        _검색 = 검색;
    }

    public async Task<IReadOnlyList<TItem>> 검색Async(
        string? 검색어,
        CancellationToken cancellationToken = default)
    {
        작업상태초기화();
        try
        {
            return await _검색(검색어, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            유효성실패(ex.Message);
            return [];
        }
    }
}
