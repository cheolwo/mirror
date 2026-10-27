using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Migrations;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화물작업시간영속R77Tests
{
    [Fact]
    public async Task 명시0과소수시간을_생성_SQL_Mongo_조회에보존한다()
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync(f.CreateCommand() with { 픽업작업시간분 = 0m, 하차작업시간분 = 7.5m });
        f.Db.ChangeTracker.Clear();
        var stored = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(0m, stored.픽업_작업시간분);
        Assert.Equal(7.5m, stored.하차_작업시간분);
        Assert.Equal(0m, created.픽업!.작업시간분);
        Assert.Equal(7.5m, created.하차!.작업시간분);
        Assert.Contains(f.Sync.LastLedger!.블록목록, b => b.Title == "상차" && b.Data.GetValueOrDefault("작업시간분") == "0");
        Assert.Contains(f.Sync.LastLedger!.블록목록, b => b.Title == "하차" && b.Data.GetValueOrDefault("작업시간분") == "7.5");
        Assert.True(화주운송의뢰매퍼.ReadHandlingTime(stored).확인됨);
    }

    [Fact]
    public async Task 구형누락은null이며음수시간은저장하지않는다()
    {
        await using var f = new 화물적재검증Fixture();
        var bad = await f.CreateHandler().Handle(f.CreateCommand() with { 픽업작업시간분 = -0.5m }, default);
        Assert.True(bad.IsFailed);
        Assert.Empty(await f.Db.화주운송의뢰.ToArrayAsync());
        var created = await f.CreateAsync();
        Assert.Null(created.픽업!.작업시간분);
        Assert.Null(created.하차!.작업시간분);
        Assert.False(화주운송의뢰매퍼.ReadHandlingTime(await f.Db.화주운송의뢰.SingleAsync()).확인됨);
    }

    [Fact]
    public async Task 수정시간은저장하고_수락뒤시간변경은거절한다()
    {
        await using var f = new 화물적재검증Fixture();
        var created = await f.CreateAsync();
        var actor = new Actor();
        var handler = new 의뢰수정CommandHandler(f.Db, new 화주운송업무담당자UseCase(f.Db, actor, TimeProvider.System), ledgerSync: f.Sync, currentUserAccessor: actor);
        var update = new 의뢰수정Command(created.의뢰Id, new(null, null, null), 화물적재검증Fixture.EmptyCargo(),
            new(null, null, null, null, null, null, null, null) { 작업시간분 = 3m },
            new(null, null, null, null, null, null, null, null) { 작업시간분 = 0m }, new(null), null);
        var saved = await handler.Handle(update, default);
        Assert.True(saved.IsSuccess, string.Join(";", saved.Errors.Select(x => x.Message)));
        Assert.Equal(3m, saved.Value.픽업!.작업시간분);
        Assert.Equal(0m, saved.Value.하차!.작업시간분);
        var cleared = await handler.Handle(update with { 픽업지 = update.픽업지 with { 작업시간분 = null, 작업시간입력초기화 = true } }, default);
        Assert.True(cleared.IsSuccess);
        Assert.Null(cleared.Value.픽업!.작업시간분);
        var restored = await handler.Handle(update, default);
        Assert.True(restored.IsSuccess);
        var entity = await f.Db.화주운송의뢰.SingleAsync();
        entity.배차상태 = "확정";
        await f.Db.SaveChangesAsync();
        var blocked = await handler.Handle(update with { 픽업지 = update.픽업지 with { 작업시간분 = 4m } }, default);
        Assert.True(blocked.IsFailed);
        Assert.Equal(3m, (await f.Db.화주운송의뢰.SingleAsync()).픽업_작업시간분);
    }

    [Fact]
    public void 정본시간을기본으로쓰고_상충하는조율입력은미확인이다()
    {
        var row = new 화주운송의뢰 { 의뢰Id = "r77", 픽업_작업시간분 = 5m, 하차_작업시간분 = 0m };
        var stored = 국내화물배차조율입력Factory.작업시간입력결합([row], null, out var none);
        Assert.Empty(none);
        Assert.Equal(new 화물상하차작업시간(5m, 0m), stored["r77"]);
        var conflict = 입력결합(row, new(0m, 0m), out var ids);
        Assert.Equal(["r77"], ids);
        Assert.False(conflict["r77"].확인됨);
        row.픽업_작업시간분 = row.하차_작업시간분 = null;
        var explicitLegacy = 입력결합(row, new(2m, 3m), out _);
        Assert.True(explicitLegacy["r77"].확인됨);
        Assert.Null(row.픽업_작업시간분); // 명시 검토 입력을 정본으로 저장하지 않는다.
    }

    private static IReadOnlyDictionary<string, 화물상하차작업시간> 입력결합(화주운송의뢰 row, 화물상하차작업시간 supplied, out string[] conflicts)
        => 국내화물배차조율입력Factory.작업시간입력결합([row], new Dictionary<string, 화물상하차작업시간> { [row.의뢰Id] = supplied }, out conflicts);

    [Fact]
    public void 구형JSON은null새필드를추가하지않고_명시0은보존한다()
    {
        var legacy = JsonSerializer.Serialize(new LocationContactDTO());
        Assert.DoesNotContain("작업시간분", JsonSerializer.Deserialize<JsonElement>(legacy).EnumerateObject().Select(x => x.Name));
        var explicitZero = JsonSerializer.Serialize(new LocationContactDTO { 작업시간분 = 0m });
        Assert.Equal(0m, JsonSerializer.Deserialize<LocationContactDTO>(explicitZero)!.작업시간분);
    }

    [Fact]
    public void Migration은기존값을채우지않는nullable두열이다()
    {
        var migration = new AddCargoHandlingMinutes();
        Assert.Equal(2, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, op => { var column = Assert.IsType<AddColumnOperation>(op); Assert.True(column.IsNullable); Assert.Null(column.DefaultValue); });
    }

    private sealed class Actor : ICurrentUserAccessor { public string? UserId => "r75-synthetic-shipper"; public string? Role => "화주"; }
}
