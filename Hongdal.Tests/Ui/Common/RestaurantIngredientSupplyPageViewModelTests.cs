using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class RestaurantIngredientSupplyPageViewModelTests
{
    [Fact]
    public async Task 초기화하면_국내산지후보와기존요청을_하위ViewModel에조립한다()
    {
        var service = new FakeIngredientSupplyService();
        using var sut = Create(service);

        var initialized = await sut.초기화Async();

        Assert.True(initialized);
        Assert.Equal(PageViewModel상태.준비됨, sut.상태);
        Assert.Equal(음식점식재료공급경로.국내산지, sut.작성.공급경로);
        Assert.All(sut.비교.후보목록, candidate =>
            Assert.Equal(음식점식재료공급경로.국내산지, candidate.공급경로));
        Assert.Single(sut.진행.요청목록);
        Assert.True(sut.SimulationMode);
    }

    [Fact]
    public async Task 공급경로를전환해도_국내와수입입력값은_각각보존한다()
    {
        var service = new FakeIngredientSupplyService();
        using var sut = Create(service);
        await sut.초기화Async();
        sut.작성.국내산지초안.품목명 = "감자";

        await sut.공급경로선택Async(음식점식재료공급경로.공동수입);
        sut.작성.공동수입초안.품목명 = "냉동 브로콜리";
        await sut.공급경로선택Async(음식점식재료공급경로.국내산지);

        Assert.Equal("감자", sut.작성.현재초안.품목명);
        Assert.All(sut.비교.후보목록, candidate =>
            Assert.Equal(음식점식재료공급경로.국내산지, candidate.공급경로));

        await sut.공급경로선택Async(음식점식재료공급경로.공동수입);
        Assert.Equal("냉동 브로콜리", sut.작성.현재초안.품목명);
    }

    [Fact]
    public async Task 예상도착단가가낮아도_후보를자동선택하지않는다()
    {
        using var sut = Create(new FakeIngredientSupplyService());

        await sut.초기화Async();

        Assert.NotNull(sut.비교.예상도착단가최저후보);
        Assert.Null(sut.비교.선택후보Id);
        Assert.Null(sut.비교.선택후보);
    }

    [Fact]
    public async Task 활성경로를다시선택해도_선택한공급후보를유지한다()
    {
        using var sut = Create(new FakeIngredientSupplyService());
        await sut.초기화Async();
        var candidate = sut.비교.후보목록[0];
        sut.공급후보선택(candidate.후보Id);

        var changed = await sut.공급경로선택Async(음식점식재료공급경로.국내산지);

        Assert.False(changed);
        Assert.Equal(candidate.후보Id, sut.비교.선택후보Id);
    }

    [Fact]
    public async Task 현재공급조건을초기화하면_현재경로초안만복원하고_후보를비운다()
    {
        using var sut = Create(new FakeIngredientSupplyService());
        await sut.초기화Async();
        sut.작성.국내산지초안.품목명 = "변경한 양파";
        sut.작성.공동수입초안.품목명 = "수입 초안 유지";

        sut.현재공급조건초기화();

        Assert.Equal("양파", sut.작성.국내산지초안.품목명);
        Assert.Equal("수입 초안 유지", sut.작성.공동수입초안.품목명);
        Assert.Empty(sut.비교.후보목록);
        Assert.Contains("기본값", sut.메시지);
    }

    [Fact]
    public async Task 공급경로후보조회가실패하면_이전경로와후보를유지한다()
    {
        var service = new FakeIngredientSupplyService();
        using var sut = Create(service);
        await sut.초기화Async();
        var existingCandidateIds = sut.비교.후보목록.Select(candidate => candidate.후보Id).ToArray();
        service.FailCandidateLookups = true;

        var changed = await sut.공급경로선택Async(음식점식재료공급경로.공동수입);

        Assert.False(changed);
        Assert.Equal(음식점식재료공급경로.국내산지, sut.작성.공급경로);
        Assert.Equal(existingCandidateIds, sut.비교.후보목록.Select(candidate => candidate.후보Id));
        Assert.Equal(음식점식재료공급메시지종류.오류, sut.메시지종류);
        Assert.Contains("조회 실패", sut.메시지);
    }

    [Fact]
    public async Task 사용자가후보를선택하면_희망단가만반영하고_계약을생성하지않는다()
    {
        using var sut = Create(new FakeIngredientSupplyService());
        await sut.초기화Async();
        var candidate = Assert.Single(sut.비교.후보목록.Take(1));

        var selected = sut.공급후보선택(candidate.후보Id);

        Assert.True(selected);
        Assert.Equal(candidate.예상도착단가, sut.작성.현재초안.희망도착단가);
        Assert.Contains("공급 계약은 아직 생성되지 않았습니다", sut.메시지);
        Assert.DoesNotContain(sut.진행.요청목록, request => request.요청.품목명 == sut.작성.현재초안.품목명);
    }

    [Fact]
    public async Task 수입공동공급은_희망원산지범위가없으면_비교와저장을막는다()
    {
        using var sut = Create(new FakeIngredientSupplyService());
        await sut.초기화Async();
        await sut.공급경로선택Async(음식점식재료공급경로.공동수입);
        sut.작성.현재초안.희망원산지 = string.Empty;

        var compared = await sut.조건으로비교Async();
        var saved = await sut.초안저장Async();

        Assert.False(compared);
        Assert.False(saved);
        Assert.Contains("희망 원산지", sut.메시지);
    }

    [Fact]
    public async Task 유효한요청은_선택후보와함께_운영효력없는초안으로저장한다()
    {
        var service = new FakeIngredientSupplyService();
        using var sut = Create(service);
        await sut.초기화Async();
        var candidate = sut.비교.후보목록[0];
        sut.공급후보선택(candidate.후보Id);

        var saved = await sut.초안저장Async();

        Assert.True(saved);
        var latest = sut.진행.요청목록.First();
        Assert.Equal(음식점식재료공급요청상태.초안, latest.상태);
        Assert.Equal(candidate.후보Id, latest.선택후보Id);
        Assert.True(latest.운영효력없음);
        Assert.Contains("운영 효력", sut.메시지);
    }

    [Fact]
    public async Task 후보조회는_품목단가와부대비용을합친_예상도착단가를제공한다()
    {
        using var sut = Create(new FakeIngredientSupplyService());
        await sut.초기화Async();

        var candidate = sut.비교.후보목록[0];

        Assert.Equal(
            candidate.품목단가 + candidate.물류작업단가 + candidate.수입부대비용단가,
            candidate.예상도착단가);
        Assert.True(candidate.예상절감률 > 0);
        Assert.True(candidate.직접조건수락필수);
        Assert.True(candidate.운영효력없음);
    }

    private static 음식점식재료공급요청PageViewModel Create(
        I음식점식재료공급요청Service service)
        => new(
            service,
            new 음식점식재료공급요청작성ViewModel(),
            new 음식점식재료공급비교ViewModel(),
            new 음식점식재료공급진행조회ViewModel());

    private sealed class FakeIngredientSupplyService : I음식점식재료공급요청Service
    {
        private readonly List<음식점식재료공급요청Snapshot> _requests =
        [
            new(
                "TEST-001",
                음식점식재료공급요청상태.수요모으는중,
                "수요 모으는 중",
                new 음식점식재료공급요청Draft
                {
                    공급경로 = 음식점식재료공급경로.국내산지,
                    품목명 = "대파",
                    품목분류 = "농산물",
                    규격 = "1kg 단",
                    필요수량 = 20,
                    희망납품일 = DateTime.Today.AddDays(3),
                    현재구매단가 = 4000,
                    희망도착단가 = 3500,
                    납품지역 = "서울",
                    사용목적 = "조리"
                },
                null,
                null,
                DateTimeOffset.Now.AddDays(-1),
                true)
        ];

        public bool SimulationMode => true;
        public bool FailCandidateLookups { get; set; }

        public Task<IReadOnlyList<음식점식재료공급후보>> 공급후보조회Async(
            음식점식재료공급요청Draft request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailCandidateLookups)
            {
                throw new InvalidOperationException("공급 후보 조회 실패");
            }

            var imported = request.공급경로 == 음식점식재료공급경로.공동수입;
            IReadOnlyList<음식점식재료공급후보> result =
            [
                Candidate("candidate-a", request.공급경로, imported, 1800, 200, imported ? 300 : 0, request.현재구매단가),
                Candidate("candidate-b", request.공급경로, imported, 1900, 250, imported ? 350 : 0, request.현재구매단가)
            ];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<음식점식재료공급요청Snapshot>> 요청목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<음식점식재료공급요청Snapshot>>(_requests.ToArray());

        public Task<음식점식재료공급요청Snapshot> 초안저장Async(
            음식점식재료공급요청Draft request,
            string? selectedCandidateId,
            CancellationToken cancellationToken = default)
        {
            var saved = new 음식점식재료공급요청Snapshot(
                $"TEST-{_requests.Count + 1:000}",
                음식점식재료공급요청상태.초안,
                "Simulation 초안",
                request.복사(),
                selectedCandidateId,
                selectedCandidateId,
                DateTimeOffset.Now,
                true);
            _requests.Add(saved);
            return Task.FromResult(saved);
        }

        private static 음식점식재료공급후보 Candidate(
            string id,
            음식점식재료공급경로 route,
            bool imported,
            decimal product,
            decimal logistics,
            decimal importCost,
            decimal benchmark)
            => new(
                id,
                route,
                imported ? "공동수입" : "국내 산지",
                $"공급 후보 {id}",
                imported ? "해외" : "국내",
                "테스트 품목",
                100,
                "kg",
                3,
                product,
                logistics,
                importCost,
                product + logistics + importCost,
                benchmark > 0 ? benchmark : 5000,
                "KRW",
                DateTime.Today.AddDays(7),
                imported ? "냉동" : "상온",
                "테스트 가격 기준",
                "변동 비용",
                imported ? ["수입자", "관세사"] : ["생산자", "운송인"],
                true,
                true);
    }
}
