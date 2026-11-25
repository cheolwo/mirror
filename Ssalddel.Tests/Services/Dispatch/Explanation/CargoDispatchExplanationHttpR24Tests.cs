using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Controllers.Common;
using 살뜰.Services.Dispatch.Explanation;

namespace Ssalddel.Tests.Services.Dispatch.Explanation;

public sealed partial class CargoDispatchExplanationR24Tests
{
    // Actual loopback Kestrel, routing, authorization and JSON with the product controller/service.
    // X-Fixture-Actor is test authentication. This is not production JWT, startup/worker, SQL/Mongo or device evidence.
    [Theory]
    [InlineData(null, 401)]
    [InlineData("unrelated-user-r24", 403)]
    [InlineData(Owner, 200)]
    [InlineData(Orderer, 200)]
    [InlineData(Driver, 200)]
    public async Task 실제HTTP경계에서익명타인을차단하고_업무본인만저장설명을읽는다(string? actor, int status)
    {
        await using var f = await Fixture.CreateAsync();
        await f.RecordAsync();
        await using var http = await ExplanationHttpHost.StartAsync(f.Service);
        using var request = http.Request(HttpMethod.Get, $"api/v1/cargo-dispatch-explanations/{f.Request.의뢰Id}?userId={Owner}", actor);
        using var response = await http.Client.SendAsync(request, http.Token);
        Assert.Equal(status, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(http.Token);
        foreach (var secret in new[] { Driver, Owner, Orderer, "raw-private-algorithm-r24", "private-phone-r24", "private-address-r24" })
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        if (status == 200)
        {
            var result = await response.Content.ReadFromJsonAsync<CargoDispatchExplanationResponse>(cancellationToken: http.Token);
            Assert.Equal("recorded", result!.Status);
            Assert.Single(result.Items);
        }
        Assert.Empty(f.Feedback.Items);
        Assert.Equal(1, await f.Db.운송이벤트.CountAsync());
    }

    [Fact]
    public async Task 실제HTTP의견왕복은본문사용자ID를신뢰하지않고_멱등충돌과GET복구뒤원업무를보존한다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var originalState = f.Queue.상태;
        var originalFare = f.Request.최종운임;
        var evidenceId = row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var requestKey = Guid.NewGuid();
        const string comment = "당시 접수 정보를 확인했습니다.";
        await using var http = await ExplanationHttpHost.StartAsync(f.Service);
        var endpoint = $"api/v1/cargo-dispatch-explanations/{f.Request.의뢰Id}/feedback";

        using (var anonymous = http.Request(HttpMethod.Post, endpoint, null))
        {
            anonymous.Content = Body(comment);
            using var denied = await http.Client.SendAsync(anonymous, http.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            Assert.Empty(f.Feedback.Items);
        }
        using (var forged = http.Request(HttpMethod.Post, endpoint, "unrelated-user-r24"))
        {
            forged.Content = Body(comment);
            using var forbidden = await http.Client.SendAsync(forged, http.Token);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Empty(f.Feedback.Items);
        }

        var first = await SubmitAsync(comment, HttpStatusCode.OK);
        var repeat = await SubmitAsync(comment, HttpStatusCode.OK);
        Assert.Equal(first!.FeedbackId, repeat!.FeedbackId);
        Assert.Equal(first.SubmittedAtUtc, repeat.SubmittedAtUtc);
        Assert.Equal(first.SubmissionFingerprint, repeat.SubmissionFingerprint);
        Assert.Equal(CargoDispatchFeedbackFingerprint.Compute("helpful", comment), first.SubmissionFingerprint);
        Assert.Equal(Owner, Assert.Single(f.Feedback.Items).UserId);
        await SubmitAsync("다른 의견", HttpStatusCode.Conflict);

        using (var get = http.Request(HttpMethod.Get,
            $"{endpoint}/{requestKey:D}?evidenceId={evidenceId}&userId={Driver}", Owner))
        using (var read = await http.Client.SendAsync(get, http.Token))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var receipt = await read.Content.ReadFromJsonAsync<CargoDispatchFeedbackReceipt>(cancellationToken: http.Token);
            Assert.Equal(first.FeedbackId, receipt!.FeedbackId);
            Assert.Equal(first.SubmissionFingerprint, receipt.SubmissionFingerprint);
            Assert.NotEqual(CargoDispatchFeedbackFingerprint.Compute("helpful", "다른 의견"), receipt.SubmissionFingerprint);
        }

        using (var get = http.Request(HttpMethod.Get,
            $"api/v1/cargo-dispatch-explanations/{f.Request.의뢰Id}", Owner))
        using (var read = await http.Client.SendAsync(get, http.Token))
        {
            var explanation = await read.Content.ReadFromJsonAsync<CargoDispatchExplanationResponse>(cancellationToken: http.Token);
            Assert.Equal(first.FeedbackId, Assert.Single(Assert.Single(explanation!.Items).MyFeedback).FeedbackId);
        }
        Assert.Equal(1, await f.Db.운송이벤트.CountAsync());
        Assert.Equal(originalState, f.Queue.상태);
        Assert.Equal(originalFare, f.Request.최종운임);
        Assert.False(f.Db.ChangeTracker.HasChanges());

        JsonContent Body(string value) => JsonContent.Create(new
        {
            ClientRequestId = requestKey, EvidenceId = evidenceId, CategoryCode = "helpful", Comment = value,
            UserId = Driver // Unknown JSON property cannot replace the authenticated actor.
        });
        async Task<CargoDispatchFeedbackReceipt?> SubmitAsync(string value, HttpStatusCode expected)
        {
            using var request = http.Request(HttpMethod.Post, endpoint, Owner);
            request.Content = Body(value);
            using var response = await http.Client.SendAsync(request, http.Token);
            Assert.Equal(expected, response.StatusCode);
            return expected == HttpStatusCode.OK
                ? await response.Content.ReadFromJsonAsync<CargoDispatchFeedbackReceipt>(cancellationToken: http.Token) : null;
        }
    }

