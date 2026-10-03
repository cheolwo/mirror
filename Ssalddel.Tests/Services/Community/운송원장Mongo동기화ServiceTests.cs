using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

public sealed class 운송원장Mongo동기화ServiceTests
{
    private const string RequestId = "cargo-sync-request";
    private const string LinkedLedgerId = "community:cargo-sync-linked";
    private static readonly DateTime InitialTime = new(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 저장된의뢰는_전달된오래된값대신_현재DB견적과근거를발행한다()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        var stale = CreateRequest();
        stale.최종운임 = 1m;
        stale.결제예정금액 = 1;
        stale.정산상태 = "오래된상태";
        stale.픽업_도로명주소 = "오래된 상차 주소";
        stale.운임구성Id = 999999;
        var store = new GatedLedgerStore(fixture);
        await using var db = fixture.Context();

        var result = await Service(db, store).화주운송의뢰동기화Async(stale, "shipper-a");

        Assert.NotNull(result);
        var saved = Assert.IsType<커뮤니티원장Dto>(store.Current);
        Assert.Equal(LinkedLedgerId, saved.원장Id);
        Assert.Equal("48000", Settlement(saved).Data["최종운임"]);
        Assert.Equal("48000", Settlement(saved).Data["결제예정금액"]);
        Assert.Equal("10", Settlement(saved).Data["예상거리Km"]);
        Assert.Equal("입력거리", Settlement(saved).Data["거리계산방식"]);
        Assert.Equal("fixture-rate-v1", Settlement(saved).Data["단가출처"]);
        Assert.Equal("청구대기", Settlement(saved).Data["정산상태"]);
        Assert.Equal("합성 상차 주소", saved.블록목록.Single(x => x.BlockId == "pickup").Data["주소"]);
        AssertMarker(saved);
        Assert.Equal(0L, Assert.Single(store.SaveAttempts).기대Revision);
        await AssertCurrentRdbAsync(fixture, "quote-original");
    }

    [Fact]
    public async Task 운송실행투영동기화는_전달된오래된투영대신_현재DB업무값과연결을발행한다()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync();
        var stale = CreateTransport();
        stale.운임 = 1m;
        stale.커뮤니티원장Id = "stale-community-link";
        stale.출발지 = "오래된 상차 주소";
        var store = new GatedLedgerStore(fixture);
        await using var db = fixture.Context();

        var result = await Service(db, store).운송실행투영동기화Async(stale, "driver-a");

