using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using MudBlazor;
using Ssalddel.Contracts.Admin.Restaurants;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

public sealed class FoodOperationsPageStateTests
{
    [Fact]
    public async Task 세조회는_순차실행하고_마지막응답전에는_정책과리뷰를발행하지않는다()
    {
        var calls = new List<string>();
        var pricing = Pending<음식배달요금정책응답>();
        var items = Pending<음식점리뷰관리목록응답>();
        var policy = Pending<음식점리뷰운영정책응답>();
        var itemsStarted = Pending<bool>();
        var policyStarted = Pending<bool>();
        var state = new 음식운영PageState(
            () => { calls.Add("pricing"); return pricing.Task; },
            new(
                () => { calls.Add("items"); itemsStarted.SetResult(true); return items.Task; },
                () => { calls.Add("review-policy"); policyStarted.SetResult(true); return policy.Task; }),
            _ => throw new InvalidOperationException("조회에서 저장하면 안 됩니다."));
        var originalPricing = state.Pricing.Policy;
        var originalEditor = state.Pricing.DriverPricing;
        var originalReviews = state.Reviews;

        var loading = state.LoadAsync();
        Assert.Equal(["pricing"], calls);
        Assert.True(state.IsLoading);
        Assert.False(state.HasLoadedData);

        var loadedPricing = new 음식배달요금정책응답 { BaseFee = 4567m };
        pricing.SetResult(loadedPricing);
        await itemsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["pricing", "items"], calls);
        Assert.Same(originalPricing, state.Pricing.Policy);
        Assert.Same(originalEditor, state.Pricing.DriverPricing);
        Assert.Same(originalReviews, state.Reviews);

