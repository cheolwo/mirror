using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

/// <summary>기존 저장소 경계의 CAS·보호 블록 보존 시험. 실제 Mongo/worker 실행 증거는 아닙니다.</summary>
public sealed class 공간공동운영원장StoreTests
{
    [Fact]
    public async Task 보호블록만_바꾸고_기존서명다이어그램포함원장속성은_보존한다()
    {
        var f = new Fixture(); var state = await f.Store.조회Async("cooperation"); Assert.NotNull(state);
        state!.Revision++; state.TermsRevision++; state.OrderExtensionJson = "order-extension-preserved";
        Assert.True(await f.Store.교체Async(state, 5, "owner"));
        var saved = Assert.IsType<커뮤니티원장저장요청>(f.Ledgers.LastSave);
        Assert.Equal(5, saved.기대Revision); Assert.Same(f.Original.다이어그램스냅샷, saved.다이어그램스냅샷);
        Assert.Same(f.Original.포함원장목록, saved.포함원장목록); Assert.Same(f.Original.참여자목록, saved.참여자목록);
        Assert.Equal("original-signature", saved.확장속성["Signature"]); Assert.Equal("original-source", saved.외부참조["Source"]);
        Assert.Equal("untouched-signature-data", saved.블록목록.Single(x => x.BlockId == "signature").Data["Seal"]);
        var reread = await f.Store.조회Async("cooperation"); Assert.Equal(6, reread!.Revision); Assert.Equal("order-extension-preserved", reread.OrderExtensionJson);
    }

    [Fact]
    public async Task 오래된payload를_다른원장이나_새revision으로_복사하면_재생을거절한다()
    {
        var f = new Fixture(); f.Ledgers.Current!.Revision++;
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.조회Async("cooperation"));
        f = new Fixture(); f.Ledgers.Current!.원장Id = "different-root";
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.조회Async("different-root"));
    }

    [Fact]
    public async Task 기존원장revision이_바뀌거나_생성근거를바꾸면_저장하지않는다()
    {
        var f = new Fixture(); var record = (await f.Store.조회Async("cooperation"))!;
        Assert.False(await f.Store.교체Async(record, 4, "owner")); Assert.Null(f.Ledgers.LastSave);
        record.Revision = 6; record.OwnerUserId = "stranger";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Store.교체Async(record, 5, "owner"));
        Assert.Null(f.Ledgers.LastSave);
    }

    [Fact]
    public async Task 일반원장데이터에는_조건과추가참여자와주문부록_원문을_두지않는다()
    {
        var f = new Fixture(); var visible = JsonSerializer.Serialize(f.Original);
        Assert.DoesNotContain("private-supplier", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("private-condition", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("private-order", visible, StringComparison.Ordinal);
        Assert.Equal("private-supplier", (await f.Store.조회Async("cooperation"))!.Participants.Single().UserId);
    }

    [Fact]
    public async Task 같은템플릿의_일반초안과공유사본은_전용상태로승격하지않는다()
    {
        var f = new Fixture(); f.Original.외부참조 = new Dictionary<string, string>();
        Assert.Null(await f.Store.조회Async("cooperation"));
    }

    private sealed class Fixture
    {
        public LedgerStub Ledgers { get; } = new();
        public 커뮤니티원장Dto Original { get; }
        public 공간공동운영원장Store Store { get; }
        public Fixture()
        {
            var protection = new EphemeralDataProtectionProvider();
            var state = new 공간공동운영Record { LedgerId = "cooperation", Revision = 5, TermsRevision = 2, OwnerUserId = "owner",
                ExecutionMode = "Simulation", CreateRequestId = Guid.NewGuid(), CreateFingerprint = "original-fingerprint",
                Terms = new() { Responsibilities = "private-condition" }, OrderExtensionJson = "private-order",
                Participants = [new() { UserId = "private-supplier", RoleCode = 공간공동운영역할Codes.Supplier }] };
            var payload = protection.CreateProtector("Ssalddel.SpaceCooperation.LedgerBlock.v1")
                .Protect(JsonSerializer.Serialize(state, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Original = new() { 원장Id = state.LedgerId, Revision = state.Revision, 생성자UserId = state.OwnerUserId,
                커뮤니티Id = "platform", 원장템플릿Key = CommunityLedgerTemplateKeys.SpaceCooperation, 제목 = "공간 제안",
                블록목록 = [new() { BlockId = 공간공동운영원장Store.BlockId, Data = new Dictionary<string, string> { ["ProtectedPayload"] = payload } },
                    new() { BlockId = "signature", Data = new Dictionary<string, string> { ["Seal"] = "untouched-signature-data" } }],
                참여자목록 = [new() { UserId = "owner", DisplayName = "공간 제안자" }],
                포함원장목록 = [new() { 원장Id = "existing-child", 관계유형 = CommunityLedgerRelationTypes.Reference }],
                다이어그램스냅샷 = new() { DiagramId = "original-diagram" },
                외부참조 = new Dictionary<string, string> { ["Source"] = "original-source", [공간공동운영원장Store.CanonicalMarker] = "v1" },
                확장속성 = new Dictionary<string, string> { ["Signature"] = "original-signature" } };
            Ledgers.Current = Original;
            // MongoClient 생성/collection 선택은 네트워크를 호출하지 않습니다. 이 시험은 native InsertOne을 실행하지 않습니다.
            Store = new(Ledgers, new MongoClient("mongodb://127.0.0.1:1"), Options.Create(new MongoDbOptions { Database = "not-used" }), protection);
        }
    }

    private sealed class LedgerStub : I커뮤니티원장저장소
    {
        public 커뮤니티원장Dto? Current { get; set; }
        public 커뮤니티원장저장요청? LastSave { get; private set; }
        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(Current is null ? [] : [Current]);
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
        {
            if (Current is null || Current.Revision != request.기대Revision) throw new InvalidOperationException("CAS conflict");
            LastSave = request;
            Current = new() { 원장Id = request.원장Id!, Revision = Current.Revision + 1, 원장템플릿Key = request.원장템플릿Key,
                생성자UserId = request.생성자UserId, 블록목록 = request.블록목록, 외부참조 = request.외부참조 };
            return Task.FromResult(Current);
        }
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
