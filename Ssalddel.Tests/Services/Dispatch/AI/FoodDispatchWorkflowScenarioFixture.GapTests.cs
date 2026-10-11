using System.Net;
using System.Text;
using System.Text.Json;

namespace Ssalddel.Tests.Services.Dispatch.AI;

/// <summary>기존 알림 복구 결손을 재현한다. 결손이 수정됐다고 인증하는 시험이 아니다.</summary>
public sealed class FoodDispatchWorkflowNotificationGapTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("fixture-orderer", HttpStatusCode.Forbidden)]
    [InlineData("unknown-Bearer", HttpStatusCode.Unauthorized)]
    public async Task DriverAcceptHttp_시험인증과기사역할관문은_권한없는수락을저장하지않는다(
        string? token, HttpStatusCode expected)
    {
        using var model = new HttpClient(new ModelResponse());
        await using var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", model);
        await fixture.RecommendAsync();
        var before = await fixture.ReadSnapshotAsync();
        using var unauthorized = fixture.CreateActorClient(token);
        using var response = await unauthorized.PostAsync(
            $"api/v1/driver/food-deliveries/offers/{fixture.OfferId}/accept", null);
        Assert.Equal(expected, response.StatusCode);
        var after = await fixture.ReadSnapshotAsync();
        Assert.Equal(0, after.AttemptCount);
        Assert.Null(after.ConfirmedDriverId);
        Assert.Equal(before.RecommendedDriverId, after.RecommendedDriverId);
        Assert.Equal(before.FrozenFee, after.FrozenFee);
        Assert.Equal(before.RecommendationRound, after.RecommendationRound);
    }

    [Fact]
    public async Task Gap_추천저장후알림Outbox생성실패는_추천재시도로복구되지않는다()
    {
        using var model = new HttpClient(new ModelResponse());
        await using var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", model);
        fixture.ThrowNotificationCreation = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.RecommendAsync());
        var failed = await fixture.ReadSnapshotAsync();
        Assert.NotNull(failed.RecommendedDriverId);
        Assert.Equal(1, failed.RecommendationRound);
        Assert.Equal(0, failed.NotificationOutboxCount);
        fixture.ThrowNotificationCreation = false;
        await fixture.RetryRecommendationAsync();
        await fixture.DispatchPendingNotificationsAsync();
        var retried = await fixture.ReadSnapshotAsync();
        Assert.Equal(0, retried.NotificationOutboxCount);
        Assert.Equal(failed.RecommendedDriverId, retried.RecommendedDriverId);
        Assert.Equal(failed.FrozenFee, retried.FrozenFee);
        Assert.Equal(0, retried.AttemptCount);
        Assert.Empty(fixture.PushMessages);
        Assert.Equal(1, fixture.ModelRequests);
    }

    [Fact]
    public async Task Gap_토큰없음Failed는_토큰복구후Pending처리나같은라운드예약으로재발송되지않는다()
    {
        using var model = new HttpClient(new ModelResponse());
        await using var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", model);
        await fixture.RecommendAsync();
        await fixture.SetPushInstallationActiveAsync(false);
        await fixture.DispatchPendingNotificationsAsync();
        Assert.Equal(["Failed"], (await fixture.ReadSnapshotAsync()).NotificationOutboxStatuses);
        Assert.Empty(fixture.PushMessages);
        await fixture.SetPushInstallationActiveAsync(true);
        await fixture.RetryNotificationRequestAsync();
        await fixture.DispatchPendingNotificationsAsync();
        var recovered = await fixture.ReadSnapshotAsync();
        Assert.Equal(1, recovered.NotificationOutboxCount);
        Assert.Equal(["Failed"], recovered.NotificationOutboxStatuses);
        Assert.Empty(fixture.PushMessages);
        using var driver = fixture.CreateActorClient(fixture.AccessToken);
        using var current = await driver.GetAsync("api/v1/driver/food-deliveries/workspace/current");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        using var document = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.Single(document.RootElement.GetProperty("recommendations").EnumerateArray());
    }

    [Fact]
    public async Task Gap_일시FCM실패Failed는_전송복구후같은라운드를재발송하지않는다()
    {
        using var model = new HttpClient(new ModelResponse());
        await using var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", model);
        await fixture.RecommendAsync(); fixture.PushSucceeded = false;
        await fixture.DispatchPendingNotificationsAsync();
        Assert.Single(fixture.PushMessages);
        Assert.Equal(["Failed"], (await fixture.ReadSnapshotAsync()).NotificationOutboxStatuses);
        fixture.PushSucceeded = true;
        await fixture.RetryNotificationRequestAsync();
        await fixture.DispatchPendingNotificationsAsync();
        Assert.Single(fixture.PushMessages);
        Assert.Equal(["Failed"], (await fixture.ReadSnapshotAsync()).NotificationOutboxStatuses);
        Assert.Equal(1, (await fixture.ReadSnapshotAsync()).RecommendationRound);
    }

    private sealed class ModelResponse : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { model = "qwen3:1.7b", done = true,
                message = new { role = "assistant", content = "{\"candidateToken\":\"C1\"}" } }), Encoding.UTF8, "application/json") });
    }
}
