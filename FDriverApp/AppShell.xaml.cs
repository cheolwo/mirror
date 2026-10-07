using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using FDriverApp.Pages;
using Font = Microsoft.Maui.Font;

namespace FDriverApp
{
    public partial class AppShell : Shell
    {
        public AppShell(MainPage mainPage)
        {
            InitializeComponent();
            WorkspaceContent.Content = mainPage;
            Routing.RegisterRoute(FDriverCompletedDeliveryNavigator.ListRoute, typeof(CompletedDeliveryListPage));
            Routing.RegisterRoute(FDriverCompletedDeliveryNavigator.DetailRoute, typeof(CompletedDeliveryDetailPage));
            Routing.RegisterRoute(FDriverProtectionSupportNavigator.RouteName, typeof(ProtectionSupportPage));
            var currentTheme = Application.Current!.RequestedTheme;
            ThemeSegmentedControl.SelectedIndex = currentTheme == AppTheme.Light ? 0 : 1;
        }
        public static async Task DisplaySnackbarAsync(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);

            await snackbar.Show(cancellationTokenSource.Token);
        }

        public static async Task DisplayToastAsync(string message)
        {
            // Toast is currently not working in MCT on Windows
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }

        private void SfSegmentedControl_SelectionChanged(object? sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
        {
            Application.Current!.UserAppTheme = e.NewIndex == 0 ? AppTheme.Light : AppTheme.Dark;
        }
    }
}
