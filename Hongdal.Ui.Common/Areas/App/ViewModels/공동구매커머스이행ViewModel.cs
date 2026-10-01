using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum 공동구매커머스단계상태
{
    대기,
    진행중,
    완료,
    보류
}

public sealed record 공동구매커머스단계표시(
    int 순서,
    string 코드,
    string 제목,
    string 설명,
    공동구매커머스단계상태 상태);

/// <summary>
/// 주문자에게 공개된 커머스 이행 계획을 조회하고 입고·출품·출고 진행도를 해석합니다.
/// 계획 변경 API는 관리자용이므로 이 ViewModel은 의도적으로 조회 전용입니다.
/// </summary>
public sealed partial class 공동구매커머스이행ViewModel : 공동구매실행업무ViewModelBase, IDisposable
{
    private static readonly IReadOnlyList<(string Code, string Title, string Description)> StageCatalog =
    [
        (공동구매커머스이행상태코드.초안, "이행 초안", "배송권과 상품 단위 이행 조건 정리"),
        (공동구매커머스이행상태코드.물류대행선택, "물류 대행 선택", "대행사와 처리 거점 확정"),
        (공동구매커머스이행상태코드.입고요청, "입고 요청", "창고 입고 일정과 수량 전달"),
        (공동구매커머스이행상태코드.입고완료, "입고 완료", "검수 후 판매 가능 재고 반영"),
        (공동구매커머스이행상태코드.출품준비, "출품 준비", "판매 상품과 채널 정보 정리"),
        (공동구매커머스이행상태코드.판매채널출품완료, "판매 채널 출품", "채널 상품번호와 판매 상태 확인"),
        (공동구매커머스이행상태코드.출고배치준비, "출고 배치 준비", "주문별 피킹·포장·출고 묶음 준비")
    ];

    private readonly I공동구매실행Service _service;
    private readonly 공동구매실행상태ViewModel _실행상태;
    private readonly 공동구매화면상태ViewModel _화면상태;

    public 공동구매커머스이행ViewModel(
        I공동구매실행Service service,
        공동구매실행상태ViewModel 실행상태,
        공동구매화면상태ViewModel 화면상태)
        : base(화면상태)
    {
        _service = service;
        _실행상태 = 실행상태;
        _화면상태 = 화면상태;
        _실행상태.PropertyChanged += 실행상태변경;
        공동구매Id = _실행상태.실행공동구매Id ?? string.Empty;
    }

    [ObservableProperty]
    public partial string 공동구매Id { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 문서관리번호 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<공동구매커머스이행계획공개Dto> 이행계획목록 { get; private set; } = [];

    public 공동구매커머스이행계획공개Dto? 선택된계획 => _실행상태.선택된커머스이행;
    public bool 보류됨 => 선택된계획?.현재상태코드 == 공동구매커머스이행상태코드.보류;
    public bool 변경가능 => false;
    public string 변경안내 => "주문자 API는 이행 계획 조회만 제공합니다. 물류 대행, 입고, 출품과 출고 상태 변경은 관리자 운영 화면에서 처리합니다.";
    public string 다음작업안내 => 다음작업(선택된계획?.현재상태코드);
    public IReadOnlyList<공동구매커머스단계표시> 진행단계 => 단계계산(선택된계획?.현재상태코드);

    public async Task<bool> 공동구매별조회Async(CancellationToken cancellationToken = default)
    {
        var groupPurchaseId = string.IsNullOrWhiteSpace(공동구매Id)
            ? _실행상태.실행공동구매Id
            : 공동구매Id.Trim();
        if (string.IsNullOrWhiteSpace(groupPurchaseId))
        {
            return 유효성실패("자동집단을 선택하거나 조회할 실행 공동구매 ID를 입력해 주세요.");
        }

        공동구매Id = groupPurchaseId;
        _실행상태.실행공동구매선택(groupPurchaseId);
        return await 작업실행Async(
            async token =>
            {
                이행계획목록 = await _service.공동구매별커머스이행조회Async(groupPurchaseId, token);
                await 조회결과선택Async(token);
            },
            "공동구매의 커머스 이행 계획을 조회했습니다.",
            cancellationToken);
    }

    public async Task<bool> 문서번호조회Async(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(문서관리번호))
        {
            return 유효성실패("조회할 문서관리번호를 입력해 주세요.");
        }

        var documentNumber = 문서관리번호.Trim();
        return await 작업실행Async(
            async token =>
            {
                이행계획목록 = await _service.문서번호로커머스이행조회Async(documentNumber, token);
                await 조회결과선택Async(token);
            },
            "문서관리번호로 커머스 이행 계획을 조회했습니다.",
            cancellationToken);
    }

