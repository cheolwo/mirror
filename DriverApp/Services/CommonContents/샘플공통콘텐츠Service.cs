using Hongdal.Contracts.CommonContents;

#pragma warning disable CS8602

namespace DriverApp.Services.CommonContents;

public sealed class 샘플공통콘텐츠Service : I공통콘텐츠Service
{
    private long _nextSessionId = 1;
    private readonly Dictionary<long, int> _sessionProgress = new();

    public Task<홍달위젯콘텐츠Dto?> 위젯콘텐츠조회Async(string 위치, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dto = new 홍달위젯콘텐츠Dto
        {
            콘텐츠Id = 1,
            제목 = 위치.Equals("lock", StringComparison.OrdinalIgnoreCase) ? "살뜰 잠금화면 콘텐츠" : "살뜰 홈 위젯 콘텐츠",
            설명 = "살뜰 공통콘텐츠 샘플 카드",
            이미지Url = null,
            이동Url = "https://example.invalid/hongdal-common-content",
            상태문구 = "살뜰과 연결됨"
        };

        return Task.FromResult<홍달위젯콘텐츠Dto?>(dto);
    }

    public Task<long?> 시청시작Async(long 콘텐츠Id, int 영상전체초, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionId = _nextSessionId++;
        _sessionProgress[sessionId] = 0;
        return Task.FromResult<long?>(sessionId);
    }

    public Task 시청진행저장Async(long 세션Id, int 현재시청초, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_sessionProgress.TryGetValue(세션Id, out var current))
        {
            _sessionProgress[세션Id] = Math.Max(current, Math.Max(0, 현재시청초));
        }

        return Task.CompletedTask;
    }

    public Task<콘텐츠시청완료Result?> 시청완료Async(long 세션Id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _sessionProgress.TryGetValue(세션Id, out var watchedSeconds);
        var rewarded = watchedSeconds >= 30;

        var result = new 콘텐츠시청완료Result
        {
            보상지급여부 = rewarded,
            메시지 = rewarded ? "샘플 보상이 지급되었습니다." : "샘플 기준 시청 시간이 부족합니다.",
            지급포인트 = rewarded ? 300 : 0,
            할인율 = 0m,
            할인금액 = 0
        };

        return Task.FromResult<콘텐츠시청완료Result?>(result);
    }

    public Task<홍달위젯콘텐츠Dto?> 위젯콘텐츠동기화Async(string 위치, CancellationToken cancellationToken = default)
    {
        return 위젯콘텐츠동기화내부Async(위치, cancellationToken);
    }

    private async Task<홍달위젯콘텐츠Dto?> 위젯콘텐츠동기화내부Async(string 위치, CancellationToken cancellationToken)
    {
        var content = await 위젯콘텐츠조회Async(위치, cancellationToken);
        if (content is null)
        {
            return null;
        }

#if ANDROID
        var context = global::Android.App.Application.Context;
        var 저장소 = context.GetSharedPreferences("hongdal_widget", global::Android.Content.FileCreationMode.Private);
        저장소.Edit()!
            .PutString("title", content.제목)
            .PutString("status", content.상태문구)
            .PutString("image_path", string.Empty)
            .Apply();

        var manager = global::Android.Appwidget.AppWidgetManager.GetInstance(context);
        var component = new global::Android.Content.ComponentName(
            context,
            Java.Lang.Class.FromType(typeof(global::DriverApp.홍달위젯Provider)));
        var ids = manager.GetAppWidgetIds(component);

        if (ids.Length > 0)
        {
            var intent = new global::Android.Content.Intent(context, typeof(global::DriverApp.홍달위젯Provider));
            intent.SetAction(global::Android.Appwidget.AppWidgetManager.ActionAppwidgetUpdate);
            intent.PutExtra(global::Android.Appwidget.AppWidgetManager.ExtraAppwidgetIds, ids);
            context.SendBroadcast(intent);
        }
#endif

        return content;
    }
}
