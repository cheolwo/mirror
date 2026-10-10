using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 복구 Query/신규 CAS 경로와 DB·Mongo 저장소 대역 시험. 외부 원장 운영 증거는 아닙니다.</summary>
public sealed class ShipperRequestServerRecoveryTests
{
    [Fact]
    public async Task 요청번호조회는_본인접수만기존상세조회로넘기고_관리자에게도타인번호를노출하지않는다()
    {
        await using var db = Context();
        db.화주운송의뢰.AddRange(Request("ledger-a", "owner-a", "same-key"), Request("ledger-b", "owner-b", "same-key"));
        await db.SaveChangesAsync();
        var actor = new Actor { UserId = "owner-a" }; var sender = new DetailSender();
        var query = new 의뢰접수요청조회QueryHandler(db, actor, sender);
        Assert.Equal("ledger-a", (await query.Handle(new("same-key"), default))!.의뢰Id);
        Assert.Equal(["ledger-a"], sender.Ids);
        actor.UserId = "server-admin"; actor.Role = "서버관리자";
        Assert.Null(await query.Handle(new("same-key"), default)); Assert.Single(sender.Ids);
        actor.UserId = null; Assert.Null(await query.Handle(new("same-key"), default));
    }

    [Fact]
    public async Task 신규접수경쟁은_첫CAS만저장하고_동일번호의다른본문으로덮어쓰지않는다()
    {
        var name = Guid.NewGuid().ToString(); await using var db1 = Context(name); await using var db2 = Context(name);
        var store = new LedgerStore { SynchronizeFirstReads = true };
        var first = Sync(db1, store).화주운송의뢰신규접수Async(Request("stable-ledger", "owner-a", "key", "첫 물품"), "owner-a");
        var second = Sync(db2, store).화주운송의뢰신규접수Async(Request("stable-ledger", "owner-a", "key", "다른 물품"), "owner-a");
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(results.Where(x => x is not null)); Assert.Single(results.Where(x => x is null));
        Assert.Single(store.Saved); Assert.Equal(1, store.Current!.Revision);
        Assert.All(store.Attempts, x => Assert.Equal(0, x.기대Revision));
    }

    [Fact]
    public async Task Mongo첫저장후SQL미투영이면_재접수로원장을수정하거나두번째생성하지않는다()
    {
        await using var db = Context(); var store = new LedgerStore(); var sync = Sync(db, store);
        Assert.NotNull(await sync.화주운송의뢰신규접수Async(Request("stable-ledger", "owner-a", "key", "첫 물품"), "owner-a"));
        Assert.Null(await sync.화주운송의뢰신규접수Async(Request("stable-ledger", "owner-a", "key", "다른 물품"), "owner-a"));
        Assert.Single(store.Saved); Assert.Equal(1, store.Current!.Revision); Assert.Empty(await db.화주운송의뢰.ToListAsync());
    }

    [Fact]
    public async Task 신규모바일첫생성과달리_기존동기화는_원래수정경로를유지한다()
    {
        await using var db = Context(); var store = new LedgerStore(); var sync = Sync(db, store);
        Assert.NotNull(await sync.화주운송의뢰동기화Async(Request("stable-ledger", "owner-a", "key", "첫 물품"), "owner-a"));
        Assert.NotNull(await sync.화주운송의뢰동기화Async(Request("stable-ledger", "owner-a", "key", "다른 물품"), "owner-a"));
        Assert.Equal(2, store.Saved.Count); Assert.Equal(2, store.Current!.Revision);
    }

    private static SsalddelContext Context(string? name = null) => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options, new Encryption());
    private static 운송원장Mongo동기화Service Sync(SsalddelContext db, LedgerStore store)
        => new(db, store, NullLogger<운송원장Mongo동기화Service>.Instance);
    private static 화주운송의뢰 Request(string id, string owner, string key, string cargo = "물품") => new()
    { 의뢰Id = id, 화주Id = owner, 주문자UserId = owner, 클라이언트요청Id = key, 화물종류 = cargo,
        픽업_도로명주소 = "상차 도로", 하차_도로명주소 = "하차 도로", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    private sealed class Actor : ICurrentUserAccessor { public string? UserId { get; set; } public string? Role { get; set; } }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class DetailSender : ISender
    {
        public List<string> Ids { get; } = [];
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        { var detail = Assert.IsType<의뢰단건조회Query>(request); Ids.Add(detail.RequestId);
            return Task.FromResult((TResponse)(object)new 화주운송의뢰응답 { 의뢰Id = detail.RequestId }); }
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class LedgerStore : I커뮤니티원장저장소
    {
        public bool SynchronizeFirstReads;
        private int reads;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public 커뮤니티원장Dto? Current;
        public List<커뮤니티원장저장요청> Saved { get; } = [];
        public List<커뮤니티원장저장요청> Attempts { get; } = [];
        public async Task<커뮤니티원장Dto?> 원장조회Async(string id, CancellationToken ct = default)
        {
            var snapshot = Current;
            if (SynchronizeFirstReads)
            { if (Interlocked.Increment(ref reads) >= 2) ready.TrySetResult(); await ready.Task.WaitAsync(ct); }
            return snapshot;
        }
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken ct = default)
        {
            lock (Saved)
            {
                Attempts.Add(request);
                if (request.기대Revision != (Current?.Revision ?? 0))
                    throw new InvalidOperationException("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.");
                Saved.Add(request);
                Current = new() { 원장Id = request.원장Id!, Revision = (Current?.Revision ?? 0) + 1,
                    커뮤니티Id = request.커뮤니티Id, 원장템플릿Key = request.원장템플릿Key,
                    제목 = request.제목, 블록목록 = request.블록목록, 외부참조 = request.외부참조 };
                return Task.FromResult(Current);
            }
        }
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