    [Fact]
    public async Task 실제HTTP는잘못된의견400과미접수404와저장소실패503을성공으로위장하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var evidenceId = row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var endpoint = $"api/v1/cargo-dispatch-explanations/{f.Request.의뢰Id}/feedback";
        await using var http = await ExplanationHttpHost.StartAsync(f.Service);
        using (var malformed = http.Request(HttpMethod.Post, endpoint, Owner))
        {
            malformed.Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
            using var response = await http.Client.SendAsync(malformed, http.Token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        foreach (var body in new[]
        {
            new CargoDispatchFeedbackRequest { EvidenceId = evidenceId, ClientRequestId = Guid.NewGuid(), CategoryCode = "helpful", Comment = "line\nbreak" },
            new CargoDispatchFeedbackRequest { EvidenceId = evidenceId, ClientRequestId = Guid.NewGuid(), CategoryCode = "unknown", Comment = "입력 확인" },
            new CargoDispatchFeedbackRequest { EvidenceId = evidenceId, ClientRequestId = Guid.Empty, CategoryCode = "helpful" }
        })
        {
            using var request = http.Request(HttpMethod.Post, endpoint, Owner);
            request.Content = JsonContent.Create(body);
            using var response = await http.Client.SendAsync(request, http.Token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using (var get = http.Request(HttpMethod.Get, $"{endpoint}/{Guid.NewGuid():D}?evidenceId={evidenceId}", Owner))
        using (var read = await http.Client.SendAsync(get, http.Token))
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        f.Feedback.FailWrites = true;
        using (var request = http.Request(HttpMethod.Post, endpoint, Owner))
        {
            request.Content = JsonContent.Create(new CargoDispatchFeedbackRequest
            { EvidenceId = evidenceId, ClientRequestId = Guid.NewGuid(), CategoryCode = "helpful", Comment = "접수 확인" });
            using var response = await http.Client.SendAsync(request, http.Token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(http.Token);
            Assert.DoesNotContain(Owner, body, StringComparison.Ordinal);
        }
        Assert.Empty(f.Feedback.Items);
        Assert.Equal(1, await f.Db.운송이벤트.CountAsync());
    }

    private sealed class ExplanationHttpHost(WebApplication app, HttpClient client, CancellationTokenSource deadline) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public CancellationToken Token => deadline.Token;
        public HttpRequestMessage Request(HttpMethod method, string route, string? actor)
        {
            var request = new HttpRequestMessage(method, route);
            if (actor is not null) request.Headers.Add("X-Fixture-Actor", actor);
            return request;
        }
        public static async Task<ExplanationHttpHost> StartAsync(ICargoDispatchExplanationService service)
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            { EnvironmentName = "Testing", ApplicationName = typeof(화물배차설명Controller).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory });
            builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Services.AddLogging();
            builder.Services.AddRouting();
            builder.Services.AddSingleton(service);
            builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
            {
                manager.ApplicationParts.Clear(); manager.FeatureProviders.Clear();
                manager.FeatureProviders.Add(new ExplanationControllerOnly());
            });
            builder.Services.AddAuthentication("ExplanationFixture")
                .AddScheme<AuthenticationSchemeOptions, ExplanationFixtureAuthentication>("ExplanationFixture", _ => { });
            builder.Services.AddAuthorization();
            var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                await app.StartAsync(deadline.Token);
                var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
                var address = new Uri(Assert.Single(addresses.Addresses));
                Assert.True(address.IsLoopback);
                var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
                return new(app, new HttpClient(handler) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(15) }, deadline);
            }
            catch
            {
                deadline.Dispose();
                await app.DisposeAsync();
                throw;
            }
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await app.StopAsync(stop.Token); }
            finally { await app.DisposeAsync(); deadline.Dispose(); }
        }
    }

    private sealed class ExplanationControllerOnly : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            => feature.Controllers.Add(typeof(화물배차설명Controller).GetTypeInfo());
    }

    public sealed class ExplanationFixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-Fixture-Actor"].ToString();
            if (string.IsNullOrEmpty(actor)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        { Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; }
        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        { Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; }
    }
}
