using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Mart;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Mart;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.마트;
using 살뜰.도메인.정산;

namespace Ssalddel.Tests.Application.Mart;

public sealed class MartReceiptResolutionR76Tests
{
    [Theory]
    [InlineData(false, "Partial")]
    [InlineData(true, "Missing")]
    [InlineData(true, "Damaged")]
    public async Task 원보류와수령은보존하고현재정본에양측전량확인후에만기존정산관문을연다(bool previousReceipt, string reason)
    {
        await using var f = await Held(previousReceipt, reason);
        var before = await Read(f); var original = JsonSerializer.Serialize(new { before.ConfirmedProof, before.Hold });
        var financial = await f.Db.Set<NeighborhoodSettlementPaymentRecord>().CountAsync();
        var proposer = f.CustomerUseCase(MartReceiptTestFixture.Seller);
        var proposal = await Proposal(f);
        var created = Value(await proposer.해결제안Async(701, proposal));
        Assert.False(created.PaymentReleaseReady); Assert.False(created.BuyerConsented); Assert.False(created.SellerConsented);
        var buyer = await Consent(f, created, true); var buyerDone = Value(await f.CustomerUseCase().해결동의Async(701, buyer));
        Assert.False(buyerDone.PaymentReleaseReady); Assert.True(buyerDone.BuyerConsented); Assert.Equal("Seller", buyerDone.NextActor);
        var seller = await Consent(f, buyerDone, true); var done = Value(await proposer.해결동의Async(701, seller));
        Assert.Equal("ResolvedFullReceipt", done.ResolutionStatus); Assert.True(done.PaymentReleaseReady);
        var after = await Read(f);
        Assert.Equal(original, JsonSerializer.Serialize(new { after.ConfirmedProof, after.Hold }));
        Assert.True(after.StoredEvidenceScopeValid); Assert.True(after.StoredEvidenceValid);
        Assert.Equal(previousReceipt, after.ConfirmedProofValid); Assert.Equal(2, after.EffectiveConfirmedQuantity);
        Assert.Equal(created.ProposalId, after.EffectiveReceiptId); Assert.Equal(done.Revision, after.EffectiveReceiptRevision);
        var source = await new NeighborhoodSettlementSourceResolver(f.Db, f.Clock).ResolveAsync(NeighborhoodSettlementCodes.MartOrder, "701", default);
        Assert.True(source.Workflow!.PaymentReleaseReady); Assert.Equal(created.ProposalId, source.CustomerReceipt!.ReceiptId);
        Assert.Equal(4, source.ActualCompletedUnits); // 피킹2·포장2: 해결 진술을 추가 작업 보수로 중복 계산하지 않습니다.
        Assert.Equal(financial, await f.Db.Set<NeighborhoodSettlementPaymentRecord>().CountAsync());
        var raw = await f.RawEvidenceAsync(701); Assert.StartsWith("r68-protected:", raw); Assert.DoesNotContain("확인 근거", raw);
        Assert.True(Value(await proposer.해결제안Async(701, proposal)).IsReplay);
        Assert.True(Value(await f.CustomerUseCase().해결동의Async(701, buyer)).IsReplay);
        Assert.True(Value(await proposer.해결요청결과조회Async(701, seller.ClientRequestId)).IsReplay);
        Assert.Equal(3, (await Read(f)).Envelope!.Resolutions.Count);
    }

    [Fact]
    public async Task 부분합의와동의거절은운영검토로남고새전량합의만후속해결한다()
    {
        await using var f = await Held();
        var input = await Proposal(f, MartReceiptResolutionCodes.OperationalReview, 1);
        var value = Value(await f.CustomerUseCase().해결제안Async(701, input));
        value = Value(await f.CustomerUseCase().해결동의Async(701, await Consent(f, value, true)));
        value = Value(await f.CustomerUseCase(MartReceiptTestFixture.Seller).해결동의Async(701, await Consent(f, value, true)));
        Assert.Equal("OperationalReview", value.ResolutionStatus); Assert.False(value.PaymentReleaseReady); Assert.Equal("OperatorReview", value.NextActor);
        value = Value(await f.CustomerUseCase().해결제안Async(701, await Proposal(f)));
        value = Value(await f.CustomerUseCase().해결동의Async(701, await Consent(f, value, false)));
        Assert.Equal("OperationalReview", value.ResolutionStatus); Assert.False(value.PaymentReleaseReady);
        Assert.True(value.CanProposeResolution); Assert.NotNull((await Read(f)).Hold);
    }

