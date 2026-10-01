using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>업무 종류와 무관하게 창고 기준정보와 담당자를 관리합니다.</summary>
public partial class 창고기준정보ViewModel : 업무작업ViewModelBase, IDisposable
{
    private readonly I입출고작업Service _service;
    private readonly 입출고화면상태ViewModel _창고상태;

    public 창고기준정보ViewModel(
        I입출고작업Service service,
        입출고화면상태ViewModel 창고상태)
    {
        _service = service;
        _창고상태 = 창고상태;
        현재사용자Context연결(창고상태.현재사용자Context);
        _창고상태.PropertyChanged += 창고상태변경;
    }

    [ObservableProperty]
    public partial 창고저장요청 창고초안 { get; private set; } = new();

    [ObservableProperty]
    public partial 창고저장요청 창고수정초안 { get; private set; } = new();

    [ObservableProperty]
    public partial 창고사용자저장요청 사용자초안 { get; private set; } = new();

    [ObservableProperty]
    public partial 창고사용자저장요청 사용자수정초안 { get; private set; } = new();

    public IReadOnlyList<창고요약응답> 창고목록 => _창고상태.창고목록;
    public 창고요약응답? 선택된창고 => _창고상태.선택된창고;
    public IReadOnlyList<창고사용자항목응답> 사용자목록 => _창고상태.창고사용자목록;
    public 창고사용자항목응답? 선택된사용자 => _창고상태.선택된창고사용자;

