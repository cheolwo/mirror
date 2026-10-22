using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleWorkspaceBusinessR9Tests
{
    private const string Food = BusinessPackageCatalog.FoodDelivery;
    private const string Cargo = BusinessPackageCatalog.FreightDelivery;

    [Fact]
    public async Task LegacyOperatorRetainsFoodWhileExplicitCargoUsesTheSameAuthRole()
    {
        var food = new Adapter(Food); var cargo = new Adapter(Cargo); var access = new Access();
        using var model = new RoleWorkspaceViewModel([food, cargo], access, new());
        await model.InitializeAsync("operator");
        Assert.Equal(Food, model.BusinessCode); Assert.Equal("음식 관리자", model.Title);
        Assert.Equal(1, food.Reads); Assert.Equal(0, cargo.Reads);
        await model.InitializeAsync("operator", businessCode: Cargo);
        Assert.Equal("화물 관리자", model.Title); Assert.Equal(1, cargo.Reads);
        Assert.All(access.Roles, role => Assert.Equal("operator", role));
        Assert.Contains("business=freight-delivery", model.WorkspaceHref);
        Assert.Equal("/workspace/operator", RoleWorkspaceNavigation.Href("operator"));
        Assert.Equal("/workspace/shipper?selected=one", RoleWorkspaceNavigation.Href("shipper", "one", Cargo));
    }

    [Theory]
    [InlineData("operator", "")]
    [InlineData("operator", "unknown")]
    [InlineData("operator", "order-warehouse")]
    [InlineData("shipper", Food)]
    [InlineData("orderer", Cargo)]
    public async Task InvalidBusinessNeverFallsBackToAnotherAdapter(string role, string business)
    {
        var food = new Adapter(Food); var cargo = new Adapter(Cargo);
        using var model = new RoleWorkspaceViewModel([food, cargo], new Access(), new());
        await model.InitializeAsync(role, businessCode: business);
        Assert.Null(model.Context); Assert.Null(model.Role); Assert.NotNull(model.Error);
        Assert.Empty(model.Items); Assert.Equal(0, food.Reads + cargo.Reads);
        Assert.Equal("/", RoleWorkspaceNavigation.Href(role, businessCode: business));
    }

    [Fact]
    public async Task LateFoodResponseCannotReplaceCargoCardOrMarkers()
    {
        var late = new TaskCompletionSource<RoleWorkspaceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var food = new Adapter(Food) { Read = (_, _) => late.Task }; var cargo = new Adapter(Cargo);
        using var model = new RoleWorkspaceViewModel([food, cargo], new Access(), new());
        var first = model.InitializeAsync("operator", businessCode: Food);
        await model.InitializeAsync("operator", businessCode: Cargo);
        late.SetResult(food.Snapshot("old-private-food"));
        await first;
        Assert.Equal(Cargo, model.BusinessCode);
        Assert.Equal("shared-id", model.SelectedId);
        Assert.DoesNotContain(model.Items, item => item.Id == "old-private-food");
        Assert.All(model.RenderState().Markers, marker => Assert.StartsWith(Cargo, marker.Id));
    }

    [Fact]
    public async Task PreferencesAndSelectionAreSeparateForBothOperatorWorkspaces()
    {
        var state = new RoleWorkspaceState();
        var food = new Adapter(Food); var cargo = new Adapter(Cargo);
        using var model = new RoleWorkspaceViewModel([food, cargo], new Access(), state);
        await model.InitializeAsync("operator", businessCode: Food);
        model.SetMapVisibility(false, false); model.SetListOnly(true); model.SetDetailsExpanded(true);
        await model.InitializeAsync("operator", businessCode: Cargo);
        Assert.True(model.ShowMarkers); Assert.True(model.ShowRoutes); Assert.False(model.ListOnly);
        Assert.False(model.DetailsExpanded);
        model.SetViewport(new(37.5, 127, 14));
        await model.InitializeAsync("operator", businessCode: Food);
        Assert.False(model.ShowMarkers); Assert.False(model.ShowRoutes); Assert.True(model.ListOnly);
        Assert.True(model.DetailsExpanded);
        Assert.Null(state.Read("operator", Cargo).Viewport);
    }

    [Fact]
    public async Task UncertainCommandKeepsItsOwnRequestIdAcrossBusinessSwitches()
    {
        var food = new Adapter(Food) { FailCommand = true }; var cargo = new Adapter(Cargo) { FailCommand = true };
        using var model = new RoleWorkspaceViewModel([food, cargo], new Access(), new());
        await model.InitializeAsync("operator", businessCode: Food);
        await model.PerformAsync("shared-id", "confirm");
        Assert.NotNull(model.CommandError);
        await model.InitializeAsync("operator", businessCode: Cargo);
        Assert.Null(model.CommandError);
        await model.PerformAsync("shared-id", "confirm");
        await model.InitializeAsync("operator", businessCode: Food);
        await model.PerformAsync("shared-id", "confirm");
        Assert.Equal(2, food.Commands.Count); Assert.Single(cargo.Commands);
        Assert.Equal(food.Commands[0], food.Commands[1]);
        Assert.NotEqual(food.Commands[0], cargo.Commands[0]);
    }

    [Fact]
    public void LoginAndIncidentReturnRoutesKeepTheBusinessAndSelectedWork()
    {
        var work = RoleWorkspaceNavigation.Href("operator", "one", Cargo);
        Assert.Equal("/workspace/operator?business=freight-delivery&selected=one", work);
        var login = RoleWorkspaceNavigation.WithReturn("/workspace-login/operator", work)!;
        Assert.Contains("returnUrl=%2Fworkspace%2Foperator%3Fbusiness%3Dfreight-delivery%26selected%3Done", login);
        var incident = RoleWorkspaceNavigation.WithReturn("/workspace/operator/cargo-incidents", work)!;
        Assert.Contains("business%3Dfreight-delivery", incident);
    }

    [Theory]
    [InlineData(Food, "음식 관리자")]
    [InlineData(Cargo, "화물 관리자")]
    public async Task RenderedManagerShowsOnlyItsOwnTools(string business, string title)
    {
        var adapter = new Adapter(business) { ShowIncident = business == Cargo };
        var html = await RenderManagerAsync(adapter, business);
        Assert.Contains(title, html); Assert.DoesNotContain("분산 보관·배송 업무", html);
        if (business == Food) Assert.DoesNotContain("화물 사건", html);
        else
        {
            Assert.Contains("화물 사건", html);
            Assert.Contains("business%3Dfreight-delivery", html);
            Assert.DoesNotContain("음식 관리자", html);
        }
        Assert.Empty(adapter.Commands);
    }

    [Fact]
    public async Task InvalidBusinessOffersRoleSelectionInsteadOfMisleadingLogin()
    {
        var adapter = new Adapter(Food);
        var html = await RenderManagerAsync(adapter, "unknown");
        Assert.Contains("업무 구분 확인", html);
        Assert.Contains("역할 선택으로", html);
        Assert.DoesNotContain("로그인이 필요합니다", html);
        Assert.DoesNotContain("workspace-login", html);
        Assert.Equal(0, adapter.Reads);
    }

    private static async Task<string> RenderManagerAsync(Adapter adapter, string business)
    {
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new());
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices(); services.AddSingleton(model);
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton<INeighborhoodMapHost, MapHost>(); services.AddSingleton<IJSRuntime, StaticJs>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<RoleMapWorkspace>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(RoleMapWorkspace.RoleKey)] = "operator", [nameof(RoleMapWorkspace.BusinessCode)] = business }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        return html;
    }

    private sealed class Adapter(string business) : IRoleWorkspaceAdapter
    {
        public string RoleKey => "operator";
        public string BusinessCode => business;
        public int Reads { get; private set; }
        public bool FailCommand { get; init; }
        public bool ShowIncident { get; init; }
        public List<Guid> Commands { get; } = [];
        public Func<string?, CancellationToken, Task<RoleWorkspaceSnapshot>>? Read { get; init; }
        public RoleWorkspaceSnapshot Snapshot(string id = "shared-id") => new("operator", [new(id,
            business == Food ? "음식 주문" : "화물 운송", "진행 중", Actions: ShowIncident
                ? [new(CargoOperatorWorkspaceAdapter.ReviewIncidents, "화물 사건", "/workspace/operator/cargo-incidents")]
                : [new("confirm", "확인", IsPrimary: true)],
            Markers: [new(business + "|driver", "담당 기사", NeighborhoodMapMarkerKinds.Driver, 37.5, 127)], IsCurrent: true)], SelectedId: id);
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
        { Reads++; return Read?.Invoke(selectedId, cancellationToken) ?? Task.FromResult(Snapshot(selectedId ?? "shared-id")); }
        public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
        { Commands.Add(requestId); return FailCommand ? Task.FromException(new HttpRequestException("test lost response")) : Task.CompletedTask; }
        public void Clear() { }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public List<string> Roles { get; } = [];
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) { Roles.Add(roleKey); return new("same-admin", 1, true); }
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StaticJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => throw new NotSupportedException();
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args) => InvokeAsync<T>(id, args);
    }
    private sealed class MapHost : INeighborhoodMapHost
    {
        public Task<NeighborhoodMapHostStatus> RenderAsync(string id, NeighborhoodMapRenderState state, Func<string, Task> selected, CancellationToken cancellationToken = default)
            => Task.FromResult(NeighborhoodMapHostStatus.Unavailable);
        public Task HideAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/workspace/operator");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
