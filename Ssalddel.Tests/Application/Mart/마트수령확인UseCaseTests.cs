using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Mart;
using Ssalddel.Contracts.Mart;
using 살뜰.도메인.마트;

namespace Ssalddel.Tests.Application.Mart;

public sealed class 마트수령확인UseCaseTests
{
    [Fact]
    public async Task 실제명령과증분시계의전량수령을보호저장하고기존전달상태를유지한다()
    {
        await using var f = await MartReceiptTestFixture.CreateAsync(); await f.CompleteWarehouseAsync();
        f.Clock.AdvanceOnRead = true; await f.DeliverAsync(701);
        var attempt = await f.Db.Set<마트배송시도>().AsNoTracking().SingleAsync();
        var history = await f.Db.재고이력.AsNoTracking().SingleAsync(row => row.원인유형 == "마트기사픽업");
        var queue = await f.Db.운송원장.AsNoTracking().SingleAsync(row => row.의뢰Id == attempt.배차의뢰Id);
        Assert.True(history.처리일시 < attempt.픽업시각Utc); Assert.True(attempt.전달시각Utc < queue.도착);
        var before = await 마트수령근거Reader.ReadAsync(f.Db, 701, f.Clock.Now);
        Assert.True(before.CanonicalDeliveryValid); Assert.False(before.PaymentReleaseReady);
        var input = await f.ConfirmationRequestAsync(701);
        var confirmed = Value(await f.CustomerUseCase().확인Async(701, input));
        Assert.True(confirmed.ReceiptRecorded); Assert.True(confirmed.ReceiptConfirmed); Assert.True(confirmed.PaymentReleaseReady);
        Assert.Equal(2, confirmed.ConfirmedQuantity); Assert.Equal(input.ExpectedRevision + 1, confirmed.Revision);
        f.Db.ChangeTracker.Clear();
        Assert.Equal("Delivered", (await f.Db.Set<마트배송시도>().AsNoTracking().SingleAsync()).상태Code);
        Assert.Equal("전달 완료", (await f.Db.마트주문.AsNoTracking().SingleAsync(row => row.Id == 701)).상태);
        var raw = await f.RawEvidenceAsync(701);
        Assert.StartsWith("r68-protected:", raw); Assert.DoesNotContain(MartReceiptTestFixture.Buyer, raw);
        Assert.DoesNotContain(input.ClientRequestId.ToString(), raw); Assert.DoesNotContain("ConfirmedProof", raw);
        var replay = Value(await f.CustomerUseCase().확인Async(701, input)); Assert.True(replay.IsReplay);
        Assert.Equal(confirmed.Revision, replay.Revision);
        Assert.Single(await f.Db.운송이벤트.Where(row => row.이벤트타입 == "MartCustomerReceiptConfirmed").ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수령전후명시보류는이전확정근거를지우지않고같은신고를재사용한다(bool afterReceipt)
    {
        await using var f = await Delivered();
        var confirmation = await f.ConfirmationRequestAsync(701);
        string? frozenProof = null;
        if (afterReceipt)
        {
            Value(await f.CustomerUseCase().확인Async(701, confirmation));
            frozenProof = JsonSerializer.Serialize((await Read(f)).ConfirmedProof);
        }
        var expected = await f.ConfirmationRequestAsync(701);
        var issue = new 마트수령문제신고요청 { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = expected.ExpectedAttemptStableId,
            ExpectedRevision = expected.ExpectedRevision, ReasonCode = "Damaged" };
        var held = Value(await f.CustomerUseCase().문제신고Async(701, issue));
        Assert.True(held.HoldRecorded); Assert.Equal("Damaged", held.HoldReasonCode); Assert.False(held.PaymentReleaseReady);
        Assert.Equal(afterReceipt, held.ReceiptRecorded); Assert.Equal(afterReceipt, held.ReceiptConfirmed);
        if (frozenProof is not null) Assert.Equal(frozenProof, JsonSerializer.Serialize((await Read(f)).ConfirmedProof));
        var raw = await f.RawEvidenceAsync(701); Assert.StartsWith("r68-protected:", raw); Assert.DoesNotContain("Damaged", raw);
        Assert.True(Value(await f.CustomerUseCase().문제신고Async(701, issue)).IsReplay);
        Assert.True(Value(await f.CustomerUseCase().요청결과조회Async(701, issue.ClientRequestId)).IsReplay);
        var retry = await f.ConfirmationRequestAsync(701);
        Code(await f.CustomerUseCase().확인Async(701, retry), "MartCustomerReceiptHeld");
        issue.ReasonCode = "Missing"; Code(await f.CustomerUseCase().문제신고Async(701, issue), "MartCustomerReceiptRequestConflict");
        Assert.Single(await f.Db.운송이벤트.Where(row => row.이벤트타입 == "MartCustomerReceiptIssueReported").ToArrayAsync());
    }

    [Fact]
    public async Task 현재구매자만읽고명령하며꺼진정책과타인암호문오류를구분하지않는다()
    {
        await using var f = await Delivered(); var input = await f.ConfirmationRequestAsync(701);
        foreach (var actor in new[] { MartReceiptTestFixture.Seller, MartReceiptTestFixture.Driver, MartReceiptTestFixture.Worker, "unrelated-admin" })
        {
            Status(await f.CustomerUseCase(actor).조회Async(701), 404);
            Status(await f.CustomerUseCase(actor).확인Async(701, input), 404);
        }
        Status(await f.CustomerUseCase(enabled: false).확인Async(701, input), 403);
        await f.Db.Database.ExecuteSqlRawAsync("UPDATE 마트배송시도 SET customer_receipt_evidence_json = 'invalid-cipher'");
        Status(await f.CustomerUseCase("outsider").조회Async(701), 404);
        Status(await f.CustomerUseCase("outsider").요청결과조회Async(701, input.ClientRequestId), 404);
    }

    [Fact]
    public async Task 미전달과전량미확인과낡은판본을확정하지않는다()
    {
        await using var f = await MartReceiptTestFixture.CreateAsync();
        Status(await f.CustomerUseCase().확인Async(701, new() { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = "missing", ExpectedRevision = 1, FullReceiptConfirmed = true }), 409);
        await f.CompleteWarehouseAsync(); await f.DeliverAsync(701);
        var input = await f.ConfirmationRequestAsync(701); input.FullReceiptConfirmed = false;
        Code(await f.CustomerUseCase().확인Async(701, input), "MartFullReceiptConfirmationRequired");
        input.FullReceiptConfirmed = true; input.ExpectedRevision--;
        Code(await f.CustomerUseCase().확인Async(701, input), "MartCustomerReceiptRevisionChanged");
        Assert.Null((await f.Db.Set<마트배송시도>().AsNoTracking().SingleAsync()).고객수령근거Json);
    }

    [Fact]
    public async Task 같은요청은복구하고다른의미와새요청의중복확정은충돌한다()
    {
        await using var f = await Delivered(); var input = await f.ConfirmationRequestAsync(701);
        var first = Value(await f.CustomerUseCase().확인Async(701, input));
        input.FullReceiptConfirmed = false; Code(await f.CustomerUseCase().확인Async(701, input), "MartCustomerReceiptRequestConflict");
        input.FullReceiptConfirmed = true;
        var next = await f.ConfirmationRequestAsync(701);
        Code(await f.CustomerUseCase().확인Async(701, next), "MartCustomerReceiptAlreadyConfirmed");
        await using var secondContext = f.CreateContext();
        var replay = Value(await f.CustomerUseCase(context: secondContext).요청결과조회Async(701, input.ClientRequestId));
        Assert.True(replay.IsReplay); Assert.Equal(first.ReceiptRevision, replay.ReceiptRevision);
        Status(await f.CustomerUseCase().요청결과조회Async(702, input.ClientRequestId), 404);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"Version\":9,\"Hold\":{}}")]
    [InlineData("{\"Version\":1,\"Hold\":{\"ReasonCode\":\"Damaged\"}}")]
    [InlineData("{\"Version\":1}")]
    public async Task 깨지거나알수없는저장근거를미확정초안으로덮지않는다(string json)
    {
        await using var f = await Delivered();
        var attempt = await f.Db.Set<마트배송시도>().SingleAsync(); attempt.고객수령근거Json = json; await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var input = await f.ConfirmationRequestAsync(701);
        Code(await f.CustomerUseCase().확인Async(701, input), "MartCustomerReceiptEvidenceInvalid");
        var view = Value(await f.CustomerUseCase().조회Async(701)); Assert.False(view.PaymentReleaseReady);
        Assert.False(view.HoldRecorded); Assert.Null(view.HoldReasonCode); Assert.Null(view.HeldAtUtc);
        Assert.Equal(json, (await f.Db.Set<마트배송시도>().AsNoTracking().SingleAsync()).고객수령근거Json);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("quantity")]
    [InlineData("history")]
    [InlineData("future-history")]
    [InlineData("future-arrival")]
    [InlineData("label")]
    public async Task 확정후원천이달라져도원요청기록은찾고새지급판정은보류한다(string defect)
    {
        await using var f = await Delivered(); var input = await f.ConfirmationRequestAsync(701);
        Value(await f.CustomerUseCase().확인Async(701, input));
        switch (defect)
        {
            case "cancel": (await f.Db.마트주문.SingleAsync(row => row.Id == 701)).상태 = "취소"; break;
            case "quantity": (await f.Db.마트주문상품.SingleAsync(row => row.마트주문Id == 701)).수량++; break;
            case "history": (await f.Db.재고이력.SingleAsync(row => row.원인유형 == "마트기사픽업")).변경수량--; break;
            case "future-history": (await f.Db.재고이력.SingleAsync(row => row.원인유형 == "마트기사픽업")).처리일시 = f.Clock.Now.AddHours(1); break;
            case "future-arrival": (await f.Db.운송원장.SingleAsync(row => row.의뢰Id == "MART-R68-701")).도착 = f.Clock.Now.AddHours(1); break;
            case "label": (await f.Db.Set<마트포장전표>().SingleAsync()).Revision++; break;
        }
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var restored = Value(await f.CustomerUseCase().요청결과조회Async(701, input.ClientRequestId));
        Assert.True(restored.ReceiptRecorded); Assert.False(restored.ReceiptConfirmed); Assert.False(restored.PaymentReleaseReady);
        Assert.True(Value(await f.CustomerUseCase().확인Async(701, input)).IsReplay);
        Assert.NotNull((await f.Db.Set<마트배송시도>().AsNoTracking().SingleAsync()).고객수령근거Json);
    }

    private static async Task<MartReceiptTestFixture> Delivered()
    { var f = await MartReceiptTestFixture.CreateAsync(); await f.CompleteWarehouseAsync(); await f.DeliverAsync(701); return f; }
    private static Task<마트수령근거Snapshot> Read(MartReceiptTestFixture f) => 마트수령근거Reader.ReadAsync(f.Db, 701, f.Clock.Now);
    private static 마트수령확인Dto Value(Result<마트수령확인Dto> result)
    { Assert.True(result.IsSuccess, string.Join(",", result.Errors)); return result.Value; }
    private static void Code(Result<마트수령확인Dto> result, string code)
    { Assert.True(result.IsFailed); Assert.Equal(code, result.Errors.Single().Metadata["ErrorCode"]); }
    private static void Status(Result<마트수령확인Dto> result, int code)
    { Assert.True(result.IsFailed); Assert.Equal(code, result.Errors.Single().Metadata["StatusCode"]); }
}
