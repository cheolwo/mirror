using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>입고·출고 원장 목록과 현재 선택을 화면의 하위 ViewModel 사이에서 공유합니다.</summary>
public sealed class 입출고원장상태ViewModel : ObservableObject
{
    private IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> _내원장목록 = [];
    private IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> _공유원장목록 = [];
    private PlatformCommunityPostLedgerChoiceResponse? _선택된원장;
    private PlatformCommunityPostLedgerContextResponse? _선택된원장상세;

    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 내원장목록
    {
        get => _내원장목록;
        private set => SetProperty(ref _내원장목록, value);
    }

    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 공유원장목록
    {
        get => _공유원장목록;
        private set => SetProperty(ref _공유원장목록, value);
    }

    public PlatformCommunityPostLedgerChoiceResponse? 선택된원장
    {
        get => _선택된원장;
        private set => SetProperty(ref _선택된원장, value);
    }

    public PlatformCommunityPostLedgerContextResponse? 선택된원장상세
    {
        get => _선택된원장상세;
        private set => SetProperty(ref _선택된원장상세, value);
    }

    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 전체원장목록
        => 내원장목록
            .Concat(공유원장목록)
            .GroupBy(x => x.원장Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderByDescending(x => x.수정시각Utc)
            .ToArray();

    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 입고원장목록
        => 원장목록(CommunityLedgerTemplateKeys.WarehouseInbound);

    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 출고원장목록
        => 원장목록(CommunityLedgerTemplateKeys.WarehouseOutbound);

    public void 목록적용(
        IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> myLedgers,
        IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> sharedLedgers)
    {
        ArgumentNullException.ThrowIfNull(myLedgers);
        ArgumentNullException.ThrowIfNull(sharedLedgers);

        var selectedId = 선택된원장?.원장Id;
        내원장목록 = myLedgers;
        공유원장목록 = sharedLedgers;
        OnPropertyChanged(nameof(전체원장목록));
        OnPropertyChanged(nameof(입고원장목록));
        OnPropertyChanged(nameof(출고원장목록));

        선택된원장 = selectedId is null
            ? null
            : 전체원장목록.FirstOrDefault(x => string.Equals(x.원장Id, selectedId, StringComparison.OrdinalIgnoreCase));
        if (선택된원장 is null)
        {
            선택된원장상세 = null;
        }
    }

    public bool 원장선택(string ledgerId, string expectedTemplateKey)
    {
        var ledger = 전체원장목록.FirstOrDefault(x =>
            string.Equals(x.원장Id, ledgerId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.원장템플릿Key, expectedTemplateKey, StringComparison.OrdinalIgnoreCase));
        if (ledger is null)
        {
            return false;
        }

        if (!string.Equals(선택된원장?.원장Id, ledger.원장Id, StringComparison.OrdinalIgnoreCase))
        {
            선택된원장상세 = null;
        }

        선택된원장 = ledger;
        return true;
    }

    public void 원장상세적용(PlatformCommunityPostLedgerContextResponse context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(선택된원장?.원장Id, context.원장Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("현재 선택한 원장과 상세 조회 결과가 일치하지 않습니다.");
        }

        선택된원장상세 = context;
    }

    private IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 원장목록(string templateKey)
        => 전체원장목록
            .Where(x => string.Equals(x.원장템플릿Key, templateKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
}

/// <summary>입출고 원장 목록 조회와 선택된 원장의 상세 조회를 담당합니다.</summary>
public sealed class 입출고원장목록ViewModel(
    I입출고원장조회Service service,
    입출고원장상태ViewModel state) : 업무작업ViewModelBase
{
    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 입고원장목록 => state.입고원장목록;
    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 출고원장목록 => state.출고원장목록;

    public Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token =>
            {
                var myLedgersTask = service.내원장목록조회Async(token);
                var sharedLedgersTask = service.공유원장목록조회Async(token);
                await Task.WhenAll(myLedgersTask, sharedLedgersTask);
                state.목록적용(await myLedgersTask, await sharedLedgersTask);
                OnPropertyChanged(nameof(입고원장목록));
                OnPropertyChanged(nameof(출고원장목록));
            },
            "입고·출고 원장 목록을 조회했습니다.",
            cancellationToken);

    public async Task<bool> 원장선택Async(
        string ledgerId,
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        if (!state.원장선택(ledgerId, templateKey))
        {
            return 유효성실패("선택할 수 있는 입고·출고 원장을 지정해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var context = await service.원장상세조회Async(ledgerId, token)
                    ?? throw new InvalidOperationException("선택한 원장의 상세 정보를 찾을 수 없습니다.");
                state.원장상세적용(context);
            },
            "선택한 원장의 현재 상태를 조회했습니다.",
            cancellationToken);
    }
}

/// <summary>특정 입출고 원장 종류의 목록·선택·단계·권한을 읽기 좋은 형태로 제공합니다.</summary>
public abstract class 입출고원장ViewModelBase : ObservableObject, IDisposable
{
    private readonly 입출고원장상태ViewModel _state;
    private readonly string _templateKey;

    protected 입출고원장ViewModelBase(입출고원장상태ViewModel state, string templateKey)
    {
        _state = state;
        _templateKey = templateKey;
        _state.PropertyChanged += 상태변경;
    }

    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> 원장목록
        => string.Equals(_templateKey, CommunityLedgerTemplateKeys.WarehouseInbound, StringComparison.OrdinalIgnoreCase)
            ? _state.입고원장목록
            : _state.출고원장목록;

    public PlatformCommunityPostLedgerChoiceResponse? 선택된원장
        => 원장종류일치(_state.선택된원장) ? _state.선택된원장 : null;

    public PlatformCommunityPostLedgerContextResponse? 원장상세
        => 선택된원장 is not null && 원장종류일치(_state.선택된원장상세)
            ? _state.선택된원장상세
            : null;

    public string? 원장Id => 선택된원장?.원장Id;
    public long? Revision => 원장상세?.Revision;
    public string? 상태 => 원장상세?.상태 ?? 선택된원장?.상태;
    public string? 현재단계 => 원장상세?.현재단계 ?? 선택된원장?.현재단계;
    public string? 접근역할 => 원장상세?.접근역할명 ?? 선택된원장?.참여역할;
    public IReadOnlyList<PlatformCommunityLedgerBlockResponse> 블록목록 => 원장상세?.블록목록 ?? [];
    public IReadOnlyList<string> 가능한행동목록 => 원장상세?.가능한행동목록 ?? [];

    public bool 원장선택(string ledgerId) => _state.원장선택(ledgerId, _templateKey);

    public void Dispose()
    {
        _state.PropertyChanged -= 상태변경;
        GC.SuppressFinalize(this);
    }

    private bool 원장종류일치(PlatformCommunityPostLedgerChoiceResponse? ledger)
        => ledger is not null
           && string.Equals(ledger.원장템플릿Key, _templateKey, StringComparison.OrdinalIgnoreCase);

    private bool 원장종류일치(PlatformCommunityPostLedgerContextResponse? ledger)
        => ledger is not null
           && string.Equals(ledger.원장템플릿Key, _templateKey, StringComparison.OrdinalIgnoreCase);

    private void 상태변경(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(string.Empty);
}

public sealed class 입고원장ViewModel(입출고원장상태ViewModel state)
    : 입출고원장ViewModelBase(state, CommunityLedgerTemplateKeys.WarehouseInbound);

public sealed class 출고원장ViewModel(입출고원장상태ViewModel state)
    : 입출고원장ViewModelBase(state, CommunityLedgerTemplateKeys.WarehouseOutbound);
