using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.WebApp;

public sealed class CommunityMapSnapshotSessionTests
{
    [Fact]
    public async Task 동일판본은_유지하고_변경판본만_현재지도시점을_보존해_적용한다()
    {
        var revisions = new Queue<string>(["r1", "r1", "r2"]);
        var handler = new MapSessionHttp((_, _) => Task.FromResult(MapSessionHttp.Json(MapSessionHttp.Snapshot(revisions.Dequeue()))));
        var applications = new List<(string Revision, bool Preserve)>();
        await using var session = SnapshotSession(handler, (snapshot, preserve) => applications.Add((snapshot.Revision, preserve)));
        await session.RefreshAsync(false, default);
        var first = session.AppliedSnapshot;
        await session.RefreshAsync(false, default);
        Assert.Same(first, session.AppliedSnapshot);
        await session.RefreshAsync(false, default);
        Assert.Equal(new[] { ("r1", false), ("r2", true) }, applications);
        Assert.NotNull(session.LastCheckedAt);
        Assert.Null(session.ConnectionMessage);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task 중첩조회는_직렬화하고_요청당시와_현재Dataset이다르면_적용하지않는다()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerCount = 0;
        var handler = new MapSessionHttp((_, _) => handlerResponse());
        Task<HttpResponseMessage> handlerResponse() => handlerCount++ == 0
            ? release.Task : Task.FromResult(MapSessionHttp.Json(MapSessionHttp.Snapshot("r2")));
        var dataset = "day-work";
        var client = MapSessionHttp.Client(handler);
        await using var session = new 커뮤니티지도SnapshotSession(client.조회Async, () => dataset,
            Dispatch, (_, _) => throw new InvalidOperationException("stale dataset applied"), () => { });
        var first = session.RefreshAsync(false, default);
        var second = session.RefreshAsync(false, default);
        Assert.Single(handler.Requests);
        dataset = "night-learning";
        release.SetResult(MapSessionHttp.Json(MapSessionHttp.Snapshot("r1")));
        await Task.WhenAll(first, second);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Contains("dataset=day-work", request.Path));
        Assert.Null(session.AppliedSnapshot);
    }

    [Fact]
    public async Task 실패는_최초내장자료와_마지막Snapshot유지_안내를_구분한다()
    {
        var attempt = 0;
        var handler = new MapSessionHttp((_, _) => Task.FromResult(++attempt == 2
            ? MapSessionHttp.Json(MapSessionHttp.Snapshot("r1"))
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var session = SnapshotSession(handler);
        await session.RefreshAsync(false, default);
        Assert.Contains("현재 내장 공개 카탈로그", session.ConnectionMessage);
        await session.RefreshAsync(false, default);
        var last = session.AppliedSnapshot;
        await session.RefreshAsync(false, default);
        Assert.Same(last, session.AppliedSnapshot);
        Assert.Contains("마지막으로 연결된 자료를 유지", session.ConnectionMessage);
    }

    [Fact]
    public async Task Polling은_즉시조회_원장조회_30초대기_순서이며_폐기때_대기를취소한다()
    {
        var events = new List<string>();
        var waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MapSessionHttp((_, _) => Task.FromResult(MapSessionHttp.Json(MapSessionHttp.Snapshot("r1"))));
        var client = MapSessionHttp.Client(handler);
        var session = new 커뮤니티지도SnapshotSession(client.조회Async, () => "day-work", Dispatch,
            (_, _) => events.Add("snapshot"), () => { }, async (duration, token) =>
            {
                Assert.Equal(TimeSpan.FromSeconds(30), duration);
                events.Add("delay"); waiting.SetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        session.StartPolling(_ => { events.Add("ledger"); return Task.CompletedTask; });
        await waiting.Task;
        await session.DisposeAsync();
        Assert.Equal(["snapshot", "ledger", "delay"], events);
        Assert.Single(handler.Requests);
        Assert.Null(session.ConnectionMessage);
    }

    [Fact]
    public async Task 폐기는_진행중Http도_취소하고_조회실패안내로_바꾸지않는다()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = false;
        var handler = new MapSessionHttp(async (_, token) =>
        {
            started.SetResult(true);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { cancelled = true; throw; }
            return MapSessionHttp.Json(MapSessionHttp.Snapshot("unused"));
        });
        var client = MapSessionHttp.Client(handler);
        var session = new 커뮤니티지도SnapshotSession(client.조회Async, () => "day-work", Dispatch, (_, _) => { }, () => { });
        session.StartPolling(_ => Task.CompletedTask);
        await started.Task;
        await session.DisposeAsync();
        Assert.True(cancelled);
        Assert.Null(session.AppliedSnapshot);
        Assert.Null(session.ConnectionMessage);
    }

    private static Task Dispatch(Action action) { action(); return Task.CompletedTask; }
    private static 커뮤니티지도SnapshotSession SnapshotSession(MapSessionHttp handler, Action<커뮤니티세계지도SnapshotDto, bool>? apply = null)
        => new(MapSessionHttp.Client(handler).조회Async, () => "day-work", Dispatch, apply ?? ((_, _) => { }), () => { });
}

public sealed class CommunityMapOwnLedgerSessionTests
{
    [Fact]
    public async Task 마커없음은_조회하지않고_선택원장부재는_복귀질의를_정리한다()
    {
        string? marker = null; string? ledger = "missing"; var cleared = 0;
        var handler = new MapSessionHttp((_, _) => Task.FromResult(MapSessionHttp.Json(new[] { new 지도신청가원장Response { 원장Id = "mine" } })));
        var session = Session(handler, () => marker, () => ledger, () => { ledger = null; cleared++; });
        session.PendingRefresh = true;
        await session.RefreshAsync(default);
        Assert.Empty(handler.Requests); Assert.Empty(session.Ledgers); Assert.False(session.PendingRefresh);
        marker = "marker-a";
        await session.RefreshAsync(default);
        Assert.Equal("mine", Assert.Single(session.Ledgers).원장Id);
        Assert.Null(ledger); Assert.Equal(1, cleared); Assert.False(session.Loading);
    }

    [Fact]
    public async Task 원형특성화_마커변경뒤_늦은성공은_목록을유지하지만_Pending은_끝낸다()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MapSessionHttp((_, _) => release.Task);
        var marker = "old";
        var session = Session(handler, () => marker);
        var request = session.RefreshAsync(default);
        Assert.True(session.Loading);
        marker = "current";
        session.Ledgers = [new() { 원장Id = "current-ledger" }];
        session.PendingRefresh = true;
        release.SetResult(MapSessionHttp.Json(new[] { new 지도신청가원장Response { 원장Id = "old-ledger" } }));
        await request;
        Assert.Equal("current-ledger", Assert.Single(session.Ledgers).원장Id);
        Assert.False(session.PendingRefresh); Assert.False(session.Loading);
    }

    [Fact]
    public async Task 원형특성화_마커변경뒤_늦은실패는_목록을비우고_실패를표시한다()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MapSessionHttp((_, _) => release.Task);
        var marker = "old";
        var session = Session(handler, () => marker);
        var request = session.RefreshAsync(default);
        marker = "current"; session.Ledgers = [new() { 원장Id = "current-ledger" }];
        release.SetException(new HttpRequestException("read failed"));
        await request;
        Assert.Empty(session.Ledgers); Assert.Equal("read failed", session.Error);
    }

    [Fact]
    public async Task 원형특성화_동일마커의_계정변경은_이전계정응답을_무효화하지않는다()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MapSessionHttp((_, _) => release.Task);
        var http = new HttpClient(handler) { BaseAddress = new("https://example.invalid/") };
        var accountToken = "old-account";
        var session = new 커뮤니티지도내원장Session(async (marker, _, token) =>
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"ledgers?marker={marker}");
            message.Headers.Authorization = new("Bearer", accountToken);
            using var response = await http.SendAsync(message, token);
            return await response.Content.ReadFromJsonAsync<지도신청가원장Response[]>(token) ?? [];
        }, () => "same-marker", () => null, () => { }, () => Task.CompletedTask);
        var request = session.RefreshAsync(default);
        accountToken = "current-account";
        release.SetResult(MapSessionHttp.Json(new[] { new 지도신청가원장Response { 원장Id = "old-account-ledger" } }));
        await request;
        Assert.Equal("old-account", Assert.Single(handler.Requests).Token);
        Assert.Equal("old-account-ledger", Assert.Single(session.Ledgers).원장Id);
    }

    [Fact]
    public async Task 취소는_기존목록을_지우지않고_Loading과Pending을_끝낸다()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MapSessionHttp(async (_, token) =>
        {
            started.SetResult(true); await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return MapSessionHttp.Json(Array.Empty<지도신청가원장Response>());
        });
        var session = Session(handler, () => "marker");
        session.Ledgers = [new() { 원장Id = "keep" }]; session.PendingRefresh = true;
        using var cancellation = new CancellationTokenSource();
        var request = session.RefreshAsync(cancellation.Token);
        await started.Task; cancellation.Cancel(); await request;
        Assert.Equal("keep", Assert.Single(session.Ledgers).원장Id);
        Assert.Null(session.Error); Assert.False(session.Loading); Assert.False(session.PendingRefresh);
    }

    private static 커뮤니티지도내원장Session Session(MapSessionHttp handler, Func<string?> marker,
        Func<string?>? ledger = null, Action? clear = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new("https://example.invalid/") };
        return new(async (markerId, ledgerId, token) =>
            await http.GetFromJsonAsync<지도신청가원장Response[]>($"ledgers?marker={markerId}&ledger={ledgerId}", token) ?? [],
            marker, ledger ?? (() => null), clear ?? (() => { }), () => Task.CompletedTask);
    }
}

