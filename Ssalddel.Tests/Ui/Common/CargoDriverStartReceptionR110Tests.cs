using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoDriverStartReceptionR110Tests
{
    [Fact]
    public async Task 첫_빈_화면에서도_운행시작과_수신_꺼짐을_구분한다()
    {
        var api = new Api(); using var model = Model(api);
        await model.InitializeAsync("cargo-driver");
        Assert.Null(model.Error); Assert.Empty(model.Items);
        Assert.True(Assert.Single(model.EmptyActions, a => a.Key == CargoDriverWorkSession.StartWork).Enabled);
        Assert.False(Assert.Single(model.EmptyActions, a => a.Key == CargoDriverWorkSession.EnableDispatch).Enabled);
        Assert.Contains("운행을 시작한 뒤", model.Message);
        Assert.Empty(api.Mutations);
    }

    [Theory]
    [InlineData("denied")]
    [InlineData("stale")]
    [InlineData("invalid")]
    public async Task 위치를_확인하지_못하면_운행시작을_보내지_않는다(string kind)
    {
        var api = new Api(); var location = new Location { Kind = kind }; using var model = Model(api, location);
        await model.InitializeAsync("cargo-driver");
        await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        Assert.DoesNotContain(api.Mutations, m => m.Path.EndsWith("/start", StringComparison.Ordinal));
        Assert.False(model.FoodStartStateUnconfirmed);
        Assert.Contains("위치", model.CommandError);
    }

    [Fact]
    public async Task 시작은_기존_화물_계약과_요청아이디를_쓰며_수신을_자동으로_켜지_않는다()
    {
        var api = new Api(); using var model = Model(api);
        await model.InitializeAsync("cargo-driver"); await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        var start = Assert.Single(api.Mutations, m => m.Path.EndsWith("/start", StringComparison.Ordinal));
        var body = Assert.IsType<기사운행시작요청>(start.Body);
        Assert.NotEqual(Guid.Empty, body.ClientRequestId); Assert.False(body.커뮤니티운행공개); Assert.False(body.커뮤니티구단위위치공개동의);
        Assert.Equal(body.ClientRequestId, api.Status.WorkStartClientRequestId);
        Assert.DoesNotContain(api.Mutations, m => m.Path.EndsWith("/intent", StringComparison.Ordinal));
        Assert.Equal(운영배차수신의사Code.Off, api.Reception.수신의사Code);
        Assert.True(Assert.Single(model.EmptyActions, a => a.Key == CargoDriverWorkSession.EnableDispatch).Enabled);
        Assert.Contains(api.Mutations, m => m.Body is 기사위치갱신요청 p && p.AppKey == 기사앱식별자.CargoYongdalDriverApp);
        Assert.All(api.Mutations, m => Assert.Equal("cargo-driver", m.Role));
    }

    [Fact]
    public async Task 이미_화물_운행중이면_새_근무를_만들지_않는다()
    {
        var api = new Api(); api.Driving();
        var session = new CargoDriverWorkSession(new(api), new Location());
        await session.PerformAsync(CargoDriverWorkSession.StartWork, Guid.NewGuid(), false, default);
        Assert.DoesNotContain(api.Mutations, m => m.Path.EndsWith("/start", StringComparison.Ordinal));
        Assert.DoesNotContain(api.Mutations, m => m.Path.EndsWith("/intent", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("FoodDelivery")]
    [InlineData("UnknownExecution")]
    [InlineData(null)]
    public async Task 다른_업무나_미확인_운행은_임의로_시작종료수신변경하지_않는다(string? executionType)
    {
        var api = new Api(); api.Driving(); api.Status.운송실행유형 = executionType;
        using var model = Model(api); await model.InitializeAsync("cargo-driver");
        Assert.All(model.EmptyActions, a => Assert.False(a.Enabled));
        Assert.Contains("다른 업무", model.Message);
        await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        await model.PerformAsync("", CargoDriverWorkSession.StopWork);
        await model.PerformAsync("", CargoDriverWorkSession.EnableDispatch);
        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task 수행중인_운송이_있으면_종료_버튼과_직접_호출을_모두_막는다()
    {
        var api = new Api { HasActive = true }; api.Driving(); using var model = Model(api);
        await model.InitializeAsync("cargo-driver");
        var stop = Assert.Single(model.EmptyActions, a => a.Key == CargoDriverWorkSession.StopWork);
        Assert.False(stop.Enabled); Assert.Contains("운송을 완료", stop.DisabledReason);
        var session = new CargoDriverWorkSession(new(api), new Location());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PerformAsync(CargoDriverWorkSession.StopWork, Guid.NewGuid(), true, default));
        Assert.DoesNotContain(api.Mutations, m => m.Path.EndsWith("/stop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 시작_성공응답_유실은_같은_정본으로_확인하고_다시_전송하지_않는다()
    {
        var api = new Api { LoseStartResponse = true }; using var model = Model(api);
        await model.InitializeAsync("cargo-driver"); await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        Assert.True(model.FoodStartStateUnconfirmed);
        await model.RefreshAsync();
        Assert.False(model.FoodStartStateUnconfirmed);
        Assert.Single(api.Mutations, m => m.Path.EndsWith("/start", StringComparison.Ordinal));
        Assert.DoesNotContain(api.Mutations, m => m.Path.EndsWith("/intent", StringComparison.Ordinal));
        Assert.Contains(model.EmptyActions, a => a.Key == CargoDriverWorkSession.StopWork);
    }

    [Fact]
    public async Task 서버에_도달했는지_모르는_시작은_대기_조회만으로_새로_보내지_않는다()
    {
        var api = new Api { LoseStartBeforeCommit = true }; using var model = Model(api);
        await model.InitializeAsync("cargo-driver"); await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        await model.RefreshAsync(); await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        Assert.True(model.FoodStartStateUnconfirmed);
        Assert.Single(api.Mutations, m => m.Path.EndsWith("/start", StringComparison.Ordinal));
        Assert.All(model.EmptyActions, a => Assert.False(a.Enabled));
    }

    [Fact]
    public async Task 명시된_거절_표식과_대기_정본이_일치할_때만_새_시도를_연다()
    {
        var api = new Api { RejectStart = true }; using var model = Model(api);
        await model.InitializeAsync("cargo-driver"); await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        Assert.True(model.FoodStartStateUnconfirmed);
        await model.RefreshAsync();
        Assert.False(model.FoodStartStateUnconfirmed);
        Assert.True(Assert.Single(model.EmptyActions, a => a.Key == CargoDriverWorkSession.StartWork).Enabled);
        Assert.Contains("거절", model.Message);
        Assert.Single(api.Mutations, m => m.Path.EndsWith("/start", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 이전_거절은_다른_운행_종료후_새_시작의_응답유실_잠금을_해제하지_않는다()
    {
        var api = new Api { RejectStart = true }; using var model = Model(api);
        await model.InitializeAsync("cargo-driver");
        await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        Assert.True(model.FoodStartStateUnconfirmed);

        api.RejectStart = false; api.Driving(); api.Status.WorkStartClientRequestId = Guid.NewGuid();
        await model.RefreshAsync();
        Assert.False(model.FoodStartStateUnconfirmed);
        await model.PerformAsync("", CargoDriverWorkSession.StopWork);
        Assert.Equal("대기", api.Status.Status);

        api.LoseStartBeforeCommit = true;
        await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        await model.RefreshAsync();
        Assert.True(model.FoodStartStateUnconfirmed);
        Assert.False(Assert.Single(model.EmptyActions, a => a.Key == CargoDriverWorkSession.StartWork).Enabled);
        await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        Assert.Equal(2, api.Mutations.Count(m => m.Path.EndsWith("/start", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task 수신은_본인_토글에서만_켜며_미응답_중지도_명시적으로_해제한다()
    {
        var api = new Api(); api.Driving(); api.Reception.미응답자동중지여부 = true; using var model = Model(api);
        await model.InitializeAsync("cargo-driver");
        Assert.Contains("3회", model.DispatchReceptionNotice);
        await model.PerformAsync("", CargoDriverWorkSession.EnableDispatch);
        var changed = Assert.IsType<운영배차수신의사변경요청>(Assert.Single(api.Mutations, m => m.Path.EndsWith("/intent", StringComparison.Ordinal)).Body);
        Assert.Equal(운영배차수신의사Code.On, changed.수신의사Code); Assert.True(changed.미응답자동중지해제); Assert.False(changed.최초On조건부적용);
        Assert.Null(model.DispatchReceptionNotice); Assert.False(model.FoodDispatchStateUnconfirmed);
        Assert.Contains(model.EmptyActions, a => a.Key == CargoDriverWorkSession.DisableDispatch);
    }

    [Fact]
    public async Task 수신_성공응답_유실도_다시_PUT하지_않고_조회한다()
    {
        var api = new Api { LoseReceptionResponse = true }; api.Driving(); using var model = Model(api);
        await model.InitializeAsync("cargo-driver"); await model.PerformAsync("", CargoDriverWorkSession.EnableDispatch);
        Assert.True(model.FoodDispatchStateUnconfirmed);
        await model.RefreshAsync();
        Assert.False(model.FoodDispatchStateUnconfirmed);
        Assert.Single(api.Mutations, m => m.Path.EndsWith("/intent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 종료_성공응답_유실도_조회로_확인한다()
    {
        var api = new Api { LoseStopResponse = true }; api.Driving(); using var model = Model(api);
        await model.InitializeAsync("cargo-driver"); await model.PerformAsync("", CargoDriverWorkSession.StopWork);
        Assert.True(model.FoodDispatchStateUnconfirmed);
        await model.RefreshAsync();
        Assert.False(model.FoodDispatchStateUnconfirmed);
        Assert.Contains(model.EmptyActions, a => a.Key == CargoDriverWorkSession.StartWork && a.Enabled);
        Assert.Single(api.Mutations, m => m.Path.EndsWith("/stop", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("reception")]
    public async Task 상태_조회_실패는_꺼짐이나_빈_업무로_대체하지_않는다(string failure)
    {
        var api = new Api { FailedRead = failure }; using var model = Model(api);
        await model.InitializeAsync("cargo-driver");
        Assert.NotNull(model.Error); Assert.Empty(model.EmptyActions); Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task 운행과_수신_응답의_기사아이디가_다르면_노출하지_않는다()
    {
        var api = new Api(); api.Reception.주체Id = "different-owner"; using var model = Model(api);
        await model.InitializeAsync("cargo-driver");
        Assert.NotNull(model.Error); Assert.Empty(model.EmptyActions); Assert.Empty(api.Mutations);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 인증이나_권한이_만료되면_기존_운행조작을_남기지_않는다(int statusCode)
    {
        var api = new Api(); using var model = Model(api);
        await model.InitializeAsync("cargo-driver");
        Assert.NotEmpty(model.EmptyActions);
        api.FailedRead = "status"; api.FailureStatus = statusCode;
        await model.RefreshAsync();
        Assert.Equal(statusCode == 401, model.RequiresLogin);
        Assert.Equal(statusCode == 403, model.AccessDenied);
        Assert.NotNull(model.Error); Assert.Empty(model.EmptyActions);
        await model.PerformAsync("", CargoDriverWorkSession.StartWork);
        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task 자동_위치조회는_권한창을_열지않고_수신변경하지_않는다()
    {
        var api = new Api(); api.Driving(); var location = new Location();
        using var model = Model(api, location); await model.InitializeAsync("cargo-driver");
        Assert.Equal(0, location.Prompts); Assert.True(location.SilentReads > 0);
        Assert.DoesNotContain(api.Mutations, m => m.Path.EndsWith("/intent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 기존_화물_토글_표현은_접근성_켜짐상태와_한국어_문구를_유지한다()
    {
        var services = new ServiceCollection().AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<FoodDriverDispatchToggle>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(FoodDriverDispatchToggle.Action)] = new RoleWorkspaceAction(CargoDriverWorkSession.DisableDispatch, "신규 배차 받기"),
                [nameof(FoodDriverDispatchToggle.ReceivingActionKey)] = CargoDriverWorkSession.DisableDispatch
            }))).ToHtmlString());
        html = WebUtility.HtmlDecode(html);
        Assert.Contains("aria-checked=\"true\"", html); Assert.Contains("켜짐", html);
        Assert.Contains("신규 배차 받기", html);
        Assert.NotNull(RoleWorkspaceActionIconResolver.Resolve(CargoDriverWorkSession.StartWork));
        Assert.Equal(RoleWorkspaceActionUsage.Conditional, RoleWorkspaceActionPolicy.GetUsage("cargo-driver", new(CargoDriverWorkSession.StartWork, "운행 시작")));
    }

    private static RoleWorkspaceViewModel Model(Api api, Location? location = null)
        => new([new CargoDriverWorkspaceAdapter(new(api), location ?? new Location())], new Access(), new RoleWorkspaceState());

    private sealed class Location : IRoleWorkspaceLocationProvider
    {
        public string Kind { get; set; } = "valid";
        public int Prompts { get; private set; }
        public int SilentReads { get; private set; }
        private RoleWorkspaceLocation? Current => Kind == "denied" ? null : new(Kind == "invalid" ? 999 : 37.58, 127.08, 5,
            DateTimeOffset.UtcNow.AddMinutes(Kind == "stale" ? -10 : 0));
        public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct) { Prompts++; return Task.FromResult(Current); }
        public Task<RoleWorkspaceLocation?> GetCurrentWithoutPermissionPromptAsync(CancellationToken ct) { SilentReads++; return Task.FromResult(Current); }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("owner", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Api : IRoleWorkspaceApi
    {
        public 기사운행상태응답 Status { get; } = new() { DriverId = "owner", Status = "대기" };
        public 운영배차수신상태Dto Reception { get; } = new() { 주체Id = "owner", 수신의사Code = 운영배차수신의사Code.Off, 실효상태Code = 운영배차실효상태Code.배차가능 };
        public List<(string Role, string Path, object Body)> Mutations { get; } = [];
        public bool HasActive { get; set; }
        public bool LoseStartResponse { get; set; }
        public bool LoseStartBeforeCommit { get; set; }
        public bool LoseReceptionResponse { get; set; }
        public bool LoseStopResponse { get; set; }
        public bool RejectStart { get; set; }
        public string? FailedRead { get; set; }
        public int FailureStatus { get; set; } = 503;
        public void Driving() { Status.Status = "운행중"; Status.ShiftId = 1; Status.운송실행유형 = 운송실행유형코드.화물운송; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
        {
            Assert.Equal("cargo-driver", roleKey); ct.ThrowIfCancellationRequested();
            if (FailedRead == "status" && path.EndsWith("/status", StringComparison.Ordinal)
                || FailedRead == "reception" && path.EndsWith("/availability", StringComparison.Ordinal))
                throw new RoleWorkspaceAccessException(FailureStatus, "조회 불가");
            object value = path switch
            {
                "api/v1/driver/work/status" => Status,
                "api/v1/driver/operational-dispatch/availability" => Reception,
                "api/v1/driver/transports/workspace" => new 기사화물운송작업공간응답 { 활성운송목록 = HasActive ? [new() { Id = 3, 상태 = "운송중" }] : [] },
                "api/v1/driver/transports/3" => new 기사운송상세응답 { Id = 3, 상태 = "운송중", 기사_운송자 = "owner" },
                "api/v1/driver/recommendations" => Array.Empty<기사배차추천항목응답>(),
                _ => throw new InvalidOperationException(path)
            };
            return Task.FromResult((T)value);
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Mutations.Add((roleKey, path, body));
            object? value;
            if (path.EndsWith("/start", StringComparison.Ordinal))
            {
                var request = Assert.IsType<기사운행시작요청>(body);
                if (RejectStart) throw new RoleWorkspaceAccessException(409, "조건 거절", rejectedWorkStartRequestId: request.ClientRequestId);
                if (LoseStartBeforeCommit) throw new HttpRequestException("응답 없음");
                Driving(); Status.WorkStartClientRequestId = request.ClientRequestId;
                if (LoseStartResponse) throw new HttpRequestException("성공 응답 유실");
                value = new 기사운행시작응답 { DriverId = "owner", Status = "운행중", ShiftId = 1 };
            }
            else if (path.EndsWith("/location", StringComparison.Ordinal))
            {
                var position = Assert.IsType<기사위치갱신요청>(body);
                value = new 기사위치갱신응답 { DriverId = "owner", Status = Status.Status, 현재위도 = position.위도, 현재경도 = position.경도 };
            }
            else if (path.EndsWith("/stop", StringComparison.Ordinal))
            {
                Status.Status = "대기";
                if (LoseStopResponse) throw new HttpRequestException("종료 응답 유실");
                value = null;
            }
            else throw new InvalidOperationException(path);
            return Task.FromResult(value is null ? default : (T)value);
        }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        {
            Assert.EndsWith("/availability/intent", path); Mutations.Add((roleKey, path, body));
            var request = Assert.IsType<운영배차수신의사변경요청>(body);
            Reception.수신의사Code = request.수신의사Code;
            if (request.미응답자동중지해제) Reception.미응답자동중지여부 = false;
            if (LoseReceptionResponse) throw new HttpRequestException("수신 성공 응답 유실");
            return Task.FromResult((T?)(object)Reception);
        }
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
    }
}
