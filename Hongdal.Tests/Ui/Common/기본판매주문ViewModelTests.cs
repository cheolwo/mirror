using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Sales;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class 기본판매주문ViewModelTests
{
    [Fact]
    public void 판매와주문은업무단위계층으로구성된다()
    {
        Assert.True(typeof(판매업무ViewModelBase).IsAssignableFrom(typeof(판매채널계정ViewModel)));
        Assert.True(typeof(판매업무ViewModelBase).IsAssignableFrom(typeof(상품등록ViewModel)));
        Assert.True(typeof(판매업무ViewModelBase).IsAssignableFrom(typeof(채널출품ViewModel)));
        Assert.True(typeof(주문업무ViewModelBase).IsAssignableFrom(typeof(주문조회ViewModel)));
        Assert.True(typeof(주문업무ViewModelBase).IsAssignableFrom(typeof(주문하위원장ViewModel)));
        Assert.True(typeof(주문업무ViewModelBase).IsAssignableFrom(typeof(주문서명ViewModel)));

        Assert.Contains(
            typeof(판매ViewModel),
            typeof(국내판매ViewModel).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Contains(
            typeof(판매ViewModel),
            typeof(해외수출ViewModel).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Contains(
            typeof(주문ViewModel),
            typeof(공동구매주문원장ViewModel).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public async Task 기본판매는계정상품출품을같은상태로조립한다()
    {
        var service = new Fake판매Service();
        var state = new 판매업무상태ViewModel();
        var account = new 판매채널계정ViewModel(service, state);
        var product = new 상품등록ViewModel(service, state);
        var listing = new 채널출품ViewModel(service, state);
        var accountQuery = new 판매채널계정조회ViewModel(account);
        var accountCreate = new 판매채널계정등록ViewModel(account);
        var accountUpdate = new 판매채널계정수정ViewModel(service, state);
        var accountDelete = new 판매채널계정삭제ViewModel(service, state);
        var productQuery = new 판매상품조회ViewModel(product);
        var productCreate = new 판매상품등록ViewModel(product);
        var productUpdate = new 판매상품수정ViewModel(service, state);
        var productDelete = new 판매상품삭제ViewModel(service, state);
        var listingQuery = new 채널출품조회ViewModel(listing);
        var listingCreate = new 채널출품등록ViewModel(listing);
        var listingUpdate = new 채널출품수정ViewModel(service, state);
        var listingDelete = new 채널출품삭제ViewModel(service, state);
        var accountCrud = new 판매채널계정CrudViewModel(accountQuery, accountCreate, accountUpdate, accountDelete);
        var productCrud = new 판매상품CrudViewModel(productQuery, productCreate, productUpdate, productDelete);
        var listingCrud = new 채널출품CrudViewModel(listingQuery, listingCreate, listingUpdate, listingDelete);
        using var sales = new 판매ViewModel(
            state,
            account,
            product,
            listing,
            accountCrud,
            productCrud,
            listingCrud);

        sales.지원채널설정(["domestic"]);
        Assert.Same(account.초안, accountCreate.초안);
        accountCreate.초안.채널종류 = "domestic";
        accountCreate.초안.상점명 = "국내 상점";
        Assert.True(await accountCreate.실행Async());

        Assert.Same(product.초안, productCreate.초안);
        productCreate.입고상품연결(10, "사과", "APPLE-01");
        productCreate.초안.판매가 = 12000m;
        Assert.True(await productCreate.실행Async());

        Assert.Same(listing.초안, listingCreate.초안);
        Assert.True(listingCreate.계정선택(state.선택된계정!.Id));
        Assert.True(listingCreate.상품선택(state.선택된상품!.Id));
        Assert.True(await listingCreate.실행Async());

        Assert.Single(sales.계정.계정목록);
        Assert.Single(sales.상품등록.상품목록);
        Assert.Single(sales.출품.출품목록);
        Assert.NotNull(state.선택된출품);
        Assert.Equal(12, sales.세부업무목록.Count);
        Assert.Equal(3, sales.Crud업무단위목록.Count);
        Assert.All(sales.Crud업무단위목록, unit => Assert.Equal(4, unit.Crud업무목록.Count));
        Assert.All(sales.세부업무목록, item => Assert.False(string.IsNullOrWhiteSpace(item.업무코드)));
        Assert.IsAssignableFrom<I목록조회ViewModel<판매상품항목응답>>(sales.상품조회);
        Assert.IsAssignableFrom<I등록ViewModel<채널출품저장요청>>(sales.출품등록);
        Assert.IsAssignableFrom<I수정ViewModel<채널출품저장요청>>(sales.출품수정);
        Assert.IsAssignableFrom<I삭제ViewModel<long>>(sales.출품삭제);

        Assert.True(sales.계정수정.선택항목적용());
        sales.계정수정.초안.상점명 = "수정된 국내 상점";
        Assert.True(await sales.계정수정.실행Async());
        Assert.Equal("수정된 국내 상점", state.선택된계정?.상점명);

        Assert.True(sales.상품수정.선택항목적용());
        sales.상품수정.초안.판매가 = 13500m;
        Assert.True(await sales.상품수정.실행Async());
        Assert.Equal(13500m, state.선택된상품?.판매가);

        Assert.True(sales.출품수정.선택항목적용());
        Assert.True(await sales.출품수정.실행Async());
        Assert.True(await sales.출품삭제.실행Async());
        Assert.True(await sales.상품삭제.실행Async());
        Assert.True(await sales.계정삭제.실행Async());
        Assert.Empty(state.출품목록);
        Assert.Empty(state.상품목록);
        Assert.Empty(state.계정목록);
    }

    [Fact]
    public async Task 기본주문은선택한원장을조회하고하위원장을연결한다()
    {
        var service = new Fake주문Service();
        var state = new 주문업무상태ViewModel();
        var query = new 주문조회ViewModel(service, state);
        var child = new 주문하위원장ViewModel(service, state);
        var signature = new 주문서명ViewModel(service, state);
        var childQuery = new 주문하위원장조회ViewModel(service, state);
        var childConnect = new 주문하위원장연결ViewModel(service, state);
        var childUpdate = new 주문하위원장수정ViewModel(service, state);
        var childDetach = new 주문하위원장분리ViewModel(service, state);
        var childCrud = new 주문하위원장관계CrudViewModel(
            childQuery,
            childConnect,
            childUpdate,
            childDetach);
        var signatureQuery = new 주문서명상태조회ViewModel(service, state);
        var signaturePrepare = new 주문서명준비ViewModel(service, state);
        var signatureCreate = new 주문서명등록ViewModel(service, state);
        using var order = new 주문ViewModel(
            state,
            query,
            child,
            signature,
            childCrud,
            signatureQuery,
            signaturePrepare,
            signatureCreate);

        order.조회.주문원장선택("order-root-1");
        Assert.True(await order.조회.조회Async());

        order.하위원장연결.초안.하위원장Id = "sales-ledger-1";
        order.하위원장연결.초안.역할 = 주문원장포함역할.판매;
        Assert.True(await order.하위원장연결.실행Async());

        Assert.Equal("order-root-1", state.선택된주문원장Id);
        Assert.Equal("orderer", state.역할별결과?.조회역할);
        Assert.Equal(2, state.현재원장Revision);
        Assert.Equal(8, order.세부업무목록.Count);
        Assert.Same(order.하위원장관계Crud, Assert.Single(order.Crud업무단위목록));
        Assert.IsAssignableFrom<I등록ViewModel<주문하위원장연결ClientRequest>>(order.하위원장연결);
        Assert.IsAssignableFrom<I수정ViewModel<주문하위원장연결ClientRequest>>(order.하위원장수정);
        Assert.IsAssignableFrom<I삭제ViewModel<주문하위원장분리초안>>(order.하위원장분리);

        Assert.True(order.하위원장수정.선택항목적용());
        order.하위원장수정.초안.역할 = 주문원장포함역할.창고입고;
        Assert.True(await order.하위원장수정.실행Async());
        Assert.Equal(주문원장포함역할.창고입고, state.선택된하위원장?.역할);

        Assert.True(order.하위원장분리.선택항목적용());
        Assert.True(await order.하위원장분리.실행Async());
        Assert.Empty(state.하위원장목록);
    }

    [Fact]
    public async Task 주문원장과서명은서로다른Revision을사용한다()
    {
        var service = new Fake주문Service();
        var state = new 주문업무상태ViewModel();
        state.주문원장선택("order-root-1");
        state.역할별결과적용(new 주문원장역할별조회공개Dto
        {
            주문원장Id = "order-root-1",
            주문원장상세 = new 주문원장원장요약Dto
            {
                원장Id = "order-root-1",
                Revision = 8
            }
        });
        state.서명상태적용(new 주문원장서명상태공개Dto
        {
            주문원장Id = "order-root-1",
            Revision = 3
        });
        var childConnect = new 주문하위원장연결ViewModel(service, state);
        var signaturePrepare = new 주문서명준비ViewModel(service, state);

        childConnect.초안.하위원장Id = "sales-ledger-1";
        childConnect.초안.역할 = 주문원장포함역할.판매;
        signaturePrepare.초안.계약문서번호 = "contract-1";
        signaturePrepare.초안.문서Hash = "document-hash";

        Assert.True(await childConnect.실행Async());
        Assert.True(await signaturePrepare.실행Async());
        Assert.Equal(8, service.마지막하위원장기대Revision);
        Assert.Equal(3, service.마지막서명기대Revision);
    }

    private sealed class Fake판매Service : I판매채널Client
    {
        private long _id;

        public Task<IReadOnlyList<판매채널계정항목응답>> 계정목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<판매채널계정항목응답>>([]);

        public Task<판매채널계정항목응답?> 계정생성Async(
            판매채널계정저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<판매채널계정항목응답?>(new()
            {
                Id = ++_id,
                채널종류 = request.채널종류,
                상점명 = request.상점명
            });

        public Task<판매채널계정항목응답?> 계정수정Async(
            long accountId,
            판매채널계정저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<판매채널계정항목응답?>(new()
            {
                Id = accountId,
                채널종류 = request.채널종류,
                상점명 = request.상점명
            });

        public Task 계정삭제Async(long accountId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<판매상품항목응답>> 상품목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<판매상품항목응답>>([]);

        public Task<판매상품항목응답?> 상품생성Async(
            판매상품저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<판매상품항목응답?>(new()
            {
                Id = ++_id,
                입고상품Id = request.입고상품Id,
                대표상품명 = request.대표상품명,
                판매SKU = request.판매SKU,
                판매가 = request.판매가
            });

        public Task<판매상품항목응답?> 상품수정Async(
            long productId,
            판매상품저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<판매상품항목응답?>(new()
            {
                Id = productId,
                입고상품Id = request.입고상품Id,
                대표상품명 = request.대표상품명,
                판매SKU = request.판매SKU,
                판매가 = request.판매가,
                샘플데이터여부 = request.샘플데이터여부,
                샘플데이터코드 = request.샘플데이터코드
            });

        public Task 상품삭제Async(long productId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<채널출품항목응답>> 출품목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<채널출품항목응답>>([]);

        public Task<채널출품항목응답?> 출품생성Async(
            채널출품저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<채널출품항목응답?>(new()
            {
                Id = ++_id,
                판매상품Id = request.판매상품Id,
                판매채널계정Id = request.판매채널계정Id
            });

        public Task<채널출품항목응답?> 출품수정Async(
            long listingId,
            채널출품저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<채널출품항목응답?>(new()
            {
                Id = listingId,
                판매상품Id = request.판매상품Id,
                판매채널계정Id = request.판매채널계정Id
            });

        public Task 출품삭제Async(long listingId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class Fake주문Service : I주문원장Service
    {
        private readonly List<주문원장포함원장참조Dto> _children = [];
        private long _revision = 1;

        public long? 마지막하위원장기대Revision { get; private set; }
        public long? 마지막서명기대Revision { get; private set; }

        public Task<주문원장역할별조회공개Dto?> 주문원장보호조회Async(
            string orderLedgerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<주문원장역할별조회공개Dto?>(new()
            {
                주문원장Id = orderLedgerId,
                조회역할 = "orderer",
                주문원장상세 = new 주문원장원장요약Dto
                {
                    원장Id = orderLedgerId,
                    Revision = _revision,
                    포함원장목록 = _children.ToArray()
                }
            });

        public Task<주문원장역할별조회공개Dto?> 주문원장역할조회Async(
            string orderLedgerId,
            string viewCode,
            CancellationToken cancellationToken = default)
            => 주문원장보호조회Async(orderLedgerId, cancellationToken);

        public Task<주문원장통합공개Dto?> 하위원장연결Async(
            string orderLedgerId,
            주문하위원장연결ClientRequest request,
            CancellationToken cancellationToken = default)
        {
            마지막하위원장기대Revision = request.기대Revision;
            var existing = _children.FindIndex(x => x.원장Id == request.하위원장Id);
            var item = new 주문원장포함원장참조Dto
            {
                원장Id = request.하위원장Id,
                역할 = request.역할,
                필수여부 = request.필수여부,
                표시순서 = request.표시순서 ?? (existing >= 0 ? _children[existing].표시순서 : _children.Count)
            };
            if (existing >= 0)
            {
                _children[existing] = item;
            }
            else
            {
                _children.Add(item);
            }

            _revision++;
            return Task.FromResult<주문원장통합공개Dto?>(Result(orderLedgerId));
        }

        public Task<주문원장통합공개Dto?> 하위원장분리Async(
            string orderLedgerId,
            string childLedgerId,
            long? expectedRevision = null,
            CancellationToken cancellationToken = default)
        {
            _children.RemoveAll(x => x.원장Id == childLedgerId);
            _revision++;
            return Task.FromResult<주문원장통합공개Dto?>(Result(orderLedgerId));
        }

        public Task<주문원장서명상태공개Dto?> 주문원장서명상태조회Async(
            string orderLedgerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<주문원장서명상태공개Dto?>(new() { 주문원장Id = orderLedgerId, Revision = 2 });

        public Task<주문원장서명상태공개Dto?> 주문원장서명준비Async(
            string orderLedgerId,
            주문원장서명준비ClientRequest request,
            CancellationToken cancellationToken = default)
        {
            마지막서명기대Revision = request.기대Revision;
            return 주문원장서명상태조회Async(orderLedgerId, cancellationToken);
        }

        public Task<주문원장서명상태공개Dto?> 주문원장서명등록Async(
            string orderLedgerId,
            주문원장서명등록ClientRequest request,
            CancellationToken cancellationToken = default)
            => 주문원장서명상태조회Async(orderLedgerId, cancellationToken);

        private 주문원장통합공개Dto Result(string orderLedgerId)
            => new()
            {
                주문원장 = new 주문원장원장요약Dto
                {
                    원장Id = orderLedgerId,
                    Revision = _revision,
                    포함원장목록 = _children.ToArray()
                }
            };
    }
}