    public Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => _창고상태.창고목록적용(await _service.창고목록조회Async(token)),
            "창고 목록을 조회했습니다.",
            cancellationToken);

    public bool 창고선택(long warehouseId)
    {
        if (!_창고상태.창고선택(warehouseId))
        {
            return 유효성실패("목록에 있는 창고를 선택해 주세요.");
        }

        return true;
    }

    public async Task<bool> 창고생성Async(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(창고초안.창고명)
            || string.IsNullOrWhiteSpace(창고초안.주소))
        {
            return 유효성실패("창고명과 주소를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.창고생성Async(창고초안, token)
                    ?? throw new InvalidOperationException("창고 생성 응답이 비어 있습니다.");
                _창고상태.창고저장적용(result);
                창고초안 = new 창고저장요청();
            },
            "창고 기준정보를 생성했습니다.",
            cancellationToken);
    }

    public bool 창고수정초안적용()
    {
        var selected = 선택된창고;
        if (selected is null)
        {
            return 유효성실패("수정할 창고를 먼저 선택해 주세요.");
        }

        창고수정초안 = new 창고저장요청
        {
            창고명 = selected.창고명,
            소유자유형 = selected.소유자유형,
            창고유형 = selected.창고유형,
            물류대행지분류 = selected.물류대행지분류,
            주소 = selected.주소,
            담당자명 = selected.담당자명,
            연락처 = selected.연락처,
            위도 = selected.위도,
            경도 = selected.경도,
            기본창고여부 = selected.기본창고여부
        };
        return true;
    }

    public async Task<bool> 창고수정Async(CancellationToken cancellationToken = default)
    {
        var warehouseId = 선택된창고?.Id;
        if (warehouseId is null)
        {
            return 유효성실패("수정할 창고를 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(창고수정초안.창고명)
            || string.IsNullOrWhiteSpace(창고수정초안.주소))
        {
            return 유효성실패("창고명과 주소를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.창고수정Async(warehouseId.Value, 창고수정초안, token)
                    ?? throw new InvalidOperationException("창고 수정 응답이 비어 있습니다.");
                _창고상태.창고저장적용(result);
                창고수정초안적용();
            },
            "창고 기준정보를 수정했습니다.",
            cancellationToken);
    }

    public async Task<bool> 창고삭제Async(CancellationToken cancellationToken = default)
    {
        var warehouseId = 선택된창고?.Id;
        if (warehouseId is null)
        {
            return 유효성실패("삭제할 창고를 먼저 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                await _service.창고삭제Async(warehouseId.Value, token);
                _창고상태.창고삭제적용(warehouseId.Value);
            },
            "창고 기준정보를 삭제했습니다.",
            cancellationToken);
    }

    public async Task<bool> 사용자목록조회Async(CancellationToken cancellationToken = default)
    {
        var warehouseId = 선택된창고?.Id;
        if (warehouseId is null)
        {
            return 유효성실패("사용자를 조회할 창고를 먼저 선택해 주세요.");
        }

        return await 작업실행Async(
            async token => _창고상태.창고사용자목록적용(
                await _service.창고사용자목록조회Async(warehouseId.Value, token)),
            "선택한 창고의 담당자 목록을 조회했습니다.",
            cancellationToken);
    }

    public async Task<bool> 사용자추가Async(CancellationToken cancellationToken = default)
    {
        var warehouseId = 선택된창고?.Id;
        if (warehouseId is null)
        {
            return 유효성실패("담당자를 추가할 창고를 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(사용자초안.UserId)
            || string.IsNullOrWhiteSpace(사용자초안.역할명))
        {
            return 유효성실패("담당자 사용자 ID와 역할을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.창고사용자추가Async(warehouseId.Value, 사용자초안, token)
                    ?? throw new InvalidOperationException("창고 담당자 추가 응답이 비어 있습니다.");
                _창고상태.창고사용자저장적용(result);
                사용자초안 = new 창고사용자저장요청();
            },
            "선택한 창고에 담당자를 추가했습니다.",
            cancellationToken);
    }

    public bool 사용자선택(long warehouseUserId)
        => _창고상태.창고사용자선택(warehouseUserId)
           || 유효성실패("목록에 있는 창고 담당자를 선택해 주세요.");

    public bool 사용자수정초안적용()
    {
        var selected = 선택된사용자;
        if (selected is null)
        {
            return 유효성실패("수정할 창고 담당자를 먼저 선택해 주세요.");
        }

        사용자수정초안 = new 창고사용자저장요청
        {
            UserId = selected.UserId,
            역할명 = selected.역할명,
            IsPrimary = selected.IsPrimary
        };
        return true;
    }

    public async Task<bool> 사용자수정Async(CancellationToken cancellationToken = default)
    {
        var warehouseId = 선택된창고?.Id;
        var warehouseUserId = 선택된사용자?.Id;
        if (warehouseId is null || warehouseUserId is null)
        {
            return 유효성실패("수정할 창고와 담당자를 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(사용자수정초안.UserId)
            || string.IsNullOrWhiteSpace(사용자수정초안.역할명))
        {
            return 유효성실패("담당자 사용자 ID와 역할을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.창고사용자수정Async(
                        warehouseId.Value,
                        warehouseUserId.Value,
                        사용자수정초안,
                        token)
                    ?? throw new InvalidOperationException("창고 담당자 수정 응답이 비어 있습니다.");
                _창고상태.창고사용자저장적용(result);
                사용자수정초안적용();
            },
            "창고 담당자를 수정했습니다.",
            cancellationToken);
    }

    public async Task<bool> 사용자삭제Async(CancellationToken cancellationToken = default)
    {
        var warehouseId = 선택된창고?.Id;
        var warehouseUserId = 선택된사용자?.Id;
        if (warehouseId is null || warehouseUserId is null)
        {
            return 유효성실패("삭제할 창고와 담당자를 먼저 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                await _service.창고사용자삭제Async(warehouseId.Value, warehouseUserId.Value, token);
                _창고상태.창고사용자삭제적용(warehouseUserId.Value);
            },
            "창고 담당자를 삭제했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
    {
        OnPropertyChanged(nameof(창고초안));
        OnPropertyChanged(nameof(창고수정초안));
        OnPropertyChanged(nameof(사용자초안));
        OnPropertyChanged(nameof(사용자수정초안));
    }

    public void Dispose()
    {
        _창고상태.PropertyChanged -= 창고상태변경;
        GC.SuppressFinalize(this);
    }

    private void 창고상태변경(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(창고목록));
        OnPropertyChanged(nameof(선택된창고));
        OnPropertyChanged(nameof(사용자목록));
        OnPropertyChanged(nameof(선택된사용자));
    }
}

/// <summary>공동구매 화면에서 공통 창고 기준정보 기능을 사용하는 얇은 파생 ViewModel입니다.</summary>
public sealed class 공동구매창고기준정보ViewModel(
    I공동구매창고Service service,
    공동구매창고상태ViewModel 창고상태)
    : 창고기준정보ViewModel(service, 창고상태);

/// <summary>입고 요청, 입고 완료, 검수와 적재 같은 입고 작업을 관리합니다.</summary>
public partial class 입고ViewModel : 업무작업ViewModelBase, IDisposable
{
    private readonly I입출고작업Service _service;
    private readonly 입출고화면상태ViewModel _창고상태;
    private readonly bool _세부업무수명소유;

    public 입고ViewModel(
        I입출고작업Service service,
        입출고화면상태ViewModel 창고상태,
        입고원장ViewModel? 원장 = null,
        입고조회ViewModel? 조회 = null,
        입고등록ViewModel? 등록 = null,
        입고수정ViewModel? 수정 = null,
        입고삭제ViewModel? 삭제 = null,
        입고완료ViewModel? 완료 = null,
        입고재고조회ViewModel? 재고조회 = null,
        입고검수ViewModel? 검수 = null,
        입고적재ViewModel? 적재 = null)
    {
        _service = service;
        _창고상태 = 창고상태;
        현재사용자Context연결(창고상태.현재사용자Context);
        this.원장 = 원장 ?? new 입고원장ViewModel(new 입출고원장상태ViewModel());
        _세부업무수명소유 = 조회 is null
                           || 등록 is null
                           || 수정 is null
                           || 삭제 is null
                           || 완료 is null
                          || 재고조회 is null
                          || 검수 is null
                          || 적재 is null;
        this.조회 = 조회 ?? new 입고조회ViewModel(service, 창고상태, this.원장);
        this.등록 = 등록 ?? new 입고등록ViewModel(service, 창고상태);
        this.수정 = 수정 ?? new 입고수정ViewModel(service, 창고상태);
        this.삭제 = 삭제 ?? new 입고삭제ViewModel(service, 창고상태);
        Crud = new 입고CrudViewModel(this.조회, this.등록, this.수정, this.삭제, 하위수명소유: false);
        this.완료 = 완료 ?? new 입고완료ViewModel(service, 창고상태);
        this.재고조회 = 재고조회 ?? new 입고재고조회ViewModel(service, 창고상태);
        this.검수 = 검수 ?? new 입고검수ViewModel(service, 창고상태);
        this.적재 = 적재 ?? new 입고적재ViewModel(service, 창고상태);
        세부업무목록 = [.. Crud.Crud업무목록, this.완료, this.재고조회, this.검수, this.적재];
        foreach (var child in 세부업무목록)
        {
            child.PropertyChanged += 세부업무변경;
        }
        _창고상태.PropertyChanged += 창고상태변경;
        this.원장.PropertyChanged += 원장상태변경;
    }

    [ObservableProperty]
    public partial 입고요청저장요청 입고요청초안 { get; private set; } = new();

    [ObservableProperty]
    public partial 입고완료요청 입고완료초안 { get; private set; } = new();

    [ObservableProperty]
    public partial 입고검수요청 검수초안 { get; private set; } = new();

    [ObservableProperty]
    public partial 적재위치배정요청 적재초안 { get; private set; } = new();

    public string 주문원장역할코드 => 주문원장포함역할.창고입고;
    public 입고원장ViewModel 원장 { get; }
    public 입고조회ViewModel 조회 { get; }
    public 입고등록ViewModel 등록 { get; }
    public 입고수정ViewModel 수정 { get; }
    public 입고삭제ViewModel 삭제 { get; }
    public 입고CrudViewModel Crud { get; }
    public 입고완료ViewModel 완료 { get; }
    public 입고재고조회ViewModel 재고조회 { get; }
    public 입고검수ViewModel 검수 { get; }
    public 입고적재ViewModel 적재 { get; }
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }
    public new bool 처리중 => base.처리중 || 세부업무목록.Any(item => item.처리중);
    public IReadOnlyList<입고요청항목응답> 입고요청목록 => _창고상태.선택창고입고요청목록;
    public IReadOnlyList<입고요청항목응답> 원장연결입고요청목록
        => 원장.원장Id is { Length: > 0 } ledgerId
            ? 입고요청목록.Where(x => string.Equals(x.커뮤니티원장Id, ledgerId, StringComparison.OrdinalIgnoreCase)).ToArray()
            : [];
    public 입고요청항목응답? 선택된입고요청 => _창고상태.선택된입고요청;
    public IReadOnlyList<입고상품항목응답> 최근입고상품목록 => _창고상태.최근입고상품목록;
    public IReadOnlyList<재고항목응답> 재고목록 => _창고상태.선택창고재고목록;
    public 재고항목응답? 선택된재고 => _창고상태.선택된재고;
    public 창고작업결과응답? 최근작업결과 => _창고상태.최근입고작업결과;

    public Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => _창고상태.입고목록적용(await _service.입고목록조회Async(token)),
            "입고원장 목록을 조회했습니다.",
            cancellationToken);

    public Task<bool> 재고목록조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => _창고상태.재고목록적용(await _service.재고목록조회Async(token)),
            "입고 검수·적재 대상 재고를 조회했습니다.",
            cancellationToken);

    public bool 입고요청선택(long inboundId)
    {
        if (!_창고상태.입고요청선택(inboundId))
        {
            return 유효성실패("선택한 창고의 입고 요청을 선택해 주세요.");
        }

        return true;
    }

    public bool 재고선택(long inboundItemId)
    {
        if (!_창고상태.재고선택(inboundItemId))
        {
            return 유효성실패("선택한 창고의 재고 항목을 선택해 주세요.");
        }

        return true;
    }

    public async Task<bool> 입고요청등록Async(CancellationToken cancellationToken = default)
    {
        입고요청초안.창고Id = _창고상태.선택된창고?.Id
            ?? 입고요청초안.창고Id;
        입고요청초안.입고흐름유형 = 입고흐름유형코드.Normalize(입고요청초안.입고흐름유형);

        if (입고요청초안.창고Id <= 0)
        {
            return 유효성실패("입고할 창고를 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(입고요청초안.공급처명))
        {
            return 유효성실패("입고 공급처를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.입고요청생성Async(입고요청초안, token)
                    ?? throw new InvalidOperationException("입고 요청 생성 응답이 비어 있습니다.");
                _창고상태.입고요청저장적용(result);
                입고요청초안 = new 입고요청저장요청 { 창고Id = result.창고Id };
            },
            "입고원장에 입고 요청을 등록했습니다.",
            cancellationToken);
    }

    public async Task<bool> 입고완료Async(CancellationToken cancellationToken = default)
    {
        var inboundId = 선택된입고요청?.Id;
        if (inboundId is null)
        {
            return 유효성실패("완료할 입고 요청을 먼저 선택해 주세요.");
        }

        if (입고완료초안.Items.Count == 0
            || 입고완료초안.Items.Any(x => string.IsNullOrWhiteSpace(x.상품명) || x.입고수량 <= 0))
        {
            return 유효성실패("상품명과 1개 이상의 입고 수량을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var items = await _service.입고완료Async(inboundId.Value, 입고완료초안, token);
                _창고상태.입고완료적용(items);
                _창고상태.재고목록적용(await _service.재고목록조회Async(token));
                입고완료초안 = new 입고완료요청();
            },
            "입고를 완료하고 생성된 재고를 조회했습니다.",
            cancellationToken);
    }

    public async Task<bool> 검수Async(CancellationToken cancellationToken = default)
    {
        var inboundItemId = 선택된재고?.입고상품Id;
        if (inboundItemId is null)
        {
            return 유효성실패("검수할 입고 재고를 먼저 선택해 주세요.");
        }

        if (검수초안.검수수량 <= 0 || 검수초안.불량수량 < 0 || 검수초안.불량수량 > 검수초안.검수수량)
        {
            return 유효성실패("검수 수량과 그 이하의 불량 수량을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.입고검수Async(inboundItemId.Value, 검수초안, token)
                    ?? throw new InvalidOperationException("입고 검수 응답이 비어 있습니다.");
                _창고상태.입고작업결과적용(result);
                검수초안 = new 입고검수요청();
            },
            "입고 재고의 검수를 완료했습니다.",
            cancellationToken);
    }

    public async Task<bool> 적재위치배정Async(CancellationToken cancellationToken = default)
    {
        var inboundItemId = 선택된재고?.입고상품Id;
        if (inboundItemId is null)
        {
            return 유효성실패("적재할 입고 재고를 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(적재초안.보관위치))
        {
            return 유효성실패("적재할 보관 위치를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.적재위치배정Async(inboundItemId.Value, 적재초안, token)
                    ?? throw new InvalidOperationException("적재 위치 배정 응답이 비어 있습니다.");
                _창고상태.입고작업결과적용(result);
                적재초안 = new 적재위치배정요청();
            },
            "입고 재고의 적재 위치를 배정했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
    {
        OnPropertyChanged(nameof(입고요청초안));
        OnPropertyChanged(nameof(입고완료초안));
        OnPropertyChanged(nameof(검수초안));
        OnPropertyChanged(nameof(적재초안));
    }

    public virtual void Dispose()
    {
        foreach (var child in 세부업무목록)
        {
            child.PropertyChanged -= 세부업무변경;
        }

        if (_세부업무수명소유)
        {
            조회.Dispose();
            등록.Dispose();
            수정.Dispose();
            삭제.Dispose();
            완료.Dispose();
            재고조회.Dispose();
            검수.Dispose();
            적재.Dispose();
        }
        Crud.Dispose();
        _창고상태.PropertyChanged -= 창고상태변경;
        원장.PropertyChanged -= 원장상태변경;
        원장.Dispose();
        GC.SuppressFinalize(this);
    }

    private void 창고상태변경(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(입고요청목록));
        OnPropertyChanged(nameof(원장연결입고요청목록));
        OnPropertyChanged(nameof(선택된입고요청));
        OnPropertyChanged(nameof(최근입고상품목록));
        OnPropertyChanged(nameof(재고목록));
        OnPropertyChanged(nameof(선택된재고));
        OnPropertyChanged(nameof(최근작업결과));
    }

    private void 원장상태변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(원장연결입고요청목록));

    private void 세부업무변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(처리중));
}

/// <summary>공동구매 입고 정책을 추가할 수 있는 한 단계 파생 ViewModel입니다.</summary>
public sealed class 공동구매입고원장ViewModel(
    I공동구매창고Service service,
    공동구매창고상태ViewModel 창고상태,
    입고원장ViewModel? 원장 = null)
    : 입고ViewModel(service, 창고상태, 원장);

/// <summary>가용 재고의 포장과 운송 인계 같은 출고 작업을 관리합니다.</summary>
public partial class 출고ViewModel : 업무작업ViewModelBase, IDisposable
{
    private readonly I입출고작업Service _service;
    private readonly bool _세부업무수명소유;
    protected 입출고화면상태ViewModel 창고상태 { get; }

    public 출고ViewModel(
        I입출고작업Service service,
        입출고화면상태ViewModel 창고상태,
        출고원장ViewModel? 원장 = null,
        출고예정조회ViewModel? 출고예정조회 = null,
        출고재고조회ViewModel? 재고조회 = null,
        출고포장ViewModel? 포장 = null,
        출고운송인계ViewModel? 운송인계 = null)
    {
        _service = service;
        this.창고상태 = 창고상태;
        현재사용자Context연결(창고상태.현재사용자Context);
        this.원장 = 원장 ?? new 출고원장ViewModel(new 입출고원장상태ViewModel());
        _세부업무수명소유 = 재고조회 is null || 포장 is null || 운송인계 is null;
        this.출고예정조회 = 출고예정조회 ?? new 출고예정조회ViewModel(service, 창고상태);
        this.재고조회 = 재고조회 ?? new 출고재고조회ViewModel(service, 창고상태);
        this.포장 = 포장 ?? new 출고포장ViewModel(service, 창고상태);
        this.운송인계 = 운송인계 ?? new 출고운송인계ViewModel(service, 창고상태);
        세부업무목록 = [this.출고예정조회, this.재고조회, this.포장, this.운송인계];
        foreach (var child in 세부업무목록)
        {
            child.PropertyChanged += 세부업무변경;
        }
        this.창고상태.PropertyChanged += 창고상태변경;
    }

    [ObservableProperty]
    public partial 포장작업요청 포장초안 { get; private set; } = new();

    [ObservableProperty]
    public partial 재고운송의뢰생성요청 운송인계초안 { get; private set; } = new();

    public string 주문원장역할코드 => 주문원장포함역할.창고출고;
    public 출고원장ViewModel 원장 { get; }
    public 출고예정조회ViewModel 출고예정조회 { get; }
    public 출고재고조회ViewModel 재고조회 { get; }
    public 출고포장ViewModel 포장 { get; }
    public 출고운송인계ViewModel 운송인계 { get; }
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }
    public new bool 처리중 => base.처리중 || 세부업무목록.Any(item => item.처리중);
    public IReadOnlyList<재고항목응답> 출고가능재고목록 => 창고상태.선택창고재고목록;
    public 재고항목응답? 선택된재고 => 창고상태.선택된재고;
    public 창고작업결과응답? 최근포장결과 => 창고상태.최근출고작업결과;
    public 화주운송의뢰응답? 최근운송의뢰 => 창고상태.최근운송의뢰;

    public bool 출고목록Api지원됨 => true;
    public bool 출고완료Api지원됨 => false;
    public string 현재지원범위
        => "역할별 출고예정 조회 → 재고 조회 → 포장 → 운송 인계까지 처리합니다. 출고완료 확정 명령은 전용 API가 추가된 뒤 연결합니다.";

    public Task<bool> 출고예정목록조회Async(
        목록조회요청 request,
        CancellationToken cancellationToken = default)
        => 출고예정조회.조회Async(request, cancellationToken);

    public Task<bool> 재고목록조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => 창고상태.재고목록적용(await _service.재고목록조회Async(token)),
            "출고 가능한 재고를 조회했습니다.",
            cancellationToken);

    public bool 재고선택(long inboundItemId)
    {
        if (!창고상태.재고선택(inboundItemId))
        {
            return 유효성실패("선택한 창고의 출고 대상 재고를 선택해 주세요.");
        }

        운송인계초안.입고상품Id = inboundItemId;
        OnPropertyChanged(nameof(운송인계초안));
        return true;
    }

    public async Task<bool> 포장Async(CancellationToken cancellationToken = default)
    {
        var inventory = 선택된재고;
        if (inventory is null)
        {
            return 유효성실패("포장할 출고 대상 재고를 먼저 선택해 주세요.");
        }

        if (포장초안.포장수량 <= 0 || 포장초안.포장수량 > inventory.가용수량)
        {
            return 유효성실패("가용 수량 이하의 포장 수량을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.포장작업Async(inventory.입고상품Id, 포장초안, token)
                    ?? throw new InvalidOperationException("출고 포장 응답이 비어 있습니다.");
                창고상태.출고작업결과적용(result);
                포장초안 = new 포장작업요청();
            },
            "출고 대상 재고를 포장했습니다.",
            cancellationToken);
    }

    public async Task<bool> 운송인계Async(CancellationToken cancellationToken = default)
    {
        var inventory = 선택된재고;
        if (inventory is null)
        {
            return 유효성실패("운송에 인계할 출고 대상 재고를 먼저 선택해 주세요.");
        }

        운송인계초안.입고상품Id = inventory.입고상품Id;
        if (운송인계초안.요청수량 <= 0
            || 운송인계초안.요청수량 > inventory.가용수량
            || string.IsNullOrWhiteSpace(운송인계초안.하차지주소))
        {
            return 유효성실패("가용 수량 이하의 요청 수량과 하차지 주소를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.운송인계Async(운송인계초안, token)
                    ?? throw new InvalidOperationException("출고 운송 인계 응답이 비어 있습니다.");
                창고상태.운송의뢰적용(result);
                운송인계초안 = new 재고운송의뢰생성요청();
            },
            "출고 대상 재고를 운송 원장으로 인계했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
    {
        OnPropertyChanged(nameof(포장초안));
        OnPropertyChanged(nameof(운송인계초안));
    }

    protected void 포장초안적용(포장작업요청 request)
    {
        포장초안 = request;
        포장.초안적용(request);
    }

    protected void 운송인계초안적용(재고운송의뢰생성요청 request)
    {
        운송인계초안 = request;
        운송인계.초안적용(request);
    }

    public virtual void Dispose()
    {
        foreach (var child in 세부업무목록)
        {
            child.PropertyChanged -= 세부업무변경;
        }

        if (_세부업무수명소유)
        {
            재고조회.Dispose();
            포장.Dispose();
            운송인계.Dispose();
        }
        창고상태.PropertyChanged -= 창고상태변경;
        원장.Dispose();
        GC.SuppressFinalize(this);
    }

    private void 창고상태변경(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(출고가능재고목록));
        OnPropertyChanged(nameof(선택된재고));
        OnPropertyChanged(nameof(최근포장결과));
        OnPropertyChanged(nameof(최근운송의뢰));
    }

    private void 세부업무변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(처리중));

}

