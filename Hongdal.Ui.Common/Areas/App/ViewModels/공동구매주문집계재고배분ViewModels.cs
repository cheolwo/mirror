using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.Warehouse;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 공동주문개별주문항목ViewModel(
    string 수요Id,
    string 개별주문원장Id,
    string 입고예정원장Id,
    string 주문자키,
    string 주문자표시명,
    long? 도착창고Id,
    string 도착창고유형,
    string 도착창고명,
    string 수령지주소참조키,
    string 입고의미상태,
    decimal 주문수량,
    string 수량단위,
    string 결제상태);

public sealed record 공동주문수량집계ViewModel(
    string 수량단위,
    decimal 총주문수량,
    int 개별주문수);

/// <summary>선택한 자동집단을 개별 주문 원장의 집합인 공동주문으로 투영합니다.</summary>
public sealed class 공동구매주문집계ViewModel : ObservableObject, IDisposable
{
    private readonly 공동구매실행상태ViewModel _실행상태;

    public 공동구매주문집계ViewModel(공동구매실행상태ViewModel 실행상태)
    {
        _실행상태 = 실행상태;
        _실행상태.PropertyChanged += 실행상태변경;
    }

    public 공동구매자동집단응답? 자동집단 => _실행상태.선택된자동집단;
    public string? 공동구매주문집계원장Id => _실행상태.공동구매주문집계원장Id;
    public string? 상품키 => 자동집단?.상품키;
    public string? 상품명 => 자동집단?.상품명;
    public IReadOnlyList<공동주문개별주문항목ViewModel> 개별주문목록
        => 자동집단?.수요목록.Select(항목생성).ToArray() ?? [];
    public IReadOnlyList<공동주문수량집계ViewModel> 수량집계
        => 개별주문목록
            .GroupBy(item => item.수량단위, StringComparer.OrdinalIgnoreCase)
            .Select(group => new 공동주문수량집계ViewModel(
                group.Key,
                group.Sum(item => item.주문수량),
                group.Count()))
            .ToArray();
    public int 개별주문수 => 개별주문목록.Count;
    public int 입고예정주문수 => 개별주문목록.Count(item =>
        string.Equals(
            item.입고의미상태,
            공동구매개별주문입고상태코드.입고예정,
            StringComparison.OrdinalIgnoreCase));
    public int 가상창고주문수 => 개별주문목록.Count(item =>
        string.Equals(item.도착창고유형, 창고유형코드.가상창고, StringComparison.OrdinalIgnoreCase)
        || (item.도착창고Id is null && !string.IsNullOrWhiteSpace(item.수령지주소참조키)));
    public IReadOnlyList<string> 검증오류
    {
        get
        {
            var errors = new List<string>();
            if (자동집단 is null)
            {
                errors.Add("집계할 자동집단을 선택해 주세요.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(공동구매주문집계원장Id))
            {
                errors.Add("공동구매 주문집계 원장 ID가 없습니다.");
            }

            if (개별주문목록.Count == 0)
            {
                errors.Add("공동주문에 포함된 개별 주문이 없습니다.");
            }

            if (개별주문목록.Any(item => string.IsNullOrWhiteSpace(item.개별주문원장Id)))
            {
                errors.Add("개별 주문 원장이 연결되지 않은 주문이 있습니다.");
            }

            if (개별주문목록
                .Where(item => !string.IsNullOrWhiteSpace(item.개별주문원장Id))
                .GroupBy(item => item.개별주문원장Id, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                errors.Add("같은 개별 주문 원장이 공동주문에 중복 포함되어 있습니다.");
            }

            if (개별주문목록.Any(item => item.주문수량 <= 0 || string.IsNullOrWhiteSpace(item.수량단위)))
            {
                errors.Add("개별 주문의 수량과 단위를 확인해 주세요.");
            }

            return errors;
        }
    }

    public bool 집계완료 => 검증오류.Count == 0;
    public string 집계안내
        => 집계완료
            ? $"개별 주문 {개별주문수}건을 공동주문 원장 {공동구매주문집계원장Id}에서 집계합니다."
            : string.Join(" ", 검증오류);

    public void Dispose()
    {
        _실행상태.PropertyChanged -= 실행상태변경;
        GC.SuppressFinalize(this);
    }

    private void 실행상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(공동구매실행상태ViewModel.선택된자동집단)
                or nameof(공동구매실행상태ViewModel.공동구매주문집계원장Id))
        {
            OnPropertyChanged(string.Empty);
        }
    }

    private static 공동주문개별주문항목ViewModel 항목생성(공동구매자동수요응답 demand)
        => new(
            demand.수요Id,
            demand.개별주문원장Id,
            demand.입고예정원장Id,
            demand.주문자키,
            demand.주문자표시명,
            demand.도착창고Id,
            demand.도착창고유형,
            demand.도착창고명,
            demand.수령지주소참조키,
            demand.입고의미상태,
            demand.희망수량,
            demand.수량단위,
            demand.결제상태);
}

public sealed class 공동구매출고배치초안ViewModel : ObservableObject
{
    private string _목적지주소;
    private long? _선호입고상품Id;

