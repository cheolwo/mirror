using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using RestaurantDeskApp.Services;

namespace RestaurantDeskApp.Platforms.Android;

public sealed class AndroidRestaurantOrderNotifications : IRestaurantOrderNotificationPlatform
{
    private const string ChannelId = "restaurant-orders";
    private const string OrderExtra = "ssalddel.restaurant.order";
    private const string TicketExtra = "ssalddel.restaurant.ticket";
    private const string OwnerPrefix = "restaurant.notification.owner.";
    private const string TicketIndex = "restaurant.notification.tickets";
    private static AndroidRestaurantOrderNotifications? _instance;
    private static RestaurantNotificationActivation? _pending;
    public AndroidRestaurantOrderNotifications() => _instance = this;
    public bool IsSupported => true;
    public event Action<RestaurantNotificationActivation>? Activated;
    public RestaurantNotificationActivation? TakePendingActivation()
    {
        var pending = _pending;
        _pending = null;
        return pending;
    }

    public static void HandleIntent(Intent? intent)
    {
        var order = intent?.GetStringExtra(OrderExtra);
        var ticket = intent?.GetStringExtra(TicketExtra);
        if (string.IsNullOrWhiteSpace(order) || string.IsNullOrWhiteSpace(ticket)) return;
        intent!.RemoveExtra(OrderExtra);
        intent.RemoveExtra(TicketExtra);
        var owner = Preferences.Default.Get(OwnerPrefix + ticket, string.Empty);
        if (string.IsNullOrWhiteSpace(owner)) return;
        Preferences.Default.Remove(OwnerPrefix + ticket);
        var activation = new RestaurantNotificationActivation(order, owner);
        if (_instance?.Activated is { } callback) callback(activation);
        else _pending = activation;
    }

    public Task<bool> RequestPermissionAsync(CancellationToken cancellationToken)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                var result = await Permissions.RequestAsync<OrderNotificationPermission>();
                cancellationToken.ThrowIfCancellationRequested();
                if (result != PermissionStatus.Granted) return false;
            }
            return NotificationManagerCompat.From(global::Android.App.Application.Context).AreNotificationsEnabled();
        });

    public Task ShowAsync(string orderNo, string ownerId, CancellationToken cancellationToken)
        => MainThread.InvokeOnMainThreadAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = global::Android.App.Application.Context;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
                && ContextCompat.CheckSelfPermission(context, global::Android.Manifest.Permission.PostNotifications) != Permission.Granted) return;
            var manager = NotificationManagerCompat.From(context);
            if (!manager.AreNotificationsEnabled()) return;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var nativeManager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
                using var channel = new NotificationChannel(ChannelId, "음식점 새 주문", NotificationImportance.High)
                { LockscreenVisibility = NotificationVisibility.Private };
                nativeManager.CreateNotificationChannel(channel);
            }
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ownerId + "::" + orderNo));
            var id = BitConverter.ToInt32(bytes) & int.MaxValue;
            var ticket = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var tickets = Preferences.Default.Get(TicketIndex, string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!tickets.Contains(ticket, StringComparer.Ordinal)) tickets.Add(ticket);
            while (tickets.Count > 256)
            {
                var removed = tickets[0]; tickets.RemoveAt(0);
                if (int.TryParse(removed, out var removedId)) manager.Cancel(removedId);
                Preferences.Default.Remove(OwnerPrefix + removed);
            }
            Preferences.Default.Set(TicketIndex, string.Join(',', tickets));
            Preferences.Default.Set(OwnerPrefix + ticket, ownerId);
            using var intent = new Intent(context, typeof(MainActivity));
            intent.SetAction("ssalddel.restaurant.open-order." + ticket);
            intent.PutExtra(OrderExtra, orderNo);
            intent.PutExtra(TicketExtra, ticket);
            intent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            using var pending = PendingIntent.GetActivity(context, id, intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            using var notification = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle("새 주문이 도착했어요")
                .SetContentText("앱에서 주문을 확인해 주세요.")
                .SetVisibility((int)NotificationVisibility.Private)
                .SetPriority((int)NotificationPriority.High)
                .SetContentIntent(pending)
                .SetAutoCancel(true)
                .SetOnlyAlertOnce(true)
                .Build();
            manager.Notify(id, notification);
        });

    public void Clear() => MainThread.BeginInvokeOnMainThread(() =>
    {
        NotificationManagerCompat.From(global::Android.App.Application.Context).CancelAll();
        foreach (var ticket in Preferences.Default.Get(TicketIndex, string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            Preferences.Default.Remove(OwnerPrefix + ticket);
        Preferences.Default.Remove(TicketIndex);
        _pending = null;
    });

    public sealed class OrderNotificationPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions
            => Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
                ? [(global::Android.Manifest.Permission.PostNotifications, true)] : [];
    }
}
