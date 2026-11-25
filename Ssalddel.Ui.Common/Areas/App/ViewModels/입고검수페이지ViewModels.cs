using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>입고 검수 대상의 검색·상태 필터·서버 페이징만 관리합니다.</summary>
public sealed partial class 입고검수대상목록ViewModel(
    I입고검수페이지Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    public partial string 검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 조회상태 { get; set; } = 입고검수조회상태코드.대기;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(항목목록))]
    [NotifyPropertyChangedFor(nameof(비어있음))]
    [NotifyPropertyChangedFor(nameof(이전페이지있음))]
    [NotifyPropertyChangedFor(nameof(다음페이지있음))]
    public partial 입고검수대상페이지응답 응답 { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(비어있음))]
    public partial bool 초기화됨 { get; private set; }

    public (string? Search, string InspectionStatus) 적용조회조건 { get; private set; }
        = (null, 입고검수조회상태코드.대기);

    public IReadOnlyList<입고검수대상목록항목응답> 항목목록 => 응답.Items;
    public IReadOnlyList<string> 조회상태목록 => 입고검수조회상태코드.전체목록;
    public bool 비어있음 => 초기화됨 && !오류발생 && 항목목록.Count == 0;
    public bool 이전페이지있음 => 응답.Page > 0;
    public bool 다음페이지있음 => 응답.HasNextPage;

    public async Task<bool> 조회Async(
        int? page = null,
        CancellationToken cancellationToken = default)
    {
        초기화됨 = false;
        var targetPage = Math.Max(0, page ?? 응답.Page);
        var succeeded = await 작업실행Async(
            async token =>
            {
                var request = new 입고검수대상목록조회요청
                {
                    Search = string.IsNullOrWhiteSpace(검색어) ? null : 검색어.Trim(),
                    InspectionStatus = 입고검수조회상태코드.Normalize(조회상태),
                    Page = targetPage,
                    PageSize = 12
                };
                var response = await service.목록조회Async(request, token);
                적용조회조건 = (request.Search, request.InspectionStatus);
                응답 = response;
            },
            "입고 검수 대상 목록을 조회했습니다.",
            cancellationToken,
            ex => $"입고 검수 대상 목록을 조회하지 못했습니다. {ex.Message}");
        초기화됨 = true;
        OnPropertyChanged(nameof(비어있음));
        return succeeded;
    }

    public Task<bool> 이전페이지Async(CancellationToken cancellationToken = default)
        => 이전페이지있음
            ? 조회Async(응답.Page - 1, cancellationToken)
            : Task.FromResult(false);

    public Task<bool> 다음페이지Async(CancellationToken cancellationToken = default)
        => 다음페이지있음
            ? 조회Async(응답.Page + 1, cancellationToken)
            : Task.FromResult(false);
}

/// <summary>명시한 한 입고상품 ID의 검수 상세 재조회만 관리합니다.</summary>
public sealed partial class 입고검수대상상세ViewModel(
    I입고검수페이지Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    public partial long? 조회대상Id { get; private set; }

    [ObservableProperty]
    public partial 입고검수대상상세응답? 항목 { get; private set; }

    [ObservableProperty]
    public partial bool 대상없음 { get; private set; }

    public void 조회대상설정(long? inboundItemId)
    {
        var normalized = inboundItemId is > 0 ? inboundItemId : null;
        if (조회대상Id == normalized)
        {
            return;
        }

        조회대상Id = normalized;
        항목 = null;
        대상없음 = false;
        작업상태초기화();
    }

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
    {
        if (조회대상Id is not { } inboundItemId)
        {
            return Task.FromResult(유효성실패("조회할 입고 검수 대상을 선택해 주세요."));
        }

        항목 = null;
        대상없음 = false;
        return 작업실행Async(
            async token =>
            {
                항목 = await service.상세조회Async(inboundItemId, token);
                대상없음 = 항목 is null;
            },
            "선택한 입고상품을 같은 ID로 다시 조회했습니다.",
            cancellationToken,
            ex => $"입고 검수 대상 상세를 조회하지 못했습니다. {ex.Message}");
    }
}

