using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacySupport;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class DistributedPhotoHoldTests
{
    private const string Source = "distributed-inbound";
    private static string CaseId => "support:" + new string('a', 64);
    [Fact]
    public async Task 실제지원UseCase접수번호가실제_SQL사진보존에선행연결된다()
    {
        await using var f = await DistributedTestFixture.Create(); var holds = new DistributedPhotoHoldService(f.Db);
        var store = new SupportStore(f);
        var options = Options.Create(new 보호지원Options());
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, DistributedTestFixture.Operator.UserId)], "fixture")) } };
        var use = new 보호지원UseCase(store, new SourceResolver(), new 한국영업일Calendar(options), options, f.Clock, http, null!, distributedRetention: holds);
        var request = new 거래분쟁접수Request { ClientRequestId = Guid.NewGuid(), SourceKind = Source, SourceId = "lot-a", Summary = "현장 적재 확인 필요" };
        var result = await use.분쟁접수Async(request);
        Assert.Equal(72, result.CaseId.Length); Assert.Equal("received", result.StatusCode);
        Assert.True((await use.분쟁접수Async(request)).Replay);
        Assert.True((await holds.보존적용조회Async(result.CaseId, Source, "lot-a"))!.Hold);
        Assert.Single(await f.Db.Set<분산사진보존정지Record>().AsNoTracking().ToArrayAsync());
    }
    [Fact]
    public async Task 실제사건번호길이를수용하고_SQL접수보호는같은출처로한번만기록한다()
    {
        await using var f = await DistributedTestFixture.Create(); var holds = new DistributedPhotoHoldService(f.Db);
        await holds.AcquireOpenCaseAsync(CaseId, Source, "lot-a"); await holds.AcquireOpenCaseAsync(CaseId, Source, "lot-a");
        var row = Assert.Single(await f.Db.Set<분산사진보존정지Record>().AsNoTracking().ToArrayAsync());
        Assert.Equal(72, row.CaseId.Length); Assert.True(row.Active); Assert.Null(row.ReviewAtUtc);
        Assert.True((await holds.보존적용조회Async(CaseId, Source, "lot-a"))!.Hold);
        Assert.Equal("DistributedPhotoHoldSourceConflict", (await Assert.ThrowsAsync<DistributedFulfillmentException>(
            () => holds.AcquireOpenCaseAsync(CaseId, Source, "lot-other"))).Code);
    }
    [Fact]
    public async Task 재검토일이지나도자동해제하지않고_낮은순번의보호가해제상태를되돌리지않는다()
    {
        await using var f = await DistributedTestFixture.Create(); var holds = new DistributedPhotoHoldService(f.Db);
        var time = f.Clock.Now.AddDays(-2);
        await holds.보존상태적용Async(CaseId, Source, "lot-a", 1, true, "검토", time);
        Assert.Equal(time, await holds.보존정지조회Async(CaseId, Source, "lot-a"));
        await holds.보존상태적용Async(CaseId, Source, "lot-a", 2, false, "종결 후 해제", null);
        await holds.보존상태적용Async(CaseId, Source, "lot-a", 1, true, "검토", time);
        Assert.True(await holds.보존정지해제확인Async(CaseId, Source, "lot-a"));
        var row = Assert.Single(await f.Db.Set<분산사진보존정지Record>().AsNoTracking().ToArrayAsync());
        Assert.Equal(2, row.Sequence); Assert.False(row.Active);
    }
    [Fact]
    public async Task 사건재개후이전해제요청재생은보호를해제하지못하고_새순번명시해제가필요하다()
    {
        await using var f = await DistributedTestFixture.Create(); var holds = new DistributedPhotoHoldService(f.Db);
        await holds.AcquireOpenCaseAsync(CaseId, Source, "lot-a");
        await holds.보존상태적용Async(CaseId, Source, "lot-a", 2, false, "종결", null);
        await holds.AcquireOpenCaseAsync(CaseId, Source, "lot-a");
        Assert.Equal("DistributedPhotoHoldSequenceConflict", (await Assert.ThrowsAsync<DistributedFulfillmentException>(
            () => holds.보존상태적용Async(CaseId, Source, "lot-a", 2, false, "종결", null))).Code);
        Assert.True((await holds.보존적용조회Async(CaseId, Source, "lot-a"))!.Hold);
        await holds.보존상태적용Async(CaseId, Source, "lot-a", 3, false, "재개 사건 종결", null);
        Assert.True(await holds.보존정지해제확인Async(CaseId, Source, "lot-a"));
    }
    private sealed class SourceResolver : I보호지원SourceResolver
    {
        public Task<보호지원Source?> 조회Async(string kind, string id, string actor, CancellationToken ct = default)
            => Task.FromResult<보호지원Source?>(new([actor]));
    }
    private sealed class SupportStore(DistributedTestFixture fixture) : I보호지원Store
    {
        private 보호지원Record? row;
        public Task<보호지원Record?> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(row?.CaseId == id ? row : null);
        public async Task<bool> 생성Async(보호지원Record record, CancellationToken ct = default)
        {
            var protection = await fixture.Db.Set<분산사진보존정지Record>().AsNoTracking().SingleAsync(x => x.CaseId == record.CaseId, ct);
            Assert.True(protection.Active); Assert.Equal(record.SourceId, protection.SourceId); row = record; return true;
        }
        public Task<bool> 교체Async(보호지원Record record, long expectedRevision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 목록Async(string? userId, string? kind, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime now, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 파기Async(보호지원Record record, DateTime now, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
