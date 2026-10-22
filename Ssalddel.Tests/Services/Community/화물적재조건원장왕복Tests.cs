using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Community;
using Ssalddel.Tests.Application.Shipper.Request;

namespace Ssalddel.Tests.Services.Community;

public sealed class 화물적재조건원장왕복Tests
{
    [Fact]
    public async Task 직렬화한Mongo접수자료로_새SQL투영을만들어도원입력과false와판본이같다()
    {
        await using var source = new 화물적재검증Fixture();
        await source.CreateAsync();
        var ledger = Clone(source.Sync.LastLedger!);
        await using var target = new 화물적재검증Fixture();

        await ProjectAsync(target, ledger);
        await ProjectAsync(target, ledger);

        var stored = Assert.Single(await target.Db.화주운송의뢰.AsNoTracking().ToListAsync());
        Assert.Equal(0.05m, stored.화물입력부피Cbm);
        Assert.Equal(0.2m, stored.화물부피Cbm);
        Assert.Equal("ConservativeDimensionsEstimate", stored.화물부피근거Code);
        Assert.Equal(화물적재입력Policy.CurrentRevision, stored.화물적재계산판본);
        Assert.False(stored.화물바닥회전가능여부);
        Assert.False(stored.화물적층가능여부);
        Assert.Equal(3.25m, stored.화물중량Kg);
        Assert.Equal("선결제", stored.정산시점);
        Assert.Equal("결제대기", stored.결제상태);
        Assert.Empty(await target.Db.운송원장.ToListAsync());
        var restored = 화주운송의뢰매퍼.To응답(stored);
        Assert.Equal(0.05m, restored.적재조건!.입력.입력부피Cbm);
        Assert.Equal(0.2m, restored.적재조건.유효부피Cbm);
    }

    [Fact]
    public async Task 새판본의원입력null은_SQL재투영에서도이전직접입력값을지운다()
    {
        await using var source = new 화물적재검증Fixture();
        var created = await source.CreateAsync(source.CreateCommand() with { 화물부피Cbm = 0.4m });
        var original = Clone(source.Sync.LastLedger!);
        await using var target = new 화물적재검증Fixture();
        await ProjectAsync(target, original);
        var updated = await source.UpdateAsync(created.의뢰Id, 화물적재검증Fixture.EmptyCargo() with { 부피입력초기화 = true });
        Assert.True(updated.IsSuccess);
        Assert.Null(updated.Value.요금옵션);
        var cleared = Clone(source.Sync.LastLedger!);
        var block = cleared.블록목록.Single(x => x.BlockId == "transport-request");
        Assert.Equal("v1", block.Data["화물적재근거포함"]);
        Assert.False(block.Data.ContainsKey("화물입력부피Cbm"));

        await ProjectAsync(target, cleared);

        var restored = await target.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Null(restored.화물입력부피Cbm);
        Assert.Equal(0.2m, restored.화물부피Cbm);
        Assert.Equal("DimensionsEstimated", restored.화물부피근거Code);
        Assert.Equal(화물적재입력Policy.CurrentRevision, restored.화물적재계산판본);
        Assert.False(restored.화물적층가능여부);
    }

    [Fact]
    public async Task 새적재근거표식없는구형사본은_현재SQL의원입력과선택을지우지않는다()
    {
        await using var source = new 화물적재검증Fixture();
        await source.CreateAsync();
        var original = Clone(source.Sync.LastLedger!);
        await using var target = new 화물적재검증Fixture();
        await ProjectAsync(target, original);
        var legacy = Clone(original);
        var block = legacy.블록목록.Single(x => x.BlockId == "transport-request");
        var newKeys = new[] { "화물적재근거포함", "화물입력부피Cbm", "화물부피근거Code", "화물적재계산판본", "화물바닥회전가능여부", "화물적층가능여부" };
        block.Data = block.Data.Where(pair => !newKeys.Contains(pair.Key, StringComparer.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        await ProjectAsync(target, legacy);

        var stored = await target.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(0.05m, stored.화물입력부피Cbm);
        Assert.Equal("ConservativeDimensionsEstimate", stored.화물부피근거Code);
        Assert.Equal(화물적재입력Policy.CurrentRevision, stored.화물적재계산판본);
        Assert.False(stored.화물바닥회전가능여부);
        Assert.False(stored.화물적층가능여부);
    }

    [Fact]
    public async Task 유효한Rdb관찰본의지연재생은_최신SQL적재근거와선택을덮지않는다()
    {
        await using var fixture = new 화물적재검증Fixture();
        var created = await fixture.CreateAsync();
        var original = await fixture.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        var save = 운송원장Mongo동기화Builder.저장요청생성(original, null);
        운송원장RdbSnapshotMarker.Attach(save, created.의뢰Id);
        var delayed = Clone(new 커뮤니티원장Dto
        {
            원장Id = save.원장Id!, 원장템플릿Key = save.원장템플릿Key, 원함 = save.원함,
            상태 = save.상태!, 현재단계Key = save.현재단계Key,
            블록목록 = save.블록목록, 참여자목록 = save.참여자목록,
            외부참조 = save.외부참조, 확장속성 = save.확장속성
        });
        Assert.True(운송원장RdbSnapshotMarker.Matches(delayed, created.의뢰Id));
        foreach (var (key, value) in new[]
        {
            ("화물입력부피Cbm", "0.4"),
            ("화물바닥회전가능여부", "True"),
            ("화물적층가능여부", "True")
        })
        {
            var altered = Clone(delayed);
            var block = altered.블록목록.Single(x => x.BlockId == "transport-request");
            var data = block.Data.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            data[key] = value;
            block.Data = data;
            Assert.False(운송원장RdbSnapshotMarker.Matches(altered, created.의뢰Id));
        }
        var changed = await fixture.UpdateAsync(created.의뢰Id, 화물적재검증Fixture.EmptyCargo() with
        {
            화물부피Cbm = 0.4m, 화물바닥회전가능여부 = true, 화물적층가능여부 = true
        });
        Assert.True(changed.IsSuccess);
        fixture.Db.ChangeTracker.Clear();

        await ProjectAsync(fixture, delayed);

        var stored = await fixture.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(0.4m, stored.화물입력부피Cbm);
        Assert.Equal(0.4m, stored.화물부피Cbm);
        Assert.Equal("UserProvided", stored.화물부피근거Code);
        Assert.Equal(화물적재입력Policy.CurrentRevision, stored.화물적재계산판본);
        Assert.True(stored.화물바닥회전가능여부);
        Assert.True(stored.화물적층가능여부);
        Assert.Equal(changed.Value.적재조건!.입력지문, 화주운송의뢰매퍼.To응답(stored).적재조건!.입력지문);
    }

    private static 커뮤니티원장Dto Clone(커뮤니티원장Dto value)
        => JsonSerializer.Deserialize<커뮤니티원장Dto>(JsonSerializer.Serialize(value))!;
    private static Task ProjectAsync(화물적재검증Fixture fixture, 커뮤니티원장Dto ledger)
        => new 운송원장업무투영Handler(fixture.Db, NullLogger<운송원장업무투영Handler>.Instance).동기화Async(ledger);
}