    [Fact]
    public async Task 타인과기사는조회명령과복구를못하고기존구매자전용권한도확대하지않는다()
    {
        await using var f = await Held(); var input = await Proposal(f);
        foreach (var actor in new[] { "outsider-admin", MartReceiptTestFixture.Driver, MartReceiptTestFixture.Worker })
        {
            Status(await f.CustomerUseCase(actor).해결조회Async(701), 404);
            Status(await f.CustomerUseCase(actor).해결제안Async(701, input), 404);
            Status(await f.CustomerUseCase(actor).해결요청결과조회Async(701, input.ClientRequestId), 404);
        }
        Assert.Equal(404, (await f.CustomerUseCase(MartReceiptTestFixture.Seller).조회Async(701)).Errors.Single().Metadata["StatusCode"]);
        Status(await f.CustomerUseCase(enabled: false).해결제안Async(701, input), 403);
        Assert.Empty((await Read(f)).Envelope!.Resolutions);
    }

    [Fact]
    public async Task 같은사람이양측동의로위장하거나낡은판본과다른수량으로수령완료할수없다()
    {
        await using var f = await Held();
        var p = await Proposal(f, quantity: 1); Status(await f.CustomerUseCase().해결제안Async(701, p), 400);
        p = await Proposal(f); var created = Value(await f.CustomerUseCase().해결제안Async(701, p));
        var buyer = await Consent(f, created, true); var stale = await Consent(f, created, true);
        var current = Value(await f.CustomerUseCase().해결동의Async(701, buyer));
        Code(await f.CustomerUseCase(MartReceiptTestFixture.Seller).해결동의Async(701, stale), "MartCustomerReceiptRevisionChanged");
        Code(await f.CustomerUseCase().해결동의Async(701, await Consent(f, current, true)), "MartReceiptResolutionConsentAlreadyRecorded");
        var wrong = await Consent(f, current, true); wrong.ConfirmedQuantity = 1;
        Status(await f.CustomerUseCase(MartReceiptTestFixture.Seller).해결동의Async(701, wrong), 400);
        buyer.EvidenceNote = "다른 내용"; Code(await f.CustomerUseCase().해결동의Async(701, buyer), "MartCustomerReceiptRequestConflict");
        Assert.False((await Read(f)).PaymentReleaseReady);
    }

    [Fact]
    public async Task 원천변경후과거요청복구는되지만새확정과지급준비는안된다()
    {
        await using var f = await Held(); var input = await Proposal(f);
        var created = Value(await f.CustomerUseCase().해결제안Async(701, input));
        var buyer = await Consent(f, created, true);
        (await f.Db.마트주문상품.SingleAsync(row => row.마트주문Id == 701)).수량++;
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var restored = Value(await f.CustomerUseCase().해결요청결과조회Async(701, input.ClientRequestId));
        Assert.True(restored.IsReplay); Assert.False(restored.PaymentReleaseReady); Assert.False(restored.CanConsentResolution);
        Code(await f.CustomerUseCase().해결동의Async(701, buyer), "MartCustomerReceiptEvidenceInvalid");
        Assert.Single((await Read(f)).Envelope!.Resolutions);
    }

    [Fact]
    public async Task 깨진해결이력은지급관문을열지않는다()
    {
        await using var f = await Held(); var p = await Proposal(f);
        Value(await f.CustomerUseCase().해결제안Async(701, p));
        var attempt = await f.Db.Set<마트배송시도>().SingleAsync();
        var envelope = 마트고객수령근거Envelope.Read(attempt.고객수령근거Json)!;
        attempt.고객수령근거Json = (envelope with { Resolutions = [envelope.Resolutions[0] with { ActorUserId = "invented" }] }).Write();
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var view = Value(await f.CustomerUseCase().해결조회Async(701)); Assert.False(view.CanProposeResolution); Assert.False(view.PaymentReleaseReady);
        Assert.False((await Read(f)).StoredEvidenceScopeValid);
    }