public sealed class CommunityMapQuestionAuthoringSessionTests
{
    [Fact]
    public async Task 익명초안은_편집할수있고_인증과필수입력과출처확인이_게시가능여부를_정한다()
    {
        var handler = new MapSessionHttp((_, _) => Task.FromResult(MapSessionHttp.Json(MapSessionHttp.Draft())));
        var session = Session(handler);
        await session.OpenAsync(MapSessionHttp.Observation(), "day-work", QuestionPurpose.Question, () => null);
        Assert.Equal("초안 제목", session.Title); Assert.Equal("초안 내용", session.Body);
        Assert.False(session.CanPublish(false)); Assert.False(session.CanPublish(true));
        session.Nickname = "사용자"; session.Password = " 1234 "; session.ConfirmSourceReference = true;
        Assert.False(session.CanPublish(false)); Assert.True(session.CanPublish(true));
        Assert.Equal("초안 제목", session.Title); Assert.Equal("초안 내용", session.Body);
        Assert.Equal("사용자", session.Nickname); Assert.True(session.ConfirmSourceReference);
        session.Password = "123"; Assert.False(session.CanPublish(true));
        Assert.Single(handler.Requests); Assert.EndsWith("/question-draft", handler.Requests[0].Path);
        Assert.Null(handler.Requests[0].Token); Assert.Null(session.Published);
    }

