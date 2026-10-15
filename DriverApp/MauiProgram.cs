using Microsoft.Extensions.Logging;
using DriverApp.Services.CommonContents;
using DriverApp.Services;
using DriverApp.Services.Samples;
using DriverApp.Handlers;
using Ssalddel.Ui.Common.Areas.App.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Security.Cryptography;
using Microsoft.Maui.Storage;

namespace DriverApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureMauiHandlers(handlers =>
			{
				handlers.AddHandler<Controls.DriverNativeMapView, DriverNativeMapViewHandler>();
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddDriverAppServices(builder.Configuration);
		builder.Services.AddSsalddelUiCommonAppServices<IAuthSession>();
        builder.Services.AddScoped<IWorkflowRecoveryStore>(services =>
        {
            var session = services.GetRequiredService<IAuthSession>();
            return new Ssalddel.Client.Infrastructure.Native.MauiRoleWorkflowRecoveryStore(
                () => new Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core.RoleWorkspaceIdentity(
                    session.UserId, session.SessionRevision, session.IsAuthenticated), "cargo-driver");
        });
        // Photos/signatures never enter the general workflow recovery store.
        var photoRecoveryRoot = Path.Combine(FileSystem.AppDataDirectory, "cargo-photo-recovery-v1");
        var photoKeyGate = new SemaphoreSlim(1, 1);
        builder.Services.AddSingleton<IDriverCargoPhotoRecoveryStore>(_ => new DriverCargoPhotoRecoveryStore(
            photoRecoveryRoot,
            async () =>
            {
                await photoKeyGate.WaitAsync();
                try
                {
                    const string keyName = "cargo-photo-recovery-key.v1";
                    var saved = await SecureStorage.Default.GetAsync(keyName);
                    if (saved is not null)
                    {
                        byte[] restored;
                        try { restored = Convert.FromBase64String(saved); }
                        catch (FormatException) { throw new InvalidOperationException("사진 복구 키를 확인하지 못했습니다."); }
                        if (restored.Length != 32) throw new InvalidOperationException("사진 복구 키를 확인하지 못했습니다.");
                        return restored;
                    }
                    if (Directory.Exists(photoRecoveryRoot) && Directory.EnumerateFiles(photoRecoveryRoot).Any())
                        throw new InvalidOperationException("기존 사진의 보호 키가 없어 복구 자료를 덮어쓰지 않았습니다.");
                    var created = RandomNumberGenerator.GetBytes(32);
                    await SecureStorage.Default.SetAsync(keyName, Convert.ToBase64String(created));
                    return created;
                }
                finally { photoKeyGate.Release(); }
            }));
#if DEBUG || SSALDDEL_USB_FIELD_TEST
		const bool allowInsecureDebugEndpoint = true;
#else
		const bool allowInsecureDebugEndpoint = false;
#endif
		builder.Services.AddSsalddelOperationalApiHttpClient(
			SsalddelServerEndpoint.ResolveMobileBaseAddress(
				typeof(MauiProgram).Assembly,
				builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
				builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
				allowInsecureDebugEndpoint),
			ServiceLifetime.Singleton);
		builder.Services.AddSsalddelDocumentOutputServices();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<NativeDriverHomePage>();
		builder.Services.AddMudServices();
		builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment.I업무사진촬영Adapter, Ssalddel.Client.Infrastructure.Native.Maui업무사진촬영Adapter>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		DriverAppServiceProvider.Initialize(app.Services);
		return app;
	}
}