    public async Task<bool> 이행계획선택Async(
        string documentManagementNumber,
        string? deliveryScopeKey = null,
        CancellationToken cancellationToken = default)
    {
        var plan = 이행계획목록.FirstOrDefault(item =>
            string.Equals(item.문서관리번호, documentManagementNumber, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(deliveryScopeKey)
                || string.Equals(item.주문자집단배송권키, deliveryScopeKey, StringComparison.OrdinalIgnoreCase)));
        if (plan is null)
        {
            return 유효성실패("선택할 커머스 이행 계획을 목록에서 찾아 주세요.");
        }

        공동구매Id = plan.공동구매Id;
        문서관리번호 = plan.문서관리번호;
        _실행상태.커머스이행적용(plan);
        await _화면상태.단계도달Async(
            공동구매절차코드.커머스,
            "커머스 이행 계획을 선택하고 입고·재고·출품·출고 추적 단계로 진행했습니다.",
            cancellationToken);
        return true;
    }

    public void Dispose()
    {
        _실행상태.PropertyChanged -= 실행상태변경;
        GC.SuppressFinalize(this);
    }

    private async Task 조회결과선택Async(CancellationToken cancellationToken)
    {
        var selected = 이행계획목록.FirstOrDefault();
        if (selected is null)
        {
            _실행상태.커머스이행적용(null);
            return;
        }

        공동구매Id = selected.공동구매Id;
        문서관리번호 = selected.문서관리번호;
        _실행상태.커머스이행적용(selected);
        await _화면상태.단계도달Async(
            공동구매절차코드.커머스,
            "커머스 이행 계획을 조회하고 입고·재고·출품·출고 추적 단계로 진행했습니다.",
            cancellationToken);
    }

    private void 실행상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(공동구매실행상태ViewModel.실행공동구매Id))
        {
            var sharedId = _실행상태.실행공동구매Id ?? string.Empty;
            if (!string.Equals(공동구매Id, sharedId, StringComparison.Ordinal))
            {
                공동구매Id = sharedId;
                이행계획목록 = [];
            }
        }

        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(공동구매실행상태ViewModel.선택된커머스이행))
        {
            OnPropertyChanged(nameof(선택된계획));
            OnPropertyChanged(nameof(보류됨));
            OnPropertyChanged(nameof(다음작업안내));
            OnPropertyChanged(nameof(진행단계));
        }
    }

    private static IReadOnlyList<공동구매커머스단계표시> 단계계산(string? currentStatus)
    {
        var currentIndex = StageCatalog
            .Select((stage, index) => (stage.Code, index))
            .FirstOrDefault(item => string.Equals(item.Code, currentStatus, StringComparison.OrdinalIgnoreCase))
            .index;
        var found = StageCatalog.Any(stage =>
            string.Equals(stage.Code, currentStatus, StringComparison.OrdinalIgnoreCase));
        var paused = string.Equals(currentStatus, 공동구매커머스이행상태코드.보류, StringComparison.OrdinalIgnoreCase);

        return StageCatalog.Select((stage, index) => new 공동구매커머스단계표시(
                index + 1,
                stage.Code,
                stage.Title,
                stage.Description,
                paused
                    ? 공동구매커머스단계상태.보류
                    : !found
                        ? 공동구매커머스단계상태.대기
                        : index < currentIndex
                            ? 공동구매커머스단계상태.완료
                            : index == currentIndex
                                ? 공동구매커머스단계상태.진행중
                                : 공동구매커머스단계상태.대기))
            .ToArray();
    }

    private static string 다음작업(string? status)
        => status switch
        {
            null or "" => "이행 계획을 조회하면 현재 단계와 다음 작업을 확인할 수 있습니다.",
            공동구매커머스이행상태코드.초안 => "운영자가 물류 대행 방식과 처리 거점을 선택할 차례입니다.",
            공동구매커머스이행상태코드.물류대행선택 => "선택한 창고 또는 물류 거점에 입고를 요청할 차례입니다.",
            공동구매커머스이행상태코드.입고요청 => "입고 검수와 판매 가능 수량 반영을 기다리고 있습니다.",
            공동구매커머스이행상태코드.입고완료 => "판매 채널에 등록할 상품 정보를 준비할 차례입니다.",
            공동구매커머스이행상태코드.출품준비 => "판매 채널 출품 결과와 외부 상품번호를 확인할 차례입니다.",
            공동구매커머스이행상태코드.판매채널출품완료 => "주문별 피킹·포장·출고 배치를 준비할 차례입니다.",
            공동구매커머스이행상태코드.출고배치준비 => "출고 배치가 준비되었습니다. 후속 창고·배송 원장에서 진행 상태를 확인하세요.",
            공동구매커머스이행상태코드.보류 => "운영자가 보류 사유를 해소하고 이행을 재개해야 합니다.",
            _ => "현재 상태의 세부 업무는 연결된 원장에서 확인해 주세요."
        };
}

