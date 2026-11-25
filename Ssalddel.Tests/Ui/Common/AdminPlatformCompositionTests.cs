using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class AdminPlatformCompositionTests
{
    [Fact]
    public async Task 관리자_개발호스트의_공용업무는_기기기능없이_조립된다()
    {
        var handler = new RejectHttp();
        var js = new RejectJs();
        await using var app = Build(handler, js);
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<NeighborhoodMemberParticipationViewModel>());
        Assert.NotNull(services.GetRequiredService<NeighborhoodMemberOffersViewModel>());
        Assert.False(services.GetRequiredService<NeighborhoodMemberWorkViewModel>().CaptureSupported);
        var location = services.GetRequiredService<IRoleWorkspaceLocationProvider>();
        Assert.Null(await location.GetCurrentAsync(CancellationToken.None));
        Assert.Null(await location.GetCurrentWithoutPermissionPromptAsync(CancellationToken.None));
        var photo = services.GetRequiredService<I업무사진촬영Adapter>();
        Assert.False(photo.IsSupported);
        await Assert.ThrowsAsync<NotSupportedException>(() => photo.CaptureAsync("격리 구성 검증", CancellationToken.None));
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, js.Calls);
    }

    [Fact]
    public async Task 관리자_미지원위치도_취소요청을_보존한다()
    {
        var services = new ServiceCollection().Add관리자플랫폼Services();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var location = scope.ServiceProvider.GetRequiredService<IRoleWorkspaceLocationProvider>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => location.GetCurrentAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => location.GetCurrentWithoutPermissionPromptAsync(cancellation.Token));
    }

    [Fact]
    public void 관리자_기본플랫폼은_이미등록한_호스트Adapter를_교체하지않는다()
    {
        var location = new HostLocation();
        var photo = new 미지원업무사진촬영Adapter();
        var services = new ServiceCollection();
        services.AddSingleton<IRoleWorkspaceLocationProvider>(location);
        services.AddSingleton<I업무사진촬영Adapter>(photo);
        services.Add관리자플랫폼Services();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.Same(location, provider.GetRequiredService<IRoleWorkspaceLocationProvider>());
        Assert.Same(photo, provider.GetRequiredService<I업무사진촬영Adapter>());
    }

    private static WebApplication Build(RejectHttp handler, RejectJs js)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ContentRootPath = Path.GetTempPath(),
            Args = []
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Host.UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; });
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddMudServices();
        builder.Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://fixture.invalid/") });
        builder.Services.RemoveAll<IJSRuntime>();
        builder.Services.AddSingleton<IJSRuntime>(js);
        builder.Services.AddSsalddelUiCommonAppServices<관리자인증세션Service>();
        builder.Services.Add관리자플랫폼Services();
        builder.Services.AddScoped<관리자인증세션Service>();
        return builder.Build();
    }

    private sealed class HostLocation : IRoleWorkspaceLocationProvider
    {
        public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken)
            => Task.FromResult<RoleWorkspaceLocation?>(null);
    }

    private sealed class RejectHttp : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("구성 검증에서는 HTTP를 호출하지 않습니다.");
        }
    }

    private sealed class RejectJs : IJSRuntime
    {
        public int Calls { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls++;
            throw new InvalidOperationException("구성 검증에서는 위치나 브라우저 기능을 호출하지 않습니다.");
        }
    }
}