/// <summary>
/// 공통 출고 작업에 공동주문별 재고 배분 선택만 추가합니다.
/// 포장·운송 작업 자체는 <see cref="출고ViewModel"/>에서 재사용합니다.
/// </summary>
public sealed class 공동구매출고원장ViewModel : 출고ViewModel
{
    private 공동구매재고배분ViewModel? _재고배분;
    private string? _선택된개별주문원장Id;
    private OutboundBatchAllocation? _선택된배분;

    public 공동구매출고원장ViewModel(
        I공동구매창고Service service,
        공동구매창고상태ViewModel 창고상태,
        출고원장ViewModel? 원장 = null)
        : base(service, 창고상태, 원장)
    {
    }

    public string? 공동구매주문집계원장Id => _재고배분?.주문집계.공동구매주문집계원장Id;

    public string? 선택된개별주문원장Id
    {
        get => _선택된개별주문원장Id;
        private set => SetProperty(ref _선택된개별주문원장Id, value);
    }

    public OutboundBatchAllocation? 선택된배분
    {
        get => _선택된배분;
        private set => SetProperty(ref _선택된배분, value);
    }

    public new string 현재지원범위
        => "공동 원장별 출고예정 조회와 공동주문별 서버 재고 배분 결과를 공통 출고 작업에 연결합니다. 출고완료 확정 명령은 전용 API가 추가된 뒤 연결합니다.";