    [Fact]
    public async Task 게시실패는_입력을보존하고_명시적재시도만_POST하며_성공과닫기초기화를_구분한다()
    {
        var posts = 0;
        var handler = new MapSessionHttp((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("question-draft")
            ? MapSessionHttp.Json(MapSessionHttp.Draft())
            : ++posts == 1 ? new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("publish failed") }
            : MapSessionHttp.Json(new 커뮤니티세계지도질문게시Response { PostHref = "/community/posts/42" })));
        var session = Session(handler);
        await session.OpenAsync(MapSessionHttp.Observation(), "day-work", QuestionPurpose.Gathering, () => "user");
        var draft = session.Draft;
        session.Title = " 내 제목 "; session.Body = " 내 내용 "; session.Nickname = " 내 이름 ";
        session.Password = " 1234 "; session.ConfirmSourceReference = true;
        await session.PublishAsync("observation-a", "day-work", "token");
        Assert.Equal(1, posts); Assert.Same(draft, session.Draft); Assert.False(session.Publishing);
        Assert.Equal(" 내 제목 ", session.Title); Assert.Equal(" 내 내용 ", session.Body);
        Assert.Equal(" 내 이름 ", session.Nickname); Assert.Equal(" 1234 ", session.Password);
        Assert.True(session.ConfirmSourceReference); Assert.Contains("publish failed", session.Error);
        await session.PublishAsync("observation-a", "day-work", "token");
        Assert.Equal(2, posts); Assert.Null(session.Draft); Assert.Equal(string.Empty, session.Password);
        Assert.False(session.ConfirmSourceReference); Assert.Null(session.Error);
        Assert.Equal(" 내 제목 ", session.Title);
        Assert.Equal(" 내 내용 ", session.Body);
        var sent = handler.Requests.Last(); Assert.Equal("token", sent.Token);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal("내 제목", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("내 내용", body.RootElement.GetProperty("body").GetString());
        Assert.Equal("내 이름", body.RootElement.GetProperty("nickname").GetString());
        Assert.Equal(" 1234 ", body.RootElement.GetProperty("password").GetString());
        Assert.True(body.RootElement.GetProperty("isInterestGatheringEnabled").GetBoolean());
        var published = session.Published;
        session.Reset(clearPublished: false); Assert.Same(published, session.Published);
        Assert.Equal(string.Empty, session.Title); Assert.Equal(string.Empty, session.Body);
        session.Reset(); Assert.Null(session.Published);
    }

