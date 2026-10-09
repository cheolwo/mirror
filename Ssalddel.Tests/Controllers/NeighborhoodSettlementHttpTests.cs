using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Controllers.Common;
using Ssalddel.Extensions;
using Ssalddel.Tests.Application.Settlement;
using Ssalddel.Tests.Application.Warehouse;
using 살뜰.Services.Options;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Tests.Controllers;

/// <summary>실제 로컬 HTTP·인증·JSON·격리 SQLite를 확인합니다. 운영 로그인이나 실제 송금 시험은 아닙니다.</summary>
public sealed class NeighborhoodSettlementHttpTests
{
    private const string Base = "/api/v1/neighborhood-settlements";

    [Fact]
    public async Task 실제원본입고근거에서당사자동의발생부분지급신고수령확인과재조회가연결된다()
    {
        await using var host = await Host.Create();
        using var anonymous = await host.Client.GetAsync(Base); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        host.Actor("goods-owner-a");
        var created = await host.Post(Base, new NeighborhoodSettlementAgreementRequest
        {
            RequestId = Guid.NewGuid(), SourceKind = C.DistributedLot, SourceId = host.Source.LotId, ActivityKind = C.StoredGoods,
            GoodsOwnerUserId = "goods-owner-a", SellerUserId = "seller-a",
            Lines = [new() { LineKey = "receipt-work", Kind = C.Inbound, Billing = C.PerUnit, UnitPriceKrw = 100, Quantity = 3,
                PayerUserId = "goods-owner-a", PayeeUserId = DistributedTestFixture.Operator.UserId, BeneficiaryRole = "Receiver" }]
        });
        var id = created.AgreementId; var path = Base + "/" + id;
        var receipt = await host.Post(path + "/consents", new NeighborhoodSettlementConsentRequest
            { RequestId = Guid.NewGuid(), ExpectedRevision = created.Revision, TermsVersion = 1, Granted = true });
        host.Actor(DistributedTestFixture.Operator.UserId);
        receipt = await host.Post(path + "/consents", new NeighborhoodSettlementConsentRequest
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, TermsVersion = 1, Granted = true });
        Assert.Equal(C.Active, receipt.StatusCode);
        var source = await host.Source.Resolver.ResolveAsync(C.DistributedLot, host.Source.LotId, default);
        var work = Assert.Single(source.CompletedWork);
        var accrual = new NeighborhoodSettlementAccrualRequest { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
            OriginKind = work.OriginKind, OriginId = work.OriginId, Lines = [new() { LineKey = "receipt-work", Quantity = 3 }] };
        receipt = await host.Post(path + "/accruals", accrual);
        var replay = await host.Post(path + "/accruals", accrual); Assert.True(replay.Replayed);
        var entryId = Assert.Single(receipt.EntryIds);
        receipt = await host.Post(path + "/entry-confirmations", new NeighborhoodSettlementEntryConfirmRequest
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, EntryId = entryId });
        host.Actor("goods-owner-a");
        receipt = await host.Post(path + "/entry-confirmations", new NeighborhoodSettlementEntryConfirmRequest
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, EntryId = entryId });
        receipt = await host.Post(path + "/payment-reports", new NeighborhoodSettlementPaymentReportRequest
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, AmountKrw = 100, EvidenceReference = "본인 지급 신고 시험",
                Allocations = [new() { EntryId = entryId, AmountKrw = 100 }] });
        var reported = (await host.Client.GetFromJsonAsync<NeighborhoodSettlementSummaryDto>(path))!;
        Assert.Equal(300, reported.Totals.KnownConfirmedAmountKrw); Assert.Equal(100, reported.Totals.ReportedPaymentAmountKrw);
        Assert.Equal(0, reported.Totals.RecipientConfirmedAmountKrw); Assert.Equal(300, reported.Totals.OutstandingAmountKrw);
        Assert.False(Assert.Single(reported.Payments).IsActualBankVerified);
        host.Actor(DistributedTestFixture.Operator.UserId);
        await host.Post(path + "/payment-receipts", new NeighborhoodSettlementPaymentReceiptRequest
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, PaymentId = receipt.PaymentId!, ConfirmedAmountKrw = 100, Reason = "수령자 확인 시험" });
        var received = (await host.Client.GetFromJsonAsync<NeighborhoodSettlementSummaryDto>(path))!;
        Assert.Equal(100, received.Totals.RecipientConfirmedAmountKrw); Assert.Equal(200, received.Totals.OutstandingAmountKrw);
        Assert.False(received.Totals.IsActualBankVerified);
        host.Actor("stranger");
        using var denied = await host.Client.GetAsync(path); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var receiptDenied = await host.Client.GetAsync(Base + "/receipts/" + accrual.RequestId); Assert.Equal(HttpStatusCode.NotFound, receiptDenied.StatusCode);
        Assert.Empty((await host.Client.GetFromJsonAsync<NeighborhoodSettlementSummaryDto[]>(Base))!);
        var export = Environment.GetEnvironmentVariable("NEIGHBORHOOD_SETTLEMENT_HTTP_EXPORT_DIR");
        if (!string.IsNullOrWhiteSpace(export))
        {
            Directory.CreateDirectory(export);
            await File.WriteAllTextAsync(Path.Combine(export, "inbound-payment-recipient-confirmed.json"),
                JsonSerializer.Serialize(received, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }

    [Theory]
    [InlineData(false, SsalddelExecutionMode.Simulation, "neighborhood-settlement-r38")]
    [InlineData(true, SsalddelExecutionMode.Operational, "neighborhood-settlement-r38")]
    [InlineData(true, SsalddelExecutionMode.Simulation, "wrong-profile")]
    public async Task 기본비활성운영모드다른판본에서는금융명령과조회모두차단한다(bool enabled, SsalddelExecutionMode mode, string profile)
    {
        await using var host = await Host.Create(enabled, mode, profile); host.Actor("goods-owner-a");
        using var response = await host.Client.GetAsync(Base); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var create = await host.Client.PostAsJsonAsync(Base, new NeighborhoodSettlementAgreementRequest { RequestId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    private sealed class Host(NeighborhoodSettlementSourceFixture source, WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public NeighborhoodSettlementSourceFixture Source { get; } = source;
        public HttpClient Client { get; } = client;
        public void Actor(string id) { Client.DefaultRequestHeaders.Remove("X-Fixture-Actor"); Client.DefaultRequestHeaders.Add("X-Fixture-Actor", id); }
        public async Task<NeighborhoodSettlementReceiptDto> Post<T>(string path, T request)
        {
            using var response = await Client.PostAsJsonAsync(path, request);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            Assert.True(response.Headers.CacheControl?.NoStore);
            return (await response.Content.ReadFromJsonAsync<NeighborhoodSettlementReceiptDto>())!;
        }
        public static async Task<Host> Create(bool enabled = true, SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation,
            string profile = NeighborhoodSettlementOptions.RequiredProfile)
        {
            var source = await NeighborhoodSettlementSourceFixture.Create();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(source.Data.Db); builder.Services.AddSingleton<TimeProvider>(source.Data.Clock);
            builder.Services.AddSingleton<ISsalddelExecutionModePolicy>(new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })));
            builder.Services.AddNeighborhoodSettlement(builder.Configuration);
            builder.Services.Configure<NeighborhoodSettlementOptions>(x => { x.Enabled = enabled; x.Profile = profile; });
            builder.Services.AddControllers().ConfigureApplicationPartManager(x =>
            { x.ApplicationParts.Clear(); x.FeatureProviders.Clear(); x.FeatureProviders.Add(new OnlyController()); }).AddControllersAsServices();
            builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, 분산물류HttpTests.FixtureAuthentication>("Fixture", _ => { });
            builder.Services.AddAuthorization(); var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)); await app.StartAsync(deadline.Token);
                var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
                return new(source, app, new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                    { BaseAddress = new Uri(Assert.Single(addresses.Addresses)), Timeout = TimeSpan.FromSeconds(20) });
            }
            catch { await app.DisposeAsync(); await source.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(deadline.Token); await app.DisposeAsync(); await Source.DisposeAsync();
        }
    }
    private sealed class OnlyController : IApplicationFeatureProvider<ControllerFeature>
    { public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature) => feature.Controllers.Add(typeof(NeighborhoodSettlementController).GetTypeInfo()); }
}
