using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>판매채널 주문 출고 후보의 서버 검색·필터·페이징만 담당합니다.</summary>
public sealed partial class 판매채널주문목록PageViewModel(
    I판매채널주문읽기Service service) : 업무작업ViewModelBase
{
    private const int DefaultPageSize = 25;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(검색조건있음))]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial string 검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(검색조건있음))]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial string? 국내외구분 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(검색조건있음))]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial string? 출고상태 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial IReadOnlyList<판매채널주문요약응답> 주문목록 { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<판매채널주문보류진단> 수집보류목록 { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(총페이지수))]
    public partial int 전체건수 { get; private set; }

    [ObservableProperty]
    public partial int 현재페이지 { get; private set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial bool 초기화됨 { get; private set; }

    public int 총페이지수 => Math.Max(1, (int)Math.Ceiling(전체건수 / (double)DefaultPageSize));
    public bool 검색조건있음 => !string.IsNullOrWhiteSpace(검색어)
                            || !string.IsNullOrWhiteSpace(국내외구분)
                            || !string.IsNullOrWhiteSpace(출고상태);
    public bool 원장없음 => 초기화됨 && 전체건수 == 0 && !검색조건있음;
    public bool 검색결과없음 => 초기화됨 && 전체건수 == 0 && 검색조건있음;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 페이지조회Async(1, cancellationToken);

    public Task<bool> 페이지조회Async(int page, CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token =>
            {
                var response = await service.목록조회Async(new 판매채널주문목록조회요청
                {
                    Page = Math.Max(0, page - 1),
                    PageSize = DefaultPageSize,
                    Search = 검색어,
                    SyncScope = 국내외구분,
                    Status = 출고상태
                }, token);
                주문목록 = response.Items;
                수집보류목록 = response.SyncIssues;
                전체건수 = response.TotalCount;
                현재페이지 = response.Page + 1;
                초기화됨 = true;
            },
            "판매채널 주문 출고 후보를 불러왔습니다.",
            cancellationToken,
            ex => $"판매채널 주문 출고 후보를 불러오지 못했습니다. 잠시 뒤 다시 시도해 주세요. {ex.Message}");

    public void 필터초기화()
    {
        검색어 = string.Empty;
        국내외구분 = null;
        출고상태 = null;
    }
}

/// <summary>주소나 목록에서 선택한 정확한 orderId의 출고 후보 묶음만 조회합니다.</summary>
public sealed partial class 판매채널주문상세PageViewModel(
    I판매채널주문읽기Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    public partial long? 요청OrderId { get; private set; }

    [ObservableProperty]
    public partial 판매채널주문상세응답? 상세 { get; private set; }

    [ObservableProperty]
    public partial bool 찾을수없음 { get; private set; }

    public Task<bool> 조회Async(long orderId, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return Task.FromResult(유효성실패("조회할 판매채널 주문 ID를 확인해 주세요."));
        }

        요청OrderId = orderId;
        상세 = null;
        찾을수없음 = false;
        return 작업실행Async(
            async token =>
            {
                상세 = await service.상세조회Async(orderId, token);
                찾을수없음 = 상세 is null;
            },
            "판매채널 주문 출고 후보 상세를 불러왔습니다.",
            cancellationToken,
            ex => $"판매채널 주문 출고 후보 상세를 불러오지 못했습니다. {ex.Message}");
    }

    public void 선택해제()
    {
        요청OrderId = null;
        상세 = null;
        찾을수없음 = false;
        작업상태초기화();
    }
}

/// <summary>공통 판매채널 접근, 주문 목록과 정확한 주문 상세를 조립합니다.</summary>
public sealed class 판매채널주문PageViewModel : 조립ViewModelBase
{
    public 판매채널주문PageViewModel(
        판매채널페이지접근ViewModel access,
        판매채널주문목록PageViewModel list,
        판매채널주문상세PageViewModel detail)
    {
        접근 = 하위ViewModel등록(access);
        목록 = 하위ViewModel등록(list);
        상세 = 하위ViewModel등록(detail);
    }

    public 판매채널페이지접근ViewModel 접근 { get; }
    public 판매채널주문목록PageViewModel 목록 { get; }
    public 판매채널주문상세PageViewModel 상세 { get; }
}