    public void 재고배분연결(공동구매재고배분ViewModel 재고배분)
    {
        ArgumentNullException.ThrowIfNull(재고배분);
        if (ReferenceEquals(_재고배분, 재고배분))
        {
            return;
        }

        if (_재고배분 is not null)
        {
            _재고배분.PropertyChanged -= 재고배분변경;
        }

        _재고배분 = 재고배분;
        _재고배분.PropertyChanged += 재고배분변경;
        선택된개별주문원장Id = null;
        선택된배분 = null;
        OnPropertyChanged(string.Empty);
    }

    public bool 배분출고선택(string individualOrderLedgerId, long inboundProductId)
    {
        if (_재고배분 is null
            || !_재고배분.서버계획결과.TryGetValue(individualOrderLedgerId, out var plan))
        {
            return 유효성실패("서버 재고 배분 결과가 있는 개별 주문을 선택해 주세요.");
        }

        var allocation = plan.Allocations.FirstOrDefault(candidate => candidate.InboundProductId == inboundProductId);
        var draft = _재고배분.출고배치초안목록.FirstOrDefault(candidate =>
            string.Equals(candidate.개별주문원장Id, individualOrderLedgerId, StringComparison.OrdinalIgnoreCase));
        if (allocation is not null
            && 창고상태.창고목록.Any(warehouse => warehouse.Id == allocation.WarehouseId))
        {
            창고상태.창고선택(allocation.WarehouseId);
        }

        if (allocation is null || draft is null || !창고상태.재고선택(inboundProductId))
        {
            return 유효성실패("배분된 입고상품 재고를 현재 창고 목록에서 찾을 수 없습니다.");
        }

        선택된개별주문원장Id = individualOrderLedgerId;
        선택된배분 = allocation;
        포장초안적용(new 포장작업요청
        {
            포장수량 = allocation.Quantity,
            포장메모 = $"공동주문 {공동구매주문집계원장Id} / 개별 주문 {individualOrderLedgerId} 배분 포장"
        });
        운송인계초안적용(new 재고운송의뢰생성요청
        {
            입고상품Id = allocation.InboundProductId,
            요청수량 = allocation.Quantity,
            하차지주소 = draft.목적지주소,
            화물종류 = string.IsNullOrWhiteSpace(allocation.ProductName)
                ? allocation.Sku
                : allocation.ProductName
        });
        OnPropertyChanged(string.Empty);
        return true;
    }

