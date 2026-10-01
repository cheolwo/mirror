using Hongdal.Contracts.Common.WarehouseScanning;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public sealed class 창고홈PageViewModel : 창고PageViewModelBase
{
    private readonly I창고작업구성Resolver _구성Resolver;

    public 창고홈PageViewModel(
        창고작업세션상태ViewModel 세션,
        I창고작업구성Resolver 구성Resolver,
        창고목록조회ViewModel 창고조회,
        입고조회ViewModel 입고조회,
        출고재고조회ViewModel 재고조회)
        : base(세션, 창고PageCodes.홈, "창고 홈")
    {
        _구성Resolver = 구성Resolver;
        this.창고조회 = 구성요소등록(창고조회);
        this.입고조회 = 구성요소등록(입고조회);
        this.재고조회 = 구성요소등록(재고조회);
    }

    public 창고목록조회ViewModel 창고조회 { get; }
    public 입고조회ViewModel 입고조회 { get; }
    public 출고재고조회ViewModel 재고조회 { get; }
    public IReadOnlyList<창고PageDefinition> 페이지목록
        => _구성Resolver.페이지목록조회(세션.운영ProfileCode);
    public IReadOnlyList<창고PageDefinition> 연결된페이지목록
        => 페이지목록.Where(page => page.화면연결됨).ToArray();
    public bool 처리중 => 창고조회.처리중 || 입고조회.처리중 || 재고조회.처리중;

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        if (!await 창고조회.조회Async(cancellationToken))
        {
            return false;
        }

        if (세션.선택된창고 is null)
        {
            return true;
        }

        var inboundLoaded = await 입고조회.조회Async(cancellationToken);
        var inventoryLoaded = await 재고조회.조회Async(cancellationToken);
        return inboundLoaded && inventoryLoaded;
    }
}

public sealed class 창고작업보드PageViewModel : 창고PageViewModelBase
{
    private readonly I창고작업구성Resolver _구성Resolver;
    private readonly IWarehousePickingBatchWorkspaceService _피킹Service;
    private IReadOnlyList<WarehousePickingTaskItem> _피킹작업목록 = [];
    private bool _조회중;
    private string? _오류메시지;

    public 창고작업보드PageViewModel(
        창고작업세션상태ViewModel 세션,
        I창고작업구성Resolver 구성Resolver,
        입고조회ViewModel 입고조회,
        출고재고조회ViewModel 재고조회,
        IWarehousePickingBatchWorkspaceService 피킹Service)
        : base(세션, 창고PageCodes.작업보드, "창고 작업 보드")
    {
        _구성Resolver = 구성Resolver;
        _피킹Service = 피킹Service;
        this.입고조회 = 구성요소등록(입고조회);
        this.재고조회 = 구성요소등록(재고조회);
    }

    public 입고조회ViewModel 입고조회 { get; }
    public 출고재고조회ViewModel 재고조회 { get; }
    public IReadOnlyList<창고PageDefinition> 작업영역목록
        => _구성Resolver.페이지목록조회(세션.운영ProfileCode)
            .Where(page => page.페이지코드 != 창고PageCodes.홈)
            .ToArray();

    public IReadOnlyList<WarehousePickingTaskItem> 피킹작업목록
    {
        get => _피킹작업목록;
        private set => SetProperty(ref _피킹작업목록, value);
    }

    public bool 조회중
    {
        get => _조회중;
        private set => SetProperty(ref _조회중, value);
    }

    public string? 오류메시지
    {
        get => _오류메시지;
        private set => SetProperty(ref _오류메시지, value);
    }

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        if (조회중)
        {
            return false;
        }

        var warehouse = 세션.선택된창고;
        if (warehouse is null)
        {
            오류메시지 = "작업 보드를 조회할 창고를 먼저 선택해 주세요.";
            return false;
        }

        조회중 = true;
        오류메시지 = null;
        try
        {
            var inboundLoaded = await 입고조회.조회Async(cancellationToken);
            var inventoryLoaded = await 재고조회.조회Async(cancellationToken);
            피킹작업목록 = await _피킹Service.GetAssignedTasksAsync(warehouse.Id, cancellationToken);
            return inboundLoaded && inventoryLoaded;
        }
        catch (Exception ex)
        {
            오류메시지 = ex.Message;
            return false;
        }
        finally
        {
            조회중 = false;
        }
    }
}

public sealed class 창고입고예정조회PageViewModel : 창고PageViewModelBase
{
    public 창고입고예정조회PageViewModel(
        창고작업세션상태ViewModel 세션,
        창고목록조회ViewModel 창고조회,
        입고예정조회ViewModel 입고예정조회)
        : base(세션, 창고PageCodes.입고예정조회, "입고 예정 조회")
    {
        this.창고조회 = 구성요소등록(창고조회);
        this.입고예정조회 = 구성요소등록(입고예정조회);
    }

    public 창고목록조회ViewModel 창고조회 { get; }
    public 입고예정조회ViewModel 입고예정조회 { get; }
    public bool 처리중 => 창고조회.처리중 || 입고예정조회.처리중;

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
        var profileCode = 세션.운영ProfileCode;
        var loaded = await 창고조회.조회Async(cancellationToken);
        if (loaded && !string.Equals(세션.운영ProfileCode, profileCode, StringComparison.OrdinalIgnoreCase))
        {
            세션.운영Profile설정(profileCode);
        }

        return loaded;
    }

    public bool 창고선택(long warehouseId)
    {
        var profileCode = 세션.운영ProfileCode;
        return 세션.창고선택(warehouseId, profileCode);
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
