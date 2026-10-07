using Ssalddel.Ui.Common.Areas.App.ViewModels;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

/// <summary>기능·인증 확인을 마친 창고 홈의 읽기와 업무 진입 상태만 관리합니다.</summary>
public sealed class 창고홈PageViewModel : 창고PageViewModelBase
{
    private readonly I창고작업구성Resolver _구성Resolver;
    private readonly WarehousePageAvailabilityService _페이지사용가능성;
    private bool _초기화됨;
    private bool _초기화중;
    private bool _기능사용가능;
    private bool _업무조회성공;
    private long _조회세대;
    private string? _업무조회소유자Id;
    private string _기능안내 = "창고 기능 상태를 확인하고 있습니다.";
    private string? _페이지오류메시지;

    public 창고홈PageViewModel(
        창고작업세션상태ViewModel 세션,
        I창고작업구성Resolver 구성Resolver,
        창고목록조회ViewModel 창고조회,
        입고조회ViewModel 입고조회,
        출고재고조회ViewModel 재고조회,
        창고로그인ViewModel 인증,
        WarehousePageAvailabilityService 페이지사용가능성)
        : base(세션, 창고PageCodes.홈, "창고 홈")
    {
        _구성Resolver = 구성Resolver;
        _페이지사용가능성 = 페이지사용가능성;
        this.창고조회 = 구성요소등록(창고조회);
        this.입고조회 = 구성요소등록(입고조회);
        this.재고조회 = 구성요소등록(재고조회);
        this.인증 = 구성요소등록(인증);
    }

    public 창고목록조회ViewModel 창고조회 { get; }
    public 입고조회ViewModel 입고조회 { get; }
    public 출고재고조회ViewModel 재고조회 { get; }
    public 창고로그인ViewModel 인증 { get; }
    public IReadOnlyList<창고PageDefinition> 페이지목록
        => _구성Resolver.페이지목록조회(세션.운영ProfileCode);
    public IReadOnlyList<창고PageDefinition> 연결된페이지목록
        => 페이지목록.Where(page => page.화면연결됨).ToArray();
    public bool 초기화됨 { get => _초기화됨; private set => SetProperty(ref _초기화됨, value); }
    public bool 초기화중 { get => _초기화중; private set => SetProperty(ref _초기화중, value); }
    public bool 기능사용가능 { get => _기능사용가능; private set => SetProperty(ref _기능사용가능, value); }
    public string 기능안내 { get => _기능안내; private set => SetProperty(ref _기능안내, value); }
    public string? 페이지오류메시지 { get => _페이지오류메시지; private set => SetProperty(ref _페이지오류메시지, value); }
    public bool 처리중 => 초기화중 || 인증.처리중 || 창고조회.처리중 || 입고조회.처리중 || 재고조회.처리중;
    public bool 업무사용가능 => 초기화됨 && !처리중 && 기능사용가능
        && 인증.창고업무접근가능 && _업무조회성공 && 페이지오류메시지 is null
        && string.Equals(_업무조회소유자Id, 인증.현재사용자Id, StringComparison.Ordinal);

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        if (처리중)
        {
            return false;
        }

        초기화됨 = false;
        _조회세대++;
        초기화중 = true;
        _업무조회성공 = false;
        페이지오류메시지 = null;
        기능사용가능 = false;
        try
        {
            var availability = await _페이지사용가능성.GetHomeAsync(cancellationToken);
            기능사용가능 = availability.IsEnabled;
            기능안내 = availability.Notice;
            if (기능사용가능)
            {
                await 인증.초기화Async(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            페이지오류메시지 = "창고 기능 상태 확인 시간이 초과되었습니다. 다시 확인해 주세요.";
        }
        catch (HttpRequestException)
        {
            페이지오류메시지 = "창고 기능 상태를 확인하지 못했습니다. 연결 후 다시 확인해 주세요.";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            페이지오류메시지 = "창고 기능 상태 응답을 처리하지 못했습니다. 다시 확인해 주세요.";
        }
        finally
        {
            초기화됨 = true;
            초기화중 = false;
        }

        return 페이지오류메시지 is null && await 인증후조회Async(cancellationToken);
    }

    public async Task<bool> 인증후조회Async(CancellationToken cancellationToken = default)
    {
        if (!초기화됨 || 처리중 || !기능사용가능 || !인증.창고업무접근가능)
        {
            return false;
        }

        초기화중 = true;
        _업무조회성공 = false;
        페이지오류메시지 = null;
        var generation = ++_조회세대;
        var ownerId = 인증.현재사용자Id;
        try
        {
            var warehouseLoaded = await 창고조회.조회Async(cancellationToken);
            if (!조회소유자확인(generation, ownerId))
            {
                return false;
            }
            if (!warehouseLoaded)
            {
                페이지오류메시지 = "작업 창고를 불러오지 못했습니다. 다시 확인해 주세요.";
                return false;
            }

            if (세션.선택된창고 is not null)
            {
                var inboundLoaded = await 입고조회.조회Async(cancellationToken);
                if (!조회소유자확인(generation, ownerId))
                {
                    return false;
                }
                var inventoryLoaded = await 재고조회.조회Async(cancellationToken);
                if (!조회소유자확인(generation, ownerId))
                {
                    return false;
                }
                if (!inboundLoaded || !inventoryLoaded)
                {
                    페이지오류메시지 = "선택한 창고의 업무 정보를 불러오지 못했습니다. 다시 확인해 주세요.";
                    return false;
                }
            }

            _업무조회소유자Id = ownerId;
            _업무조회성공 = true;
            return true;
        }
        finally
        {
            초기화중 = false;
            OnPropertyChanged(nameof(업무사용가능));
        }
    }

    public void 인증해제적용()
    {
        _조회세대++;
        _업무조회성공 = false;
        _업무조회소유자Id = null;
        페이지오류메시지 = null;
        OnPropertyChanged(nameof(업무사용가능));
    }

    private bool 조회소유자확인(long generation, string? ownerId)
    {
        if (generation == _조회세대 && 인증.창고업무접근가능
            && string.Equals(ownerId, 인증.현재사용자Id, StringComparison.Ordinal))
        {
            return true;
        }

        if (인증.창고업무접근가능)
        {
            페이지오류메시지 = "로그인 상태가 변경되었습니다. 창고 업무를 다시 확인해 주세요.";
        }
        return false;
    }
}