    public 공동구매출고배치초안ViewModel(
        공동주문개별주문항목ViewModel order,
        string productKey,
        string productName,
        string destinationAddress)
    {
        개별주문 = order;
        상품키 = productKey;
        상품명 = productName;
        _목적지주소 = destinationAddress;
    }

    public 공동주문개별주문항목ViewModel 개별주문 { get; }
    public string 상품키 { get; }
    public string 상품명 { get; }
    public string 개별주문원장Id => 개별주문.개별주문원장Id;
    public string 라인Key => string.IsNullOrWhiteSpace(개별주문.수요Id)
        ? 개별주문.개별주문원장Id
        : 개별주문.수요Id;
    public string 목적지주소
    {
        get => _목적지주소;
        set => SetProperty(ref _목적지주소, value?.Trim() ?? string.Empty);
    }

    public long? 선호입고상품Id
    {
        get => _선호입고상품Id;
        set => SetProperty(ref _선호입고상품Id, value);
    }

    public bool 정수수량 => 개별주문.주문수량 > 0
                           && 개별주문.주문수량 <= int.MaxValue
                           && decimal.Truncate(개별주문.주문수량) == 개별주문.주문수량;
    public bool 목적지확인됨 => !string.IsNullOrWhiteSpace(목적지주소);
    public bool 요청생성가능 => 정수수량 && 목적지확인됨 && !string.IsNullOrWhiteSpace(개별주문원장Id);
    public string? 준비오류
        => !정수수량
            ? "출고배치 API는 1개 단위의 정수 수량만 지원합니다."
            : !목적지확인됨
                ? "실제 출고 목적지 주소를 확인해 주세요."
                : string.IsNullOrWhiteSpace(개별주문원장Id)
                    ? "개별 주문 원장 ID가 필요합니다."
                    : null;

    public OutboundBatchPlanRequest 요청생성(string? sellerUserId = null)
    {
        if (!요청생성가능)
        {
            throw new InvalidOperationException(준비오류);
        }

        return new OutboundBatchPlanRequest
        {
            OrderReference = 개별주문원장Id,
            SellerUserId = sellerUserId?.Trim() ?? string.Empty,
            OrdererUserId = 개별주문.주문자키,
            DestinationAddress = 목적지주소,
            Lines =
            [
                new OutboundBatchPlanLineRequest
                {
                    LineKey = 라인Key,
                    PreferredInboundProductId = 선호입고상품Id,
                    Sku = 상품키,
                    ProductName = 상품명,
                    Quantity = decimal.ToInt32(개별주문.주문수량)
                }
            ]
        };
    }
}

/// <summary>
/// 공동주문을 주문자별 출고배치 요청으로 분리하고 서버 재고 배분 결과를 보관합니다.
/// 배분 계산은 서버 OutboundBatchEngine의 책임이며 ViewModel은 계산 규칙을 복제하지 않습니다.
/// </summary>
public sealed class 공동구매재고배분ViewModel : ObservableObject, IDisposable
{
    private readonly 공동구매창고상태ViewModel _창고상태;
    private readonly Dictionary<string, OutboundBatchPlanResult> _서버계획결과 = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<공동구매출고배치초안ViewModel> _출고배치초안목록 = [];
    private 공동구매자동집단응답? _대상자동집단;

    public 공동구매재고배분ViewModel(
        공동구매주문집계ViewModel 주문집계,
        공동구매창고상태ViewModel 창고상태)
    {
        this.주문집계 = 주문집계;
        _창고상태 = 창고상태;
        this.주문집계.PropertyChanged += 원본상태변경;
        _창고상태.PropertyChanged += 원본상태변경;
        초안재구성();
    }