        var loadedItems = new 음식점리뷰관리목록응답 { Items = [new() { 리뷰Id = 17 }] };
        items.SetResult(loadedItems);
        await policyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["pricing", "items", "review-policy"], calls);
        Assert.Same(originalPricing, state.Pricing.Policy);
        Assert.Same(originalReviews, state.Reviews);
        Assert.False(state.HasLoadedData);

        var loadedPolicy = new 음식점리뷰운영정책응답 { 기본저평점게시일수 = 7 };
        policy.SetResult(loadedPolicy);
        await loading;
        Assert.Same(loadedPricing, state.Pricing.Policy);
        Assert.NotSame(originalEditor, state.Pricing.DriverPricing);
        Assert.Same(loadedItems, state.Reviews.Items);
        Assert.Same(loadedPolicy, state.Reviews.Policy);
        Assert.True(state.HasLoadedData);
        Assert.False(state.IsLoading);
        Assert.Null(state.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task 어느조회든실패하면_이전세객체를유지하고_편집저장을공동차단한다(int failureStage)
    {
        var failing = false;
        var calls = new List<int>();
        var saveCount = 0;
        Task<T> Read<T>(int stage, T value)
        {
            calls.Add(stage);
            return failing && stage == failureStage
                ? Task.FromException<T>(new InvalidOperationException($"stage-{stage}"))
                : Task.FromResult(value);
        }
        var state = new 음식운영PageState(
            () => Read(1, new 음식배달요금정책응답()),
            new(() => Read(2, new 음식점리뷰관리목록응답()), () => Read(3, new 음식점리뷰운영정책응답())),
            value => { saveCount++; return Task.FromResult(value); });
        await state.LoadAsync();
        var originalPricing = state.Pricing.Policy;
        var originalEditor = state.Pricing.DriverPricing;
        var originalReviews = state.Reviews;

        failing = true;
        calls.Clear();
        await state.LoadAsync();
        await state.SavePricingPolicyAsync();

        Assert.Equal(Enumerable.Range(1, failureStage), calls);
        Assert.Same(originalPricing, state.Pricing.Policy);
        Assert.Same(originalEditor, state.Pricing.DriverPricing);
        Assert.Same(originalReviews, state.Reviews);
        Assert.False(state.IsLoading);
        Assert.False(state.HasLoadedData);
        Assert.False(state.IsSaving);
        Assert.Equal(0, saveCount);
        Assert.Equal(Severity.Error, state.MessageSeverity);
        Assert.Equal($"음식 운영 정보를 불러오지 못했습니다: stage-{failureStage}", state.Message);

        failing = false;
        await state.LoadAsync();
        Assert.True(state.HasLoadedData);
        Assert.Null(state.Message);
    }

    [Fact]
    public async Task 최초조회전과조회중저장은차단하고_저장중중복요청은보내지않는다()
    {
        var pricing = Pending<음식배달요금정책응답>();
        var saved = Pending<음식배달요금정책응답>();
        var saveCount = 0;
        var state = new 음식운영PageState(
            () => pricing.Task,
            new(() => Task.FromResult(new 음식점리뷰관리목록응답()), () => Task.FromResult(new 음식점리뷰운영정책응답())),
            _ => { saveCount++; return saved.Task; });

        await state.SavePricingPolicyAsync();
        Assert.Equal(0, saveCount);
        var loading = state.LoadAsync();
        await state.SavePricingPolicyAsync();
        Assert.Equal(0, saveCount);
        pricing.SetResult(new());
        await loading;

        var saving = state.SavePricingPolicyAsync();
        Assert.True(state.IsSaving);
        Assert.Null(state.Message);
        await state.SavePricingPolicyAsync();
        Assert.Equal(1, saveCount);
        saved.SetResult(new());
        await saving;
        Assert.False(state.IsSaving);
        Assert.Equal(Severity.Success, state.MessageSeverity);
        Assert.Equal("배달 요금 정책을 저장했습니다. 이후 새 제안부터 적용되며 기존 제안 금액은 유지됩니다.", state.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 현재정책의_픽업배분과숨은필드를저장하고_서버반환정책으로편집기를교체한다(bool split)
    {
        var updatedAt = new DateTime(2026, 10, 20, 1, 2, 3, DateTimeKind.Utc);
        var draft = new 음식배달요금정책응답
        {
            DriverBasePayout = 2500m, DriverPickupPayout = split ? 600m : null,
            DriverWeatherSurchargeEnabled = false, DriverWeatherSurcharge = 1234m,
            DriverWeatherSurchargePolicyRevision = "retained-r9", UpdatedAtUtc = updatedAt, UpdatedByUserId = "previous-admin"
        };
        var returned = new 음식배달요금정책응답
        {
            BaseFee = 3300m, DriverBasePayout = 2700m, DriverPickupPayout = 800m,
            DriverWeatherSurchargeEnabled = true, DriverWeatherSurcharge = 900m,
            DriverWeatherSurchargePolicyRevision = "server-r10", UpdatedAtUtc = updatedAt.AddMinutes(1), UpdatedByUserId = "server-admin"
        };
        음식배달요금정책응답? sent = null;
        var state = Create(draft, value => { sent = value; return Task.FromResult(returned); });
        await state.LoadAsync();
        var editor = state.Pricing.DriverPricing;
        draft.BaseFee = 3150m;
        if (split)
            editor.PickupFee = 700m;
        var reviews = state.Reviews;

        await state.SavePricingPolicyAsync();

        Assert.Same(draft, sent);
        Assert.Equal(3150m, sent!.BaseFee);
        Assert.Equal(split ? 700m : (decimal?)null, sent.DriverPickupPayout);
        Assert.Equal(split ? 2600m : 2500m, sent.DriverBasePayout);
        Assert.False(sent.DriverWeatherSurchargeEnabled);
        Assert.Equal(1234m, sent.DriverWeatherSurcharge);
        Assert.Equal("retained-r9", sent.DriverWeatherSurchargePolicyRevision);
        Assert.Equal(updatedAt, sent.UpdatedAtUtc);
        Assert.Equal("previous-admin", sent.UpdatedByUserId);
        Assert.Same(returned, state.Pricing.Policy);
        Assert.NotSame(editor, state.Pricing.DriverPricing);
        Assert.Equal(800m, state.Pricing.DriverPricing.PickupFee);
        Assert.Equal(1900m, state.Pricing.DriverPricing.DropoffFee);
        Assert.Equal("server-r10", state.Pricing.Policy.DriverWeatherSurchargePolicyRevision);
        Assert.Equal("server-admin", state.Pricing.Policy.UpdatedByUserId);
        Assert.Same(reviews, state.Reviews);
        state.Pricing.DriverPricing.DropoffFee = 2000m;
        Assert.Equal(2800m, returned.DriverBasePayout);
        Assert.Equal(split ? 2600m : 2500m, draft.DriverBasePayout);
    }

    [Fact]
    public async Task 저장실패는_편집입력과편집기를유지하고_기존오류문구로돌아온다()
    {
        var draft = new 음식배달요금정책응답 { DriverPickupPayout = 500m };
        var pending = Pending<음식배달요금정책응답>();
        var state = Create(draft, _ => pending.Task);
        await state.LoadAsync();
        var editor = state.Pricing.DriverPricing;
        editor.PickupFee = 750m;
        var reviews = state.Reviews;

        var saving = state.SavePricingPolicyAsync();
        Assert.True(state.IsSaving);
        pending.SetException(new HttpRequestException("offline"));
        await saving;

        Assert.Same(draft, state.Pricing.Policy);
        Assert.Same(editor, state.Pricing.DriverPricing);
        Assert.Equal(750m, editor.PickupFee);
        Assert.Same(reviews, state.Reviews);
        Assert.True(state.HasLoadedData);
        Assert.False(state.IsSaving);
        Assert.Equal(Severity.Error, state.MessageSeverity);
        Assert.Equal("정책 저장에 실패했습니다: offline", state.Message);
    }

    [Fact]
    public async Task 실제서비스조립은_기존세GET순서와같은정책PUT을사용한다()
    {
        var calls = new List<(HttpMethod Method, string Path)>();
        음식배달요금정책응답? received = null;
        using var handler = new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            calls.Add((request.Method, path));
            if (request.Method == HttpMethod.Put)
            {
                received = await request.Content!.ReadFromJsonAsync<음식배달요금정책응답>();
                return Json(received!);
            }
            return path switch
            {
                "/api/v1/admin/food-delivery-pricing-policy" => Json(new 음식배달요금정책응답 { DriverWeatherSurchargePolicyRevision = "http-r7" }),
                "/api/v1/admin/restaurant-reviews" => Json(new 음식점리뷰관리목록응답()),
                "/api/v1/admin/restaurant-reviews/policy" => Json(new 음식점리뷰운영정책응답()),
                _ => throw new InvalidOperationException(path)
            };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/") };
        var service = new 음식운영Service(http, new(new ConfigurationBuilder().Build(), new Environment()));
        var state = new 음식운영PageState(service);
        await state.LoadAsync();
        state.Pricing.Policy.BaseFee = 3400m;
        await state.SavePricingPolicyAsync();

        Assert.Equal(
            [(HttpMethod.Get, "/api/v1/admin/food-delivery-pricing-policy"),
             (HttpMethod.Get, "/api/v1/admin/restaurant-reviews"),
             (HttpMethod.Get, "/api/v1/admin/restaurant-reviews/policy"),
             (HttpMethod.Put, "/api/v1/admin/food-delivery-pricing-policy")], calls);
        Assert.True(state.HasLoadedData);
        Assert.Equal(3400m, received!.BaseFee);
        Assert.Null(received.DriverPickupPayout);
        Assert.Equal("http-r7", received.DriverWeatherSurchargePolicyRevision);
        Assert.Equal(3400m, state.Pricing.Policy.BaseFee);
    }

    private static 음식운영PageState Create(
        음식배달요금정책응답 pricing,
        Func<음식배달요금정책응답, Task<음식배달요금정책응답>> save)
        => new(() => Task.FromResult(pricing),
            new(() => Task.FromResult(new 음식점리뷰관리목록응답()), () => Task.FromResult(new 음식점리뷰운영정책응답())), save);

    private static TaskCompletionSource<T> Pending<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => response(request);
    }

    private sealed class Environment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "fixture";
        public string EnvironmentName { get; set; } = "Production";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
