using System.Text.Json;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Tests.Ui.BackOffice;

public sealed class 같이수입준비초안Tests
{
    private const string CanonicalJson = """
        {"\uC694\uCCAD\uBA71\uB4F1\uD0A4":"","\uAE30\uB300Revision":null,"\uC7AC\uB8CC\uD0A4":"legacy","\uC7AC\uB8CC\uBA85":"name","\uC7AC\uB8CC\uD488\uBAA9\uBAA9\uB85D":[{"\uC7AC\uB8CC\uD0A4":"a","\uC7AC\uB8CC\uBA85":"","\uC6D0\uCC9C\uC790\uB3D9\uC9D1\uB2E8Id":"","\uC6D0\uCC9CHs\uCF54\uB4DC":"","\uBAA8\uC778\uC218\uC694\uC218\uB7C9":0,"\uC218\uB7C9\uB2E8\uC704":""},{"\uC7AC\uB8CC\uD0A4":"b","\uC7AC\uB8CC\uBA85":"","\uC6D0\uCC9C\uC790\uB3D9\uC9D1\uB2E8Id":"","\uC6D0\uCC9CHs\uCF54\uB4DC":"","\uBAA8\uC778\uC218\uC694\uC218\uB7C9":0,"\uC218\uB7C9\uB2E8\uC704":""}],"\uCD9C\uBC1C\uAD6D\uAC00\uCF54\uB4DC":"","\uB3C4\uCC29\uAD6D\uAC00\uCF54\uB4DC":"KR","\uAE30\uC900\uD1B5\uD654\uCF54\uB4DC":"KRW","\uD3EC\uC6CC\uB354\uC778\uACC4":null,"\uAD6D\uC81C\uC6B4\uC1A1\uAC80\uD1A0":null,"\uACF5\uAE09\uC790\uADFC\uAC70\uBAA9\uB85D":[],"\uACAC\uC801\uBAA9\uB85D":[],"\uC608\uC0C1\uBE44\uC6A9\uBAA9\uB85D":[],"\uD488\uBAA9\uBD84\uB958\uD6C4\uBCF4\uBAA9\uB85D":[],"\uAD6D\uAC00\uBCC4\uAC80\uD1A0\uD56D\uBAA9\uBAA9\uB85D":[],"\uCC45\uC784\uCD08\uC548\uBAA9\uB85D":[],"\uBBF8\uD655\uC778\uD56D\uBAA9\uBAA9\uB85D":[]}
        """;
    private const string CanonicalHash = "7f6d47b1082e455b96e84fc58fa6aa9de20c6fcd2e9bc5ea57c36e7982444174";