/// <summary>선택된 한 입고상품의 검수 입력과 Command 제출만 관리합니다.</summary>
public sealed partial class 입고검수작성ViewModel(
    I입고검수페이지Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial long? 대상Id { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial int 검수수량 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial int 불량수량 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial string 검수메모 { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial bool 수량대조확인 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial bool 포장파손확인 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial bool 품질기한확인 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial bool 보관조건확인 { get; set; }

    [ObservableProperty]
    public partial 창고작업결과응답? 결과 { get; private set; }

    public bool 제출가능
        => !처리중
           && 대상Id is > 0
           && 검수수량 is >= 1 and <= 100_000
           && 불량수량 >= 0
           && 불량수량 <= 검수수량
           && (검수메모 ?? string.Empty).Trim().Length <= 400
           && 수량대조확인
           && 포장파손확인
           && 품질기한확인
           && 보관조건확인;

    public void 대상준비(입고검수대상상세응답? item)
    {
        대상Id = item is { CanInspect: true } ? item.InboundItemId : null;
        검수수량 = item?.ReceivedQuantity ?? 0;
        불량수량 = item?.DefectiveQuantity ?? 0;
        검수메모 = string.Empty;
        수량대조확인 = false;
        포장파손확인 = false;
        품질기한확인 = false;
        보관조건확인 = false;
        결과 = null;
        작업상태초기화();
        OnPropertyChanged(nameof(제출가능));
    }

    public Task<bool> 실행Async(
        입고검수대상상세응답? item,
        CancellationToken cancellationToken = default)
    {
        if (item is null || 대상Id != item.InboundItemId || !item.CanInspect)
        {
            return Task.FromResult(유효성실패("검수 가능한 입고상품을 다시 선택해 주세요."));
        }

        if (!제출가능)
        {
            return Task.FromResult(유효성실패("검수 수량과 네 가지 확인 항목을 모두 확인해 주세요."));
        }

        return 작업실행Async(
            async token =>
            {
                결과 = await service.검수Async(item.InboundItemId, new 입고검수요청
                {
                    검수수량 = 검수수량,
                    불량수량 = 불량수량,
                    검수메모 = (검수메모 ?? string.Empty).Trim()
                }, token) ?? throw new InvalidOperationException("입고 검수 저장 응답이 비어 있습니다.");
            },
            "입고 검수 결과를 저장했습니다.",
            cancellationToken,
            ex => $"입고 검수 결과를 저장하지 못했습니다. {ex.Message}");
    }
}

/// <summary>한 입고상품의 검수 Command와 성공 뒤 같은 ID 재조회 순서만 조정합니다.</summary>
public sealed class 입고검수실행ViewModel : PageViewModelBase
{
    private long _inboundItemId;

    public 입고검수실행ViewModel(
        입고검수대상상세ViewModel detail,
        입고검수작성ViewModel writer)
    {
        상세 = 하위ViewModel등록(detail);
        작성 = 하위ViewModel등록(writer);
    }

    public 입고검수대상상세ViewModel 상세 { get; }
    public 입고검수작성ViewModel 작성 { get; }

    protected override bool 하위ViewModel처리중
        => 상세.처리중 || 작성.처리중;

    public Task<bool> 초기화Async(
        long inboundItemId,
        CancellationToken cancellationToken = default)
    {
        _inboundItemId = inboundItemId > 0 ? inboundItemId : 0;
        return base.초기화Async(cancellationToken);
    }

    public Task<bool> 경로대상변경Async(
        long inboundItemId,
        CancellationToken cancellationToken = default)
    {
        _inboundItemId = inboundItemId > 0 ? inboundItemId : 0;
        return base.새로고침Async(cancellationToken);
    }

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        if (_inboundItemId <= 0)
        {
            throw new InvalidOperationException("검수할 입고상품 ID가 필요합니다.");
        }

        상세.조회대상설정(_inboundItemId);
        var loaded = await 상세.조회Async(cancellationToken);
        if (!loaded || 상세.항목?.InboundItemId != _inboundItemId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                상세.오류메시지
                ?? (상세.대상없음
                    ? "선택한 입고 검수 대상을 찾을 수 없거나 조회 범위에 없습니다."
                    : "선택한 입고 검수 대상을 조회하지 못했습니다."));
        }

        작성.대상준비(상세.항목);
    }

    public async Task<bool> 검수후재조회Async(CancellationToken cancellationToken = default)
    {
        var selected = 상세.항목;
        if (selected is null || selected.InboundItemId != _inboundItemId
            || !await 작성.실행Async(selected, cancellationToken))
        {
            return false;
        }

        var detailReloaded = await 상세.조회Async(cancellationToken);
        return detailReloaded
               && 상세.항목?.InboundItemId == _inboundItemId
               && 상세.항목.CanInspect == false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            상세.작업취소();
            작성.작업취소();
        }

        base.Dispose(disposing);
    }
}
