using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class 공동구매업무계층ViewModelTests
{
    [Fact]
    public void 영역별기본업무는공동구매원장업무를상속한다()
    {
        Assert.True(typeof(공동구매원장업무ViewModelBase).IsAssignableFrom(typeof(공동구매의사결정업무ViewModelBase)));
        Assert.True(typeof(공동구매원장업무ViewModelBase).IsAssignableFrom(typeof(공동구매모집업무ViewModelBase)));
        Assert.True(typeof(공동구매원장업무ViewModelBase).IsAssignableFrom(typeof(공동구매합의업무ViewModelBase)));
        Assert.True(typeof(공동구매원장업무ViewModelBase).IsAssignableFrom(typeof(공동구매공급업무ViewModelBase)));
        Assert.True(typeof(공동구매원장업무ViewModelBase).IsAssignableFrom(typeof(공동구매물류업무ViewModelBase)));
        Assert.True(typeof(공동구매원장업무ViewModelBase).IsAssignableFrom(typeof(공동구매실행업무ViewModelBase)));
    }

    [Fact]
    public void 구체ViewModel은담당업무영역의자식이다()
    {
        AssertDerivedFrom<공동구매가격의사결정ViewModel, 공동구매의사결정업무ViewModelBase>();
        AssertDerivedFrom<공동구매거래경로분기ViewModel, 공동구매의사결정업무ViewModelBase>();

        AssertDerivedFrom<공동구매목록ViewModel, 공동구매모집업무ViewModelBase>();
        AssertDerivedFrom<공동구매제안ViewModel, 공동구매모집업무ViewModelBase>();
        AssertDerivedFrom<공동구매수요참여ViewModel, 공동구매모집업무ViewModelBase>();
        AssertDerivedFrom<공동구매이의검토ViewModel, 공동구매모집업무ViewModelBase>();

        AssertDerivedFrom<공동구매모집마감ViewModel, 공동구매합의업무ViewModelBase>();
        AssertDerivedFrom<공동구매결의ViewModel, 공동구매합의업무ViewModelBase>();
        AssertDerivedFrom<공동구매전자서명ViewModel, 공동구매합의업무ViewModelBase>();

        AssertDerivedFrom<공동구매생산자연결ViewModel, 공동구매공급업무ViewModelBase>();
        AssertDerivedFrom<공동구매공급제안ViewModel, 공동구매공급업무ViewModelBase>();
        AssertDerivedFrom<공동구매공급적합성ViewModel, 공동구매공급업무ViewModelBase>();
        AssertDerivedFrom<공동구매협상ViewModel, 공동구매공급업무ViewModelBase>();

        AssertDerivedFrom<공동구매이행계획ViewModel, 공동구매물류업무ViewModelBase>();

        AssertDerivedFrom<공동구매자동집단ViewModel, 공동구매실행업무ViewModelBase>();
        AssertDerivedFrom<공동구매주문원장조회ViewModel, 공동구매주문원장실행업무ViewModelBase>();
        AssertDerivedFrom<공동구매하위원장ViewModel, 공동구매주문원장실행업무ViewModelBase>();
        AssertDerivedFrom<공동구매주문원장서명ViewModel, 공동구매주문원장실행업무ViewModelBase>();
        AssertDerivedFrom<공동구매커머스이행ViewModel, 공동구매실행업무ViewModelBase>();
        AssertDerivedFrom<국내판매ViewModel, 공동구매판매실행업무ViewModelBase>();
        AssertDerivedFrom<해외수출ViewModel, 공동구매판매실행업무ViewModelBase>();
    }

    [Fact]
    public void 기본업무는선택된공동구매와원장문맥을제공한다()
    {
        using var state = new 공동구매화면상태ViewModel(new NoopLedgerProgressClient());
        var campaignId = Guid.NewGuid();
        state.선택적용(new CommunityVoteResponse
        {
            Id = campaignId,
            CommunityLedgerId = "group-purchase-ledger-1"
        });
        var viewModel = new Test모집업무ViewModel(state);

        Assert.Equal(공동구매업무영역코드.모집, viewModel.업무영역코드);
        Assert.Equal("모집", viewModel.업무영역명);
        Assert.True(viewModel.대상선택됨);
        Assert.Equal(campaignId, viewModel.선택된공동구매Id);
        Assert.Equal("group-purchase-ledger-1", viewModel.공동구매원장Id);
        Assert.Contains(공동구매절차코드.수요모집, viewModel.관련절차단계코드);
    }

    private static void AssertDerivedFrom<TChild, TParent>()
        => Assert.True(typeof(TParent).IsAssignableFrom(typeof(TChild)));

    private sealed class Test모집업무ViewModel(공동구매화면상태ViewModel state)
        : 공동구매모집업무ViewModelBase(state);

    private sealed class NoopLedgerProgressClient : I공동구매원장절차Client
    {
        public Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
            Guid campaignId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(null);

        public Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
            Guid campaignId,
            CommunityGroupPurchaseLedgerProgressRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(null);
    }
}
