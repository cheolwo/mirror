using System.Net;
using System.Net.Http.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverDeliveryExperienceTests
{
    [Fact]
    public async Task 공유설정은기본꺼짐이며현재사용자의별도설정만조회한다()
    {
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            Assert.Equal("/api/v1/driver/preferences/delivery-experience-sharing", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Ok(Preference()));
        });
        var model = Model(handler, new());
        Assert.False(model.ShareDeliveryExperience); Assert.False(model.ExperienceSharingLoaded);
        await model.LoadExperienceSharingCommand.ExecuteAsync(null);
        Assert.True(model.ExperienceSharingLoaded); Assert.True(model.CanEditExperienceSharing);
        Assert.False(model.ShareDeliveryExperience); Assert.Contains("꺼져", model.ExperienceSharingNotice);
        Assert.Single(handler.Paths);
    }

    [Fact]
    public async Task 공유저장은확인한판본을보내고서버재조회후적용한다()
    {
        var current = Preference(); var reads = 0;
        DeliveryExperienceSharingPreferenceRequest? written = null;
        var handler = new FDriverTestHttpHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                written = await request.Content!.ReadFromJsonAsync<DeliveryExperienceSharingPreferenceRequest>(token);
                current = Preference(written!.ShareWithCurrentParticipants, written.ExpectedRevision + 1);
            }
            else reads++;
            return Ok(current);
        });
        var model = Model(handler, new()); await model.LoadExperienceSharingCommand.ExecuteAsync(null);
        model.ShareDeliveryExperience = true;
        await model.SaveExperienceSharingCommand.ExecuteAsync(null);
        Assert.Equal(4, written!.ExpectedRevision); Assert.True(written.ShareWithCurrentParticipants);
        Assert.True(model.ShareDeliveryExperience); Assert.True(model.ExperienceSharingLoaded); Assert.Equal(2, reads);
        Assert.False(model.IsOnDuty); Assert.False(model.HasCurrentLocation);
        Assert.All(handler.Paths, path => Assert.EndsWith("/delivery-experience-sharing", path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장결과불명확시켜짐으로표시하지않고재조회전다시쓰지않는다(bool conflict)
    {
        var reads = 0; var writes = 0;
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                writes++;
                if (conflict) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));
                return Task.FromResult(Ok(Preference(true, 5)));
            }
            if (++reads > 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            return Task.FromResult(Ok(Preference()));
        });
        var model = Model(handler, new()); await model.LoadExperienceSharingCommand.ExecuteAsync(null);
        model.ShareDeliveryExperience = true; await model.SaveExperienceSharingCommand.ExecuteAsync(null);
        Assert.False(model.ExperienceSharingLoaded); Assert.False(model.ShareDeliveryExperience);
        Assert.Contains("다시 조회", model.ExperienceSharingNotice);
        await model.SaveExperienceSharingCommand.ExecuteAsync(null); Assert.Equal(1, writes);
    }

    [Fact]
    public async Task 서버기능이꺼져있으면공유설정변경을허용하지않는다()
    {
        var settings = Preference(); settings.Enabled = false;
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(Ok(settings)));
        var model = Model(handler, new()); await model.LoadExperienceSharingCommand.ExecuteAsync(null);
        Assert.False(model.CanEditExperienceSharing);
        await model.SaveExperienceSharingCommand.ExecuteAsync(null); Assert.Single(handler.Paths);
        Assert.Contains("아직 제공되지", model.ExperienceSharingNotice);
    }

    [Fact]
    public async Task 계정변경중늦게도착한공유설정은다른계정에적용하지않는다()
    {
        var session = new FDriverTestSession();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FDriverTestHttpHandler((_, _) => { entered.TrySetResult(); return reply.Task; });
        var model = Model(handler, session); var load = model.LoadExperienceSharingCommand.ExecuteAsync(null);
        await entered.Task;
        await session.ApplyAsync(new ClientAuthTokenSnapshot("other-access", DateTime.UtcNow.AddMinutes(30), "other-refresh",
            DateTime.UtcNow.AddHours(1), "other-driver", "다른 기사", ["Driver"]));
        reply.SetResult(Ok(Preference(true, 5))); await load;
        Assert.False(model.ExperienceSharingLoaded); Assert.False(model.ShareDeliveryExperience);
        Assert.False(model.ExperienceSharingBusy); Assert.False(model.CanEditExperienceSharing);
    }

    [Fact]
    public void 현재단계에맞는자기경험만표시하고미확인건수는0으로대체하지않는다()
    {
        var model = FDriverLifecycleTestSupport.Model(new(), new()); model.IsAuthenticated = true;
        var source = Active(); source.ExperienceSummaries = [Summary("PickupCompleted", "Restaurant", 4),
            Summary("DeliveryCompleted", "Road", 12), Summary("DeliveryCompleted", "ApartmentComplex", 8)];
        model.ActiveDelivery = ActiveDeliveryPreview.From(source);
        Assert.Contains("픽업 완료 4건", model.PickupExperienceText); Assert.True(model.HasPickupExperience);
        Assert.False(model.HasDestinationExperience); Assert.DoesNotContain("번째", model.PickupExperienceText);
        model.ActiveDelivery = model.ActiveDelivery with { WorkStatus = DriverWorkOfferStatus.MovingToDropoff };
        Assert.False(model.HasPickupExperience); Assert.Contains("12건", model.DestinationExperienceText);
        Assert.Contains("8건", model.DestinationExperienceSecondText);
        var unknown = Summary("DeliveryCompleted", "Road", null); unknown.EvidenceStatusCode = "Unknown";
        model.ActiveDelivery = model.ActiveDelivery with { ExperienceSummaries = [unknown] };
        Assert.False(model.HasDestinationExperience); Assert.False(model.HasDestinationSecondExperience);
        Assert.Empty(model.DestinationExperienceText);
        model.ActiveDelivery = null;
        Assert.False(model.HasPickupExperience); Assert.False(model.HasDestinationExperience);
    }

    [Fact]
    public async Task 최신업무조회실패는기존수행카드를유지하되경험요약을숨긴다()
    {
        var source = Active(); source.ExperienceSummaries = [Summary("PickupCompleted", "Restaurant", 4)];
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto { ActiveDeliveries = [source] }) };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        try
        {
            await model.InitializeAsync(); Assert.True(model.HasPickupExperience);
            api.Workspace = _ => throw new FDriverApiException("최신 조회 실패", HttpStatusCode.ServiceUnavailable);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.NotNull(model.ActiveDelivery); Assert.False(model.HasPickupExperience);
            api.Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto { ActiveDeliveries = [source] });
            await FDriverLifecycleTestSupport.RefreshBackground(model); Assert.True(model.HasPickupExperience);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public void 업무접기는경험과기존지도위치를변경하지않는다()
    {
        var model = FDriverLifecycleTestSupport.Model(new(), new()); model.IsAuthenticated = true;
        var source = Active(); source.ExperienceSummaries = [Summary("PickupCompleted", "Restaurant", 4)];
        model.ActiveDelivery = ActiveDeliveryPreview.From(source);
        model.MapCenterLatitude = 37.55; model.MapCenterLongitude = 127.05; model.MapZoom = 16;
        var changes = new List<string>();
        model.PropertyChanged += (_, args) => { if (args.PropertyName?.StartsWith("Map") == true) changes.Add(args.PropertyName); };
        var text = model.PickupExperienceText;
        model.ToggleCurrentDeliveryCardCommand.Execute(null); model.ToggleCurrentDeliveryCardCommand.Execute(null);
        Assert.Equal(text, model.PickupExperienceText); Assert.Empty(changes);
        Assert.Equal(37.55, model.MapCenterLatitude); Assert.Equal(127.05, model.MapCenterLongitude); Assert.Equal(16, model.MapZoom);
    }

    private static DeliveryExperienceSharingPreferenceDto Preference(bool shared = false, long revision = 4)
        => new() { Enabled = true, ShareWithCurrentParticipants = shared, Revision = revision, UpdatedAtUtc = DateTime.UtcNow };
    private static HttpResponseMessage Ok(DeliveryExperienceSharingPreferenceDto value)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static MainPageModel Model(FDriverTestHttpHandler handler, FDriverTestSession session)
    {
        var http = new HttpClient(handler) { BaseAddress = new("https://local.invalid/") }; var auth = new FDriverAuthApiService(http, session);
        return new(new(), session, auth, new FoodDeliveryDriverApiService(http, session, auth), new FDriverTestLocationService(), new())
            { IsAuthenticated = true };
    }
    private static DeliveryExperienceSummaryDto Summary(string metric, string place, long? count)
        => new() { MetricCode = metric, PlaceKind = place, CompletedCount = count, EvidenceStatusCode = "Confirmed",
            PlaceDisplayName = place == "Road" ? "면목로" : place == "ApartmentComplex" ? "확인 단지" : "확인 지점",
            AsOfUtc = DateTime.UtcNow.AddMinutes(-1), ExecutionModeCode = "Operational" };
    private static FoodDeliveryDriverActiveDeliveryDto Active()
        => new() { TransportId = 1, OfferId = "active", OrderNo = "food-order", DeliveryAttemptId = "attempt", AttemptRevision = 1,
            RestaurantName = "확인 지점", WorkStatus = DriverWorkOfferStatus.Accepted, DriverPayout = 4200,
            Pickup = new() { Address = "픽업 주소" }, Dropoff = new() { Address = "전달 주소" } };
}
