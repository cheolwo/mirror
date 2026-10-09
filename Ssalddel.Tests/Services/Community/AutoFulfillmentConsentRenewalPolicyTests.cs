using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Services.Fulfillment;
using static Ssalddel.Tests.Services.Community.AutoFulfillmentCoordinatorTests;

namespace Ssalddel.Tests.Services.Community;

public sealed class AutoFulfillmentConsentRenewalPolicyTests
{
    [Theory]
    [InlineData(AutoFulfillmentStates.Failed)]
    [InlineData(AutoFulfillmentStates.Cancelled)]
    public async Task 조리전확정종료뒤에만조건을갱신하며원주문분담정책은유지한다(string attemptState)
    {
        await using var f = await Fixture.CreateAsync();
        var key = await StartFood(f);
        await EndAttempt(f, attemptState);
        var before = (await f.Service.상태Async(key))!;
        Assert.True(before.CanRenewConsent);
        Assert.Null(before.ConsentRenewalLockedReasonCode);
        var request = Consent(f, before.ConsentRevision);
        request.MaxUserChargeKrw = 15000;

        var renewed = await f.Service.사전동의저장Async(key, "owner", request);

        Assert.True(renewed.IsSuccess);
        Assert.Equal(before.ConsentRevision + 1, renewed.Value.ConsentRevision);
        Assert.Equal(15000, renewed.Value.MaxUserChargeKrw);
        Assert.Equal(1200, renewed.Value.FrozenRestaurantSupportKrw);
        Assert.Equal(1, renewed.Value.RestaurantSupportRevision);
        Assert.Equal(before.CostSharingPolicy!.Mode, renewed.Value.CostSharingPolicy!.Mode);
        Assert.Single(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
    }

    [Theory]
    [InlineData(AutoFulfillmentStates.ResultUnknown, "FulfillmentActive")]
    [InlineData(AutoFulfillmentStates.CancelPending, "FulfillmentActive")]
    [InlineData(AutoFulfillmentStates.Completed, "FulfillmentCompleted")]
    [InlineData("unsupported-provider-state", "FulfillmentStateUnknown")]
    [InlineData("", "FulfillmentStateUnknown")]
    public async Task 진행중또는미지원상태의조회와동의저장은같은사유로잠그고기존조건을보존한다(string attemptState, string reason)
    {
        await using var f = await Fixture.CreateAsync();
        var key = await StartFood(f);
        await EndAttempt(f, attemptState);
        var before = (await f.Service.상태Async(key))!;
        Assert.False(before.CanRenewConsent);
        Assert.Equal(reason, before.ConsentRenewalLockedReasonCode);
        var request = Consent(f, before.ConsentRevision);
        request.MaxUserChargeKrw = 90000;

        var result = await f.Service.사전동의저장Async(key, "owner", request);

        Assert.Equal(reason, ErrorCode(result));
        var stored = await f.Db.AutoFulfillmentConsents.AsNoTracking().SingleAsync();
        Assert.Equal(before.ConsentRevision, stored.Revision);
        Assert.Equal(before.MaxUserChargeKrw, stored.MaxUserChargeKrw);
        Assert.Equal(1200, stored.FrozenRestaurantSupportKrw);
        Assert.Single(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
    }

    [Fact]
    public async Task 종료시도가있어도현재수행상태가미확인이면동의판본을새로만들지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var key = await StartFood(f);
        await EndAttempt(f, AutoFulfillmentStates.Failed);
        var current = await f.Db.NeighborhoodFulfillments.SingleAsync();
        current.StateCode = "unsupported-current-state";
        await f.Db.SaveChangesAsync();
        var before = (await f.Service.상태Async(key))!;

        var result = await f.Service.사전동의저장Async(key, "owner", Consent(f, before.ConsentRevision));

        Assert.False(before.CanRenewConsent);
        Assert.Equal("FulfillmentStateUnknown", before.ConsentRenewalLockedReasonCode);
        Assert.Equal(before.ConsentRenewalLockedReasonCode, ErrorCode(result));
        Assert.Equal(before.ConsentRevision, (await f.Db.AutoFulfillmentConsents.SingleAsync()).Revision);
    }

    [Fact]
    public async Task 최초동의가없어도조리가시작된음식원천의조건확정을허용하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var key = new FulfillmentSourceKey(FulfillmentSourceKinds.FoodOrder, "already-cooking");
        f.Food.Snapshot = f.Food.Snapshot with { Key = key, HasCookingStarted = true };
        var shown = (await f.Service.상태Async(key))!;

        var result = await f.Service.사전동의저장Async(key, "owner", f.Consent());

        Assert.False(shown.CanRenewConsent);
        Assert.Equal("CookingAlreadyStarted", shown.ConsentRenewalLockedReasonCode);
        Assert.Equal(shown.ConsentRenewalLockedReasonCode, ErrorCode(result));
        Assert.Empty(await f.Db.AutoFulfillmentConsents.ToListAsync());
        Assert.Empty(await f.Db.NeighborhoodFulfillments.ToListAsync());
    }

    private static async Task<FulfillmentSourceKey> StartFood(Fixture f)
    {
        var key = new FulfillmentSourceKey(FulfillmentSourceKinds.FoodOrder, "food-renewal");
        f.Food.Snapshot = f.Source.Snapshot with { Key = key, RestaurantId = 7, WeightKg = null };
        f.Db.RestaurantFulfillmentSupports.Add(new() { RestaurantId = 7, Enabled = true, Revision = 1, FixedContributionKrw = 1200 });
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Service.사전동의저장Async(key, "owner", Consent(f, 0))).IsSuccess);
        Assert.True((await f.Service.선정실행Async(key, Guid.NewGuid())).IsSuccess);
        return key;
    }

    private static AutoFulfillmentConsent Consent(Fixture f, long revision)
    {
        var request = f.Consent();
        request.ExpectedRevision = revision;
        request.AllowedProviderIds = ["mock-local"];
        request.ConfirmedRestaurantSupportKrw = 1200;
        request.ConfirmedRestaurantSupportRevision = 1;
        return request;
    }

    private static async Task EndAttempt(Fixture f, string state)
    {
        var attempt = await f.Db.NeighborhoodFulfillmentAttempts.SingleAsync();
        attempt.StateCode = state;
        attempt.ConfirmedIncurredCostKrw = 0;
        var current = await f.Db.NeighborhoodFulfillments.SingleAsync();
        current.StateCode = AutoFulfillmentStates.NeedsReview;
        current.Revision++;
        await f.Db.SaveChangesAsync();
    }

    private static string? ErrorCode(Result<AutoFulfillmentSnapshot> result)
        => result.Errors.FirstOrDefault()?.Metadata.GetValueOrDefault("ErrorCode") as string;
}
