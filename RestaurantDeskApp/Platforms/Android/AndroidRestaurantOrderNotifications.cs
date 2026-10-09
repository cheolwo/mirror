using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Common.Notifications;
using System.Text.Json;

namespace RestaurantDeskApp.Platforms.Android;

public sealed class AndroidRestaurantOrderNotifications : IRestaurantOrderNotificationPlatform
{
    private const string ChannelId = "restaurant-orders";
    private const string ChannelPrefix = "restaurant-orders-alert.v2";
    private const string PreferencesPrefix = "restaurant.notification.preferences.";
    private const string OrderExtra = "ssalddel.restaurant.order";
    private const string TicketExtra = "ssalddel.restaurant.ticket";
    private const string OwnerPrefix = "restaurant.notification.owner.";
    private const string TicketIndex = "restaurant.notification.tickets";
    private static AndroidRestaurantOrderNotifications? _instance;
    private static RestaurantNotificationActivation? _pending;
    public AndroidRestaurantOrderNotifications() => _instance = this;
    public bool IsSupported => true;
    public NotificationAlertPreferences GetAlertPreferences(string ownerId)
    {
        try
        {
            var text = Preferences.Default.Get(PreferencesKey(ownerId), string.Empty);
            return string.IsNullOrWhiteSpace(text) ? new() : JsonSerializer.Deserialize<NotificationAlertPreferences>(text) ?? new();
        }
        catch (JsonException) { return new(false, false, false); }
    }
    public void SetAlertPreferences(string ownerId, NotificationAlertPreferences preferences)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        Preferences.Default.Set(PreferencesKey(ownerId), JsonSerializer.Serialize(preferences));
        if (!preferences.Enabled) Clear();
    }
    private static string PreferencesKey(string ownerId)
        => PreferencesPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ownerId)));
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
            return NotificationManagerCompat.From(global::Android.App.Application.Context)?.AreNotificationsEnabled() == true;
        });

    public Task ShowAsync(string orderNo, string ownerId, CancellationToken cancellationToken)
        => MainThread.InvokeOnMainThreadAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = global::Android.App.Application.Context;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
                && ContextCompat.CheckSelfPermission(context, global::Android.Manifest.Permission.PostNotifications) != Permission.Granted) return;
            var manager = NotificationManagerCompat.From(context);
            if (manager is null || !manager.AreNotificationsEnabled()) return;
            var plan = NotificationAlertPolicy.Evaluate(GetAlertPreferences(ownerId), DateTimeOffset.UtcNow);
            if (!plan.Enabled) return;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var nativeManager = context.GetSystemService(Context.NotificationService) as NotificationManager;
                if (nativeManager is null) return;
                var legacy = nativeManager.GetNotificationChannel(ChannelId);
                var modes = (nativeManager.NotificationChannels ?? []).Where(x => x.Id?.StartsWith(ChannelPrefix, StringComparison.Ordinal) == true).ToArray();
                plan = NotificationAlertPolicy.PreserveLegacy(plan, ToState(legacy), modes.Select(x => ToState(x)!));
                if (!plan.Enabled) return;
                using var channel = new NotificationChannel(NotificationAlertPolicy.ChannelId(ChannelPrefix, plan), "살뜰 새 주문", NotificationImportance.High)
                { LockscreenVisibility = NotificationVisibility.Private };
                using var attributes = new global::Android.Media.AudioAttributes.Builder()
                    .SetUsage(global::Android.Media.AudioUsageKind.Notification)!.SetContentType(global::Android.Media.AudioContentType.Sonification)!.Build();
                channel.SetSound(plan.Sound ? global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/ssalddel_order") : null, attributes);
                channel.EnableVibration(plan.Vibrate);
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
            using var builder = new NotificationCompat.Builder(context, NotificationAlertPolicy.ChannelId(ChannelPrefix, plan));
            builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
            builder.SetContentTitle("살뜰 주문 · 새 주문");
            builder.SetContentText("앱에서 주문을 확인해 주세요.");
            builder.SetVisibility((int)NotificationVisibility.Private);
            builder.SetPriority((int)NotificationPriority.High);
            builder.SetContentIntent(pending);
            builder.SetAutoCancel(true);
            builder.SetOnlyAlertOnce(true);
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                builder.SetSound(plan.Sound ? global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/ssalddel_order") : null);
                if (plan.Vibrate) builder.SetVibrate([0L, 180L]);
            }
            using var notification = builder.Build();
            if (notification is null) return;
            manager.Notify(id, notification);
        });

    private static NotificationChannelState? ToState(NotificationChannel? channel)
        => channel is null ? null : new(channel.Id!, channel.Importance == NotificationImportance.None,
            (int)channel.Importance >= (int)NotificationImportance.Default && channel.Sound is not null,
            (int)channel.Importance >= (int)NotificationImportance.Default && channel.ShouldVibrate());

    public void Clear() => MainThread.BeginInvokeOnMainThread(() =>
    {
        NotificationManagerCompat.From(global::Android.App.Application.Context)?.CancelAll();
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
