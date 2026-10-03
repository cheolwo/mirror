using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererFoodOrderComposerRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 복원실패업무화면은_진행표시대신로그인진입과오류를보이고_작성내용으로계속한다(bool throws)
    {
        const string restoreError = "합성 저장 세션을 복원하지 못했습니다.";
        var services = new FoodServices();
        var auth = new AuthenticationService
        {
            RestoreError = throws ? null : restoreError,
            RestoreFailure = throws ? new IOException(restoreError) : null
        };
        var page = await CreatePageAsync(services, auth);
        var requestId = page.작성.클라이언트요청Id;
        Assert.False(page.인증.초기화됨);
        Assert.False(page.인증.처리중);
        Assert.True(page.인증.오류발생);

        var failedHtml = await RenderComposerAsync(page.인증, page.작성);

        Assert.DoesNotContain("type=\"password\"", failedHtml);
        Assert.Contains("로그인하고 주문 계속", failedHtml);
        Assert.Contains(restoreError, failedHtml);
        Assert.DoesNotContain("주문자 로그인 상태를 확인하고 있습니다.", failedHtml);
        AssertDraft(page.작성, requestId);

        Assert.True(await page.로그인Async("orderer", "test-password"));
        var recoveredHtml = await RenderComposerAsync(page.인증, page.작성);
        Assert.Contains("수령인 이름", recoveredHtml);
        Assert.DoesNotContain("type=\"password\"", recoveredHtml);
        AssertDraft(page.작성, requestId);
        Assert.True(await page.주문등록Async());
        Assert.Equal(requestId, Assert.Single(services.Requests).클라이언트요청Id);
    }

    [Fact]
    public async Task 미초기화업무화면은_로그인진입만보이고_실제복원처리중에만진행표시를보인다()
    {
        var completion = new TaskCompletionSource<주문자앱인증결과>(TaskCreationOptions.RunContinuationsAsynchronously);
        var auth = new 주문자앱인증ViewModel(new AuthenticationService { PendingRestore = completion.Task });
        var page = await CreatePageAsync(new FoodServices(), new AuthenticationService());
        var requestId = page.작성.클라이언트요청Id;
        var idleHtml = await RenderComposerAsync(auth, page.작성);
        Assert.DoesNotContain("type=\"password\"", idleHtml);
        Assert.Contains("로그인하고 주문 계속", idleHtml);
        Assert.DoesNotContain("주문자 로그인 상태를 확인하고 있습니다.", idleHtml);

        var pending = auth.복원Async();
        Assert.True(auth.처리중);
        var pendingHtml = await RenderComposerAsync(auth, page.작성);
        Assert.Contains("주문자 로그인 상태를 확인하고 있습니다.", pendingHtml);
        Assert.DoesNotContain("type=\"password\"", pendingHtml);

        completion.SetResult(new 주문자앱인증결과(주문자앱세션상태.익명));
        Assert.True(await pending);
        var completedHtml = await RenderComposerAsync(auth, page.작성);
        Assert.DoesNotContain("type=\"password\"", completedHtml);
        Assert.Contains("로그인하고 주문 계속", completedHtml);
        Assert.DoesNotContain("주문자 로그인 상태를 확인하고 있습니다.", completedHtml);
        AssertDraft(page.작성, requestId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 최종401뒤_재로그인하고_작성내용과동일요청Id로재시도한다(bool structuredApiError)
    {
        var services = new FoodServices();
        Exception authenticationFailure = structuredApiError
            ? new SsalddelApiException("synthetic authentication failure", 401, "음식 주문 등록", "{}", null)
            : HttpFailure(HttpStatusCode.Unauthorized);
        services.Write = (_, _) => services.Requests.Count == 1
            ? Task.FromException<음식주문응답>(authenticationFailure)
            : Task.FromResult(new 음식주문응답 { 주문번호 = "FOOD-RECOVERY-1" });
        var auth = new AuthenticationService();
        var page = await CreatePageAsync(services, auth);
        var requestId = page.작성.클라이언트요청Id;

        Assert.False(await page.주문등록Async());

        Assert.True(page.작성.재로그인필요);
        Assert.True(page.인증화면표시);
        Assert.False(page.인증.로그인됨);
        Assert.Equal(1, auth.LogoutCalls);
        AssertDraft(page.작성, requestId);
        Assert.False(await page.주문등록Async());
        Assert.Single(services.Requests);

        Assert.True(await page.로그인Async("orderer", "test-password"));
        Assert.False(page.작성.재로그인필요);
        Assert.False(page.인증화면표시);
        AssertDraft(page.작성, requestId);
        Assert.True(await page.주문등록Async());

        Assert.Equal("FOOD-RECOVERY-1", page.작성.등록응답?.주문번호);
        Assert.Equal(2, services.Requests.Count);
        Assert.All(services.Requests, request =>
        {
            Assert.Equal(requestId, request.클라이언트요청Id);
            Assert.Equal(101, request.음식점Id);
            Assert.Equal(string.Empty, request.주문자UserId);
            Assert.Equal("수령인", request.수령인정보.수령인명);
            Assert.Equal("상세 주소", request.수령인정보.상세주소);
            Assert.Equal("문 앞", request.수령인정보.요청사항);
            Assert.Equal(2, Assert.Single(request.상품목록).수량);
        });
    }

    [Fact]
    public async Task 최종401뒤_저장소정리가실패해도_재로그인입력과제출차단을유지한다()
    {
        var services = new FoodServices
        {
            Write = (_, _) => Task.FromException<음식주문응답>(HttpFailure(HttpStatusCode.Unauthorized))
        };
        var auth = new AuthenticationService { LogoutFailure = new IOException("storage unavailable") };
        var page = await CreatePageAsync(services, auth);
        var requestId = page.작성.클라이언트요청Id;

        Assert.False(await page.주문등록Async());

        Assert.True(page.작성.재로그인필요);
        Assert.False(page.작성.제출가능);
        Assert.True(page.인증.오류발생);
        AssertDraft(page.작성, requestId);
        Assert.False(await page.주문등록Async());
        Assert.Single(services.Requests);

        Assert.True(await page.로그인Async("orderer", "test-password"));
        Assert.False(page.작성.재로그인필요);
        Assert.True(page.작성.제출가능);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task 최종401이아닌실패는_세션과작성내용을보존한다(HttpStatusCode statusCode)
    {
        var services = new FoodServices
        {
            Write = (_, _) => Task.FromException<음식주문응답>(HttpFailure(statusCode))
        };
        var auth = new AuthenticationService();
        var page = await CreatePageAsync(services, auth);
        var requestId = page.작성.클라이언트요청Id;

        Assert.False(await page.주문등록Async());

        Assert.True(page.인증.로그인됨);
        Assert.False(page.작성.재로그인필요);
        Assert.Equal(0, auth.LogoutCalls);
        Assert.Equal((int)statusCode, page.작성.오류?.Http상태코드);
        AssertDraft(page.작성, requestId);
    }

    [Fact]
    public async Task 화면이탈은_전송토큰을취소하고_취소무시한늦은성공과이동을차단한다()
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices { Write = (_, _) => completion.Task };
        var page = await CreatePageAsync(services, new AuthenticationService());
        var workspace = new TestWorkspace(page);
        var submitted = new List<string>();
#pragma warning disable BL0005 // 렌더 없는 이벤트/Dispose 회귀를 위해 이 매개변수만 직접 설정합니다.
        workspace.OrderSubmitted = EventCallback.Factory.Create<string>(new object(), submitted.Add);
#pragma warning restore BL0005
        var pending = workspace.SubmitAsync();
        Assert.Single(services.Requests);

        workspace.Dispose();

        Assert.True(services.LastWriteToken.IsCancellationRequested);
        completion.SetResult(new 음식주문응답 { 주문번호 = "FOOD-LATE" });
        await pending;
        Assert.Null(page.작성.등록응답);
        Assert.True(page.작성.취소됨);
        Assert.Empty(submitted);
        await workspace.SubmitAsync();
        Assert.Single(services.Requests);
    }

    [Fact]
    public async Task 화면이탈뒤_늦은401은_세션을종료하지않는다()
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices { Write = (_, _) => completion.Task };
        var auth = new AuthenticationService();
        var page = await CreatePageAsync(services, auth);
        var workspace = new TestWorkspace(page);
        var pending = workspace.SubmitAsync();

        workspace.Dispose();
        completion.SetException(HttpFailure(HttpStatusCode.Unauthorized));
        await pending;

        Assert.True(services.LastWriteToken.IsCancellationRequested);
        Assert.Equal(0, auth.LogoutCalls);
        Assert.True(page.인증.로그인됨);
        Assert.False(page.작성.재로그인필요);
    }

    [Fact]
    public async Task 음식점선택변경은_이전등록을취소하고_늦은성공을새작성에반영하지않는다()
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices { Write = (_, _) => completion.Task };
        var page = await CreatePageAsync(services, new AuthenticationService());
        var previousId = page.작성.클라이언트요청Id;
        var pending = page.주문등록Async();

        page.작성.음식점설정(CreateRestaurant(202));

        Assert.True(services.LastWriteToken.IsCancellationRequested);
        Assert.NotEqual(previousId, page.작성.클라이언트요청Id);
        completion.SetResult(new 음식주문응답 { 주문번호 = "FOOD-OTHER-RESTAURANT" });
        Assert.False(await pending);
        Assert.Null(page.작성.등록응답);
        Assert.Empty(page.작성.선택항목목록);
        Assert.Equal("수령인", page.작성.수령인명);
    }

    [Theory]
    [InlineData("menu", 0)]
    [InlineData("menu", 401)]
    [InlineData("menu", 503)]
    [InlineData("address", 0)]
    [InlineData("address", 401)]
    [InlineData("address", 503)]
    public async Task 전송중입력변경은_늦은성공과오류를버리고_새요청으로만계속한다(string changedInput, int statusCode)
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices();
        services.Write = (_, _) => services.Requests.Count == 1
            ? completion.Task
            : Task.FromResult(new 음식주문응답 { 주문번호 = "FOOD-CHANGED" });
        var auth = new AuthenticationService();
        var page = await CreatePageAsync(services, auth);
        var previousId = page.작성.클라이언트요청Id;
        var states = new List<Api작업상태>();
        page.작성.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(음식주문작성ViewModel.상태))
            {
                states.Add(page.작성.상태);
            }
        };
        var submitted = new List<string>();
        using var workspace = new TestWorkspace(page);