    [Fact]
    public async Task 원형특성화_초안대기중_초기화해도_늦은응답이_다시채운다()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MapSessionHttp((_, _) => release.Task);
        var session = Session(handler); var nickname = "before";
        var request = session.OpenAsync(MapSessionHttp.Observation(), "day-work", QuestionPurpose.Proposal, () => nickname);
        session.Reset(); nickname = "after";
        release.SetResult(MapSessionHttp.Json(MapSessionHttp.Draft())); await request;
        Assert.NotNull(session.Draft); Assert.Equal("초안 제목", session.Title);
        Assert.Equal("after", session.Nickname); Assert.False(session.DraftLoading);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task 원형특성화_게시대기중_초기화해도_늦은게시결과가_표시상태에_남는다()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MapSessionHttp((request, _) => request.RequestUri!.AbsolutePath.EndsWith("question-draft")
            ? Task.FromResult(MapSessionHttp.Json(MapSessionHttp.Draft())) : release.Task);
        var session = Session(handler);
        await session.OpenAsync(MapSessionHttp.Observation(), "day-work", QuestionPurpose.Question, () => "user");
        var request = session.PublishAsync("observation-a", "day-work", "token");
        session.Reset(); release.SetResult(MapSessionHttp.Json(new 커뮤니티세계지도질문게시Response { PostHref = "/community/posts/42" }));
        await request;
        Assert.Equal("/community/posts/42", session.Published?.PostHref);
        Assert.Null(session.Draft); Assert.False(session.Publishing);
        Assert.Equal(2, handler.Requests.Count);
    }

    private static 커뮤니티지도질문작성Session Session(MapSessionHttp handler)
    {
        var client = MapSessionHttp.Client(handler);
        return new(client.질문초안생성Async, client.질문게시Async);
    }
}

internal sealed class MapSessionHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<(HttpMethod Method, string Path, string? Token, string? Body)> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
        return await respond(request, cancellationToken);
    }
    public static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    public static 커뮤니티세계지도Client Client(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new("https://example.invalid/") });
    public static 커뮤니티세계지도SnapshotDto Snapshot(string revision) => new("day-work", revision, DateTimeOffset.UtcNow, [], []);
    public static 커뮤니티세계지도ObservationDto Observation() => new("observation-a", "day-work", "public-price", "KR", "한국",
        36.5, 127.8, "공개 자료", "공개 요약", "공식 출처", DateTimeOffset.UtcNow, "curated", "/detail");
    public static 커뮤니티세계지도질문초안Response Draft() => new()
    {
        Evidence = new() { ObservationStableId = "observation-a" },
        SuggestedPost = new() { Title = "초안 제목", Body = "초안 내용", OriginalLanguageCode = "ko" }
    };
}
