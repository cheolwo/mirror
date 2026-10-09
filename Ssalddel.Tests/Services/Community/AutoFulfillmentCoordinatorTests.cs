using System.Data.Common;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Fulfillment;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

/// <summary>실제 SQLite의 저장·복구·출처 구분·판본을 확인하며 실제 업체 배송이나 정산을 증명하지 않습니다.</summary>
public sealed class AutoFulfillmentCoordinatorTests
{
    [Theory]
    [InlineData(DeliveryCostSharingModes.CustomerPays, 0, 8500)]
    [InlineData(DeliveryCostSharingModes.FixedContribution, 2000, 6500)]
    [InlineData(DeliveryCostSharingModes.PercentageShare, 4250, 4250)]
    [InlineData(DeliveryCostSharingModes.RestaurantPays, 8500, 0)]
    public async Task 네음식정책이같은SQLite선정과상세분담계산을사용한다(string mode, int restaurant, int customer)
    {
        await using var f = await Fixture.CreateAsync();
        var policy = new DeliveryCostSharingPolicy { Mode = mode,
            FixedAmountKrw = mode == DeliveryCostSharingModes.FixedContribution ? 2000 : null,
            RestaurantSharePercent = mode == DeliveryCostSharingModes.PercentageShare ? 50 : null };
        var key = await FoodPolicyAsync(f, policy); var request = FoodConsent(f, policy);
        Assert.True((await f.Service.사전동의저장Async(key, "owner", request)).IsSuccess);
        var selected = await f.Service.선정실행Async(key, Guid.NewGuid());
        Assert.Equal("mock-local", selected.Value.ProviderId); Assert.Equal((decimal)restaurant, selected.Value.RestaurantContributionKrw);
        Assert.Equal((decimal)customer, selected.Value.UserChargeKrw); Assert.True(selected.Value.CostSharingAllowed);
        await using var restored = f.OpenDb(); var state = (await f.NewService(restored).상태Async(key))!;
        Assert.Equal(mode, state.CostSharingPolicy!.Mode); Assert.Equal(selected.Value.AttemptId, state.AttemptId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 기존null정액과새JSON정액은구형금액판본동의를유지하고갱신해도동결액이바뀌지않는다(bool explicitJson)
    {
        await using var f = await Fixture.CreateAsync(); var policy = new DeliveryCostSharingPolicy { Mode = DeliveryCostSharingModes.FixedContribution, FixedAmountKrw = 2000 };
        var key = await FoodPolicyAsync(f, policy); var setting = await f.Db.RestaurantFulfillmentSupports.SingleAsync();
        if (!explicitJson) { setting.CostSharingPolicyJson = null; await f.Db.SaveChangesAsync(); }
        var old = f.Consent(); old.ConfirmedRestaurantSupportKrw = 2000; old.ConfirmedRestaurantSupportRevision = 1;
        Assert.True((await f.Service.사전동의저장Async(key, "owner", old)).IsSuccess);
        setting = await f.Db.RestaurantFulfillmentSupports.SingleAsync(); setting.FixedContributionKrw = 10000; setting.Revision++;
        setting.CostSharingPolicyJson = DeliveryCostSharingStorage.Write(new() { Mode = DeliveryCostSharingModes.RestaurantPays });
        await f.Db.SaveChangesAsync(); var renewal = f.Consent(); renewal.ExpectedRevision = 1;
        var renewed = await f.Service.사전동의저장Async(key, "owner", renewal);
        Assert.True(renewed.IsSuccess); Assert.Equal(2000m, renewed.Value.FrozenRestaurantSupportKrw);
        Assert.Equal(1, renewed.Value.RestaurantSupportRevision); Assert.Equal(DeliveryCostSharingModes.FixedContribution, renewed.Value.CostSharingPolicy!.Mode);
    }

    [Theory]
    [InlineData(DeliveryCostSharingModes.CustomerPays)]
    [InlineData(DeliveryCostSharingModes.PercentageShare)]
    [InlineData(DeliveryCostSharingModes.RestaurantPays)]
    public async Task 정액외정책은구형동의또는다른전체정책으로시작할수없다(string mode)
    {
        await using var f = await Fixture.CreateAsync(); var policy = new DeliveryCostSharingPolicy { Mode = mode,
            RestaurantSharePercent = mode == DeliveryCostSharingModes.PercentageShare ? 50 : null };
        var key = await FoodPolicyAsync(f, policy); var old = f.Consent(); old.ConfirmedRestaurantSupportKrw = 0; old.ConfirmedRestaurantSupportRevision = 1;
        Assert.Equal("CostSharingPolicyConfirmationRequired", Code(await f.Service.사전동의저장Async(key, "owner", old)));
        old.ConfirmedCostSharingPolicy = new() { Mode = DeliveryCostSharingModes.FixedContribution, FixedAmountKrw = 0 };
        Assert.Equal("RestaurantSupportChanged", Code(await f.Service.사전동의저장Async(key, "owner", old)));
        Assert.Equal(0, await f.Db.AutoFulfillmentConsents.CountAsync()); Assert.Equal(0, await f.Db.FulfillmentMockJobs.CountAsync());
    }

    [Fact]
    public async Task 음식점전액한도로후보가없으면보류사유를알리고고객추가금액을만들지않는다()
    {
        await using var f = await Fixture.CreateAsync(); var policy = new DeliveryCostSharingPolicy
            { Mode = DeliveryCostSharingModes.RestaurantPays, MaximumRestaurantChargeKrw = 8000 };
        var key = await FoodPolicyAsync(f, policy); await f.Service.사전동의저장Async(key, "owner", FoodConsent(f, policy));
        var blocked = await f.Service.선정실행Async(key, Guid.NewGuid());
        Assert.Equal("RestaurantChargeLimitExceeded", blocked.Value.ReviewCode); Assert.False(blocked.Value.CostSharingAllowed);
        Assert.Null(blocked.Value.UserChargeKrw); Assert.Equal(0, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
        Assert.Equal(0, await f.Db.FulfillmentMockJobs.CountAsync());
    }

    [Fact]
    public async Task 비율정책과식당한도는실패비용을누적해자동재선정에서초기화하지않는다()
    {
        await using var f = await Fixture.CreateAsync(); var policy = new DeliveryCostSharingPolicy
            { Mode = DeliveryCostSharingModes.PercentageShare, RestaurantSharePercent = 50, MaximumRestaurantChargeKrw = 4000 };
        var key = await FoodPolicyAsync(f, policy); await f.Service.사전동의저장Async(key, "owner", FoodConsent(f, policy));
        var first = await f.Service.선정실행Async(key, Guid.NewGuid());
        var next = await f.Service.모의증거기록Async(first.Value.AttemptId!, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = first.Value.Revision,
            EvidenceKind = FulfillmentEvidenceKinds.Rejected, ConfirmedIncurredCostKrw = 3000 });
        Assert.Equal(13000m, next.Value.TotalCostKrw); Assert.Equal(4000m, next.Value.RestaurantContributionKrw); Assert.Equal(9000m, next.Value.UserChargeKrw);
        // 각 시도에 4천원 지원을 새로 주면 8천원이 되는 잘못된 계산을 방지합니다.
        Assert.Equal(4000m, next.Value.CostSharingPolicy!.MaximumRestaurantChargeKrw);
    }

    [Fact]
    public async Task 음식실패발생비용이불명확하면새정책도분담금과재선정을확정하지않는다()
    {
        await using var f = await Fixture.CreateAsync(); var policy = new DeliveryCostSharingPolicy
            { Mode = DeliveryCostSharingModes.PercentageShare, RestaurantSharePercent = 50 };
        var key = await FoodPolicyAsync(f, policy); await f.Service.사전동의저장Async(key, "owner", FoodConsent(f, policy));
        var first = await f.Service.선정실행Async(key, Guid.NewGuid());
        var result = await f.Service.모의증거기록Async(first.Value.AttemptId!, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = first.Value.Revision,
            EvidenceKind = FulfillmentEvidenceKinds.Rejected });
        Assert.Equal("IncurredCostUnknown", result.Value.ReviewCode); Assert.Null(result.Value.CostSharingAllowed);
        Assert.Null(result.Value.TotalCostKrw); Assert.Null(result.Value.UserChargeKrw);
        Assert.Equal(1, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
    }

    [Fact]
    public async Task 동의요청을기다리는동안원본정책을바꿔도저장조건은복사본을사용하고수치판본은멱등적이다()
    {
        await using var f = await Fixture.CreateAsync(); var policy = new DeliveryCostSharingPolicy
            { Mode = DeliveryCostSharingModes.PercentageShare, RestaurantSharePercent = 50.00m };
        var key = await FoodPolicyAsync(f, policy); var request = FoodConsent(f, policy);
        f.Food.ReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.Food.ReadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Service.사전동의저장Async(key, "owner", request); await f.Food.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        request.ConfirmedCostSharingPolicy!.RestaurantSharePercent = 100; f.Food.ReadRelease.TrySetResult();
        var saved = await pending; Assert.True(saved.IsSuccess); Assert.Equal(50m, saved.Value.CostSharingPolicy!.RestaurantSharePercent);
        request.ConfirmedCostSharingPolicy.RestaurantSharePercent = 50;
        Assert.True((await f.Service.사전동의저장Async(key, "owner", request)).IsSuccess);
        request.ConfirmedCostSharingPolicy.RestaurantSharePercent = 51;
        Assert.Equal("FulfillmentIdempotencyConflict", Code(await f.Service.사전동의저장Async(key, "owner", request)));
    }

    private static async Task<FulfillmentSourceKey> FoodPolicyAsync(Fixture f, DeliveryCostSharingPolicy policy)
    {
        var key = new FulfillmentSourceKey(FulfillmentSourceKinds.FoodOrder, "food-policy");
        f.Food.Snapshot = f.Source.Snapshot with { Key = key, RestaurantId = 7, WeightKg = null };
        f.Db.RestaurantFulfillmentSupports.Add(new() { RestaurantId = 7, Enabled = true, Revision = 1,
            FixedContributionKrw = DeliveryCostSharingStorage.LegacyAmount(policy), CostSharingPolicyJson = DeliveryCostSharingStorage.Write(policy) });
        await f.Db.SaveChangesAsync(); return key;
    }
    private static AutoFulfillmentConsent FoodConsent(Fixture f, DeliveryCostSharingPolicy policy)
    {
        var request = f.Consent(); request.ConfirmedRestaurantSupportRevision = 1;
        request.ConfirmedRestaurantSupportKrw = DeliveryCostSharingStorage.LegacyAmount(policy);
        request.ConfirmedCostSharingPolicy = DeliveryCostSharingCalculator.Clone(policy); return request;
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 자동원천또는저장된자동current는구형배차변경API로우회할수없다(bool canonicalFlag)
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.화주운송의뢰.Add(new()
        {
            의뢰Id = f.Key.SourceId, 주문자UserId = "owner", 자동처리처수행 = canonicalFlag, 모의처리처수행 = true,
            클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + Guid.NewGuid().ToString("N")
        });
        if (!canonicalFlag) f.Db.NeighborhoodFulfillments.Add(new() { RequestId = f.Key.SourceId, ActorId = "owner", IsAutomatic = true });
        await f.Db.SaveChangesAsync();
        var service = new 생활배송배차선택Service(f.Db, new Actor());
        var response = await service.선택Async(f.Key.SourceId, new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedDispatchRevision = 1, DispatchMode = NeighborhoodDispatchModes.PublicCall
        }, default);
        Assert.Equal("AutomaticFulfillmentManaged", response.Errors.FirstOrDefault()?.Metadata.GetValueOrDefault("ErrorCode"));
        Assert.Empty(await f.Db.운송이벤트.ToListAsync()); Assert.Empty(await f.Db.운송원장.ToListAsync());
    }

    private sealed class Actor : ICurrentUserAccessor
    {
        public string? UserId => "owner";
        public string? Role => "일반회원";
    }
    [Fact]
    public void 총비용시간상한을만족하고동률은완료시간과처리처로정한다()
    {
        var now = DateTime.UtcNow;
        AutoFulfillmentQuote[] quotes = [new("c", "", true, 1000, now.AddMinutes(10)), new("b", "", true, 1000, now.AddMinutes(5)),
            new("a", "", true, 1000, now.AddMinutes(5)), new("unknown", "", true, null, now.AddMinutes(1))];
        Assert.Equal("a", 수행처선정Engine.선정(quotes, now, now.AddMinutes(6), 2000, 1000, 2000)!.ProviderId);
        Assert.Null(수행처선정Engine.선정(quotes, now, now.AddMinutes(3), 0, 0, 9999));
        Assert.Null(수행처선정Engine.선정(quotes, now, null, 2000, 0, 2999));
    }

    [Theory]
    [InlineData(false, SsalddelExecutionMode.Simulation)]
    [InlineData(true, SsalddelExecutionMode.Operational)]
    public async Task 비활성과운영모드는새표를조회하지않는다(bool enabled, SsalddelExecutionMode mode)
    {
        await using var f = await Fixture.CreateAsync(false);
        var service = f.NewService(f.Db, enabled, mode);
        Assert.Null(await service.상태Async(f.Key));
        Assert.Equal(0, await service.대기처리Async());
        Assert.False(await service.내부수행허용Async(f.Key));
        Assert.True((await service.사전동의저장Async(f.Key, "owner", f.Consent())).IsFailed);
    }

    [Fact]
    public async Task 동의멱등재송신과소유자변경을검사한다()
    {
        await using var f = await Fixture.CreateAsync(); var request = f.Consent();
        var first = await f.Service.사전동의저장Async(f.Key, "owner", request);
        Assert.True(first.IsSuccess); Assert.Equal(1, first.Value.ConsentRevision);
        Assert.True((await f.Service.사전동의저장Async(f.Key, "owner", request)).IsSuccess);
        Assert.Equal(1, await f.Db.AutoFulfillmentConsents.CountAsync());
        request.MaxUserChargeKrw++;
        Assert.Equal("FulfillmentIdempotencyConflict", Code(await f.Service.사전동의저장Async(f.Key, "owner", request)));
        Assert.Equal("FulfillmentSourceNotFound", Code(await f.Service.사전동의저장Async(f.Key, "other", f.Consent())));
    }

    [Fact]
    public async Task 같은번호의음식과생활요청은별도current와동의로보존한다()
    {
        await using var f = await Fixture.CreateAsync();
        var food = new FulfillmentSourceKey(FulfillmentSourceKinds.FoodOrder, f.Key.SourceId);
        f.Food.Snapshot = f.Source.Snapshot with { Key = food, RestaurantId = 7, WeightKg = null };
        f.Db.RestaurantFulfillmentSupports.Add(new() { RestaurantId = 7, Enabled = true, FixedContributionKrw = 3000, Revision = 1 });
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Service.사전동의저장Async(f.Key, "owner", f.Consent())).IsSuccess);
        var consent = f.Consent(); consent.ConfirmedRestaurantSupportKrw = 3000; consent.ConfirmedRestaurantSupportRevision = 1;
        Assert.True((await f.Service.사전동의저장Async(food, "owner", consent)).IsSuccess);
        Assert.Equal(2, await f.Db.NeighborhoodFulfillments.CountAsync());
        Assert.Equal(0, (await f.Service.상태Async(f.Key))!.FrozenRestaurantSupportKrw);
        Assert.Equal(3000, (await f.Service.상태Async(food))!.FrozenRestaurantSupportKrw);
    }

    [Fact]
    public async Task 가장싼처리처하나만저장하고시간만으로배정픽업완료를만들지않는다()
    {
        await using var f = await Fixture.CreateAsync(); await f.StartAsync();
        var first = (await f.Service.상태Async(f.Key))!;
        Assert.Equal("mock-local", first.ProviderId); Assert.Equal(AutoFulfillmentStates.Accepted, first.StateCode);
        f.Clock.Now = f.Clock.Now.AddDays(1);
        await using var reopened = f.OpenDb(); var service = f.NewService(reopened);
        var same = await service.선정실행Async(f.Key, Guid.NewGuid());
        Assert.Equal(first.AttemptId, same.Value.AttemptId); Assert.Equal(AutoFulfillmentStates.Accepted, same.Value.StateCode);
        Assert.Equal(1, await reopened.NeighborhoodFulfillmentAttempts.CountAsync()); Assert.Equal(1, await reopened.FulfillmentMockJobs.CountAsync());
        Assert.False(same.Value.IsOperationalSettlementEligible);
    }

    [Fact]
    public async Task 외부명시증거는배정픽업전달순서를따르고늦은판본은거부한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.StartAsync(); var current = (await f.Service.상태Async(f.Key))!;
        var invalid = await f.Service.모의증거기록Async(current.AttemptId!, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision, EvidenceKind = FulfillmentEvidenceKinds.Delivered });
        Assert.Equal("InvalidMockTransition", Code(invalid));
        var request = new FulfillmentMockEvidenceRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision,
            EvidenceKind = FulfillmentEvidenceKinds.DriverAssigned, ExternalDriverReference = "opaque-driver" };
        var assigned = await f.Service.모의증거기록Async(current.AttemptId!, request);
        Assert.True(assigned.IsSuccess); Assert.Equal(AutoFulfillmentStates.DriverAssigned, assigned.Value.StateCode);
        Assert.Equal(assigned.Value.Revision, (await f.Service.모의증거기록Async(current.AttemptId!, request)).Value.Revision);
        var late = await f.Service.모의증거기록Async(current.AttemptId!, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = current.Revision, EvidenceKind = FulfillmentEvidenceKinds.PickedUp });
        Assert.Equal("FulfillmentRevisionConflict", Code(late));
        var picked = await f.Service.모의증거기록Async(current.AttemptId!, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = assigned.Value.Revision, EvidenceKind = FulfillmentEvidenceKinds.PickedUp });
        var delivered = await f.Service.모의증거기록Async(current.AttemptId!, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = picked.Value.Revision, EvidenceKind = FulfillmentEvidenceKinds.Delivered });
        Assert.Equal(AutoFulfillmentStates.Completed, delivered.Value.StateCode);
        Assert.Equal(8500, delivered.Value.TotalCostKrw); Assert.Equal(1, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
    }

    [Fact]
    public async Task 접수응답유실은같은job조회로회복하고다른업체에보내지않는다()
    {
        await using var f = await Fixture.CreateAsync(); f.Local.LoseResponse = true;
        await f.StartAsync(); Assert.Equal(AutoFulfillmentStates.ResultUnknown, (await f.Service.상태Async(f.Key))!.StateCode);
        await using var nextDb = f.OpenDb(); var next = f.NewService(nextDb);
        var recovered = await next.선정실행Async(f.Key, Guid.NewGuid());
        Assert.Equal(AutoFulfillmentStates.Accepted, recovered.Value.StateCode);
        Assert.Equal(1, f.Local.Submits); Assert.Equal(1, await nextDb.NeighborhoodFulfillmentAttempts.CountAsync());
    }

    [Fact]
    public async Task job저장뒤중단된Submitting은견적만료뒤에도원접수결과를회복한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.StartAsync(); var snapshot = (await f.Service.상태Async(f.Key))!;
        var current = await f.Db.NeighborhoodFulfillments.SingleAsync(); var a = await f.Db.NeighborhoodFulfillmentAttempts.SingleAsync();
        current.StateCode = a.StateCode = AutoFulfillmentStates.Submitting; a.ExternalRequestId = null;
        await f.Db.SaveChangesAsync(); f.Clock.Now = f.Clock.Now.AddHours(1);
        await using var restarted = f.OpenDb(); var result = await f.NewService(restarted).선정실행Async(f.Key, Guid.NewGuid());
        Assert.Equal(snapshot.AttemptId, result.Value.AttemptId); Assert.Equal(AutoFulfillmentStates.Accepted, result.Value.StateCode);
        Assert.Equal(1, await restarted.FulfillmentMockJobs.CountAsync()); Assert.Equal(1, f.Local.Submits);
    }

    [Fact]
    public async Task 종료된원천도접수결과불명의공급자를취소확정으로바꾸지않는다()
    {
        await using var f = await Fixture.CreateAsync(); f.Local.LoseResponse = true; await f.StartAsync();
        f.Source.Snapshot = f.Source.Snapshot with { IsClosed = true };
        var state = (await f.Service.상태Async(f.Key))!;
        Assert.True(state.SourceClosed); Assert.Equal(AutoFulfillmentStates.ResultUnknown, state.StateCode);
        Assert.False(state.CanRenewConsent); Assert.False(state.CanCancel);
    }

    [Fact]
    public async Task 취소실제비용을합산하고지원액은동의갱신에도한번만쓴다()
    {
        await using var f = await Fixture.CreateAsync(); var food = new FulfillmentSourceKey(FulfillmentSourceKinds.FoodOrder, "food-1");
        f.Food.Snapshot = f.Source.Snapshot with { Key = food, RestaurantId = 7, WeightKg = null };
        f.Db.RestaurantFulfillmentSupports.Add(new() { RestaurantId = 7, Enabled = true, FixedContributionKrw = 4000, Revision = 1 }); await f.Db.SaveChangesAsync();
        var request = f.Consent(); request.MaxUserChargeKrw = 5000; request.ConfirmedRestaurantSupportKrw = 4000; request.ConfirmedRestaurantSupportRevision = 1;
        Assert.True((await f.Service.사전동의저장Async(food, "owner", request)).IsSuccess);
        var first = await f.Service.선정실행Async(food, Guid.NewGuid());
        var failed = await f.Service.모의증거기록Async(first.Value.AttemptId!, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = first.Value.Revision,
            EvidenceKind = FulfillmentEvidenceKinds.Rejected, ConfirmedIncurredCostKrw = 3000 });
        Assert.Equal(AutoFulfillmentStates.NeedsReview, failed.Value.StateCode); Assert.Equal(3000, failed.Value.TotalCostKrw);
        var setting = await f.Db.RestaurantFulfillmentSupports.SingleAsync(); setting.FixedContributionKrw = 50000; setting.Revision++;
        await f.Db.SaveChangesAsync(); var renewed = f.Consent(); renewed.ExpectedRevision = 1; renewed.MaxUserChargeKrw = 10000;
        Assert.True((await f.Service.사전동의저장Async(food, "owner", renewed)).IsSuccess);
        var next = await f.Service.선정실행Async(food, Guid.NewGuid());
        Assert.Equal(4000, next.Value.FrozenRestaurantSupportKrw); Assert.Equal(11500, next.Value.TotalCostKrw); Assert.Equal(7500, next.Value.UserChargeKrw);
    }

    [Fact]
    public async Task 취소비용이불명확하면자동재선정을멈춘다()
    {
        await using var f = await Fixture.CreateAsync(); await f.StartAsync(); var first = (await f.Service.상태Async(f.Key))!;
        var result = await f.Service.모의증거기록Async(first.AttemptId!, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = first.Revision, EvidenceKind = FulfillmentEvidenceKinds.Rejected });
        Assert.Equal("IncurredCostUnknown", result.Value.ReviewCode); Assert.Null(result.Value.TotalCostKrw);
        Assert.Equal(1, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
    }

    [Fact]
    public async Task 호출직전철회경합은모의접수sideeffect를만들지않는다()
    {
        await using var f = await Fixture.CreateAsync(); var consent = f.Consent(); await f.Service.사전동의저장Async(f.Key, "owner", consent);
        f.Local.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.Local.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var send = f.Service.선정실행Async(f.Key, Guid.NewGuid()); await f.Local.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var otherDb = f.OpenDb(); var other = f.NewService(otherDb);
        var revoked = await other.사전동의저장Async(f.Key, "owner", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1, Revoked = true });
        Assert.True(revoked.IsSuccess); f.Local.Release.TrySetResult(); await send;
        Assert.Equal(0, await otherDb.FulfillmentMockJobs.CountAsync());
        var stopped = await other.선정실행Async(f.Key, Guid.NewGuid());
        Assert.Equal("ConsentRevoked", stopped.Value.ReviewCode); Assert.Equal(0, await otherDb.FulfillmentMockJobs.CountAsync());
    }

    [Fact]
    public async Task 두SQL연결의동시선정은동일current와한모의job만남긴다()
    {
        await using var f = await Fixture.CreateAsync(); await f.Service.사전동의저장Async(f.Key, "owner", f.Consent());
        var barrier = new InitialTransactionBarrier(); await using var firstDb = f.OpenDb(barrier); await using var secondDb = f.OpenDb(barrier);
        var first = f.NewService(firstDb); var second = f.NewService(secondDb);
        var results = await Task.WhenAll(Task.Run(() => first.선정실행Async(f.Key, Guid.NewGuid())), Task.Run(() => second.선정실행Async(f.Key, Guid.NewGuid())));
        Assert.All(results, x => Assert.True(x.IsSuccess));
        await using var inspection = f.OpenDb(); Assert.Equal(1, await inspection.NeighborhoodFulfillmentAttempts.CountAsync());
        Assert.Equal(1, await inspection.FulfillmentMockJobs.CountAsync()); Assert.Equal(1, await inspection.NeighborhoodFulfillments.CountAsync());
    }

    private static string? Code(Result<AutoFulfillmentSnapshot> result) => result.Errors.FirstOrDefault()?.Metadata.GetValueOrDefault("ErrorCode") as string;

    [Fact]
    public async Task 원천만저장된요청은미확정조건으로표시하고새명시동의로회복한다()
    {
        await using var f = await Fixture.CreateAsync();
        var pending = await f.Service.상태Async(f.Key);
        Assert.NotNull(pending); Assert.Equal("InitialConsentRequired", pending.ReviewCode);
        Assert.Equal(0, pending.ConsentRevision); Assert.True(pending.CanRenewConsent);
        Assert.Equal(0, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
        await f.StartAsync(); Assert.Equal(AutoFulfillmentStates.Accepted, (await f.Service.상태Async(f.Key))!.StateCode);
    }

    [Fact]
    public async Task 음식미확정동의의지원제안은현재설정을반환하고확인뒤에만주문에동결한다()
    {
        await using var f = await Fixture.CreateAsync(); var key = new FulfillmentSourceKey(FulfillmentSourceKinds.FoodOrder, "food-pending");
        f.Food.Snapshot = f.Source.Snapshot with { Key = key, RestaurantId = 7, WeightKg = null };
        f.Db.RestaurantFulfillmentSupports.Add(new() { RestaurantId = 7, Enabled = true, Revision = 3, FixedContributionKrw = 4000 }); await f.Db.SaveChangesAsync();
        var pending = (await f.Service.상태Async(key))!;
        Assert.Equal(0, pending.ConsentRevision); Assert.Equal(4000, pending.FrozenRestaurantSupportKrw); Assert.Equal(3, pending.RestaurantSupportRevision);
        Assert.Equal(0, await f.Db.AutoFulfillmentConsents.CountAsync());
        var request = f.Consent(); request.ConfirmedRestaurantSupportKrw = pending.FrozenRestaurantSupportKrw; request.ConfirmedRestaurantSupportRevision = pending.RestaurantSupportRevision;
        var accepted = await f.Service.사전동의저장Async(key, "owner", request);
        Assert.True(accepted.IsSuccess); Assert.Equal(4000, accepted.Value.FrozenRestaurantSupportKrw); Assert.Equal(1, accepted.Value.ConsentRevision);
    }

    [Theory]
    [InlineData(AutoFulfillmentStates.CancelPending)]
    [InlineData(AutoFulfillmentStates.NeedsReview)]
    public async Task 현재원청수행이중단되면이전internalAccepted로권위를부여하지않는다(string state)
    {
        await using var f = await Fixture.CreateAsync(); var consent = f.Consent(); consent.AllowedProviderIds = ["internal"];
        await f.Service.사전동의저장Async(f.Key, "owner", consent); await f.Service.선정실행Async(f.Key, Guid.NewGuid());
        Assert.True(await f.Service.내부수행허용Async(f.Key));
        var current = await f.Db.NeighborhoodFulfillments.SingleAsync(); current.StateCode = state; current.Revision++; await f.Db.SaveChangesAsync();
        Assert.False(await f.Service.내부수행허용Async(f.Key));
    }

    /// <summary>공유 SQLite fixture. 테스트 source port는 배차와 음식 원천 업무 자체를 대신 증명하지 않습니다.</summary>
    public sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection keeper; private readonly string cs;
        public SsalddelContext Db { get; } public Clock Clock { get; } = new();
        public TestSource Source { get; } public TestSource Food { get; }
        public RecordingProvider Local { get; private set; } = null!;
        public FulfillmentSourceKey Key { get; } = new(FulfillmentSourceKinds.NeighborhoodDelivery, "source-1");
        public 자동수행Coordinator Service => NewService(Db);
        private Fixture(SqliteConnection keeper, string cs, SsalddelContext db)
        {
            this.keeper = keeper; this.cs = cs; Db = db;
            Source = new(new(Key, "owner", 1, "digest", false, true, false, true, 3, 10000, Clock.Now, WeightKg: 2, TravelMinutes: 9));
            Food = new(Source.Snapshot with { Key = new(FulfillmentSourceKinds.FoodOrder, "none"), WeightKg = null });
            Local = new(new 모의자동수행Provider(db, Clock, "mock-local", "모의 생활", .85m, 30, 10));
        }
        public static async Task<Fixture> CreateAsync(bool schema = true)
        {
            var cs = "Data Source=auto-" + Guid.NewGuid().ToString("N") + ";Mode=Memory;Cache=Shared";
            var keeper = new SqliteConnection(cs); await keeper.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(cs).Options, new Encryption());
            if (schema) await db.Database.EnsureCreatedAsync(); return new(keeper, cs, db);
        }
        public SsalddelContext OpenDb(IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(cs);
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return new(builder.Options, new Encryption());
        }
        public 자동수행Coordinator NewService(SsalddelContext db, bool enabled = true, SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation)
        {
            var inner = new 모의자동수행Provider(db, Clock, "mock-local", "모의 생활", .85m, 30, 10);
            if (db == Db) Local ??= new(inner);
            I자동수행Provider local = db == Db ? Local : new 모의자동수행Provider(db, Clock, "mock-local", "모의 생활", .85m, 30, 10);
            return new(db, [Source, Food], [local, new 모의자동수행Provider(db, Clock, "mock-quick", "모의 퀵", 1.15m, 20, 20), new 자체자동수행Provider(Clock)],
                Options.Create(new FulfillmentRoutingOptions { Enabled = enabled }), new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })), Clock);
        }
        public AutoFulfillmentConsent Consent() => new() { ClientRequestId = Guid.NewGuid(), MaxUserChargeKrw = 20000,
            AllowedProviderIds = ["internal", "mock-quick", "mock-local"], AutomaticSubmissionAgreed = true, ProviderInformationSharingAgreed = true, DirectPaymentAgreed = true };
        public async Task StartAsync() { Assert.True((await Service.사전동의저장Async(Key, "owner", Consent())).IsSuccess); Assert.True((await Service.선정실행Async(Key, Guid.NewGuid())).IsSuccess); }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await keeper.DisposeAsync(); }
    }
    public sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2080, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    public sealed class TestSource(FulfillmentSourceSnapshot snapshot) : I수행원천Adapter
    {
        public FulfillmentSourceSnapshot Snapshot { get; set; } = snapshot; public string SourceKind => Snapshot.Key.SourceKind;
        public TaskCompletionSource? ReadEntered; public TaskCompletionSource? ReadRelease;
        public async Task<FulfillmentSourceSnapshot?> 읽기Async(FulfillmentSourceKey key, CancellationToken ct)
        {
            ReadEntered?.TrySetResult(); if (ReadRelease is not null) await ReadRelease.Task.WaitAsync(ct);
            return key == Snapshot.Key ? Snapshot : null;
        }
        public Task<FulfillmentExecutionResult> 내부접수Async(FulfillmentAttemptContext a, CancellationToken ct) => Task.FromResult(new FulfillmentExecutionResult(AutoFulfillmentStates.Accepted));
        public Task<FulfillmentExecutionResult> 내부조회Async(FulfillmentAttemptContext a, CancellationToken ct) => Task.FromResult(new FulfillmentExecutionResult(AutoFulfillmentStates.Accepted));
        public Task<FulfillmentExecutionResult> 내부취소Async(FulfillmentAttemptContext a, CancellationToken ct) => Task.FromResult(new FulfillmentExecutionResult(AutoFulfillmentStates.Cancelled, ConfirmedIncurredCostKrw: 0));
        public Task<Result> 외부증거반영Async(FulfillmentExecutionEvidence e, CancellationToken ct) => Task.FromResult(Result.Ok());
    }
    public sealed class RecordingProvider(I자동수행Provider inner) : I자동수행Provider
    {
        public string Id => inner.Id; public string Name => inner.Name; public int Submits; public bool LoseResponse;
        public TaskCompletionSource? Entered; public TaskCompletionSource? Release;
        public Task<AutoFulfillmentQuote> 견적Async(FulfillmentSourceSnapshot source, DateTime? deadline, CancellationToken ct) => inner.견적Async(source, deadline, ct);
        public async Task<FulfillmentExecutionResult> 접수Async(FulfillmentAttemptContext a, I수행원천Adapter source, CancellationToken ct)
        {
            Interlocked.Increment(ref Submits); Entered?.TrySetResult(); if (Release is not null) await Release.Task;
            var result = await inner.접수Async(a, source, ct); if (LoseResponse) throw new HttpRequestException("response lost"); return result;
        }
        public Task<FulfillmentExecutionResult> 조회Async(FulfillmentAttemptContext a, I수행원천Adapter source, CancellationToken ct) => inner.조회Async(a, source, ct);
        public Task<FulfillmentExecutionResult> 취소Async(FulfillmentAttemptContext a, I수행원천Adapter source, CancellationToken ct) => inner.취소Async(a, source, ct);
    }
    public sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class InitialTransactionBarrier : DbTransactionInterceptor
    {
        private readonly HashSet<DbConnection> connections = []; private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            lock (connections) { if (!connections.Add(connection)) return result; if (connections.Count == 2) released.TrySetResult(); }
            await released.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); return result;
        }
    }
}
