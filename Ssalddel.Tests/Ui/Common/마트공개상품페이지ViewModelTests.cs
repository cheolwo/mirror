using Ssalddel.Contracts.Mart;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 마트공개상품페이지ViewModelTests
{
    [Fact]
    public async Task 목록은_검색과판매가능조건및페이지를서버요청에전달한다()
    {
        var service = new FakeMartProductService
        {
            ListResponse = new 마트공개상품목록응답
            {
                TotalCount = 13,
                Page = 2,
                PageSize = 12,
                Items = [new 마트공개상품요약응답 { Id = 4, 상품명 = "생수" }]
            }
        };
        var viewModel = new 마트공개상품목록ViewModel(service)
        {
            검색어 = "생수",
            판매가능만 = true
        };

        Assert.True(await viewModel.조회Async());
        Assert.True(await viewModel.페이지조회Async(2));

        Assert.Equal("생수", service.LastRequest!.검색어);
        Assert.True(service.LastRequest.판매가능만);
        Assert.Equal(2, service.LastRequest.Page);
        Assert.Equal(13, viewModel.전체건수);
        Assert.Equal(2, viewModel.총페이지수);
    }

    [Fact]
    public void 화면검토자료는_서버요청없이명시적으로목록에적용된다()
    {
        var service = new FakeMartProductService();
        var viewModel = new 마트공개상품목록ViewModel(service);

        viewModel.화면검토자료적용(new 마트공개상품목록응답
        {
            Items = [new 마트공개상품요약응답 { Id = 91_001, 상품명 = "화면 검토용 감자" }],
            TotalCount = 1,
            Page = 1,
            PageSize = 12,
            재고기준안내 = "실제 주문 자료가 아닙니다."
        });

        Assert.True(viewModel.초기화됨);
        Assert.Equal("화면 검토용 감자", Assert.Single(viewModel.상품목록).상품명);
        Assert.Null(service.LastRequest);
    }

    [Fact]
    public async Task 정확한상세가없어도다른상품이나첫상품으로대체하지않는다()
    {
        var viewModel = new 마트공개상품상세ViewModel(
            new FakeMartProductService { DetailResponse = null });

        var succeeded = await viewModel.조회Async(41);

        Assert.True(succeeded);
        Assert.Equal(41, viewModel.요청ProductId);
        Assert.True(viewModel.찾을수없음);
        Assert.Null(viewModel.상세);
    }

    [Fact]
    public async Task 접근ViewModel은_마트기능비활성을별도상태로유지한다()
    {
        var viewModel = new 마트페이지접근ViewModel(new FakeAccessService(false));

        var succeeded = await viewModel.확인Async();

        Assert.True(succeeded);
        Assert.True(viewModel.기능비활성);
        Assert.False(viewModel.사용가능);
    }

    [Fact]
    public async Task 후기ViewModel은_로그인사용자의명시적입력을완료원장후기요청으로전달한다()
    {
        var service = new FakeReviewService();
        var viewModel = new 마트공개상품후기작성ViewModel(
            service,
            new FakeCurrentUserContext(new 현재사용자Snapshot("buyer-1", "감자 구매자", ["Orderer"])));
        viewModel.준비(41, "제철 감자");
        viewModel.글비밀번호 = "safe-password";
        viewModel.본문 = "상품 상태와 공동구매 경험이 좋았습니다.";

        var succeeded = await viewModel.작성Async();

        Assert.True(succeeded);
        Assert.Equal(41, service.LastProductId);
        Assert.NotNull(service.LastRequest);
        Assert.Equal("감자 구매자", service.LastRequest.작성자표시명);
        Assert.Equal("제철 감자 구매 후기", service.LastRequest.제목);
        Assert.Equal("상품 상태와 공동구매 경험이 좋았습니다.", service.LastRequest.본문);
        Assert.Equal(string.Empty, viewModel.글비밀번호);
        Assert.Equal(string.Empty, viewModel.본문);
        Assert.True(viewModel.성공함);
    }

    [Fact]
    public async Task 후기ViewModel은_익명사용자에게서버요청을보내지않는다()
    {
        var service = new FakeReviewService();
        var viewModel = new 마트공개상품후기작성ViewModel(
            service,
            new FakeCurrentUserContext(현재사용자Snapshot.익명));
        viewModel.준비(41, "제철 감자");
        viewModel.작성자표시명 = "익명";
        viewModel.글비밀번호 = "1234";
        viewModel.본문 = "완료 후기";

        var succeeded = await viewModel.작성Async();

        Assert.False(succeeded);
        Assert.Null(service.LastRequest);
        Assert.Contains("로그인", viewModel.오류메시지);
    }

    [Fact]
    public async Task 후기PageViewModel은_작성성공뒤목록이아니라같은상품Id만다시조회한다()
    {
        var productService = new FakeMartProductService
        {
            DetailResponse = new 마트공개상품상세응답
            {
                Id = 41,
                상품명 = "제철 감자"
            }
        };
        var reviewService = new FakeReviewService();
        var viewModel = new 마트공개상품후기PageViewModel(
            new 마트공개상품상세ViewModel(productService),
            new 마트공개상품후기작성ViewModel(
                reviewService,
                new FakeCurrentUserContext(new 현재사용자Snapshot("buyer-1", "감자 구매자", ["Orderer"]))));

        Assert.True(await viewModel.상세조회Async(41));
        viewModel.후기.글비밀번호 = "safe-password";
        viewModel.후기.본문 = "공개 가능한 구매 후기입니다.";

        var succeeded = await viewModel.작성후같은상품재조회Async();

        Assert.True(succeeded);
        Assert.Equal([41L, 41L], productService.DetailProductIds);
        Assert.Null(productService.LastRequest);
        Assert.Equal(41, viewModel.상세.요청ProductId);
    }

    private sealed class FakeAccessService(bool enabled) : I마트페이지접근Service
    {
        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default)
            => Task.FromResult(enabled);
    }

    private sealed class FakeMartProductService : I마트공개상품읽기Service
    {
        public 마트공개상품목록응답 ListResponse { get; init; } = new();
        public 마트공개상품상세응답? DetailResponse { get; init; }
        public 마트공개상품목록조회요청? LastRequest { get; private set; }
        public List<long> DetailProductIds { get; } = [];

        public Task<마트공개상품목록응답> 목록Async(
            마트공개상품목록조회요청 request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(ListResponse);
        }

        public Task<마트공개상품상세응답?> 상세Async(
            long productId,
            CancellationToken cancellationToken = default)
        {
            DetailProductIds.Add(productId);
            return Task.FromResult(DetailResponse);
        }
    }

    private sealed class FakeReviewService : I마트공개상품후기작성Service
    {
        public long? LastProductId { get; private set; }
        public 마트공개상품구매후기작성요청? LastRequest { get; private set; }

        public Task<마트공개상품구매후기응답> 작성Async(
            long productId,
            마트공개상품구매후기작성요청 request,
            CancellationToken cancellationToken = default)
        {
            LastProductId = productId;
            LastRequest = request;
            return Task.FromResult(new 마트공개상품구매후기응답
            {
                게시글Id = 93,
                제목 = request.제목,
                본문요약 = request.본문,
                작성자표시명 = request.작성자표시명,
                작성시각Utc = new DateTime(2026, 7, 21, 9, 0, 0, DateTimeKind.Utc)
            });
        }
    }

    private sealed class FakeCurrentUserContext(현재사용자Snapshot user)
        : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot 현재사용자 { get; } = user;
    }
}