    public 공동구매주문집계ViewModel 주문집계 { get; }
    public IReadOnlyList<공동구매출고배치초안ViewModel> 출고배치초안목록
    {
        get => _출고배치초안목록;
        private set => SetProperty(ref _출고배치초안목록, value);
    }
    public IReadOnlyDictionary<string, OutboundBatchPlanResult> 서버계획결과 => _서버계획결과;
    public IReadOnlyList<재고항목응답> 재고후보목록
        => _창고상태.재고목록.Where(상품일치).ToArray();
    public decimal 총주문수량 => 주문집계.개별주문목록.Sum(item => item.주문수량);
    public int 총가용재고 => 재고후보목록.Sum(item => item.가용수량);
    public decimal 참고재고부족수량 => Math.Max(0, 총주문수량 - 총가용재고);
    public bool 참고재고충족 => 주문집계.집계완료 && 참고재고부족수량 == 0;
    public bool 계획Api지원됨 => false;
    public string 계획Api안내
        => "서버에는 출고배치 엔진이 있지만 직접 호출하는 공개 API는 아직 없습니다. 이 ViewModel은 요청 초안과 서버 계산 결과 적용 지점을 제공합니다.";
    public bool 요청초안준비완료
        => 주문집계.집계완료
           && 출고배치초안목록.Count == 주문집계.개별주문수
           && 출고배치초안목록.All(draft => draft.요청생성가능);
    public bool 서버배분완료
        => 요청초안준비완료
           && 출고배치초안목록.All(draft =>
               _서버계획결과.TryGetValue(draft.개별주문원장Id, out var result)
               && result.IsComplete);

    public void 초안재구성()
    {
        var group = 주문집계.자동집단;
        _대상자동집단 = group;
        foreach (var draft in 출고배치초안목록)
        {
            draft.PropertyChanged -= 초안변경;
        }

        _서버계획결과.Clear();
        출고배치초안목록 = 주문집계.개별주문목록
            .Select(order =>
            {
                var address = order.도착창고Id is { } warehouseId
                    ? _창고상태.창고목록.FirstOrDefault(warehouse => warehouse.Id == warehouseId)?.주소
                    : null;
                var draft = new 공동구매출고배치초안ViewModel(
                    order,
                    주문집계.상품키 ?? string.Empty,
                    주문집계.상품명 ?? string.Empty,
                    address ?? string.Empty);
                draft.PropertyChanged += 초안변경;
                return draft;
            })
            .ToArray();
        OnPropertyChanged(string.Empty);
    }

    public bool 선호재고선택(string individualOrderLedgerId, long inboundProductId)
    {
        var draft = 출고배치초안목록.FirstOrDefault(candidate =>
            string.Equals(candidate.개별주문원장Id, individualOrderLedgerId, StringComparison.OrdinalIgnoreCase));
        var inventory = 재고후보목록.FirstOrDefault(candidate => candidate.입고상품Id == inboundProductId);
        if (draft is null || inventory is null)
        {
            return false;
        }

        draft.선호입고상품Id = inventory.입고상품Id;
        return true;
    }

    public void 서버계획적용(string individualOrderLedgerId, OutboundBatchPlanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var draft = 출고배치초안목록.FirstOrDefault(candidate =>
            string.Equals(candidate.개별주문원장Id, individualOrderLedgerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"공동주문에 없는 개별 주문 원장입니다: {individualOrderLedgerId}");
        if (result.Allocations.Any(allocation =>
                !string.Equals(allocation.LineKey, draft.라인Key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("서버 배분 결과에 다른 개별 주문의 출고 라인이 포함되어 있습니다.");
        }

        _서버계획결과[individualOrderLedgerId] = result;
        OnPropertyChanged(string.Empty);
    }

    public void Dispose()
    {
        주문집계.PropertyChanged -= 원본상태변경;
        _창고상태.PropertyChanged -= 원본상태변경;
        foreach (var draft in 출고배치초안목록)
        {
            draft.PropertyChanged -= 초안변경;
        }

        주문집계.Dispose();
        GC.SuppressFinalize(this);
    }

    private bool 상품일치(재고항목응답 inventory)
        => (!string.IsNullOrWhiteSpace(주문집계.상품키)
            && string.Equals(inventory.SKU, 주문집계.상품키, StringComparison.OrdinalIgnoreCase))
           || (!string.IsNullOrWhiteSpace(주문집계.상품명)
               && string.Equals(inventory.상품명, 주문집계.상품명, StringComparison.OrdinalIgnoreCase));

    private void 원본상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(_대상자동집단, 주문집계.자동집단))
        {
            초안재구성();
            return;
        }

        foreach (var draft in 출고배치초안목록.Where(draft =>
                     !draft.목적지확인됨 && draft.개별주문.도착창고Id is not null))
        {
            draft.목적지주소 = _창고상태.창고목록.FirstOrDefault(warehouse =>
                warehouse.Id == draft.개별주문.도착창고Id)?.주소 ?? string.Empty;
        }

        OnPropertyChanged(string.Empty);
    }

    private void 초안변경(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(string.Empty);
}
