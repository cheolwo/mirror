using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class TransportQueueMetadataRefactorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 메타데이터병합은_대소문자만다른값과기존주소좌표를보존하고_실제변경때만시각을갱신한다(bool changeSource)
    {
        var at = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var row = new 운송원장
        {
            의뢰Id = "REQUEST", 운송번호 = "REQUEST", 원본의뢰유형 = "Source", 원본의뢰Id = "SourceId", 화주Id = "Owner",
            픽업_도로명주소 = "기존 상차", 픽업_상세주소 = "기존 상세", 픽업_위도 = 0m, 픽업_경도 = 0m,
            하차_도로명주소 = "기존 하차", 하차_위도 = 1m, 배차업무유형 = 상태값.배차업무유형.용달운송,
            상태 = "완료", UpdatedAt = at
        };
        var updater = new 운송의뢰배차메타데이터Updater(new 운송의뢰배차원천분류Service());

        updater.ApplyExistingMetadata(row, new() { 상차주소 = "새 상차", 상차위도 = 37m, 하차주소 = "새 하차" },
            new() { 픽업상세주소 = "새 상세", 하차상세주소 = "  ", 상태 = 상태값.배차대기상태.대기 },
            "request", "source", changeSource ? " new-source " : " sourceid ", " owner ");

        Assert.Equal("REQUEST", row.의뢰Id);
        Assert.Equal("Source", row.원본의뢰유형);
        Assert.Equal("Owner", row.화주Id);
        Assert.Equal(changeSource ? "new-source" : "SourceId", row.원본의뢰Id);
        Assert.Equal("기존 상차", row.픽업_도로명주소);
        Assert.Equal("기존 상세", row.픽업_상세주소);
        Assert.Equal(0m, row.픽업_위도);
        Assert.Equal("기존 하차", row.하차_도로명주소);
        Assert.Equal("완료", row.상태);
        if (changeSource) Assert.True(row.UpdatedAt > at);
        else Assert.Equal(at, row.UpdatedAt);
    }

    [Theory]
    [InlineData("food", "waiting", "projection,cargo:remove,food:upsert")]
    [InlineData("cargo", "waiting", "projection,food:remove,cargo:upsert")]
    [InlineData("food", "not-waiting", "projection,food:remove,cargo:remove")]
    [InlineData("cargo", "confirmed", "projection,food:remove,cargo:remove")]
    [InlineData("food", "terminal", "projection,food:remove,cargo:remove")]
    [InlineData("food", "unknown", "projection,food:remove,cargo:remove")]
    [InlineData("cargo", "unknown", "projection,food:remove,cargo:remove")]
    public async Task 실행인덱스는_투영뒤_반대업무제거와등록또는전체제거순서를보존한다(string work, string state, string expected)
    {
        var calls = new List<string>();
        var projection = new RecordingProjection(calls, state == "unknown");
        var projector = new 배차대기실행공간Projector(projection,
            new RecordingSpaces("food", calls), new RecordingSpaces("cargo", calls));
        var row = new 운송원장
        {
            의뢰Id = "request", 배차업무유형 = work == "food" ? 상태값.배차업무유형.음식배달 : 상태값.배차업무유형.용달운송,
            상태 = state == "not-waiting" ? "완료" : 상태값.배차대기상태.대기,
            배차큐단계 = state == "confirmed" ? 상태값.배차큐단계.확정
                : state == "terminal" ? 상태값.배차큐단계.종료 : 상태값.배차큐단계.계획배차,
            픽업_위도 = state == "unknown" ? null : 37.5m,
            픽업_경도 = state == "unknown" ? null : 127m
        };

        await projector.UpsertAsync(row, default);

        Assert.Equal(expected.Split(','), calls);
    }

    [Fact]
    public async Task 신규큐는_원천메모초기화와분류후Add하고_투영인덱스를호출하며_호출자저장전에는DB에없다()
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("queue-refactor-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request", 정산메모 = 생활배송배차Policy.신청메모(new NeighborhoodDeliveryRequest
                { DispatchMode = NeighborhoodDispatchModes.Automatic })
        });
        await db.SaveChangesAsync();
        var calls = new List<string>();
        var classifier = new RecordingClassifier(db, calls);
        var projection = new RecordingProjection(calls, false, row =>
        {
            Assert.Equal(EntityState.Added, db.Entry(row).State);
            Assert.Empty(db.운송원장.AsNoTracking());
            Assert.Equal(NeighborhoodDispatchModes.Automatic, row.생활배송배차방식);
        });
        var service = new 운송의뢰배차대기Service(db, classifier, projection,
            new RecordingSpaces("food", calls), new RecordingSpaces("cargo", calls));

        var created = await service.생성또는조회Async(new 출고예정운송대상
        {
            운송의뢰Id = "request", 원천참조번호 = "source", 판매자UserId = "owner",
            상차위도 = 37.5m, 상차경도 = 127m
        });

        Assert.Equal(new[] { "classify", "projection", "food:remove", "cargo:upsert" }, calls);
        Assert.Equal(EntityState.Added, db.Entry(created).State);
        Assert.Empty(await db.운송원장.AsNoTracking().ToArrayAsync());
        await db.SaveChangesAsync();
        Assert.Single(await db.운송원장.AsNoTracking().ToArrayAsync());
    }

    private sealed class RecordingClassifier(SsalddelContext db, List<string> calls) : I운송의뢰배차원천분류Service
    {
        public 운송의뢰배차원천분류 분류(운송원장 row)
        {
            calls.Add("classify");
            Assert.Equal(EntityState.Detached, db.Entry(row).State);
            Assert.Equal(NeighborhoodDispatchModes.Automatic, row.생활배송배차방식);
            return new 운송의뢰배차원천분류Service().분류(row);
        }
    }

    private sealed class RecordingProjection(List<string> calls, bool unknown, Action<운송원장>? inspect = null)
        : I운송원장배달권연결Service
    {
        public Task<운송원장배달권연결결과> 투영추적Async(운송원장 row, CancellationToken cancellationToken = default)
        {
            calls.Add("projection");
            inspect?.Invoke(row);
            var area = new 배달권판정결과(unknown ? "unknown" : "bjd-sigungu:11260", "fixture", "fixture");
            return Task.FromResult(new 운송원장배달권연결결과(area, area));
        }
    }

    private sealed class RecordingSpaces(string name, List<string> calls) : I음식배달권실행공간Store, I국내화물배달권실행공간Store
    {
        public Task Upsert기사Async(string area, string id, IReadOnlyList<string> adjacent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task Remove기사Async(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Upsert운송의뢰Async(string area, string id, IReadOnlyList<string> adjacent, CancellationToken cancellationToken = default)
        { calls.Add(name + ":upsert"); return Task.CompletedTask; }
        public Task Remove운송의뢰Async(string id, CancellationToken cancellationToken = default)
        { calls.Add(name + ":remove"); return Task.CompletedTask; }
        Task<음식배달권실행공간Snapshot?> I음식배달권실행공간Store.GetAsync(string area, CancellationToken cancellationToken)
            => Task.FromResult<음식배달권실행공간Snapshot?>(null);
        Task<국내화물배달권실행공간Snapshot?> I국내화물배달권실행공간Store.GetAsync(string area, CancellationToken cancellationToken)
            => Task.FromResult<국내화물배달권실행공간Snapshot?>(null);
        Task<IReadOnlyList<음식배달권실행공간Snapshot>> I음식배달권실행공간Store.SnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<음식배달권실행공간Snapshot>>([]);
        Task<IReadOnlyList<국내화물배달권실행공간Snapshot>> I국내화물배달권실행공간Store.SnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<국내화물배달권실행공간Snapshot>>([]);
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