    public override void Dispose()
    {
        if (_재고배분 is not null)
        {
            _재고배분.PropertyChanged -= 재고배분변경;
        }

        base.Dispose();
    }

    private void 재고배분변경(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(공동구매주문집계원장Id));
        if (선택된개별주문원장Id is not null
            && (_재고배분 is null
                || !_재고배분.서버계획결과.ContainsKey(선택된개별주문원장Id)))
        {
            선택된개별주문원장Id = null;
            선택된배분 = null;
        }
    }
}

/// <summary>공통 입출고 작업, 커뮤니티 원장 상태와 창고 기준정보를 하나의 화면 단위로 조립합니다.</summary>
public sealed class 입출고화면ViewModel : 조립ViewModelBase, ICrudPageViewModel
{
    public 입출고화면ViewModel(
        입출고화면상태ViewModel 상태,
        입출고원장상태ViewModel 원장상태,
        입출고원장목록ViewModel 원장목록,
        창고기준정보ViewModel 기준정보,
        창고CrudViewModel 창고Crud,
        창고사용자CrudViewModel 창고사용자Crud,
        입고ViewModel 입고,
        출고ViewModel 출고)
    {
        this.상태 = 상태;
        this.원장상태 = 원장상태;
        this.원장목록 = 하위ViewModel등록(원장목록);
        this.기준정보 = 하위ViewModel등록(기준정보, 수명소유: false);
        this.창고Crud = 하위ViewModel등록(창고Crud);
        this.창고사용자Crud = 하위ViewModel등록(창고사용자Crud);
        this.입고 = 하위ViewModel등록(입고);
        this.출고 = 하위ViewModel등록(출고);
        Crud업무단위목록 = [창고Crud, 창고사용자Crud, 입고.Crud];
        기준정보세부업무목록 = [.. 창고Crud.Crud업무목록, .. 창고사용자Crud.Crud업무목록];
        세부업무목록 = [.. 기준정보세부업무목록, .. 입고.세부업무목록, .. 출고.세부업무목록];
    }