public sealed class 공동구매실행기능ViewModel : 조립ViewModelBase
{
    public 공동구매실행기능ViewModel(
        공동구매실행상태ViewModel 상태,
        공동구매자동집단ViewModel 자동집단,
        공동구매재고배분ViewModel 재고배분,
        공동구매주문원장ViewModel 주문원장,
        공동구매커머스이행ViewModel 커머스이행,
        공동구매창고기능ViewModel 창고,
        공동구매자동집단조회ViewModel 자동집단조회,
        공동구매자동수요등록ViewModel 자동수요등록,
        공동구매주문원장상세조회ViewModel 주문원장상세조회,
        공동구매주문하위원장연결ViewModel 주문하위원장연결,
        공동구매주문하위원장분리ViewModel 주문하위원장분리,
        공동구매주문서명상태조회ViewModel 주문서명상태조회,
        공동구매주문서명준비ViewModel 주문서명준비,
        공동구매주문서명등록ViewModel 주문서명등록,
        공동구매커머스이행조회ViewModel 커머스이행조회,
        공동구매커머스문서조회ViewModel 커머스문서조회)
    {
        // Scoped 공유 상태의 수명은 DI scope가 관리합니다. 이 transient 조립 객체가 먼저 폐기하지 않습니다.
        this.상태 = 상태;
        this.자동집단 = 하위ViewModel등록(자동집단, 수명소유: false);
        this.재고배분 = 하위ViewModel등록(재고배분);
        this.주문원장 = 하위ViewModel등록(주문원장);
        this.커머스이행 = 하위ViewModel등록(커머스이행, 수명소유: false);
        this.창고 = 하위ViewModel등록(창고);
        this.창고.출고원장.재고배분연결(this.재고배분);
        세부업무목록 =
        [
            하위ViewModel등록(자동집단조회, 수명소유: false),
            하위ViewModel등록(자동수요등록, 수명소유: false),
            하위ViewModel등록(주문원장상세조회, 수명소유: false),
            하위ViewModel등록(주문하위원장연결, 수명소유: false),
            하위ViewModel등록(주문하위원장분리, 수명소유: false),
            하위ViewModel등록(주문서명상태조회, 수명소유: false),
            하위ViewModel등록(주문서명준비, 수명소유: false),
            하위ViewModel등록(주문서명등록, 수명소유: false),
            하위ViewModel등록(커머스이행조회, 수명소유: false),
            하위ViewModel등록(커머스문서조회, 수명소유: false),
            .. this.창고.입고원장.세부업무목록,
            .. this.창고.출고원장.세부업무목록
        ];
    }

    public 공동구매실행상태ViewModel 상태 { get; }
    public 공동구매자동집단ViewModel 자동집단 { get; }
    public 공동구매주문집계ViewModel 주문집계 => 재고배분.주문집계;
    public 공동구매재고배분ViewModel 재고배분 { get; }
    public 공동구매주문원장ViewModel 주문원장 { get; }
    public 공동구매커머스이행ViewModel 커머스이행 { get; }
    public 공동구매창고기능ViewModel 창고 { get; }
    public 공동구매입고원장ViewModel 입고원장 => 창고.입고원장;
    public 공동구매출고원장ViewModel 출고원장 => 창고.출고원장;
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }
    public bool 처리중 => 자동집단.처리중 || 주문원장.처리중 || 커머스이행.처리중 || 창고.처리중;
}
