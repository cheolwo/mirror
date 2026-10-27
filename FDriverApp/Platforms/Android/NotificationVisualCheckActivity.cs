#if DEBUG && DRIVER_NOTIFICATION_VISUAL_CHECK
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using FDriverApp.Services;
using Microsoft.Maui.Storage;
using Ssalddel.Contracts.Common.Notifications;
using 기사알림설정응답 = Ssalddel.Contracts.Driver.Notification.기사알림설정응답;
using DriverWorkKinds = Ssalddel.Contracts.Driver.Settings.DriverWorkKinds;
using Button = Android.Widget.Button;
using CheckBox = Android.Widget.CheckBox;
using ScrollView = Android.Widget.ScrollView;
using Orientation = Android.Widget.Orientation;

namespace FDriverApp.Platforms.Android;

/// <summary>명시적 Debug 도구입니다. 계정·운행·배차 API를 호출하지 않고 OS 표시만 확인합니다.</summary>
[Activity(Name = "kr.ssalddel.fdriver.NotificationVisualCheckActivity", Exported = true,
    Label = "음식 알림 화면 확인", Theme = "@android:style/Theme.Material.Light.NoActionBar")]
public sealed class NotificationVisualCheckActivity : Activity
{
    private const int AlertId = 31081;
    private const int PermissionRequest = 3191;
    private const string Owner = "device-visual-check.food";
    private static readonly string[] PreferenceKeys =
    [
        "ssalddel.fdriver.foodNotification.receipt.v1", "ssalddel.fdriver.foodNotification.target.v1",
        "ssalddel.fdriver.alert.settings.v1", "ssalddel.fdriver.alert.settings-fence.v1",
        "ssalddel.fdriver.alert.published.v1"
    ];
    private readonly HashSet<string> _originalChannels = new(StringComparer.Ordinal);
    private CheckBox _master = null!;
    private CheckBox _food = null!;
    private TextView _status = null!;
    private FDriverFoodNotificationService? _publisher;
    private long? _publishedAt;
    private bool _closed;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Intent extras and saved instance data are deliberately not consumed as fixture input.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            foreach (var channel in Manager()?.NotificationChannels ?? [])
                if (channel.Id is { } id) _originalChannels.Add(id);
        var scroll = new ScrollView(this);
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.SetPadding(Dp(20), Dp(28), Dp(20), Dp(20));
        content.SetBackgroundColor(global::Android.Graphics.Color.White);
        scroll.AddView(content);
        SetContentView(scroll);
        AddText(content, "음식 알림 화면 확인", 24);
        AddText(content, "기기 화면 확인용 · 실제 배차가 아닙니다", 17);
        AddText(content, "이 화면의 선택은 저장하지 않습니다. 실제 계정·로그인·운행·배차에는 변경을 보내지 않습니다.", 15);
        var icon = new ImageView(this);
        icon.SetImageResource(global::FDriverApp.Resource.Drawable.ic_driver_food);
        icon.SetColorFilter(global::Android.Graphics.Color.ParseColor("#2563EB"));
        content.AddView(icon, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        AddText(content, DriverRecommendationAlertPresentation.Resolve(DriverWorkKinds.FoodDelivery)!.Title, 18);
        _master = AddChoice(content, "전체 추천 알림 켜기");
        _food = AddChoice(content, "음식 배달 알림 켜기");
        AddButton(content, "기기 알림 허용", RequestPermission);
        AddButton(content, "음식 시험 알림 보내기", Publish);
        AddButton(content, "알림 지우기", () => { ClearOwnAlert(); _status.Text = "시험 알림을 지웠습니다."; });
        AddButton(content, "닫기", Finish);
        _status = AddText(content, "두 선택을 켜고 시험 알림을 보내세요. 끄면 게시가 차단됩니다.", 16);
        AddText(content, "시험 알림은 최대 90초 동안 표시됩니다. 알림을 누르면 실제 앱이 열리므로 이 시험에서는 알림창의 그림·문구만 확인해 주세요.", 14);
    }

