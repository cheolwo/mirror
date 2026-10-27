using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Community;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.External.Google;
using 살뜰.Services.Operations;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화물적재조건영속Tests
{
    [Fact]
    public async Task 생성은_유효부피와원입력과명시false를_원장과SQL과조회에보존한다()
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync();
        var entity = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        var expected = 화주운송의뢰매퍼.CalculateLoadingEvidence(f.CreateCommand());

        Assert.Equal(0.2m, entity.화물부피Cbm);
        Assert.Equal(0.05m, entity.화물입력부피Cbm);
        Assert.Equal("ConservativeDimensionsEstimate", entity.화물부피근거Code);
        Assert.Equal(화물적재입력Policy.CurrentRevision, entity.화물적재계산판본);
        Assert.False(entity.화물바닥회전가능여부);
        Assert.False(entity.화물적층가능여부);
        Assert.Equal(3.25m, entity.화물중량Kg);
        Assert.Equal(expected.유효부피Cbm, created.적재조건!.유효부피Cbm);
        Assert.Equal(expected.입력.수량, created.적재조건.입력.수량);
        Assert.Equal(0.05m, created.적재조건.입력.입력부피Cbm);
        Assert.Equal(0.2m, created.화물.부피Cbm);
        Assert.False(created.화물.바닥회전가능여부);
        var block = f.Sync.LastLedger!.블록목록.Single(x => x.BlockId == "transport-request");
        Assert.Equal("0.05", block.Data["화물입력부피Cbm"]);
        Assert.Equal("v1", block.Data["화물적재근거포함"]);
    }

    [Fact]
    public async Task 누락수량과선택은_원래null로보존하고_유효가정만별도로반환한다()
    {
        await using var f = new 화물적재검증Fixture();
        var command = f.CreateCommand() with { 화물수량 = null, 화물부피Cbm = null, 화물바닥회전가능여부 = null, 화물적층가능여부 = null };
        var result = await f.CreateHandler().Handle(command, default);

        Assert.True(result.IsSuccess);
        var stored = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Null(stored.화물수량);
        Assert.Null(stored.화물바닥회전가능여부);
        Assert.Null(stored.화물적층가능여부);
        Assert.Equal(0.1m, stored.화물부피Cbm);
        Assert.Equal(1, result.Value.적재조건!.적용수량);
        Assert.True(result.Value.적재조건.바닥회전가능여부);
        Assert.NotEmpty(result.Value.적재조건.경고);
    }

    [Fact]
    public async Task 유효부피의구형왕복은_작은원입력을유지하고_수량변경으로다시계산한다()
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync();
        var result = await f.UpdateAsync(created.의뢰Id, new(null, null, 3, null, created.화물.부피Cbm, null, null));

        Assert.True(result.IsSuccess);
        var stored = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(0.05m, stored.화물입력부피Cbm);
        Assert.Equal(0.3m, stored.화물부피Cbm);
        Assert.Equal("ConservativeDimensionsEstimate", stored.화물부피근거Code);
    }

    [Fact]
    public async Task 명시적인입력부피지우기는_치수기준으로돌아가고_동시부피입력은저장전에거절한다()
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync(f.CreateCommand() with { 화물부피Cbm = 0.4m });
        var before = JsonSerializer.Serialize(await f.Db.화주운송의뢰.AsNoTracking().SingleAsync());
        var rejected = await f.UpdateAsync(created.의뢰Id, 화물적재검증Fixture.EmptyCargo() with { 부피입력초기화 = true, 화물부피Cbm = 0.4m });
        Assert.True(rejected.IsFailed);
        Assert.Equal(before, JsonSerializer.Serialize(await f.Db.화주운송의뢰.AsNoTracking().SingleAsync()));

        var updated = await f.UpdateAsync(created.의뢰Id, 화물적재검증Fixture.EmptyCargo() with { 부피입력초기화 = true });
        Assert.True(updated.IsSuccess);
        var stored = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Null(stored.화물입력부피Cbm);
        Assert.Equal(0.2m, stored.화물부피Cbm);
        Assert.Equal("DimensionsEstimated", stored.화물부피근거Code);
    }

    [Theory]
    [InlineData("rotation")]
    [InlineData("stacking")]
    [InlineData("clear")]
    public async Task 승인뒤회전적층과원부피변경은_기존핵심조건잠금을통과하지못한다(string change)
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync();
        var entity = await f.Db.화주운송의뢰.SingleAsync();
        entity.정산상태 = "후불승인완료";
        entity.배차상태 = "배차확정";
        await f.Db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(entity);
        var cargo = change switch
        {
            "rotation" => 화물적재검증Fixture.EmptyCargo() with { 화물바닥회전가능여부 = true },
            "stacking" => 화물적재검증Fixture.EmptyCargo() with { 화물적층가능여부 = true },
            _ => 화물적재검증Fixture.EmptyCargo() with { 부피입력초기화 = true }
        };
        var result = await f.UpdateAsync(created.의뢰Id, cargo);
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, x => x.Metadata.TryGetValue("ErrorCode", out var code) && Equals(code, "FreightCoreConditionsLocked"));
        Assert.Equal(before, JsonSerializer.Serialize(entity));
    }

    [Fact]
    public async Task 미확인구형값은_조회와같은유효값재전송으로직접입력근거가되지않는다()
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync();
        var entity = await f.Db.화주운송의뢰.SingleAsync();
        entity.화물입력부피Cbm = null;
        entity.화물부피Cbm = 0.4m;
        entity.화물부피근거Code = null;
        entity.화물적재계산판본 = null;
        await f.Db.SaveChangesAsync();
        var response = 화주운송의뢰매퍼.To응답(entity);
        Assert.Equal("LegacyUnverified", response.적재조건!.부피근거Code);
        Assert.Empty(response.적재조건.계산판본);
        Assert.Null(response.적재조건.입력.입력부피Cbm);
        Assert.Null(entity.화물부피근거Code);

        Assert.True((await f.UpdateAsync(created.의뢰Id, UnchangedCargo(entity), "구형 의뢰 변경 연락처")).IsSuccess);
        Assert.Null(entity.화물입력부피Cbm);
        Assert.Null(entity.화물적재계산판본);
        Assert.Null(entity.화물부피근거Code);
        Assert.Equal(0.4m, entity.화물부피Cbm);
        Assert.Equal("구형 의뢰 변경 연락처", entity.픽업_연락처_이름);
    }

    [Fact]
    public async Task 구형의0중량과치수를그대로재전송한연락처수정은_원값과미확인근거를보존한다()
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync();
        var entity = await f.Db.화주운송의뢰.SingleAsync();
        entity.화물중량Kg = 0m;
        entity.화물길이Mm = 0;
        entity.화물입력부피Cbm = null;
        entity.화물부피Cbm = 0.4m;
        entity.화물부피근거Code = null;
        entity.화물적재계산판본 = null;
        await f.Db.SaveChangesAsync();

        var result = await f.UpdateAsync(created.의뢰Id, UnchangedCargo(entity), "기존 값 유지 연락처");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(x => x.Message)));
        var stored = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(0m, stored.화물중량Kg);
        Assert.Equal(0, stored.화물길이Mm);
        Assert.Null(stored.화물입력부피Cbm);
        Assert.Equal(0.4m, stored.화물부피Cbm);
        Assert.Null(stored.화물부피근거Code);
        Assert.Null(stored.화물적재계산판본);
        Assert.False(stored.화물바닥회전가능여부);
        Assert.False(stored.화물적층가능여부);
        Assert.Equal("기존 값 유지 연락처", stored.픽업_연락처_이름);
    }

    [Fact]
    public async Task 양수중량을새로0으로바꾸는요청은_원장과SQL변경전에거절한다()
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync();
        var before = JsonSerializer.Serialize(await f.Db.화주운송의뢰.AsNoTracking().SingleAsync());
        var ledgerBefore = f.Sync.LastLedger;

        var result = await f.UpdateAsync(created.의뢰Id,
            화물적재검증Fixture.EmptyCargo() with { 화물중량Kg = 0m }, "거절될 연락처");

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, x => x.Message.Contains("총중량", StringComparison.Ordinal));
        Assert.Equal(before, JsonSerializer.Serialize(await f.Db.화주운송의뢰.AsNoTracking().SingleAsync()));
        Assert.Same(ledgerBefore, f.Sync.LastLedger);
    }

    [Theory]
    [InlineData("raw")]
    [InlineData("effective")]
    public async Task 승인뒤동일원입력이나GET유효값전체재전송과연락처수정은_핵심조건변경이아니다(string volumeKind)
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync();
        var entity = await f.Db.화주운송의뢰.SingleAsync();
        entity.정산상태 = "후불승인완료";
        entity.배차상태 = "배차확정";
        await f.Db.SaveChangesAsync();
        var cargo = new 화물정보입력값(entity.화물종류, entity.화물설명, entity.화물수량, entity.화물중량Kg,
            volumeKind == "raw" ? entity.화물입력부피Cbm : entity.화물부피Cbm, entity.화물파손주의여부, entity.화물온도조건,
            entity.화물길이Mm, entity.화물폭Mm, entity.화물높이Mm, entity.화물팔레트개수,
            entity.화물바닥회전가능여부, entity.화물적층가능여부);

        var result = await f.UpdateAsync(created.의뢰Id, cargo, "합성 변경 연락처");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(x => x.Message)));
        Assert.Equal(0.05m, entity.화물입력부피Cbm);
        Assert.Equal(0.2m, entity.화물부피Cbm);
        Assert.Equal("ConservativeDimensionsEstimate", entity.화물부피근거Code);
        Assert.Equal(화물적재입력Policy.CurrentRevision, entity.화물적재계산판본);
        Assert.Equal("후불승인완료", entity.정산상태);
        Assert.Equal("배차확정", entity.배차상태);
        Assert.Equal("합성 변경 연락처", entity.픽업_연락처_이름);
    }

    [Fact]
    public async Task 잘못된포장수량은_원장접수와SQL변경전에거절한다()
    {
        await using var f = new 화물적재검증Fixture();
        var result = await f.CreateHandler().Handle(f.CreateCommand() with { 화물수량 = 0 }, default);
        Assert.True(result.IsFailed);
        Assert.Null(f.Sync.LastLedger);
        Assert.Empty(await f.Db.화주운송의뢰.ToListAsync());
    }

    [Fact]
    public async Task 설명미입력은_원장의주소포함요약문으로바뀌지않고_생성과재조회지문이같다()
    {
        await using var f = new 화물적재검증Fixture();
        var command = f.CreateCommand() with { 화물설명 = null };
        var before = 화주운송의뢰매퍼.CalculateLoadingEvidence(command);

        var created = await f.CreateAsync(command);
        var stored = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();

        Assert.False(string.IsNullOrWhiteSpace(f.Sync.LastLedger!.원함));
        Assert.Empty(stored.화물설명);
        Assert.True(string.IsNullOrEmpty(created.적재조건!.입력.설명));
        Assert.Equal(before.입력지문, created.적재조건.입력지문);
        Assert.Equal(before.입력지문, 화주운송의뢰매퍼.To응답(stored).적재조건!.입력지문);
    }

    [Theory]
    [InlineData("2147483646.1", int.MaxValue)]
    [InlineData("2147483647.1", null)]
    [InlineData("1000000000000000000000000", null)]
    public async Task 정수범위밖총중량도_접수후예외없이원decimal을보존한다(string weightText, int? projectedWeight)
    {
        await using var f = new 화물적재검증Fixture();
        var weight = decimal.Parse(weightText, System.Globalization.CultureInfo.InvariantCulture);

        var created = await f.CreateAsync(f.CreateCommand() with { 화물중량Kg = weight });

        Assert.Equal(weight, (await f.Db.화주운송의뢰.AsNoTracking().SingleAsync()).화물중량Kg);
        Assert.Equal(projectedWeight, (await f.Db.화물요구조건.AsNoTracking().SingleAsync()).화물무게Kg);
        Assert.Equal(weight, created.적재조건!.입력.총중량Kg);
    }

    [Fact]
    public async Task 일괄CSV는_명시false와누락을구분하고_잘못된선택은오류로남긴다()
    {
        var csv = "화물종류,픽업도로명주소,하차도로명주소,화물바닥회전가능여부,화물적층가능여부\n시험,가상 상차,가상 하차,false,false\n시험,가상 상차,가상 하차,,\n시험,가상 상차,가상 하차,maybe,true";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var parsed = await new 화주운송의뢰일괄등록파서Service().ParseAsync(stream, "loading.csv");
        Assert.False(parsed.행목록[0].화물바닥회전가능여부);
        Assert.False(parsed.행목록[0].화물적층가능여부);
        Assert.Null(parsed.행목록[1].화물바닥회전가능여부);
        Assert.Null(parsed.행목록[1].화물적층가능여부);
        Assert.Contains(parsed.오류목록, x => x.Contains("화물바닥회전가능여부", StringComparison.Ordinal));
    }

    private static 화물정보입력값 UnchangedCargo(화주운송의뢰 entity)
        => new(entity.화물종류, entity.화물설명, entity.화물수량, entity.화물중량Kg,
            entity.화물부피Cbm, entity.화물파손주의여부, entity.화물온도조건,
            entity.화물길이Mm, entity.화물폭Mm, entity.화물높이Mm, entity.화물팔레트개수,
            entity.화물바닥회전가능여부, entity.화물적층가능여부);
}