#pragma warning disable BL0005 // 실제 Workspace 콜백에 연결하여 늦은 응답의 이동을 확인합니다.
        workspace.OrderSubmitted = EventCallback.Factory.Create<string>(new object(), submitted.Add);
#pragma warning restore BL0005
        var pending = workspace.SubmitAsync();
        var previousToken = services.LastWriteToken;
        Assert.Equal(2, Assert.Single(services.Requests).상품목록.Single().수량);

        if (changedInput == "menu")
        {
            page.작성.메뉴수량변경(1001, 1);
        }
        else
        {
            // Disabled 렌더 뒤 이미 대기열에 있던 입력이 VM까지 도착하는 경계를 호출합니다.
            page.작성.주소 = "변경된 합성 주소";
        }

        var changedId = page.작성.클라이언트요청Id;
        Assert.NotEqual(previousId, changedId);
        Assert.True(previousToken.IsCancellationRequested);
        Assert.Null(page.작성.등록응답);
        Assert.False(page.작성.제출가능);
        await workspace.SubmitAsync();
        Assert.Single(services.Requests);
        Assert.Empty(submitted);

        if (statusCode == 0)
        {
            completion.SetResult(new 음식주문응답 { 주문번호 = "FOOD-STALE-INPUT" });
        }
        else
        {
            completion.SetException(HttpFailure((HttpStatusCode)statusCode));
        }

        await pending;
        Assert.Null(page.작성.등록응답);
        Assert.Null(page.작성.오류);
        Assert.Null(page.작성.오류메시지);
        Assert.Null(page.작성.성공메시지);
        Assert.Equal(Api작업상태.대기, page.작성.상태);
        Assert.DoesNotContain(Api작업상태.성공, states);
        Assert.DoesNotContain(Api작업상태.실패, states);
        Assert.True(page.작성.제출가능);
        Assert.Empty(submitted);
        Assert.Equal(0, auth.LogoutCalls);
        Assert.True(page.인증.로그인됨);
        Assert.False(page.작성.재로그인필요);
        Assert.Equal(changedId, page.작성.클라이언트요청Id);
        Assert.Equal(changedInput == "menu" ? 3 : 2, page.작성.메뉴수량(1001));
        Assert.Equal(changedInput == "address" ? "변경된 합성 주소" : "합성 배달 주소", page.작성.주소);

        await workspace.SubmitAsync();
        Assert.Equal("FOOD-CHANGED", Assert.Single(submitted));
        Assert.Equal(2, services.Requests.Count);
        Assert.Equal(previousId, services.Requests[0].클라이언트요청Id);
        Assert.Equal("합성 배달 주소", services.Requests[0].수령인정보.주소);
        Assert.Equal(changedId, services.Requests[1].클라이언트요청Id);
        Assert.Equal(page.작성.메뉴수량(1001), Assert.Single(services.Requests[1].상품목록).수량);
        Assert.Equal(page.작성.주소, services.Requests[1].수령인정보.주소);
        Assert.Equal("현장결제", services.Requests[1].결제수단);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 동일수령정보와수량상한의변화없는조작은_진행중요청을유지한다(bool atQuantityLimit)
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices { Write = (_, _) => completion.Task };
        var page = await CreatePageAsync(services, new AuthenticationService());
        if (atQuantityLimit)
        {
            page.작성.메뉴수량변경(1001, 98);
        }

        var requestId = page.작성.클라이언트요청Id;
        var pending = page.주문등록Async();
        page.작성.주소 = page.작성.주소;
        page.작성.수령인명 = page.작성.수령인명;
        page.작성.주문자본인수령여부 = page.작성.주문자본인수령여부;
        page.작성.메뉴수량변경(1001, atQuantityLimit ? 1 : 0);

        Assert.Equal(requestId, page.작성.클라이언트요청Id);
        Assert.False(services.LastWriteToken.IsCancellationRequested);
        Assert.Equal(atQuantityLimit ? 100 : 2, page.작성.메뉴수량(1001));
        completion.SetResult(new 음식주문응답 { 주문번호 = "FOOD-NOOP" });
        Assert.True(await pending);
        Assert.Equal("FOOD-NOOP", page.작성.등록응답?.주문번호);
        Assert.Equal(Api작업상태.성공, page.작성.상태);
        Assert.Equal(requestId, Assert.Single(services.Requests).클라이언트요청Id);
    }

    [Fact]
    public async Task 이전세션의늦은401은_새로그인세션을종료하지않는다()
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices { Write = (_, _) => completion.Task };
        var auth = new AuthenticationService();
        var page = await CreatePageAsync(services, auth);
        var session = page.인증.세션;
        var pending = page.주문등록Async();

        Assert.True(await page.로그인Async("orderer", "test-password"));
        // 같은 계정의 record 값이 같으면 ObservableProperty는 기존 참조를 그대로 유지합니다.
        Assert.Same(session, page.인증.세션);
        Assert.True(services.LastWriteToken.IsCancellationRequested);
        completion.SetException(HttpFailure(HttpStatusCode.Unauthorized));

        Assert.False(await pending);
        Assert.Equal(0, auth.LogoutCalls);
        Assert.True(page.인증.로그인됨);
        Assert.False(page.작성.재로그인필요);
    }

    [Fact]
    public async Task 새로그인시_이전등록을취소하고_늦은성공응답을작성에남기지않는다()
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices { Write = (_, _) => completion.Task };
        var page = await CreatePageAsync(services, new AuthenticationService());
        var requestId = page.작성.클라이언트요청Id;
        var pending = page.주문등록Async();

        Assert.True(await page.로그인Async("orderer", "test-password"));

        Assert.True(services.LastWriteToken.IsCancellationRequested);
        completion.SetResult(new 음식주문응답 { 주문번호 = "FOOD-PREVIOUS-SESSION" });
        Assert.False(await pending);
        Assert.Null(page.작성.등록응답);
        Assert.True(page.인증.로그인됨);
        AssertDraft(page.작성, requestId);
    }

    [Fact]
    public async Task 로그인처리중인대기제출은_이전인증으로등록하지않는다()
    {
        var completion = new TaskCompletionSource<주문자앱인증결과>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices();
        var auth = new AuthenticationService { PendingLogin = completion.Task };
        var page = await CreatePageAsync(services, auth);
        var requestId = page.작성.클라이언트요청Id;
        var pendingLogin = page.로그인Async("orderer", "test-password");

        Assert.True(page.인증.처리중);
        Assert.False(await page.주문등록Async());
        Assert.Empty(services.Requests);

        completion.SetResult(new 주문자앱인증결과(new 주문자앱세션상태(true, "orderer-1", "주문자")));
        Assert.True(await pendingLogin);
        AssertDraft(page.작성, requestId);
        Assert.True(await page.주문등록Async());
        Assert.Single(services.Requests);
    }

    [Fact]
    public async Task 현재화면의정상등록은_동일서버주문번호로한번이동한다()
    {
        var services = new FoodServices();
        var page = await CreatePageAsync(services, new AuthenticationService());
        var submitted = new List<string>();
        using var workspace = new TestWorkspace(page);
#pragma warning disable BL0005 // 렌더 없는 성공 콜백 회귀를 위해 이 매개변수만 직접 설정합니다.
        workspace.OrderSubmitted = EventCallback.Factory.Create<string>(new object(), submitted.Add);
#pragma warning restore BL0005

        await workspace.SubmitAsync();

        Assert.Equal("FOOD-TEST-1", Assert.Single(submitted));
        Assert.Single(services.Requests);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task 비로그인또는기능비활성은_등록Api를호출하지않는다(bool enabled, bool signedIn)
    {
        var services = new FoodServices { Enabled = enabled };
        var auth = new AuthenticationService { SignedIn = signedIn };
        var page = await CreatePageAsync(services, auth);

        Assert.False(await page.주문등록Async());
        Assert.Empty(services.Requests);
    }

    [Fact]
    public async Task 공개탐색은_자격증명없이업무만보이고_명시로그인과취소는초안을보존한다()
    {
        var services = new FoodServices();
        var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = false });
        var requestId = page.작성.클라이언트요청Id;
        var modes = new List<bool>();

        await WithRenderedWorkspaceAsync(page, async html =>
        {
            AssertBusinessOnly(html(), signedIn: false);
            Assert.True(page.인증화면진입());
            AssertAuthenticationOnly(html());
            AssertDraft(page.작성, requestId);
            Assert.True(page.탐색화면복귀());
            AssertBusinessOnly(html(), signedIn: false);
            AssertDraft(page.작성, requestId);
            Assert.False(await page.주문등록Async());
            Assert.Empty(services.Requests);
        }, modes);

        Assert.Equal(new[] { true, false }, modes);
    }

    [Fact]
    public async Task 인증성공은_원래주문서로복귀하지만_자동제출하지않는다()
    {
        var services = new FoodServices();
        var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = false });
        var requestId = page.작성.클라이언트요청Id;

        await WithRenderedWorkspaceAsync(page, async html =>
        {
            Assert.True(page.인증화면진입());
            AssertAuthenticationOnly(html());
            Assert.True(await page.로그인Async("orderer", "test-password"));
            AssertBusinessOnly(html(), signedIn: true);
            AssertDraft(page.작성, requestId);
            Assert.Empty(services.Requests);
            Assert.True(await page.주문등록Async());
            Assert.Equal(requestId, Assert.Single(services.Requests).클라이언트요청Id);
        });
    }

    [Fact]
    public async Task 로그인실패는_인증만보이고_취소후공개탐색과초안으로복귀한다()
    {
        var services = new FoodServices();
        var page = await CreatePageAsync(services, new AuthenticationService
        {
            SignedIn = false,
            LoginFailure = new IOException("합성 인증 서버 오류")
        });
        var requestId = page.작성.클라이언트요청Id;

        await WithRenderedWorkspaceAsync(page, async html =>
        {
            Assert.True(page.인증화면진입());
            Assert.False(await page.로그인Async("orderer", "test-password"));
            AssertAuthenticationOnly(html());
            Assert.Contains("합성 인증 서버 오류", html());
            Assert.True(page.탐색화면복귀());
            AssertBusinessOnly(html(), signedIn: false);
            AssertDraft(page.작성, requestId);
            Assert.Empty(services.Requests);
        });
    }

    [Fact]
    public async Task 최종401은_업무입력을제거하고_재로그인전용화면에서동일초안으로복귀한다()
    {
        var services = new FoodServices
        {
            Write = (_, _) => Task.FromException<음식주문응답>(HttpFailure(HttpStatusCode.Unauthorized))
        };
        var page = await CreatePageAsync(services, new AuthenticationService());
        var requestId = page.작성.클라이언트요청Id;

        await WithRenderedWorkspaceAsync(page, async html =>
        {
            AssertBusinessOnly(html(), signedIn: true);
            Assert.False(await page.주문등록Async());
            AssertAuthenticationOnly(html());
            Assert.Contains("로그인 정보를 다시 확인해 주세요", html());
            AssertDraft(page.작성, requestId);
            Assert.True(await page.로그인Async("orderer", "test-password"));
            AssertBusinessOnly(html(), signedIn: true);
            AssertDraft(page.작성, requestId);
            Assert.Single(services.Requests);
        });
    }

    [Fact]
    public async Task 로그인진행중은_복귀와주문제출을막고_인증영역만유지한다()
    {
        var completion = new TaskCompletionSource<주문자앱인증결과>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices();
        var page = await CreatePageAsync(services, new AuthenticationService
        {
            SignedIn = false,
            PendingLogin = completion.Task
        });
        var requestId = page.작성.클라이언트요청Id;

        await WithRenderedWorkspaceAsync(page, async html =>
        {
            Assert.True(page.인증화면진입());
            var pending = page.로그인Async("orderer", "test-password");
            Assert.True(page.인증.처리중);
            Assert.False(page.탐색화면복귀());
            Assert.False(await page.주문등록Async());
            AssertAuthenticationOnly(html());
            var backButton = Regex.Matches(html(), "<button\\b[^>]*>[\\s\\S]*?</button>")
                .Cast<Match>()
                .Single(match => match.Value.Contains("음식점과 메뉴로 돌아가기", StringComparison.Ordinal));
            Assert.Contains("disabled", backButton.Value);
            completion.SetResult(new 주문자앱인증결과(new 주문자앱세션상태(true, "orderer-1", "주문자")));
            Assert.True(await pending);
            AssertBusinessOnly(html(), signedIn: true);
            AssertDraft(page.작성, requestId);
            Assert.Empty(services.Requests);
        });
    }

    [Fact]
    public async Task 인증화면에서의이탈뒤_늦은로그인은_화면복귀나자동제출을만들지않는다()
    {
        var completion = new TaskCompletionSource<주문자앱인증결과>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices();
        var page = await CreatePageAsync(services, new AuthenticationService
        {
            SignedIn = false,
            PendingLogin = completion.Task
        });
        var requestId = page.작성.클라이언트요청Id;
        Assert.True(page.인증화면진입());
        var pending = page.로그인Async("orderer", "test-password");
        page.Dispose();
        completion.SetResult(new 주문자앱인증결과(new 주문자앱세션상태(true, "orderer-1", "주문자")));

        Assert.False(await pending);
        Assert.True(page.인증화면표시);
        Assert.False(page.탐색화면복귀());
        Assert.False(page.인증화면진입());
        AssertDraft(page.작성, requestId);
        Assert.Empty(services.Requests);
    }

    [Fact]
    public async Task 주문전송중에는_독립인증업무로진입하지않는다()
    {
        var completion = new TaskCompletionSource<음식주문응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new FoodServices { Write = (_, _) => completion.Task };
        var page = await CreatePageAsync(services, new AuthenticationService());
        var pending = page.주문등록Async();

        Assert.False(page.인증화면진입());
        Assert.False(page.인증화면표시);
        completion.SetResult(new 음식주문응답 { 주문번호 = "FOOD-ACTIVE" });
        Assert.True(await pending);
        Assert.Single(services.Requests);
    }

    [Fact]
    public async Task 인증화면에진입한뒤_이전업무화면의대기콜백은_음식점과초안을바꾸지않는다()
    {
        var services = new FoodServices();
        var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = false });
        await page.상세.조회Async(101);
        var requestId = page.작성.클라이언트요청Id;
        using var workspace = new TestWorkspace(page);
        Assert.True(page.인증화면진입());

        await workspace.ClearAsync();
        await workspace.SelectAsync(202);
        await workspace.SubmitAsync();

        Assert.Equal(101L, page.상세.요청RestaurantId);
        AssertDraft(page.작성, requestId);
        Assert.Empty(services.Requests);
    }

    private static void AssertBusinessOnly(string html, bool signedIn)
    {
        Assert.Contains("조회 기준 직접 선택", html);
        Assert.Contains("공개 메뉴 1개", html);
        Assert.Contains("선택 메뉴와 수령 정보 확인", html);
        Assert.DoesNotContain("type=\"password\"", html);
        Assert.DoesNotContain("아이디 또는 이메일", html);
        if (signedIn)
        {
            Assert.Contains("수령인 이름", html);
            Assert.Contains("주문 제출", html);
        }
        else
        {
            Assert.Contains("로그인하고 주문 계속", html);
            Assert.DoesNotContain("수령인 이름", html);
        }
    }

    private static void AssertAuthenticationOnly(string html)
    {
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("아이디 또는 이메일", html);
        Assert.Contains("음식점과 메뉴로 돌아가기", html);
        Assert.DoesNotContain("조회 기준 직접 선택", html);
        Assert.DoesNotContain("PUBLIC RESTAURANT DIRECTORY", html);
        Assert.DoesNotContain("EXACT RESTAURANT", html);
        Assert.DoesNotContain("공개 메뉴 1개", html);
        Assert.DoesNotContain("선택 메뉴와 수령 정보 확인", html);
        Assert.DoesNotContain("수령인 이름", html);
        Assert.DoesNotContain("배달 주소", html);
    }

    private static async Task WithRenderedWorkspaceAsync(
        음식점탐색PageViewModel page,
        Func<Func<string>, Task> verify,
        List<bool>? modes = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJsRuntime>();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddSingleton(page);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            // 실제 Orderer Routes와 같은 provider 형제를 유지합니다. 공개 탐색의 MudSelect가 소비합니다.
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var rendered = await renderer.RenderComponentAsync<OrdererRestaurantWorkspace>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(OrdererRestaurantWorkspace.RestaurantId)] = 101L,
                    [nameof(OrdererRestaurantWorkspace.AuthenticationModeChanged)] = EventCallback.Factory.Create<bool>(
                        new object(), mode => modes?.Add(mode))
                }));
            Assert.False(page.기준.오류발생, page.기준.오류메시지);
            Assert.False(page.상세.오류발생, page.상세.오류메시지);
            await verify(() => WebUtility.HtmlDecode(rendered.ToHtmlString()));
        });
    }

    private static async Task<음식점탐색PageViewModel> CreatePageAsync(FoodServices services, AuthenticationService auth)
    {
        var writer = new 음식주문작성ViewModel(services);
        writer.음식점설정(CreateRestaurant());
        writer.메뉴수량변경(1001, 2);
        writer.수령인명 = "수령인";
        writer.연락처 = "010-0000-0000";
        writer.주소 = "합성 배달 주소";
        writer.상세주소 = "상세 주소";
        writer.요청사항 = "문 앞";
        var page = new 음식점탐색PageViewModel(
            new 음식배달페이지접근ViewModel(services),
            new 주문자앱인증ViewModel(auth),
            new 음식점탐색기준ViewModel(services, services),
            new 음식점공개목록ViewModel(services),
            new 음식점공개상세ViewModel(services),
            writer);
        await page.접근.확인Async();
        await page.인증.복원Async();
        return page;
    }

    private static void AssertDraft(음식주문작성ViewModel writer, Guid requestId)
    {
        Assert.Equal(requestId, writer.클라이언트요청Id);
        Assert.Equal(2, writer.메뉴수량(1001));
        Assert.Equal(9_000m, writer.주문금액);
        Assert.Equal("수령인", writer.수령인명);
        Assert.Equal("010-0000-0000", writer.연락처);
        Assert.Equal("합성 배달 주소", writer.주소);
        Assert.Equal("상세 주소", writer.상세주소);
        Assert.Equal("문 앞", writer.요청사항);
    }

    private static 음식점공개상세응답 CreateRestaurant(long id = 101)
        => new()
        {
            음식점 = new 음식점공개요약응답
            {
                Id = id, 상호명 = "합성 음식점", 주문가능여부 = true, 최소주문금액 = 8_000
            },
            메뉴목록 = [new 음식점메뉴공개응답 { Id = 1001, 메뉴명 = "합성 메뉴", 판매가 = 4_500 }]
        };

    private static HttpRequestException HttpFailure(HttpStatusCode code)
        => new("synthetic HTTP failure", null, code);

    // 실제 Razor와 Mud component를 정적으로 렌더합니다. JS/내비게이션만 기술 경계로 대체합니다.
    private static async Task<string> RenderComposerAsync(주문자앱인증ViewModel auth, 음식주문작성ViewModel writer)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJsRuntime>();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<OrdererFoodOrderComposer>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(OrdererFoodOrderComposer.Authentication)] = auth,
                    [nameof(OrdererFoodOrderComposer.Writer)] = writer
                }));
            return WebUtility.HtmlDecode(rendered.ToHtmlString());
        });
    }

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/food/restaurants");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    // 렌더링 대체 없이 실제 Workspace의 이벤트 처리/Dispose 경로만 호출합니다.
    private sealed class TestWorkspace : OrdererRestaurantWorkspace
    {
        public TestWorkspace(음식점탐색PageViewModel page) => Services = new PageServiceProvider(page);

        public Task SubmitAsync()
            => (Task)typeof(OrdererRestaurantWorkspace)
                .GetMethod("SubmitOrderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null)!;

        public Task ClearAsync()
            => (Task)typeof(OrdererRestaurantWorkspace)
                .GetMethod("ClearSelectionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null)!;

        public Task SelectAsync(long restaurantId)
            => (Task)typeof(OrdererRestaurantWorkspace)
                .GetMethod("SelectRestaurantAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, [restaurantId, true])!;
    }

    private sealed class PageServiceProvider(음식점탐색PageViewModel page) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(음식점탐색PageViewModel) ? page : null;
    }

    private sealed class AuthenticationService : I주문자앱인증Service
    {
        public bool SignedIn { get; init; } = true;
        public Exception? LogoutFailure { get; init; }
        public string? RestoreError { get; init; }
        public Exception? RestoreFailure { get; init; }
        public Task<주문자앱인증결과>? PendingRestore { get; init; }
        public Task<주문자앱인증결과>? PendingLogin { get; init; }
        public Exception? LoginFailure { get; init; }
        public int LogoutCalls { get; private set; }

        public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
            => RestoreFailure is not null
                ? Task.FromException<주문자앱인증결과>(RestoreFailure)
                : PendingRestore ?? Task.FromResult(new 주문자앱인증결과(
                    new 주문자앱세션상태(SignedIn, "orderer-1", "주문자"), RestoreError));

        public Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
            => LoginFailure is not null
                ? Task.FromException<주문자앱인증결과>(LoginFailure)
                : PendingLogin ?? Task.FromResult(new 주문자앱인증결과(new 주문자앱세션상태(true, "orderer-1", "주문자")));

        public Task 로그아웃Async(CancellationToken cancellationToken = default)
        {
            LogoutCalls++;
            return LogoutFailure is null ? Task.CompletedTask : Task.FromException(LogoutFailure);
        }
    }

    private sealed class FoodServices : I음식배달페이지접근Service, I음식점탐색정책읽기Service,
        I음식점공개읽기Service, I주문자음식주문쓰기Service
    {
        public bool Enabled { get; init; } = true;
        public List<음식주문등록요청> Requests { get; } = [];
        public CancellationToken LastWriteToken { get; private set; }
        public Func<음식주문등록요청, CancellationToken, Task<음식주문응답>> Write { get; set; }
            = (_, _) => Task.FromResult(new 음식주문응답 { 주문번호 = "FOOD-TEST-1" });

        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default)
            => Task.FromResult(Enabled);

        public Task<RestaurantSearchPolicyDto> 조회Async(CancellationToken cancellationToken = default)
            => Task.FromResult(new RestaurantSearchPolicyDto());

        public Task<IReadOnlyList<음식점탐색권역응답>> 권역목록Async(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<음식점탐색권역응답>>([]);

        public Task<음식점공개목록응답> 목록Async(음식점공개목록조회요청 request, CancellationToken cancellationToken = default)
            => Task.FromResult(new 음식점공개목록응답());

        public Task<음식점공개상세응답?> 상세Async(long restaurantId, CancellationToken cancellationToken = default)
            => Task.FromResult<음식점공개상세응답?>(CreateRestaurant(restaurantId));

        public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            LastWriteToken = cancellationToken;
            return Write(request, cancellationToken);
        }
    }
}