    public 입출고화면상태ViewModel 상태 { get; }
    public 입출고원장상태ViewModel 원장상태 { get; }
    public 입출고원장목록ViewModel 원장목록 { get; }
    public 창고기준정보ViewModel 기준정보 { get; }
    public 창고CrudViewModel 창고Crud { get; }
    public 창고사용자CrudViewModel 창고사용자Crud { get; }
    public 입고ViewModel 입고 { get; }
    public 출고ViewModel 출고 { get; }
    public IReadOnlyList<I업무단위CrudViewModel> Crud업무단위목록 { get; }
    public IReadOnlyList<I업무조각ViewModel> 기준정보세부업무목록 { get; }
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }
    public bool 처리중 => 원장목록.처리중 || 기준정보.처리중 || 입고.처리중 || 출고.처리중;

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        if (!await 기준정보.목록조회Async(cancellationToken))
        {
            return false;
        }

        var ledgersLoaded = await 원장목록.목록조회Async(cancellationToken);
        var inboundLoaded = await 입고.목록조회Async(cancellationToken);
        var inventoryLoaded = await 출고.재고목록조회Async(cancellationToken);
        return ledgersLoaded && inboundLoaded && inventoryLoaded;
    }
}

/// <summary>공동구매 실행 화면에 창고 기준정보, 입고원장과 출고원장을 조립합니다.</summary>
public sealed class 공동구매창고기능ViewModel : 조립ViewModelBase
{
    public 공동구매창고기능ViewModel(
        공동구매창고상태ViewModel 상태,
        공동구매창고기준정보ViewModel 기준정보,
        공동구매입고원장ViewModel 입고원장,
        공동구매출고원장ViewModel 출고원장)
    {
        this.상태 = 상태;
        this.기준정보 = 하위ViewModel등록(기준정보);
        this.입고원장 = 하위ViewModel등록(입고원장);
        this.출고원장 = 하위ViewModel등록(출고원장);
    }

    public 공동구매창고상태ViewModel 상태 { get; }
    public 공동구매창고기준정보ViewModel 기준정보 { get; }
    public 공동구매입고원장ViewModel 입고원장 { get; }
    public 공동구매출고원장ViewModel 출고원장 { get; }
    public bool 처리중 => 기준정보.처리중 || 입고원장.처리중 || 출고원장.처리중;

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        if (!await 기준정보.목록조회Async(cancellationToken))
        {
            return false;
        }

        var inboundLoaded = await 입고원장.목록조회Async(cancellationToken);
        var inventoryLoaded = await 출고원장.재고목록조회Async(cancellationToken);
        return inboundLoaded && inventoryLoaded;
    }
}
