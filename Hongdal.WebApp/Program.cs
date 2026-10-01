using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Hongdal.Client.Infrastructure;
using Hongdal.Client.Infrastructure.Security;
using Hongdal.Client.Infrastructure.Transport;
using Hongdal.Contracts.Driver.Recommendation;
using Hongdal.WebApp;
using Hongdal.WebApp.Models;
using Hongdal.WebApp.Services;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = HongdalApiEndpoint.ResolveBaseAddress(
    builder.Configuration[HongdalApiEndpoint.ConfigurationKey],
    new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHongdalApiHttpClient(apiBaseAddress);
builder.Services.Configure<ClientDataModeOptions>(builder.Configuration.GetSection(ClientDataModeOptions.SectionName));
builder.Services.AddScoped<ITransportRequestLedgerObserver, TransportRequestLedgerObserver>();
builder.Services.AddHongdalUiCommonAppServices<WebAuthSessionService>();
builder.Services.AddTransient<화주Controller기능모음ViewModel>();
builder.Services.AddHongdalDocumentOutputServices();
builder.Services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
builder.Services.AddScoped<WebAuthSessionService>();
builder.Services.AddScoped<CommunityPersonalPreferenceService>();
builder.Services.AddScoped<IDiagramCollaborationClientService, DiagramCollaborationClientService>();
builder.Services.AddScoped<WebShipperWarehouseWorkspaceService>();
builder.Services.AddScoped<IWarehouseWorkspaceService>(sp => sp.GetRequiredService<WebShipperWarehouseWorkspaceService>());
builder.Services.AddScoped<WebShipperSalesWorkspaceService>();
builder.Services.AddScoped<GlobalTradeWorkspaceService>();
builder.Services.AddScoped<IWarehouseWorkEntryGateService, SampleWarehouseWorkEntryGateService>();
builder.Services.AddScoped<IInboundReceivingWorkflowService, SampleInboundReceivingWorkflowService>();
builder.Services.AddScoped<IWarehousePickingBatchWorkspaceService, SampleWarehousePickingBatchWorkspaceService>();
builder.Services.AddScoped<운송의뢰자동저장Service>();
builder.Services.AddScoped<화주운송의뢰등록Service>();
builder.Services.AddScoped<화주결제정산Service>();
builder.Services.AddScoped<기사내정보Service>();
builder.Services.AddScoped<기사정산Service>();
builder.Services.AddScoped<기사알림Service>();
builder.Services.AddScoped<기사운행Service>();
builder.Services.AddScoped<기사추천수신Service>();
builder.Services.AddScoped<I기사추천수신Service>(sp => sp.GetRequiredService<기사추천수신Service>());
builder.Services.AddScoped<기사운송증빙Service>();
builder.Services.AddScoped<운송의뢰작성ViewModel>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
