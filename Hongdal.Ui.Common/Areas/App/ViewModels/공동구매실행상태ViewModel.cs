using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 공동구매 확정 이후의 자동집단, 주문원장, 커머스 이행 화면이 공유하는 실행 식별자와 조회 결과입니다.
/// 커뮤니티 투표 원장과 주문 루트 원장은 의미가 다르므로 별도 값으로 관리합니다.
/// </summary>
public sealed class 공동구매실행상태ViewModel : ObservableObject, IDisposable
{
    private readonly 공동구매화면상태ViewModel _화면상태;
    private Guid? _원본공동구매Id;
    private string? _원본커뮤니티원장Id;
    private string? _실행공동구매Id;
    private 공동구매자동집단응답? _선택된자동집단;
    private string? _공동구매주문집계원장Id;
    private string? _선택된주문원장Id;
    private 주문원장통합공개Dto? _주문원장통합결과;
    private 주문원장역할별조회공개Dto? _주문원장역할결과;
    private 주문원장서명상태공개Dto? _주문원장서명상태;
    private 공동구매커머스이행계획공개Dto? _선택된커머스이행;

    public 공동구매실행상태ViewModel(공동구매화면상태ViewModel 화면상태)
    {
        _화면상태 = 화면상태;
        _화면상태.PropertyChanged += 화면상태변경;
        원본공동구매동기화();
    }

    public Guid? 원본공동구매Id
    {
        get => _원본공동구매Id;
        private set => SetProperty(ref _원본공동구매Id, value);
    }

    public string? 원본커뮤니티원장Id
    {
        get => _원본커뮤니티원장Id;
        private set => SetProperty(ref _원본커뮤니티원장Id, Clean(value));
    }

    /// <summary>자동집단 API와 커머스 이행 API를 연결하는 공동구매 실행 ID입니다.</summary>
    public string? 실행공동구매Id
    {
        get => _실행공동구매Id;
        private set => SetProperty(ref _실행공동구매Id, Clean(value));
    }

    public 공동구매자동집단응답? 선택된자동집단
    {
        get => _선택된자동집단;
        private set => SetProperty(ref _선택된자동집단, value);
    }

    /// <summary>공동구매 화면 내부에서 확정된 개별 주문들을 집계하는 하위 원장 ID입니다.</summary>
    public string? 공동구매주문집계원장Id
    {
        get => _공동구매주문집계원장Id;
        private set => SetProperty(ref _공동구매주문집계원장Id, Clean(value));
    }

    /// <summary>발주·원장 생성 단계에서 발급된 주문 루트 원장 ID입니다.</summary>
    public string? 선택된주문원장Id
    {
        get => _선택된주문원장Id;
        private set => SetProperty(ref _선택된주문원장Id, Clean(value));
    }

    public 주문원장통합공개Dto? 주문원장통합결과
    {
        get => _주문원장통합결과;
        private set => SetProperty(ref _주문원장통합결과, value);
    }

    public 주문원장역할별조회공개Dto? 주문원장역할결과
    {
        get => _주문원장역할결과;
        private set => SetProperty(ref _주문원장역할결과, value);
    }

    public 주문원장서명상태공개Dto? 주문원장서명상태
    {
        get => _주문원장서명상태;
        private set => SetProperty(ref _주문원장서명상태, value);
    }

    public 공동구매커머스이행계획공개Dto? 선택된커머스이행
    {
        get => _선택된커머스이행;
        private set => SetProperty(ref _선택된커머스이행, value);
    }

    public void 자동집단적용(공동구매자동집단응답 group)
    {
        ArgumentNullException.ThrowIfNull(group);
        선택된자동집단 = group;
        실행공동구매Id = group.자동집단Id;
        공동구매주문집계원장Id = group.공동구매주문집계원장Id;
        선택된커머스이행 = null;
    }

    public void 실행공동구매선택(string? groupPurchaseId)
    {
        var normalized = Clean(groupPurchaseId);
        if (!string.Equals(실행공동구매Id, normalized, StringComparison.Ordinal))
        {
            선택된자동집단 = null;
            공동구매주문집계원장Id = null;
            선택된커머스이행 = null;
        }

        실행공동구매Id = normalized;
    }

    public void 주문집계선택(string? aggregationLedgerId)
        => 공동구매주문집계원장Id = Clean(aggregationLedgerId);

    public void 주문원장선택(string? orderLedgerId)
    {
        var normalized = Clean(orderLedgerId);
        if (string.Equals(선택된주문원장Id, normalized, StringComparison.Ordinal))
        {
            return;
        }

        선택된주문원장Id = normalized;
        주문원장통합결과 = null;
        주문원장역할결과 = null;
        주문원장서명상태 = null;
    }

    public void 주문원장통합적용(주문원장통합공개Dto result)
    {
        ArgumentNullException.ThrowIfNull(result);
        주문원장통합결과 = result;
        주문원장역할결과 = null;
        주문원장서명상태 = result.주문자서명상태 ?? 주문원장서명상태;
    }

    public void 주문원장역할적용(주문원장역할별조회공개Dto result)
    {
        ArgumentNullException.ThrowIfNull(result);
        주문원장역할결과 = result;
        주문원장통합결과 = null;
    }

    public void 주문원장서명적용(주문원장서명상태공개Dto result)
    {
        ArgumentNullException.ThrowIfNull(result);
        주문원장서명상태 = result;
    }

    public void 커머스이행적용(공동구매커머스이행계획공개Dto? plan)
    {
        if (plan is not null
            && !string.Equals(실행공동구매Id, plan.공동구매Id, StringComparison.Ordinal))
        {
            선택된자동집단 = null;
            실행공동구매Id = plan.공동구매Id;
        }

        선택된커머스이행 = plan;
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(공동구매화면상태ViewModel.선택된공동구매)
                or nameof(공동구매화면상태ViewModel.선택된공동구매Id))
        {
            원본공동구매동기화();
        }
    }

    private void 원본공동구매동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        var changed = 원본공동구매Id != campaign?.Id;
        원본공동구매Id = campaign?.Id;
        원본커뮤니티원장Id = campaign?.CommunityLedgerId;

        if (!changed)
        {
            return;
        }

        실행공동구매Id = null;
        선택된자동집단 = null;
        공동구매주문집계원장Id = null;
        선택된주문원장Id = null;
        주문원장통합결과 = null;
        주문원장역할결과 = null;
        주문원장서명상태 = null;
        선택된커머스이행 = null;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