    private void Publish()
    {
        if (HasOtherAlert()) { _status.Text = "기존 배차 알림이 있어 시험을 보류했습니다. 기존 알림은 변경하지 않았습니다."; return; }
        var preferences = Preferences.Default;
        var snapshot = PreferenceKeys.ToDictionary(key => key,
            key => (Exists: preferences.ContainsKey(key), Value: preferences.Get(key, string.Empty)), StringComparer.Ordinal);
        try
        {
            _publisher ??= new FDriverFoodNotificationService();
            _publisher.ApplySettings(Owner, new 기사알림설정응답
            {
                DriverId = Owner, 배차추천알림사용 = _master.Checked, 음식배달추천알림사용 = _food.Checked,
                소리사용 = false, 진동사용 = false, 야간알림제한 = false
            }, DateTime.UtcNow);
            var posted = _publisher.PublishChecked(Owner, "visual-food-" + Guid.NewGuid().ToString("N"),
                DateTime.UtcNow.AddSeconds(90), DriverWorkKinds.FoodDelivery, CancellationToken.None, () => !_closed);
            if (posted) _publishedAt = Manager()?.GetActiveNotifications()?.FirstOrDefault(x => x.Id == AlertId)?.PostTime;
            _status.Text = posted ? "음식 시험 알림을 표시했습니다. 휴대폰 알림창을 내려 식기 그림과 문구를 확인하세요."
                : !_master.Checked || !_food.Checked ? "차단 확인: 전체 알림 또는 음식 알림이 꺼져 있어 게시하지 않았습니다."
                : "알림을 표시하지 못했습니다. 기기 알림 권한과 기존 채널 설정을 확인해 주세요.";
        }
        catch (Exception)
        {
            _status.Text = "시험을 완료하지 못했습니다. 기존 설정은 복원합니다.";
        }
        finally
        {
            // Raw values stay in process memory only, never in screenshots, files or logs.
            foreach (var (key, prior) in snapshot)
                if (prior.Exists) preferences.Set(key, prior.Value); else preferences.Remove(key);
        }
    }

    private bool HasOtherAlert()
        => Manager()?.GetActiveNotifications()?.Any(x => x.Id == AlertId && x.PostTime != _publishedAt) == true;

    private void ClearOwnAlert()
    {
        if (_publishedAt.HasValue && !HasOtherAlert()) _publisher?.Cancel();
        _publishedAt = null;
    }

    private void RequestPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
            && CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            RequestPermissions([global::Android.Manifest.Permission.PostNotifications], PermissionRequest);
        else _status.Text = "기기 알림 권한 상태를 확인했습니다. 시험 알림 보내기를 눌러 주세요.";
    }

    private NotificationManager? Manager()
        => GetSystemService(global::Android.Content.Context.NotificationService) as NotificationManager;

    protected override void OnDestroy()
    {
        _closed = true;
        var otherAlert = HasOtherAlert();
        ClearOwnAlert();
        if (!otherAlert && Build.VERSION.SdkInt >= BuildVersionCodes.O)
            foreach (var channel in Manager()?.NotificationChannels ?? [])
                if (channel.Id is { } id && id.StartsWith("food_dispatch_alerts.v2.", StringComparison.Ordinal)
                    && !_originalChannels.Contains(id)) Manager()?.DeleteNotificationChannel(id);
        base.OnDestroy();
    }

    private CheckBox AddChoice(LinearLayout content, string text)
    {
        var choice = new CheckBox(this) { Text = text, Checked = false };
        choice.SetTextColor(global::Android.Graphics.Color.ParseColor("#172B36"));
        choice.SetMinHeight(Dp(52));
        choice.CheckedChange += (_, _) => { ClearOwnAlert(); if (_status is not null) _status.Text = "선택을 변경했습니다. 이 확인 화면에서만 적용됩니다."; };
        content.AddView(choice);
        return choice;
    }

    private TextView AddText(LinearLayout content, string text, float size)
    {
        var view = new TextView(this) { Text = text, TextSize = size };
        view.SetTextColor(global::Android.Graphics.Color.ParseColor("#172B36"));
        view.SetPadding(0, Dp(6), 0, Dp(12));
        content.AddView(view);
        return view;
    }

    private void AddButton(LinearLayout content, string text, Action action)
    {
        var button = new Button(this) { Text = text };
        button.SetAllCaps(false);
        button.SetMinHeight(Dp(52));
        button.Click += (_, _) => action();
        content.AddView(button, new LinearLayout.LayoutParams(-1, -2));
    }

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + .5f);
}
#endif