    [Fact]
    public void Codec은_WebJSON의선언순서와키초기화바이트를고정한다()
    {
        var request = FingerprintFixture();
        var clone = 같이수입준비초안Codec.Clone(request);
        Assert.Equal(CanonicalJson, JsonSerializer.Serialize(clone, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(CanonicalHash, 같이수입준비초안Codec.Fingerprint(request));

        request.요청멱등키 = "retry-key";
        request.기대Revision = 42;
        Assert.Equal(CanonicalHash, 같이수입준비초안Codec.Fingerprint(request));
        Assert.Equal("retry-key", request.요청멱등키);
        Assert.Equal(42, request.기대Revision);
        request.재료품목목록.Reverse();
        Assert.NotEqual(CanonicalHash, 같이수입준비초안Codec.Fingerprint(request));
    }

    [Fact]
    public void Clone은_중첩자료와목록을분리하고_원본의키Revision과시각을보존한다()
    {
        var request = 같이수입준비초안Factory.CreateBase(Group());
        request.요청멱등키 = "original";
        request.기대Revision = 7;
        var checkedAt = new DateTimeOffset(2026, 10, 20, 12, 30, 0, TimeSpan.FromHours(9));
        request.예상비용목록.Add(같이수입준비초안Factory.NewCost("c", "Goods", "label", "USD", checkedAt));
        var clone = 같이수입준비초안Codec.Clone(request);
        Assert.NotSame(request, clone);
        Assert.NotSame(request.재료품목목록[0], clone.재료품목목록[0]);
        Assert.NotSame(request.국제운송검토, clone.국제운송검토);
        Assert.NotSame(request.포워더인계.전달항목코드목록, clone.포워더인계.전달항목코드목록);
        Assert.Equal(checkedAt, clone.예상비용목록[0].확인시각Utc);
        Assert.Equal("original", clone.요청멱등키);
        Assert.Equal(7, clone.기대Revision);
        clone.재료품목목록[0].재료명 = "edited";
        clone.국제운송검토.방식후보목록.Clear();
        clone.포워더인계.전달항목코드목록.Clear();
        clone.예상비용목록[0].예상금액 = 99m;
        Assert.Equal("상품", request.재료품목목록[0].재료명);
        Assert.Equal(2, request.국제운송검토.방식후보목록.Count);
        Assert.NotEmpty(request.포워더인계.전달항목코드목록);
        Assert.Equal(0m, request.예상비용목록[0].예상금액);
    }

    [Fact]
    public void Clone의null입력은_기존새요청기본값을유지한다()
    {
        var clone = 같이수입준비초안Codec.Clone(null!);
        Assert.Equal("", clone.요청멱등키);
        Assert.Null(clone.기대Revision);
        Assert.Equal("KR", clone.도착국가코드);
        Assert.Equal("KRW", clone.기준통화코드);
    }

    [Fact]
    public void 순수Factory는_재료근거와초기기본값을_새목록에보존한다()
    {
        var group = Group();
        var draft = 같이수입준비초안Factory.CreateBase(group);
        var material = Assert.Single(draft.재료품목목록);
        Assert.Equal("ingredient", draft.재료키);
        Assert.Equal("상품", draft.재료명);
        Assert.Equal("group-1", material.원천자동집단Id);
        Assert.Equal("ingredient", material.재료키);
        Assert.Equal("상품", material.재료명);
        Assert.Equal("2106.90", material.원천Hs코드);
        Assert.Equal(12.5m, material.모인수요수량);
        Assert.Equal("kg", material.수량단위);
        Assert.Equal("KR", draft.도착국가코드);
        Assert.Equal("KRW", draft.기준통화코드);
        Assert.Equal(new[] { "공급자·견적·품목분류·국가별 규제 자료 확인 필요" }, draft.미확인항목목록);
        Assert.Equal(new[] { "LCL", "FCL" }, draft.국제운송검토.방식후보목록);
        Assert.False(draft.포워더인계.개인정보포함여부);
        Assert.Empty(draft.예상비용목록);
        draft.재료품목목록.Clear();
        Assert.Single(같이수입준비초안Factory.CreateBase(group).재료품목목록);
        Assert.Equal("상품", group.상품명);
    }

    [Theory]
    [InlineData("Goods", "상품 원가")]
    [InlineData("InternationalFreightAndInsurance", "국제 운송·보험")]
    [InlineData("CustomsDuty", "예상 관세")]
    [InlineData("ImportTax", "예상 수입 세금")]
    [InlineData("DomesticFulfillment", "국내 이행 예상비")]
    [InlineData("Other", "Other")]
    public void 비용형식은_주어진시각과기존명칭을그대로쓴다(string category, string label)
    {
        var now = DateTimeOffset.UnixEpoch.AddSeconds(123);
        Assert.Equal(label, 같이수입준비초안Factory.CostLabel(category));
        var cost = 같이수입준비초안Factory.NewCost("cost", category, label, "USD", now);
        Assert.Equal("cost", cost.비용키);
        Assert.Equal(category, cost.범주코드);
        Assert.Equal(label, cost.표시명);
        Assert.Equal("USD", cost.통화코드);
        Assert.Equal(now, cost.확인시각Utc);
        Assert.Equal("", cost.재료키);
        Assert.Null(cost.유효기한Utc);
    }

    [Fact]
    public void 분류후보는_현재목록길이와도착국가기준을읽고_초안을수정하지않는다()
    {
        var draft = 같이수입준비초안Factory.CreateBase(Group());
        draft.도착국가코드 = "US";
        draft.품목분류후보목록.Add(new() { 후보키 = "existing" });
        var now = DateTimeOffset.UnixEpoch.AddSeconds(321);
        var candidate = 같이수입준비초안Factory.NewClassification(Group(), draft, now);
        Assert.Equal("classification-2", candidate.후보키);
        Assert.Equal("ingredient", candidate.재료키);
        Assert.Equal("2106.90", candidate.품목코드);
        Assert.Equal("US", candidate.관할국가코드);
        Assert.Equal("HTSUS", candidate.분류체계코드);
        Assert.Equal("QualifiedReviewRequired", candidate.검토상태코드);
        Assert.True(candidate.전문가검토필요);
        Assert.Equal(now, candidate.확인시각Utc);
        Assert.Single(draft.품목분류후보목록);
    }

    [Fact]
    public async Task 실패재시도와Revision만바뀐저장은_같은키와본문지문을유지하고_내용변경은새키를쓴다()
    {
        var client = new Client();
        var vm = new 같이수입준비관리ViewModel(client);
        await vm.초기화Async("관리자");
        client.Save = _ => Task.FromException<같이수입준비원장응답>(new HttpRequestException("uncertain"));
        await vm.저장Async();
        Assert.True(vm.오류메시지);
        client.Save = request => Task.FromResult(Response(request, client.Saves.Count));
        await vm.저장Async();
        await vm.저장Async();

        Assert.Equal(3, client.Saves.Count);
        Assert.All(client.Saves, request => Assert.Equal(client.Saves[0].요청멱등키, request.요청멱등키));
        Assert.StartsWith("admin-readiness-", client.Saves[0].요청멱등키);
        Assert.Null(client.Saves[0].기대Revision);
        Assert.Null(client.Saves[1].기대Revision);
        Assert.Equal(2, client.Saves[2].기대Revision);
        Assert.Equal(JsonSerializer.Serialize(client.Saves[0]), JsonSerializer.Serialize(client.Saves[1]));
        var firstHash = 같이수입준비초안Codec.Fingerprint(client.Saves[0]);
        Assert.All(client.Saves, request => Assert.Equal(firstHash, 같이수입준비초안Codec.Fingerprint(request)));
        vm.초안.재료명 = "수정";
        vm.초안변경됨();
        await vm.저장Async();
        Assert.NotEqual(client.Saves[0].요청멱등키, client.Saves[3].요청멱등키);
        Assert.Equal(3, client.Saves[3].기대Revision);
    }

    [Fact]
    public async Task 저장중RunAsync의공동잠금은_다른명령과선택을대기열없이생략한다()
    {
        var client = new Client();
        var vm = new 같이수입준비관리ViewModel(client);
        await vm.초기화Async("관리자");
        var pending = new TaskCompletionSource<같이수입준비원장응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Save = _ => pending.Task;
        var saving = vm.저장Async();
        Assert.True(vm.처리중);
        await vm.저장Async();
        await vm.미리보기Async();
        await vm.Os작업실행Async("check", false);
        await vm.선택Async(new() { 자동집단Id = "other" });
        Assert.Single(client.Saves);
        Assert.Equal(0, client.Previews);
        Assert.Equal(0, client.OsRuns);
        Assert.Equal(1, client.OperatingReads);
        Assert.Equal("group-1", vm.선택집단!.자동집단Id);
        pending.SetResult(Response(client.Saves[0], 1));
        await saving;
        Assert.False(vm.처리중);
        Assert.Single(client.Saves);
        Assert.Equal(0, client.Previews);
        Assert.Equal(0, client.OsRuns);
    }

    private static 같이수입준비원장저장요청 FingerprintFixture() => new()
    {
        재료키 = "legacy", 재료명 = "name",
        재료품목목록 = [new() { 재료키 = "a" }, new() { 재료키 = "b" }],
        포워더인계 = null!, 국제운송검토 = null!
    };

    private static 공동구매자동집단요약응답 Group() => new()
    {
        자동집단Id = "group-1", 상품키 = "ingredient", 상품명 = "상품", HS코드 = "2106.90",
        총희망수량 = 12.5m, 수량단위 = "kg", 현재상태 = 공동구매자동집단상태코드.확정
    };

    private static 같이수입준비원장응답 Response(같이수입준비원장저장요청 request, long revision)
        => new() { 자동집단Id = "group-1", 원장Id = "readiness", Revision = revision, 준비자료 = request };

    private sealed class Client : I같이수입준비관리Client
    {
        public List<같이수입준비원장저장요청> Saves { get; } = [];
        public Func<같이수입준비원장저장요청, Task<같이수입준비원장응답>> Save { get; set; }
            = request => Task.FromResult(Response(request, 1));
        public int Previews { get; private set; }
        public int OsRuns { get; private set; }
        public int OperatingReads { get; private set; }
        public Task<IReadOnlyList<공동구매자동집단요약응답>> 작업대목록조회Async(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<공동구매자동집단요약응답>>([Group()]);
        public Task<공동구매수요모집Os상태응답?> 운영상태조회Async(string id, CancellationToken ct = default)
        {
            OperatingReads++;
            return Task.FromResult<공동구매수요모집Os상태응답?>(new()
            {
                자동집단Id = id, 후속워크플로우활성여부 = true,
                인계상태 = 공동구매수요모집인계상태코드.승인후속대기
            });
        }
        public Task<같이수입준비원장응답?> 준비원장조회Async(string id, CancellationToken ct = default)
            => Task.FromResult<같이수입준비원장응답?>(null);
        public Task<같이수입준비원장응답> 저장Async(string id, 같이수입준비원장저장요청 request, CancellationToken ct = default)
        {
            Saves.Add(request);
            return Save(request);
        }
        public Task<같이수입준비원장응답> 미리보기Async(string id, 같이수입준비원장저장요청 request, CancellationToken ct = default)
        {
            Previews++;
            return Task.FromResult(Response(request, 0));
        }
        public Task<같이수입준비Os상태응답?> 준비Os상태조회Async(string id, CancellationToken ct = default)
            => Task.FromResult<같이수입준비Os상태응답?>(new() { 자동집단Id = id, 원장Revision = Saves.Count, 기능활성여부 = true });
        public Task<공동구매수요모집인계승인응답> 인계승인Async(string id, 공동구매수요모집인계승인요청 request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<같이수입준비Os상태응답> 준비Os작업실행Async(string id, 같이수입준비Os작업실행요청 request, CancellationToken ct = default)
        {
            OsRuns++;
            throw new NotSupportedException();
        }
        public Task<같이수입준비Os상태응답> 전문검토인계Async(string id, 같이수입준비Os전문검토인계요청 request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