internal sealed class 화물적재검증Fixture : IAsyncDisposable
{
    public SsalddelContext Db { get; } = new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("cargo-loading-r75-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
    public LoadingSync Sync { get; }
    private readonly Actor actor = new();
    public 화물적재검증Fixture() => Sync = new(Db);
    public 의뢰생성CommandHandler CreateHandler() => new(Db, new Geo(), actor, Sync, new FreightPolicy(), new Handoff());
    public 의뢰생성Command CreateCommand() => 화주운송의뢰일괄등록지원.To의뢰생성Command(new()
    {
        화물종류 = "합성 검증 상자", 화물설명 = "가상 포장 2개", 화물수량 = 2,
        화물길이Mm = 1000, 화물폭Mm = 500, 화물높이Mm = 200, 화물중량Kg = 3.25m, 화물부피Cbm = 0.05m,
        화물바닥회전가능여부 = false, 화물적층가능여부 = false,
        픽업도로명주소 = "가상 상차 도로", 하차도로명주소 = "가상 하차 도로",
        픽업연락처이름 = "합성 상차 담당", 픽업연락처전화번호 = "010-0000-0000",
        하차연락처이름 = "합성 하차 담당", 하차연락처전화번호 = "010-0000-0000",
        픽업시간창시작일시 = DateTime.UtcNow.AddHours(1),
        픽업시간창종료일시 = DateTime.UtcNow.AddHours(2),
        하차시간창시작일시 = DateTime.UtcNow.AddHours(3),
        하차시간창종료일시 = DateTime.UtcNow.AddHours(4)
    }, new() { 운송방식 = "혼적", 차량종류 = "합성 차량", 결제수단 = "계좌이체", 정산시점 = "선결제" });
    public async Task<화주운송의뢰응답> CreateAsync(의뢰생성Command? command = null)
    {
        var result = await CreateHandler().Handle(command ?? CreateCommand(), default);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(x => x.Message)));
        return result.Value;
    }
    public static 화물정보입력값 EmptyCargo() => new(null, null, null, null, null, null, null);
    public Task<Result<화주운송의뢰응답>> UpdateAsync(string id, 화물정보입력값 cargo, string? pickupContact = null)
        => new 의뢰수정CommandHandler(Db, new 화주운송업무담당자UseCase(Db, actor, TimeProvider.System), ledgerSync: Sync, currentUserAccessor: actor)
            .Handle(new(id, new(null, null, null), cargo, new(null, null, null, null, pickupContact, null, null, null),
                new(null, null, null, null, null, null, null, null), new(null), null), default);
    public ValueTask DisposeAsync() => Db.DisposeAsync();
    private sealed record Actor : ICurrentUserAccessor { public string? UserId => "r75-synthetic-shipper"; public string? Role => "화주"; }
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Geo : IGeocodingService { public Task<(decimal lat, decimal lng)?> GeocodeAsync(string address) => Task.FromResult<(decimal lat, decimal lng)?>((37m, 127m)); }
    private sealed class FreightPolicy : IOperatingMarketFreightWorkflowPolicy
    {
        public string MarketCode => "KR";
        public OperatingMarketFreightWorkflowDecision Evaluate(OperatingMarketFreightWorkflowRequest request) => new() { CanProceed = true };
    }
    private sealed class Handoff : I화주운송의뢰화물운송인계Service
    {
        public Task<운영체제업무인계Dto> 인계Async(string requestId, CancellationToken cancellationToken = default) => Task.FromResult(new 운영체제업무인계Dto());
    }
    internal sealed class LoadingSync(SsalddelContext db) : I운송원장Mongo동기화Service
    {
        public 커뮤니티원장Dto? LastLedger { get; private set; }
        public async Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 request, string updatedBy, CancellationToken cancellationToken = default)
        {
            var save = 운송원장Mongo동기화Builder.저장요청생성(request, null);
            LastLedger = new() { 원장Id = save.원장Id!, 원장템플릿Key = save.원장템플릿Key, 원함 = save.원함, 상태 = save.상태!,
                현재단계Key = save.현재단계Key, 블록목록 = save.블록목록, 외부참조 = save.외부참조,
                확장속성 = save.확장속성, 참여자목록 = save.참여자목록 };
            await new 운송원장업무투영Handler(db, NullLogger<운송원장업무투영Handler>.Instance).동기화Async(LastLedger, cancellationToken);
            return LastLedger;
        }
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 projection, string updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string requestId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
