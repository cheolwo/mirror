using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.Widget;

#pragma warning disable CS8602

namespace DriverApp;

[BroadcastReceiver(Label = "살뜰 위젯", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData("android.appwidget.provider", Resource = "@xml/hongdal_widget_info")]
public sealed class 홍달위젯Provider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is null)
        {
            return;
        }

        var resources = context.Resources;
        var packageName = context.PackageName;
        if (resources is null || string.IsNullOrWhiteSpace(packageName))
        {
            return;
        }

        var layoutId = resources.GetIdentifier("hongdal_widget", "layout", packageName);
        var rootId = resources.GetIdentifier("widget_root", "id", packageName);
        var titleId = resources.GetIdentifier("widget_title", "id", packageName);
        var statusId = resources.GetIdentifier("widget_status", "id", packageName);
        var imageId = resources.GetIdentifier("widget_image", "id", packageName);

        if (layoutId == 0)
        {
            return;
        }

        foreach (var appWidgetId in appWidgetIds)
        {
            var views = new RemoteViews(packageName, layoutId);

            var 저장소 = context.GetSharedPreferences("hongdal_widget", FileCreationMode.Private);
            var 제목 = 저장소.GetString("title", "살뜰") ?? "살뜰";
            var 상태문구 = 저장소.GetString("status", "살뜰과 연결됨") ?? "살뜰과 연결됨";
            var 이미지경로 = 저장소.GetString("image_path", null);

            if (titleId != 0)
            {
                views.SetTextViewText(titleId, 제목);
            }

            if (statusId != 0)
            {
                views.SetTextViewText(statusId, 상태문구);
            }

            if (!string.IsNullOrWhiteSpace(이미지경로))
            {
                var bitmap = BitmapFactory.DecodeFile(이미지경로);
                if (bitmap is not null)
                {
                    if (imageId != 0)
                    {
                        views.SetImageViewBitmap(imageId, bitmap);
                    }
                }
            }

            var intent = context.PackageManager?.GetLaunchIntentForPackage(packageName);
            if (intent is not null)
            {
                intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);

                var pendingIntent = PendingIntent.GetActivity(
                    context,
                    0,
                    intent,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

                if (rootId != 0)
                {
                    views.SetOnClickPendingIntent(rootId, pendingIntent);
                }
            }

            appWidgetManager.UpdateAppWidget(appWidgetId, views);
        }
    }
}