    [Fact]
    public async Task 이미양측완료한뒤정본이달라지면과거요청은보존하되새합의로소급인증하지않는다()
    {
        await using var f = await Held(); var proposal = await Proposal(f);
        var value = Value(await f.CustomerUseCase().해결제안Async(701, proposal));
        value = Value(await f.CustomerUseCase().해결동의Async(701, await Consent(f, value, true)));
        value = Value(await f.CustomerUseCase(MartReceiptTestFixture.Seller).해결동의Async(701, await Consent(f, value, true)));
        Assert.True(value.PaymentReleaseReady);
        f.Clock.Advance(TimeSpan.FromSeconds(5));
        (await f.Db.운송원장.SingleAsync(row => row.의뢰Id == "MART-R68-701")).도착 = f.Clock.Now.AddSeconds(-1);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var reader = await Read(f); Assert.True(reader.CanonicalDeliveryValid); Assert.True(reader.StoredEvidenceValid);
        Assert.False(reader.PaymentReleaseReady);
        var historical = Value(await f.CustomerUseCase().해결요청결과조회Async(701, proposal.ClientRequestId));
        Assert.True(historical.IsReplay); Assert.Equal("OperationalReview", historical.ResolutionStatus);
        Assert.Equal("OperatorReview", historical.NextActor); Assert.False(historical.CanProposeResolution); Assert.False(historical.CanConsentResolution);
        Code(await f.CustomerUseCase().해결제안Async(701, await Proposal(f)), "MartReceiptResolutionAlreadyResolved");
        Assert.Equal(3, reader.Envelope!.Resolutions.Count);
    }

    private static async Task<MartReceiptTestFixture> Held(bool previousReceipt = false, string reason = "Missing")
    {
        var f = await MartReceiptTestFixture.CreateAsync(); await f.CompleteWarehouseAsync(); await f.DeliverAsync(701);
        if (previousReceipt) Assert.True((await f.CustomerUseCase().확인Async(701, await f.ConfirmationRequestAsync(701))).IsSuccess);
        var r = await f.ConfirmationRequestAsync(701);
        Assert.True((await f.CustomerUseCase().문제신고Async(701, new() { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = r.ExpectedAttemptStableId,
            ExpectedRevision = r.ExpectedRevision, ReasonCode = reason })).IsSuccess); return f;
    }
    private static Task<마트수령근거Snapshot> Read(MartReceiptTestFixture f) => 마트수령근거Reader.ReadAsync(f.Db, 701, f.Clock.Now);
    private static async Task<마트수령해결제안요청> Proposal(MartReceiptTestFixture f,
        string code = MartReceiptResolutionCodes.FullReceiptReconfirmed, long quantity = 2)
    {
        var r = await f.ConfirmationRequestAsync(701); return new() { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = r.ExpectedAttemptStableId,
            ExpectedRevision = r.ExpectedRevision, OutcomeCode = code, ConfirmedQuantity = quantity, EvidenceNote = "확인 근거 · 문제 조치 후 물품 수량 확인" };
    }
    private static Task<마트수령해결동의요청> Consent(MartReceiptTestFixture f, 마트수령문제해결Dto value, bool granted)
        => Task.FromResult(new 마트수령해결동의요청 { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = value.AttemptStableId,
            ExpectedRevision = value.Revision, ProposalId = value.ProposalId!, Granted = granted, ConfirmedQuantity = value.ProposedQuantity!.Value,
            EvidenceNote = "확인 근거 · 본인이 제안 수량과 상태 대조" });
    private static 마트수령문제해결Dto Value(Result<마트수령문제해결Dto> r) { Assert.True(r.IsSuccess, string.Join(",", r.Errors)); return r.Value; }
    private static void Status(Result<마트수령문제해결Dto> r, int expected) { Assert.True(r.IsFailed); Assert.Equal(expected, r.Errors.Single().Metadata["StatusCode"]); }
    private static void Code(Result<마트수령문제해결Dto> r, string expected) { Assert.True(r.IsFailed); Assert.Equal(expected, r.Errors.Single().Metadata["ErrorCode"]); }
}
