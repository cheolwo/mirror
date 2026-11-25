using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Inventory;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.기사;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class 출고회차작업흐름Tests
{
    private static readonly DateTime Start = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 한시간의단건원장과작업미연결을네공정에서구분한다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var plan = Plan(warehouse, 7); db.출고예정.Add(plan); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var review = await Read(db, Request(warehouse));
        Assert.Equal(4, review.Stages.Count);
        var source = Assert.Single(review.Groups);
        Assert.Equal("source-ledger", source.Kind); Assert.Equal(7, source.Quantity); Assert.Null(source.CompletedQuantity);
        Assert.StartsWith("단일 출고 원장 ·", source.Label, StringComparison.Ordinal); Assert.Contains("가상 상품", source.Label, StringComparison.Ordinal);
        Assert.Empty(source.Members); Assert.Null(review.SelectedGroup);
        Assert.All(review.Stages, stage => Assert.Equal(7, stage.SourceQuantity));
        var request = Request(warehouse, OutboundWorkflowStageCodes.Picking);
        var picking = await Read(db, request);
        var missing = Assert.Single(picking.Groups);
        Assert.Equal("unconfirmed", missing.Kind); Assert.Equal(0, missing.TaskCount); Assert.True(missing.NeedsReview);
        Assert.Equal(0, picking.Stages.Single(stage => stage.Code == OutboundWorkflowStageCodes.Picking).WorkQuantity);
        Assert.Empty(db.ChangeTracker.Entries()); Assert.Equal(0, await db.SaveChangesAsync());
    }

    [Fact]
    public async Task 정상취소로작업이없는출고를연결오류로표시하지않는다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var plan = Plan(warehouse, 7); plan.상태 = 출고상태.취소;
        db.출고예정.Add(plan); await db.SaveChangesAsync();
        foreach (var stage in new[] { OutboundWorkflowStageCodes.Picking, OutboundWorkflowStageCodes.Packing })
        {
            var request = Request(warehouse, stage); var result = await Read(db, request);
            var group = Assert.Single(result.Groups);
            Assert.Equal("source-ledger", group.Kind); Assert.StartsWith("취소된 출고 ·", group.Label, StringComparison.Ordinal);
            Assert.Contains("가상 상품", group.Label, StringComparison.Ordinal);
            Assert.False(group.NeedsReview); Assert.Equal(0, group.Quantity); Assert.Null(group.CompletedQuantity);
            Assert.Equal(1, result.Stages.Single(summary => summary.Code == stage).CancelledCount);
            Assert.Equal(0, result.Stages.Single(summary => summary.Code == stage).UnconfirmedGroupCount);
            request.BatchKey = group.Key; var detail = await Read(db, request);
            var member = Assert.Single(detail.SelectedGroup!.Members);
            Assert.Equal(출고상태.취소, member.Status); Assert.False(member.CanOpenReview);
        }
    }

    [Fact]
    public async Task 같은작업자바코드와시간대는실제묶음이되지않고한출고의분할작업을각각보인다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var plan = Plan(warehouse, 10); db.출고예정.Add(plan); await db.SaveChangesAsync();
        var first = Work(plan, "pick-a", 4); var second = Work(plan, "pick-b", 6);
        first.묶음바코드 = second.묶음바코드 = "private-phone-barcode";
        first.작업자표시명 = first.작업자UserId;
        db.피킹포장작업.AddRange(first, second); await db.SaveChangesAsync();
        var request = Request(warehouse, OutboundWorkflowStageCodes.Picking);
        var result = await Read(db, request);
        Assert.Equal(2, result.TotalGroups); Assert.All(result.Groups, group => Assert.Equal("single-task", group.Kind));
        Assert.All(result.Groups, group => Assert.Contains("가상 상품", group.Label, StringComparison.Ordinal));
        Assert.Equal(0, result.Stages.Single(stage => stage.Code == OutboundWorkflowStageCodes.Picking).RecordedBatchCount);
        request.BatchKey = result.Groups[0].Key;
        var detail = await Read(db, request);
        Assert.Equal(1, detail.SelectedGroup!.SourcePlanCount);
        Assert.Empty(Assert.Single(detail.SelectedGroup.Members).WorkerDisplayName);
        var json = JsonSerializer.Serialize(detail);
        Assert.DoesNotContain("private-phone-barcode", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-worker", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 명시출고묶음과구성작업을페이지별로읽고이전미완료를보존한다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var bundle = new 출고묶음 { 출고창고Id = warehouse, 출고묶음번호 = "BUNDLE-1" }; db.출고묶음.Add(bundle); await db.SaveChangesAsync();
        var first = Plan(warehouse, 3); first.CreatedAt = Start.AddDays(-1); first.UpdatedAt = Start.AddMinutes(1); first.출고묶음Id = bundle.Id;
        var second = Plan(warehouse, 4); second.출고묶음Id = bundle.Id;
        db.출고예정.AddRange(first, second); await db.SaveChangesAsync();
        db.피킹포장작업.AddRange(Work(first, "pick-old", 3), Work(second, "pick-now", 4)); await db.SaveChangesAsync();
        var request = Request(warehouse, OutboundWorkflowStageCodes.Picking); request.MemberPageSize = 1;
        var result = await Read(db, request); var group = Assert.Single(result.Groups);
        Assert.Equal("recorded-batch", group.Kind); Assert.Equal(2, group.SourcePlanCount); Assert.Equal(2, group.TaskCount);
        Assert.Equal("BUNDLE-1 · 피킹", group.Label);
        request.BatchKey = group.Key;
        var page = await Read(db, request); Assert.True(page.SelectedGroup!.HasNextMemberPage); Assert.True(Assert.Single(page.SelectedGroup.Members).IsCarryover);
        request.MemberPage = 1;
        var next = await Read(db, request); Assert.False(next.SelectedGroup!.HasNextMemberPage); Assert.Equal(second.Id, Assert.Single(next.SelectedGroup.Members).OutboundPlanId);
    }

    [Fact]
    public async Task 포장완료공유근거가같은재고의여러출고작업합류를증명한다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var (first, second, packA, packB) = await PackingBatch(db, warehouse);
        var request = Request(warehouse, OutboundWorkflowStageCodes.Packing);
        var result = await Read(db, request); var group = Assert.Single(result.Groups);
        Assert.Equal("recorded-batch", group.Kind); Assert.Equal(7, group.Quantity); Assert.Equal(7, group.CompletedQuantity);
        Assert.StartsWith("기존 포장 완료 묶음 ·", group.Label, StringComparison.Ordinal); Assert.Contains("가상 상품", group.Label, StringComparison.Ordinal);
        Assert.Equal(2, group.SourcePlanCount); Assert.True(group.MembershipComplete);
        Assert.DoesNotContain("packing-private", group.Key, StringComparison.Ordinal);
        request.BatchKey = group.Key;
        var detail = await Read(db, request);
        Assert.Equal(new[] { first.Id, second.Id }, detail.SelectedGroup!.Members.Select(member => member.OutboundPlanId).ToArray());
        Assert.Equal(new[] { packA.작업Key, packB.작업Key }, detail.SelectedGroup.Members.Select(member => member.TaskKey).ToArray());
        Assert.All(detail.SelectedGroup.Members, member => Assert.Null(member.StartedAtUtc));
        Assert.DoesNotContain("actor-private", JsonSerializer.Serialize(detail), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 명시묶음번호가공백이면새번호를만들지않고번호미확인으로읽는다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var bundle = new 출고묶음 { 출고창고Id = warehouse, 출고묶음번호 = "  " }; db.출고묶음.Add(bundle); await db.SaveChangesAsync();
        var plan = Plan(warehouse, 3); plan.출고묶음Id = bundle.Id; db.출고예정.Add(plan); await db.SaveChangesAsync();
        db.피킹포장작업.Add(Work(plan, "private-label-key", 3)); await db.SaveChangesAsync();
        var result = await Read(db, Request(warehouse, OutboundWorkflowStageCodes.Picking));
        var group = Assert.Single(result.Groups);
        Assert.Equal("출고묶음 번호 미확인 · 피킹", group.Label); Assert.Equal("recorded-batch", group.Kind); Assert.Equal(3, group.Quantity);
        Assert.DoesNotContain("private-label-key", group.Label, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 확인필요묶음은현재범위상품명두개와남은상품수를이름에표시한다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var bundle = new 출고묶음 { 출고창고Id = warehouse, 출고묶음번호 = "BUNDLE-PRIVATE" }; db.출고묶음.Add(bundle); await db.SaveChangesAsync();
        var plans = new[] { Plan(warehouse, 3), Plan(warehouse, 3), Plan(warehouse, 3) };
        for (var index = 0; index < plans.Length; index++) { plans[index].상품명 = "상품" + (char)('A' + index); plans[index].출고묶음Id = bundle.Id; }
        db.출고예정.AddRange(plans); await db.SaveChangesAsync();
        db.피킹포장작업.AddRange(plans.Select((plan, index) => Work(plan, "private-key-" + index, index == 2 ? 4 : 3))); await db.SaveChangesAsync();
        var result = await Read(db, Request(warehouse, OutboundWorkflowStageCodes.Picking)); var group = Assert.Single(result.Groups);
        Assert.Equal("unconfirmed", group.Kind); Assert.Equal("작업·묶음 연결 확인 필요 · 상품A · 상품B 외 1종", group.Label);
        Assert.Equal(3, group.SourcePlanCount); Assert.Equal(6, group.Quantity);
        Assert.DoesNotContain("private-key", group.Label, StringComparison.Ordinal); Assert.DoesNotContain("private-worker", group.Label, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("duplicate-share")]
    [InlineData("wrong-sum")]
    [InlineData("different-proof")]
    [InlineData("foreign-member")]
    [InlineData("missing-history")]
    [InlineData("malformed")]
    public async Task 잘못된포장묶음근거는실제묶음으로승격하지않는다(string mode)
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var (_, _, packA, packB) = await PackingBatch(db, warehouse);
        var proof = WarehouseActivityCompletionEvidence.Read(packA.완료근거Json)!;
        if (mode == "duplicate-share") packA.완료근거Json = JsonSerializer.Serialize(proof with { PackingBatchShares = [new(packA.Id, 3), new(packA.Id, 4)] });
        if (mode == "wrong-sum") packA.완료근거Json = JsonSerializer.Serialize(proof with { PackingBatchQuantity = 99 });
        if (mode == "different-proof") packB.완료근거Json = JsonSerializer.Serialize(WarehouseActivityCompletionEvidence.Read(packB.완료근거Json)! with { PackingBatchKey = "other" });
        if (mode == "foreign-member") packB.창고Id = warehouse + 999;
        if (mode == "missing-history") db.재고이력.RemoveRange(db.재고이력);
        if (mode == "malformed") packA.완료근거Json = "{";
        await db.SaveChangesAsync();
        var result = await Read(db, Request(warehouse, OutboundWorkflowStageCodes.Packing));
        Assert.DoesNotContain(result.Groups, group => group.Kind == "recorded-batch");
        Assert.Contains(result.Groups, group => group.Kind == "unconfirmed" && group.NeedsReview);
    }

    [Fact]
    public async Task 묶음구성중조회범위밖완료출고는범위안수량과구성만보인다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var (_, outside, _, _) = await PackingBatch(db, warehouse);
        outside.CreatedAt = Start.AddDays(-2); outside.UpdatedAt = Start; outside.상태 = 출고상태.출고완료; outside.출고처리일시 = Start;
        await db.SaveChangesAsync();
        var request = Request(warehouse, OutboundWorkflowStageCodes.Packing);
        var result = await Read(db, request); var group = Assert.Single(result.Groups);
        Assert.Equal("recorded-batch", group.Kind); Assert.True(group.HasOutsideScopeMembers); Assert.False(group.MembershipComplete);
        Assert.Equal(3, group.Quantity); Assert.Equal(1, group.SourcePlanCount);
        request.BatchKey = group.Key;
        var detail = await Read(db, request);
        Assert.DoesNotContain(detail.SelectedGroup!.Members, member => member.OutboundPlanId == outside.Id);
    }

    [Fact]
    public async Task 작업원장에만연결된범위밖묶음구성도외부작업정보없이표시한다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var bundle = new 출고묶음 { 출고창고Id = warehouse, 출고묶음번호 = "EXPLICIT-BUNDLE" }; db.출고묶음.Add(bundle); await db.SaveChangesAsync();
        var inside = Plan(warehouse, 3); inside.출고묶음Id = bundle.Id;
        var outside = Plan(warehouse, 9); outside.CreatedAt = Start.AddDays(-1); outside.상태 = 출고상태.출고완료;
        db.출고예정.AddRange(inside, outside); await db.SaveChangesAsync();
        var outsideTask = Work(outside, "outside-private-task", 9); outsideTask.출고묶음Id = bundle.Id;
        db.피킹포장작업.AddRange(Work(inside, "inside-task", 3), outsideTask); await db.SaveChangesAsync();
        var request = Request(warehouse, OutboundWorkflowStageCodes.Picking); var result = await Read(db, request);
        var group = Assert.Single(result.Groups); Assert.Equal("recorded-batch", group.Kind);
        Assert.True(group.HasOutsideScopeMembers); Assert.False(group.MembershipComplete); Assert.Equal(3, group.Quantity);
        request.BatchKey = group.Key; var detail = await Read(db, request);
        Assert.Equal(inside.Id, Assert.Single(detail.SelectedGroup!.Members).OutboundPlanId);
        Assert.DoesNotContain("outside-private-task", JsonSerializer.Serialize(detail), StringComparison.Ordinal);
        Assert.Equal(1, detail.Stages.Single(stage => stage.Code == OutboundWorkflowStageCodes.Picking).TaskCount);
    }

    [Fact]
    public async Task 전체공정집계와묶음페이지는기존검색과목록페이지에독립이다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var plans = Enumerable.Range(0, 75).Select(_ => Plan(warehouse, 2)).ToArray(); db.출고예정.AddRange(plans); await db.SaveChangesAsync();
        db.피킹포장작업.AddRange(plans.Select((plan, index) => Work(plan, "pick-" + index, 2))); await db.SaveChangesAsync();
        var request = Request(warehouse, OutboundWorkflowStageCodes.Picking); request.Search = "없음"; request.Page = 99; request.BatchPage = 1; request.BatchPageSize = 6;
        var response = await Use(db).목록Async(request, default);
        Assert.True(response.IsSuccess); Assert.Empty(response.Value.Items); Assert.Empty(response.Value.Period!.Items);
        var result = response.Value.Period.Workflow!;
        Assert.Equal(75, result.TotalGroups); Assert.Equal(6, result.Groups.Count); Assert.True(result.HasNextPage);
        var summary = result.Stages.Single(stage => stage.Code == OutboundWorkflowStageCodes.Picking);
        Assert.Equal(75, summary.TaskCount); Assert.Equal(150, summary.WorkQuantity); Assert.Equal(150, summary.SourceQuantity);
        Assert.All(result.Groups, group => Assert.Empty(group.Members));
    }

    [Fact]
    public async Task 연결키는같은출고의상호일치한공정연결만반환한다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        var first = Plan(warehouse, 3); var second = Plan(warehouse, 3); first.입고상품Id = second.입고상품Id = 71;
        db.출고예정.AddRange(first, second); await db.SaveChangesAsync();
        var pick = Work(first, "pick", 3); var pack = Work(first, "pack", 3, 피킹포장작업유형.포장);
        pick.다음작업Key = pack.작업Key; pack.이전작업Key = pick.작업Key;
        db.피킹포장작업.AddRange(pick, pack); await db.SaveChangesAsync();
        var request = Request(warehouse, OutboundWorkflowStageCodes.Picking); var groups = await Read(db, request);
        request.BatchKey = groups.Groups.Single(group => group.Kind == "single-task").Key;
        var linked = await Read(db, request); Assert.Equal("pack", Assert.Single(linked.SelectedGroup!.Members).NextTaskKey);
        pack.출고예정Id = second.Id; await db.SaveChangesAsync();
        var unlinked = await Read(db, request); Assert.Null(Assert.Single(unlinked.SelectedGroup!.Members).NextTaskKey);
        Assert.Contains("연결", unlinked.SelectedGroup.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 잘못된공정과다른공정묶음및권한밖선택은대체하지않고거절한다()
    {
        await using var db = Context(); var warehouse = await Warehouse(db);
        db.출고예정.Add(Plan(warehouse, 1)); await db.SaveChangesAsync();
        var request = Request(warehouse); var review = await Read(db, request);
        request.PeriodStage = OutboundWorkflowStageCodes.Picking; request.BatchKey = review.Groups[0].Key;
        Assert.Equal(404, (await Use(db).목록Async(request, default)).Errors.Single().Metadata["StatusCode"]);
        request.BatchKey = null; request.PeriodStage = "unsupported";
        Assert.Equal(400, (await Use(db).목록Async(request, default)).Errors.Single().Metadata["StatusCode"]);
        request = Request(warehouse); request.BatchKey = "review:source:9999";
        Assert.Equal(404, (await Use(db).목록Async(request, default)).Errors.Single().Metadata["StatusCode"]);
        var hidden = await Warehouse(db, "other"); request = Request(hidden);
        Assert.Equal(404, (await Use(db).목록Async(request, default)).Errors.Single().Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 관계형DB에서도전체공정과선택구성원을읽기전용으로조회한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        await db.Database.EnsureCreatedAsync(); var warehouse = await Warehouse(db);
        var plan = Plan(warehouse, 5); db.출고예정.Add(plan); await db.SaveChangesAsync();
        db.피킹포장작업.Add(Work(plan, "sql-pick", 5)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var request = Request(warehouse, OutboundWorkflowStageCodes.Picking); var result = await Read(db, request);
        request.BatchKey = Assert.Single(result.Groups).Key;
        var detail = await Read(db, request); Assert.Equal("sql-pick", Assert.Single(detail.SelectedGroup!.Members).TaskKey);
        Assert.Empty(db.ChangeTracker.Entries()); Assert.Equal(0, await db.SaveChangesAsync());
    }

    private static async Task<(출고예정 First, 출고예정 Second, 피킹포장작업 PackA, 피킹포장작업 PackB)> PackingBatch(SsalddelContext db, long warehouse)
    {
        var inventory = new 입고상품 { 창고Id = warehouse, 입고요청Id = 71, 상품명 = "가상 상품", SKU = "SAMPLE", 가용수량 = 7 };
        db.입고상품.Add(inventory); await db.SaveChangesAsync();
        var first = Plan(warehouse, 3); var second = Plan(warehouse, 4); first.입고상품Id = second.입고상품Id = inventory.Id;
        db.출고예정.AddRange(first, second); await db.SaveChangesAsync();
        var packA = Work(first, "pack-a", 3, 피킹포장작업유형.포장); var packB = Work(second, "pack-b", 4, 피킹포장작업유형.포장);
        packA.상태 = packB.상태 = 피킹포장작업상태.완료; packA.완료일시Utc = packB.완료일시Utc = Start.AddMinutes(2);
        db.피킹포장작업.AddRange(packA, packB); await db.SaveChangesAsync();
        var shares = new[] { new WarehousePackingBatchShare(packA.Id, 3), new WarehousePackingBatchShare(packB.Id, 4) };
        packA.완료근거Json = WarehouseActivityCompletionEvidence.Capture(packA, "actor-private", Start.AddMinutes(2), "packing-private", 7, shares);
        packB.완료근거Json = WarehouseActivityCompletionEvidence.Capture(packB, "actor-private", Start.AddMinutes(2), "packing-private", 7, shares);
        db.재고이력.Add(new 재고이력 { 입고상품Id = inventory.Id, 이력유형 = "포장", 원인유형 = "포장작업", 처리UserId = "actor-private", 변경후수량 = 7, 처리일시 = Start.AddMinutes(2) });
        await db.SaveChangesAsync(); return (first, second, packA, packB);
    }
    private static async Task<OutboundPeriodWorkflow> Read(SsalddelContext db, 출고예정검토목록조회요청 request)
    {
        var result = await Use(db).목록Async(request, default); Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        return result.Value.Period!.Workflow!;
    }
    private static 출고예정검토목록조회요청 Request(long warehouse, string stage = OutboundWorkflowStageCodes.Review)
        => new() { WarehouseId = warehouse, PeriodStartUtc = Start, PeriodEndUtc = Start.AddHours(1), PeriodStage = stage };
    private static 출고예정검토UseCase Use(SsalddelContext db) => new(db, new User());
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
    private static async Task<long> Warehouse(SsalddelContext db, string owner = "worker")
    {
        var warehouse = new 창고 { 소유자UserId = owner, 창고명 = "가상 창고", 주소 = "가상 주소", IsActive = true };
        db.창고.Add(warehouse); await db.SaveChangesAsync(); return warehouse.Id;
    }
    private static 출고예정 Plan(long warehouse, int quantity) => new()
        { 출고창고Id = warehouse, 수량 = quantity, 상품명 = "가상 상품", SKU = "SAMPLE", 주문참조번호 = "ORDER", CreatedAt = Start, UpdatedAt = Start, 상태 = 출고상태.준비중 };
    private static 피킹포장작업 Work(출고예정 plan, string key, int quantity, string kind = 피킹포장작업유형.피킹) => new()
        { 출고예정Id = plan.Id, 창고Id = plan.출고창고Id, 입고상품Id = plan.입고상품Id, 상품명 = plan.상품명, SKU = plan.SKU, 주문참조번호 = plan.주문참조번호,
            작업Key = key, 작업유형 = kind, 수량 = quantity, 상태 = 피킹포장작업상태.대기, 작업자UserId = "private-worker", 작업자표시명 = "가상 담당자", CreatedAt = Start, UpdatedAt = Start };
    private sealed class User : ICurrentUserAccessor { public string? UserId => "worker"; public string? Role => 역할명.창고관리자; }
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