        Assert.NotNull(result);
        var saved = Assert.IsType<커뮤니티원장Dto>(store.Current);
        Assert.Equal(LinkedLedgerId, saved.원장Id);
        Assert.Equal(LinkedLedgerId, saved.외부참조["커뮤니티원장Id"]);
        Assert.Equal("48000", Settlement(saved).Data["최종운임"]);
        Assert.Equal("10", Settlement(saved).Data["예상거리Km"]);
        AssertMarker(saved);
        await AssertCurrentRdbAsync(fixture, "quote-original");
    }

    [Fact]
    public async Task DB에없는의뢰는_전달객체의양수Id만으로_RdbSnapshot표시를만들지않는다()
    {
        var fixture = new Fixture();
        var unsaved = CreateRequest();
        unsaved.Id = 42;
        var store = new GatedLedgerStore(fixture);
        await using var db = fixture.Context();

        var result = await Service(db, store).화주운송의뢰동기화Async(unsaved, "shipper-a");

        Assert.NotNull(result);
        var saved = Assert.IsType<커뮤니티원장Dto>(store.Current);
        Assert.Equal($"transport:{RequestId}", saved.원장Id);
        Assert.False(saved.확장속성.ContainsKey(운송원장RdbSnapshotMarker.SourceKey));
        Assert.False(saved.확장속성.ContainsKey(운송원장RdbSnapshotMarker.DigestKey));
        Assert.False(saved.확장속성.ContainsKey(운송원장RdbSnapshotMarker.RequestIdKey));
        await using var verification = fixture.Context();
        var projectedRequest = Assert.Single(await verification.화주운송의뢰.AsNoTracking().ToListAsync());
        var projectedTransport = Assert.Single(await verification.운송원장.AsNoTracking().ToListAsync());
        Assert.Equal(RequestId, projectedRequest.의뢰Id);
        Assert.Equal(48000m, projectedRequest.최종운임);
        Assert.Equal("합성 상차 주소", projectedRequest.픽업_도로명주소);
        Assert.Equal(RequestId, projectedTransport.의뢰Id);
        Assert.Equal(상태값.배차대기상태.대기, projectedTransport.상태);
    }

    [Theory]
    [InlineData("quote-newer")]
    [InlineData("approval-newer")]
    public async Task InMemory에서_지연발행충돌뒤_최신견적또는승인을_재조회한다(string newerState)
    {
        // 실제 서비스와 업무 투영의 단위 시험입니다. MySQL transaction/snapshot isolation 증거가 아닙니다.
        var fixture = new Fixture();
        await fixture.SeedAsync();
        var store = new GatedLedgerStore(fixture, delayFirstWrite: true);
        await using var firstDb = fixture.Context();
        var delayedWrite = Service(firstDb, store).화주운송의뢰동기화Async(CreateRequest(), "shipper-a");

        try
        {
            await store.FirstWriteEntered.WaitAsync(TimeSpan.FromSeconds(10));
            await fixture.ReplaceCurrentTupleAsync(newerState);
            await using var secondDb = fixture.Context();
            var newerWrite = await Service(secondDb, store)
                .화주운송의뢰동기화Async(CreateRequest(), "shipper-a");
            Assert.NotNull(newerWrite);
            store.ReleaseFirstWrite();
            Assert.NotNull(await delayedWrite.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.Equal(1, store.RevisionConflicts);
            Assert.Equal(new long?[] { 0, 0, 1 }, store.SaveAttempts.Select(x => x.기대Revision).ToArray());
            var saved = Assert.IsType<커뮤니티원장Dto>(store.Current);
            Assert.Equal(2L, saved.Revision);
            Assert.Equal(LinkedLedgerId, saved.원장Id);
            AssertMarker(saved);
            var newerQuote = newerState == "quote-newer";
            Assert.Equal(newerQuote ? "61000" : "48000", Settlement(saved).Data["최종운임"]);
            Assert.Equal(newerQuote ? "20" : "10", Settlement(saved).Data["예상거리Km"]);
            Assert.Equal(newerQuote ? "fixture-rate-v2" : "fixture-rate-v1", Settlement(saved).Data["단가출처"]);
            Assert.Equal(newerQuote ? "청구대기" : "후불승인완료", Settlement(saved).Data["정산상태"]);
            Assert.Equal("결제대기", Settlement(saved).Data["결제상태"]);
            await AssertCurrentRdbAsync(fixture, newerState);
        }
        finally
        {
            store.ReleaseFirstWrite();
        }
    }

    private static 운송원장Mongo동기화Service Service(SsalddelContext db, I커뮤니티원장저장소 store)
        => new(db, store, NullLogger<운송원장Mongo동기화Service>.Instance);

    private static 커뮤니티원장블록Dto Settlement(커뮤니티원장Dto ledger)
        => ledger.블록목록.Single(x => x.BlockId == "settlement");

    private static void AssertMarker(커뮤니티원장Dto ledger)
    {
        Assert.Equal(운송원장RdbSnapshotMarker.SourceValue, ledger.확장속성[운송원장RdbSnapshotMarker.SourceKey]);
        Assert.Equal(RequestId, ledger.확장속성[운송원장RdbSnapshotMarker.RequestIdKey]);
        Assert.False(string.IsNullOrWhiteSpace(ledger.확장속성[운송원장RdbSnapshotMarker.DigestKey]));
        Assert.True(운송원장RdbSnapshotMarker.Matches(ledger, RequestId));
    }

    private static async Task AssertCurrentRdbAsync(Fixture fixture, string expectedState)
    {
        await using var db = fixture.Context();
        var request = Assert.Single(await db.화주운송의뢰.AsNoTracking().ToListAsync());
        var transport = Assert.Single(await db.운송원장.AsNoTracking().ToListAsync());
        var fare = Assert.Single(await db.운임구성.AsNoTracking().ToListAsync());
        var newerQuote = expectedState == "quote-newer";
        var newerApproval = expectedState == "approval-newer";
        var expectedFare = newerQuote ? 61000m : 48000m;
        Assert.Equal(expectedFare, request.최종운임);
        Assert.Equal(decimal.ToInt32(expectedFare), request.결제예정금액);
        Assert.Equal(expectedFare, transport.운임);
        Assert.Equal(expectedFare, fare.최종운임);
        Assert.Equal(newerQuote ? 20m : 10m, fare.예상거리Km);
        Assert.Equal(newerQuote ? "fixture-rate-v2" : "fixture-rate-v1", fare.단가출처);
        Assert.Equal(newerApproval ? "후불승인완료" : "청구대기", request.정산상태);
        Assert.Equal(newerApproval ? 상태값.배차상태.매칭중 : 상태값.배차상태.미시작, request.배차상태);
        Assert.Equal("결제대기", request.결제상태);
        Assert.Equal("합성 상차 주소", request.픽업_도로명주소);
        Assert.Equal(LinkedLedgerId, transport.커뮤니티원장Id);
    }

    private static 화주운송의뢰 CreateRequest()
        => new()
        {
            Id = 7, 의뢰Id = RequestId, 화주Id = "shipper-a", 주문자UserId = "shipper-a",
            화물종류 = "합성 박스", 화물수량 = 3, 화물설명 = "동기화 단위 시험",
            운송방식 = "일반", 차량종류 = "1톤 카고", 결제수단 = "FakePG",
            정산시점 = "운송완료후정산", 증빙방식 = "없음", 수납주체 = "화주",
            정산상태 = "청구대기", 결제상태 = "결제대기", 배차상태 = 상태값.배차상태.미시작,
            상태 = "생성됨", 최종운임 = 48000m, 결제예정금액 = 48000,
            픽업_도로명주소 = "합성 상차 주소", 픽업_상세주소 = "합성 1층",
            픽업_연락처_이름 = "합성 상차 담당", 픽업_연락처_전화번호 = "010-0000-0001",
            픽업_시간창_시작일시 = InitialTime, 픽업_시간창_종료일시 = InitialTime.AddHours(1),
            하차_도로명주소 = "합성 하차 주소", 하차_상세주소 = "합성 2층",
            하차_연락처_이름 = "합성 하차 담당", 하차_연락처_전화번호 = "010-0000-0002",
            CreatedAt = InitialTime, UpdatedAt = InitialTime
        };

    private static 운송원장 CreateTransport()
        => new()
        {
            Id = 99, 의뢰Id = RequestId, 운송번호 = RequestId, 화주Id = "shipper-a",
            상태 = 상태값.배차대기상태.대기, 운임 = 48000m,
            출발지 = "합성 상차 주소", 도착지 = "합성 하차 주소",
            커뮤니티원장Id = LinkedLedgerId, CreatedAt = InitialTime, UpdatedAt = InitialTime
        };

    private sealed class Fixture
    {
        // EF caches the internal provider by singleton options, including this root.
        // Share its lifetime across fixtures while keeping each database name isolated.
        private static readonly InMemoryDatabaseRoot SharedDatabaseRoot = new();
        private readonly DbContextOptions<SsalddelContext> _options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"cargo-ledger-sync-{Guid.NewGuid():N}", SharedDatabaseRoot).Options;

        public SsalddelContext Context() => new(_options, new Encryption());

        public async Task SeedAsync()
        {
            await using var db = Context();
            var request = CreateRequest();
            var fare = new 운임구성
            {
                의뢰Id = RequestId, 기본운임 = 35000m, 거리운임 = 13000m, 최종운임 = 48000m,
                예상거리Km = 10m, Km당단가 = 1300m, 최소운임 = 35000m,
                거리계산방식 = "입력거리", 단가출처 = "fixture-rate-v1", CreatedAt = InitialTime, UpdatedAt = InitialTime
            };
            db.운임구성.Add(fare);
            await db.SaveChangesAsync();
            request.운임구성Id = fare.Id;
            db.화주운송의뢰.Add(request);
            db.운송원장.Add(CreateTransport());
            await db.SaveChangesAsync();
        }

        public async Task ReplaceCurrentTupleAsync(string newerState)
        {
            await using var db = Context();
            var request = await db.화주운송의뢰.SingleAsync();
            var fare = await db.운임구성.SingleAsync();
            var transport = await db.운송원장.SingleAsync();
            if (newerState == "quote-newer")
            {
                request.최종운임 = 61000m;
                fare.최종운임 = 61000m;
                transport.운임 = 61000m;
                request.결제예정금액 = 61000;
                fare.거리운임 = 26000m;
                fare.예상거리Km = 20m;
                fare.단가출처 = "fixture-rate-v2";
            }
            else
            {
                request.정산상태 = "후불승인완료";
                request.배차상태 = 상태값.배차상태.매칭중;
            }
            request.UpdatedAt = fare.UpdatedAt = transport.UpdatedAt = InitialTime.AddMinutes(1);
            await db.SaveChangesAsync();
        }
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class GatedLedgerStore(Fixture fixture, bool delayFirstWrite = false) : I커뮤니티원장저장소
    {
        private readonly object _gate = new();
        private readonly List<커뮤니티원장저장요청> _attempts = [];
        private readonly TaskCompletionSource<bool> _firstWriteEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirstWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private 커뮤니티원장Dto? _current;
        private int _revisionConflicts;

        public Task FirstWriteEntered => _firstWriteEntered.Task;
        public void ReleaseFirstWrite() => _releaseFirstWrite.TrySetResult(true);
        public int RevisionConflicts { get { lock (_gate) return _revisionConflicts; } }
        public 커뮤니티원장Dto? Current { get { lock (_gate) return _current; } }
        public IReadOnlyList<커뮤니티원장저장요청> SaveAttempts { get { lock (_gate) return _attempts.ToArray(); } }

        public async Task<커뮤니티원장Dto> 원장저장Async(
            커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
        {
            // Snapshot dictionaries are copied, including the marker. Holding the original request reference
            // would let later mutation conceal the stale-write interleaving that this test exercises.
            var captured = Copy(request);
            int attempt;
            lock (_gate)
            {
                _attempts.Add(captured);
                attempt = _attempts.Count;
            }
            if (delayFirstWrite && attempt == 1)
            {
                _firstWriteEntered.TrySetResult(true);
                await _releaseFirstWrite.Task.WaitAsync(cancellationToken);
            }

            커뮤니티원장Dto saved;
            lock (_gate)
            {
                var actualRevision = _current?.Revision ?? 0L;
                if (captured.기대Revision.HasValue && captured.기대Revision.Value != actualRevision)
                {
                    _revisionConflicts++;
                    throw new InvalidOperationException("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다. 최신 원장을 다시 조회한 뒤 재시도해야 합니다.");
                }
                saved = ToDto(captured, actualRevision + 1);
                _current = saved;
            }

            await using var projectionDb = fixture.Context();
            await new 운송원장업무투영Handler(projectionDb, NullLogger<운송원장업무투영Handler>.Instance)
                .동기화Async(saved, cancellationToken);
            return saved;
        }

        public Task<커뮤니티원장Dto?> 원장조회Async(string ledgerId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
                return Task.FromResult(_current?.원장Id == ledgerId ? _current : null);
        }

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
        {
            lock (_gate)
                return Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(_current is null ? [] : [_current]);
        }

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static 커뮤니티원장저장요청 Copy(커뮤니티원장저장요청 source)
            => new()
            {
                원장Id = source.원장Id, 기대Revision = source.기대Revision, 커뮤니티Id = source.커뮤니티Id,
                원장템플릿Key = source.원장템플릿Key, 제목 = source.제목, 원함 = source.원함,
                상태 = source.상태, 현재단계Key = source.현재단계Key,
                대상OsCode = source.대상OsCode, 대상OsName = source.대상OsName,
                생성자UserId = source.생성자UserId, 생성자표시명 = source.생성자표시명,
                블록목록 = source.블록목록.Select(x => new 커뮤니티원장블록Dto
                {
                    BlockId = x.BlockId, BlockType = x.BlockType, Title = x.Title, State = x.State,
                    담당자목록 = x.담당자목록.ToArray(), Data = new Dictionary<string, string>(x.Data)
                }).ToArray(),
                참여자목록 = source.참여자목록.ToArray(), 포함원장목록 = source.포함원장목록?.ToArray(),
                다이어그램스냅샷 = source.다이어그램스냅샷,
                외부참조 = new Dictionary<string, string>(source.외부참조),
                확장속성 = new Dictionary<string, string>(source.확장속성)
            };

        private static 커뮤니티원장Dto ToDto(커뮤니티원장저장요청 request, long revision)
            => new()
            {
                원장Id = request.원장Id!, Revision = revision, 커뮤니티Id = request.커뮤니티Id,
                원장템플릿Key = request.원장템플릿Key, 제목 = request.제목, 원함 = request.원함,
                상태 = request.상태 ?? 커뮤니티원장상태.초안, 현재단계Key = request.현재단계Key,
                대상OsCode = request.대상OsCode, 대상OsName = request.대상OsName,
                생성자UserId = request.생성자UserId, 생성자표시명 = request.생성자표시명 ?? "합성 요청자",
                블록목록 = request.블록목록, 참여자목록 = request.참여자목록,
                포함원장목록 = request.포함원장목록 ?? [], 다이어그램스냅샷 = request.다이어그램스냅샷,
                외부참조 = new Dictionary<string, string>(request.외부참조),
                확장속성 = new Dictionary<string, string>(request.확장속성)
            };
    }
}
