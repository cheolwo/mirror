using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.WarehouseScanning;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public sealed class 창고작업보드PageViewModel : 창고PageViewModelBase
{
    private readonly I창고작업구성Resolver _구성Resolver;
    private readonly WarehousePageAvailabilityService _페이지사용가능성;
    private bool _초기화됨;
    private bool _초기화중;
    private bool _기능사용가능;
    private long? _입고요청Id;
    private string _기능안내 = "창고 작업 보드 기능 상태를 확인하고 있습니다.";
    private string? _페이지오류메시지;

    public 창고작업보드PageViewModel(
        창고작업세션상태ViewModel 세션,
        I창고작업구성Resolver 구성Resolver,
        창고로그인ViewModel 인증,
        WarehousePageAvailabilityService 페이지사용가능성,
        입고상세조회ViewModel 입고상세조회)
        : base(세션, 창고PageCodes.작업보드, "창고 작업 보드")
    {
        _구성Resolver = 구성Resolver;
        _페이지사용가능성 = 페이지사용가능성;
        this.인증 = 구성요소등록(인증);
        this.입고상세조회 = 구성요소등록(입고상세조회);
    }

    public 창고로그인ViewModel 인증 { get; }
    public 입고상세조회ViewModel 입고상세조회 { get; }
    public IReadOnlyList<창고PageDefinition> 작업영역목록
        => _구성Resolver.페이지목록조회(세션.운영ProfileCode)
            .Where(page => page.화면연결됨)
            .Where(page => page.페이지코드 is not 창고PageCodes.홈 and not 창고PageCodes.작업보드)
            .ToArray();

    public bool 초기화됨
    {
        get => _초기화됨;
        private set => SetProperty(ref _초기화됨, value);
    }

    public bool 초기화중
    {
        get => _초기화중;
        private set => SetProperty(ref _초기화중, value);
    }

    public bool 기능사용가능
    {
        get => _기능사용가능;
        private set => SetProperty(ref _기능사용가능, value);
    }

    public long? 입고요청Id
    {
        get => _입고요청Id;
        private set => SetProperty(ref _입고요청Id, value);
    }

    public string 기능안내
    {
        get => _기능안내;
        private set => SetProperty(ref _기능안내, value);
    }

    public string? 페이지오류메시지
    {
        get => _페이지오류메시지;
        private set => SetProperty(ref _페이지오류메시지, value);
    }

    public 입고작업보드상태? 작업상태
        => 입고상세조회.항목 is { } item
            ? 입고작업보드정책.해석(item.상태)
            : null;
    public bool 처리중 => 초기화중 || 인증.처리중 || 입고상세조회.처리중;
    public bool 조회대상선택됨 => 입고요청Id.HasValue;

    public async Task<bool> 초기화Async(
        long? inboundId,
        CancellationToken cancellationToken = default)
    {
        입고요청Id = inboundId is > 0 ? inboundId : null;
        입고상세조회.조회대상설정(입고요청Id);
        초기화됨 = false;
        초기화중 = true;
        페이지오류메시지 = null;
        try
        {
            var availability = await _페이지사용가능성.GetWorkBoardAsync(cancellationToken);
            기능사용가능 = availability.IsEnabled;
            기능안내 = availability.Notice;
            if (기능사용가능)
            {
                await 인증.초기화Async(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            기능사용가능 = false;
            페이지오류메시지 = "기능 상태 확인 시간이 초과되었습니다.";
        }
        catch (HttpRequestException)
        {
            기능사용가능 = false;
            페이지오류메시지 = "서버에서 창고 기능 상태를 확인하지 못했습니다.";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            기능사용가능 = false;
            페이지오류메시지 = "창고 기능 상태 응답을 처리하지 못했습니다.";
        }
        finally
        {
            초기화중 = false;
        }

        if (!기능사용가능 || !인증.창고업무접근가능)
        {
            초기화됨 = true;
            return false;
        }

        return await 인증후조회Async(cancellationToken);
    }

    public async Task<bool> 인증후조회Async(CancellationToken cancellationToken = default)
    {
        if (!기능사용가능 || !인증.창고업무접근가능 || 초기화중)
        {
            초기화됨 = true;
            return false;
        }

        초기화중 = true;
        try
        {
            if (!입고요청Id.HasValue)
            {
                return true;
            }

            return await 입고상세조회.조회Async(cancellationToken);
        }
        finally
        {
            초기화중 = false;
            초기화됨 = true;
        }
    }

    public Task<bool> 다시조회Async(CancellationToken cancellationToken = default)
        => 인증후조회Async(cancellationToken);

    public void 인증해제적용()
    {
        입고상세조회.조회결과초기화();
        초기화됨 = true;
    }
}

public sealed class 창고입고예정조회PageViewModel : 창고PageViewModelBase
{
    private bool _초기화됨;
    private bool _초기화중;
    private bool _기능사용가능;
    private long? _선택된창고Id;
    private int _목록갱신번호;
    private string _기능안내 = "창고 입출고 기능 상태를 확인하고 있습니다.";
    private string? _페이지오류메시지;
    private readonly WarehousePageAvailabilityService _페이지사용가능성;

    public 창고입고예정조회PageViewModel(
        창고작업세션상태ViewModel 세션,
        창고로그인ViewModel 인증,
        WarehousePageAvailabilityService 페이지사용가능성,
        창고목록조회ViewModel 창고조회,
        입고예정조회ViewModel 입고예정조회)
        : base(세션, 창고PageCodes.입고예정조회, "입고 예정 조회")
    {
        _페이지사용가능성 = 페이지사용가능성;
        this.인증 = 구성요소등록(인증);
        this.창고조회 = 구성요소등록(창고조회);
        this.입고예정조회 = 구성요소등록(입고예정조회);
    }

    public 창고로그인ViewModel 인증 { get; }
    public 창고목록조회ViewModel 창고조회 { get; }
    public 입고예정조회ViewModel 입고예정조회 { get; }
    public bool 초기화됨
    {
        get => _초기화됨;
        private set => SetProperty(ref _초기화됨, value);
    }

    public bool 초기화중
    {
        get => _초기화중;
        private set => SetProperty(ref _초기화중, value);
    }

    public bool 기능사용가능
    {
        get => _기능사용가능;
        private set => SetProperty(ref _기능사용가능, value);
    }

    public string 기능안내
    {
        get => _기능안내;
        private set => SetProperty(ref _기능안내, value);
    }

    public string? 페이지오류메시지
    {
        get => _페이지오류메시지;
        private set => SetProperty(ref _페이지오류메시지, value);
    }

    public long? 선택된창고Id
    {
        get => _선택된창고Id;
        private set => SetProperty(ref _선택된창고Id, value);
    }

    public int 목록갱신번호
    {
        get => _목록갱신번호;
        private set => SetProperty(ref _목록갱신번호, value);
    }

    public bool 처리중
        => 초기화중 || 인증.처리중 || 창고조회.처리중 || 입고예정조회.처리중;
    public bool 창고목록비어있음
        => 초기화됨
           && string.IsNullOrWhiteSpace(창고조회.오류메시지)
           && 창고조회.항목목록.Count == 0;

    public string 입고작업경로
        => 세션.운영ProfileCode switch
        {
            창고운영ProfileCodes.보세수입 => WarehouseManagerRoutes.ImportArrival,
            창고운영ProfileCodes.마트도심 => WarehouseManagerRoutes.MartInboundWorkStart,
            창고운영ProfileCodes.공동주택물류 => WarehouseManagerRoutes.ApartmentInbound,
            _ => WarehouseManagerRoutes.InboundProductScan
        };

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        초기화됨 = false;
        초기화중 = true;
        페이지오류메시지 = null;
        try
        {
            var availability = await _페이지사용가능성.GetExpectedInboundsAsync(cancellationToken);
            기능사용가능 = availability.IsEnabled;
            기능안내 = availability.Notice;
            if (기능사용가능)
            {
                await 인증.초기화Async(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            기능사용가능 = false;
            페이지오류메시지 = "기능 상태 확인 시간이 초과되었습니다.";
        }
        catch (HttpRequestException)
        {
            기능사용가능 = false;
            페이지오류메시지 = "서버에서 창고 기능 상태를 확인하지 못했습니다.";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            기능사용가능 = false;
            페이지오류메시지 = "창고 기능 상태 응답을 처리하지 못했습니다.";
        }
        finally
        {
            초기화중 = false;
        }

        if (!기능사용가능 || !인증.창고업무접근가능)
        {
            초기화됨 = true;
            return false;
        }

        return await 인증후조회Async(cancellationToken);
    }

    public async Task<bool> 인증후조회Async(CancellationToken cancellationToken = default)
    {
        if (!기능사용가능 || !인증.창고업무접근가능 || 초기화중)
        {
            초기화됨 = true;
            return false;
        }

        초기화중 = true;
        var profileCode = 세션.운영ProfileCode;
        try
        {
            var loaded = await 창고조회.조회Async(cancellationToken);
            if (loaded && !string.Equals(세션.운영ProfileCode, profileCode, StringComparison.OrdinalIgnoreCase))
            {
                세션.운영Profile설정(profileCode);
            }

            선택된창고Id = 세션.선택된창고?.Id;
            초기화됨 = true;
            return loaded;
        }
        finally
        {
            초기화중 = false;
        }
    }

    public bool 창고선택(long? warehouseId)
    {
        if (!warehouseId.HasValue)
        {
            return false;
        }

        var profileCode = 세션.운영ProfileCode;
        if (!세션.창고선택(warehouseId.Value, profileCode))
        {
            return false;
        }

        선택된창고Id = warehouseId;
        목록새로고침요청();
        return true;
    }

    public void 목록새로고침요청()
    {
        if (인증.창고업무접근가능 && 선택된창고Id.HasValue)
        {
            목록갱신번호++;
        }
    }

    public string? 입고작업선택(입고요청항목응답 item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return 입고예정조회.선택(item.Id)
            ? WarehouseManagerRoutes.WorkBoardForInbound(item.Id)
            : null;
    }

    public void 인증해제적용()
    {
        선택된창고Id = null;
        초기화됨 = true;
    }
}

public sealed class 창고작업시작PageViewModel : 창고PageViewModelBase
{
    private readonly IWarehouseWorkEntryGateService _작업자확인Service;
    private string _processCode = WarehouseWorkProcessCodes.Inbound;
    private bool _확인중;
    private WarehouseWorkOperatorVerificationResult? _확인결과;

    public 창고작업시작PageViewModel(
        창고작업세션상태ViewModel 세션,
        IWarehouseWorkEntryGateService 작업자확인Service)
        : base(세션, 창고PageCodes.작업시작, "창고 작업 시작")
    {
        _작업자확인Service = 작업자확인Service;
    }

    public string ProcessCode
    {
        get => _processCode;
        private set => SetProperty(ref _processCode, value);
    }

    public bool 확인중
    {
        get => _확인중;
        private set => SetProperty(ref _확인중, value);
    }

    public WarehouseWorkOperatorVerificationResult? 확인결과
    {
        get => _확인결과;
        private set => SetProperty(ref _확인결과, value);
    }

    public void 초기화(string? processCode)
    {
        ProcessCode = string.IsNullOrWhiteSpace(processCode)
            ? WarehouseWorkProcessCodes.Inbound
            : processCode.Trim();
        확인결과 = null;
    }

    public async Task<bool> 작업자확인Async(
        string phoneLastEightDigits,
        CancellationToken cancellationToken = default)
        => (await 작업자확인결과Async(phoneLastEightDigits, cancellationToken)).IsAllowed;

    public async Task<WarehouseWorkOperatorVerificationResult> 작업자확인결과Async(
        string phoneLastEightDigits,
        CancellationToken cancellationToken = default)
    {
        if (확인중)
        {
            return 확인결과
                   ?? new WarehouseWorkOperatorVerificationResult(
                       false,
                       string.Empty,
                       string.Empty,
                       "작업자 확인이 이미 진행 중입니다.");
        }

        확인중 = true;
        try
        {
            확인결과 = await _작업자확인Service.VerifyAsync(
                ProcessCode,
                phoneLastEightDigits,
                cancellationToken);
            if (확인결과.IsAllowed)
            {
                세션.작업시작(ProcessCode, 확인결과);
            }

            return 확인결과;
        }
        finally
        {
            확인중 = false;
        }
    }
}

public sealed class 창고작업대스캔PageViewModel : 창고PageViewModelBase
{
    private string _작업대Barcode = string.Empty;
    private string? _안내메시지;

    public 창고작업대스캔PageViewModel(창고작업세션상태ViewModel 세션)
        : base(세션, 창고PageCodes.작업대스캔, "창고 작업대 스캔")
    {
    }

    public string 작업대Barcode
    {
        get => _작업대Barcode;
        set
        {
            if (SetProperty(ref _작업대Barcode, value))
            {
                안내메시지 = null;
                OnPropertyChanged(nameof(확인가능));
            }
        }
    }

    public string? 안내메시지
    {
        get => _안내메시지;
        private set => SetProperty(ref _안내메시지, value);
    }

    public bool 확인가능 => 유효한작업대Barcode(작업대Barcode);

    public bool 작업대확인()
    {
        if (!확인가능)
        {
            안내메시지 = "WB:, BENCH: 또는 TABLE:로 시작하는 작업대 바코드를 입력해 주세요.";
            return false;
        }

        try
        {
            세션.작업대확인(작업대Barcode);
        }
        catch (InvalidOperationException ex)
        {
            안내메시지 = ex.Message;
            return false;
        }

        안내메시지 = $"작업대 {세션.현재작업대Barcode} 확인이 완료되었습니다.";
        return true;
    }

    public void 초기화()
    {
        작업대Barcode = string.Empty;
        안내메시지 = null;
    }

    private static bool 유효한작업대Barcode(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.StartsWith("WB:", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("BENCH:", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("TABLE:", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class 창고스캔스테이션PageViewModel : 창고PageViewModelBase
{
    private IReadOnlyList<WarehouseBarcodeScan> _스캔목록 = [];
    private WarehouseScanAction? _최근확정작업;

    public 창고스캔스테이션PageViewModel(창고작업세션상태ViewModel 세션)
        : base(세션, 창고PageCodes.스캔, "창고 스캔 스테이션")
    {
    }

    public IReadOnlyList<WarehouseBarcodeScan> 스캔목록
    {
        get => _스캔목록;
        private set => SetProperty(ref _스캔목록, value);
    }

    public WarehouseScanAction? 최근확정작업
    {
        get => _최근확정작업;
        private set => SetProperty(ref _최근확정작업, value);
    }

    public void 스캔목록적용(IReadOnlyList<WarehouseBarcodeScan> scans)
    {
        ArgumentNullException.ThrowIfNull(scans);
        스캔목록 = scans.ToArray();
    }

    public bool 작업확정(WarehouseScanAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        최근확정작업 = action;
        if (!action.IsReady)
        {
            return false;
        }

        세션.작업선택(action.ActionCode);
        return true;
    }

    public void 초기화()
    {
        스캔목록 = [];
        최근확정작업 = null;
    }
}

public sealed class 창고예외처리PageViewModel(창고작업세션상태ViewModel 세션)
    : 창고PageViewModelBase(세션, 창고PageCodes.예외처리, "창고 예외 처리");

public sealed class 창고작업이력PageViewModel(창고작업세션상태ViewModel 세션)
    : 창고PageViewModelBase(세션, 창고PageCodes.작업이력, "창고 작업 이력");

public sealed class 창고설정PageViewModel : 창고PageViewModelBase, ICrudPageViewModel
{
    private readonly I창고작업구성Resolver _구성Resolver;

    public 창고설정PageViewModel(
        창고작업세션상태ViewModel 세션,
        I창고작업구성Resolver 구성Resolver,
        창고CrudViewModel 창고Crud,
        창고사용자CrudViewModel 창고사용자Crud)
        : base(세션, 창고PageCodes.설정, "창고 설정")
    {
        _구성Resolver = 구성Resolver;
        this.창고Crud = 구성요소등록(창고Crud);
        this.창고사용자Crud = 구성요소등록(창고사용자Crud);
        Crud업무단위목록 = [창고Crud, 창고사용자Crud];
    }

    public 창고CrudViewModel 창고Crud { get; }
    public 창고사용자CrudViewModel 창고사용자Crud { get; }
    public IReadOnlyList<I업무단위CrudViewModel> Crud업무단위목록 { get; }
    public IReadOnlyList<창고운영ProfileDefinition> 운영Profile목록
        => 창고운영ProfileCatalog.전체;
    public IReadOnlyList<창고PageDefinition> 현재Profile페이지목록
        => _구성Resolver.페이지목록조회(세션.운영ProfileCode);
}
