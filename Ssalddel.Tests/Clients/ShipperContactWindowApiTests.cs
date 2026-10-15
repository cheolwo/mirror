using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Tests.UiCommon;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 모바일 adapter와 모의 HTTP 원장의 계약 시험. 실제 서버/DB/기사 UI 실행 증거는 아닙니다.</summary>
public sealed class ShipperContactWindowApiTests
{
    [Fact]
    public async Task 동의기록은_인증된공통API를호출하고_반환증적만생성요청에실어보낸다()
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new ConsentHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(client, auth, CreateAuthApi(client, auth), observer, realtime);
        var request = new 신청개인정보동의기록Request
        {
            증적Id = Guid.NewGuid(), 업무Code = 신청개인정보업무Codes.운송대행,
            출처Code = 신청개인정보출처Codes.커뮤니티지도, 동의문버전 = 신청개인정보동의정책.현재버전,
            수집이용동의 = true, 연령요건확인 = true
        };
        var recorded = await service.RecordPrivacyConsentAsync(request);
        Assert.Equal(request.증적Id, recorded?.증적Id);
        await service.AddRequestAsync(new()
        {
            의뢰Id = "client-bound-consent", 화물종류 = "검증 화물", 결제수단 = "카드",
            신청개인정보동의증적Id = recorded!.증적Id, 신청출처Code = recorded.출처Code
        });
        Assert.Equal(request.증적Id, handler.CreatedPayload?.신청개인정보동의증적Id);
        Assert.Equal(request.출처Code, handler.CreatedPayload?.신청출처Code);
        Assert.Equal(["/api/v1/common/application-privacy-consents", "/api/v1/shipper/requests"], handler.Paths);
    }

    [Theory]
    [InlineData("카드", 결제수단.카드)]
    [InlineData("가상계좌", 결제수단.가상계좌)]
    [InlineData("계좌이체", 결제수단.계좌이체)]
    public async Task 신규의뢰는_입력한화물과부가조건및명확한결제선택을전송한다(string payment, 결제수단 expected)
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new LedgerHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(client, auth, CreateAuthApi(client, auth), observer, realtime);
        var cargo = new CargoDTO
        {
            화물종류 = "냉장 상자", 설명 = "원래 취급 조건", 수량 = 5,
            중량Kg = 12.5m, 부피Cbm = .48m, 온도조건 = "냉장",
            길이Mm = 1000, 폭Mm = 800, 높이Mm = 600, 팔레트개수 = 2, 화물파손주의여부 = true
        };
        var source = new ShipperRequestItem
        {
            의뢰Id = "client-cargo-key", 화물종류 = cargo.화물종류, 화물원본 = cargo,
            화물적재형태 = "일반 화물(박스/팔레트)", 결제수단 = payment,
            서비스레벨 = "지정 시간", 요청사항 = "상차 전 연락", 대기료 = 0m,
            수작업비 = 1500m, 할증 = null, 알선소Id = "broker-reference", 절차메모 = "최초 절차",
            픽업지 = "상차지", 하차지 = "하차지"
        };
        var created = await service.AddRequestAsync(source);
        var payload = handler.CreatedPayload!;
        Assert.Equal(5, payload.화물.수량); Assert.Equal(12.5m, payload.화물.중량Kg);
        Assert.Equal(.48m, payload.화물.부피Cbm); Assert.Equal("냉장", payload.화물.온도조건);
        Assert.Equal("원래 취급 조건", payload.화물.설명);
        Assert.Equal(1000, payload.화물.길이Mm); Assert.Equal(800, payload.화물.폭Mm);
        Assert.Equal(600, payload.화물.높이Mm); Assert.Equal(2, payload.화물.팔레트개수);
        Assert.True(payload.화물.화물파손주의여부);
        Assert.Equal("지정 시간", payload.요금옵션?.서비스레벨);
        Assert.Equal("상차 전 연락\n적재 형태: 일반 화물(박스/팔레트)", payload.요금옵션?.요청사항);
        Assert.Equal(0m, payload.요금옵션?.대기료); Assert.Equal(1500m, payload.요금옵션?.수작업비);
        Assert.Null(payload.요금옵션?.할증);
        Assert.Equal("broker-reference", payload.요금옵션?.알선정책?.알선소Id);
        Assert.Equal("최초 절차", payload.요금옵션?.알선정책?.정책메모);
        Assert.Equal(payment, payload.결제수단); Assert.Equal(expected, payload.정산조건?.결제수단);
        Assert.Equal(정산시점.선결제, payload.정산조건?.정산시점);
        Assert.Equal(수납주체.플랫폼, payload.정산조건?.수납주체);
        var reloaded = await service.GetRequestAsync(created.의뢰Id);
        Assert.Equal(5, reloaded?.화물원본?.수량); Assert.Equal("냉장", reloaded?.화물원본?.온도조건);
        Assert.Equal(0m, reloaded?.대기료); Assert.Equal(1500m, reloaded?.수작업비); Assert.Null(reloaded?.할증);
        Assert.Equal(payload.요금옵션?.요청사항, reloaded?.요청사항);
        reloaded!.화물원본!.수량 = 99;
        Assert.Equal(5, cargo.수량); Assert.Equal("냉장", cargo.온도조건);
    }

    [Fact]
    public async Task 신규전송에서_미입력측정값과실제0을구분한다()
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new LedgerHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(client, auth, CreateAuthApi(client, auth), observer, realtime);
        await service.AddRequestAsync(new()
        {
            의뢰Id = "unknown-cargo-values", 화물종류 = "부피 측정 화물",
            화물원본 = new() { 화물종류 = "부피 측정 화물", 부피Cbm = .2m, 중량Kg = 0m },
            픽업지 = "상차지", 하차지 = "하차지"
        });
        var payload = handler.CreatedPayload!;
        Assert.Null(payload.화물.수량); Assert.Equal(0m, payload.화물.중량Kg);
        Assert.Equal(.2m, payload.화물.부피Cbm); Assert.Null(payload.화물.온도조건);
        Assert.Null(payload.화물.길이Mm); Assert.Null(payload.화물.팔레트개수);
        Assert.Null(payload.요금옵션?.대기료); Assert.Null(payload.요금옵션?.수작업비); Assert.Null(payload.요금옵션?.할증);
        await service.AddRequestAsync(new() { 의뢰Id = "no-cargo-snapshot", 화물종류 = "미측정 화물", 픽업지 = "상차지", 하차지 = "하차지" });
        Assert.Null(handler.CreatedPayload!.화물.수량);
        Assert.Null(handler.CreatedPayload.화물.중량Kg);
        Assert.Equal("미측정 화물", handler.CreatedPayload.화물.화물종류);
    }

    [Theory]
    [InlineData("인수증")]
    [InlineData("현금성 정산")]
    [InlineData("0")]
    [InlineData("알 수 없는 결제")]
    public async Task 모호한결제선택은_카드로바꾸거나서버접수하지않는다(string payment)
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new LedgerHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(client, auth, CreateAuthApi(client, auth), observer, realtime);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddRequestAsync(new() { 의뢰Id = "unconfirmed-payment", 결제수단 = payment }));
        Assert.Contains("정산 시점과 수납 주체", failure.Message);
        Assert.Empty(handler.Methods);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.OK)]
    public async Task 접수결과조회는_404만미발견으로구분하고_오류를빈결과로바꾸지않는다(HttpStatusCode status)
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new RecoveryHandler(status);
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(client, auth, CreateAuthApi(client, auth), observer, realtime);
        if (status == HttpStatusCode.ServiceUnavailable)
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetRequestByClientRequestIdAsync("shipper-app-key"));
        else
        {
            var result = await service.GetRequestByClientRequestIdAsync("shipper-app-key");
            if (status == HttpStatusCode.NotFound) Assert.Null(result);
            else Assert.Equal("server-ledger-id", result!.의뢰Id);
        }
        Assert.Equal("/api/v1/shipper/requests/by-client-request/shipper-app-key", handler.Path);
    }

    [Fact]
    public async Task 실제등록과수정DTO의인계정보는_같은의뢰재조회에서도보존된다()
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new LedgerHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(client, auth, CreateAuthApi(client, auth), observer, realtime);
        var state = ShipperRequestContactWindowTests.CompleteState();
        state.상차상세주소 = "상차장 3";
        state.상차연락처이름 = "상차 실담당";
        state.상차연락처전화번호 = "010-1111-2222";
        state.하차연락처이름 = "하차 실담당";
        state.하차연락처전화번호 = "010-3333-4444";
        state.하차시간창시작일시 = new(2030, 10, 3, 14, 0, 0);
        state.하차시간창종료일시 = new(2030, 10, 3, 16, 0, 0);
        var input = new ShipperRequestItem
        {
            의뢰Id = "client-key", 화물종류 = state.화물종류, 결제예정금액 = 50000,
            픽업지 = state.상차도로명주소, 하차지 = state.하차도로명주소,
            픽업정보 = ShipperRequestHandoffMapper.CreatePickup(state.ToDraft()),
            하차정보 = ShipperRequestHandoffMapper.CreateDropoff(state.ToDraft())
        };
        var created = await service.AddRequestAsync(input);
        Assert.Equal("server-ledger-id", created.의뢰Id);
        Assert.Equal("client-key", handler.CreatedPayload?.클라이언트요청Id);
        Assert.Equal("상차 실담당", handler.CreatedPayload?.픽업?.연락처.이름);
        Assert.Equal("010-1111-2222", handler.CreatedPayload?.픽업?.연락처.전화번호);
        Assert.Equal("상차장 3", handler.CreatedPayload?.픽업?.주소.상세주소);
        Assert.Equal(new DateTime(2030, 10, 3, 0, 0, 0, DateTimeKind.Utc), handler.CreatedPayload?.픽업?.시간창?.시작일시);
        Assert.Equal(new DateTime(2030, 10, 3, 5, 0, 0, DateTimeKind.Utc), handler.CreatedPayload?.하차?.시간창?.시작일시);

        var reloaded = await service.GetRequestAsync(created.의뢰Id);
        Assert.Equal(created.의뢰Id, reloaded?.의뢰Id);
        Assert.Equal("010-3333-4444", reloaded?.하차정보?.연락처.전화번호);
        Assert.Equal(created.픽업정보?.시간창?.종료일시, reloaded?.픽업정보?.시간창?.종료일시);
        reloaded!.픽업지 = "수정한 상차 주소";
        reloaded.하차정보!.연락처.전화번호 = "010-5555-6666";
        reloaded.하차정보.시간창!.종료일시 = reloaded.하차정보.시간창.종료일시.AddHours(1);
        await service.UpdateRequestAsync(reloaded);
        var updated = await service.GetRequestAsync(created.의뢰Id);
        Assert.Equal("수정한 상차 주소", handler.UpdatedPayload?.픽업?.주소.도로명주소);
        Assert.Equal("010-5555-6666", handler.UpdatedPayload?.하차?.연락처.전화번호);
        Assert.Equal("010-5555-6666", updated?.하차정보?.연락처.전화번호);
        Assert.Equal(reloaded.하차정보.시간창.종료일시, updated?.하차정보?.시간창?.종료일시);
        Assert.Equal(created.의뢰Id, observer.GetSnapshot(created.의뢰Id)?.RequestId);
        Assert.Equal(["POST", "GET", "PUT", "GET"], handler.Methods);
    }

    [Fact]
    public async Task 직접등록의미입력정보도_임의전화나시간으로채우지않고_서버거절을알린다()
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new LedgerHandler { RejectWrites = true };
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
        var service = new ServerBackedShipperOperationsService(client, auth, CreateAuthApi(client, auth), observer, realtime);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddRequestAsync(new()
        {
            의뢰Id = "missing-input", 픽업지 = "상차지", 하차지 = "하차지"
        }));
        Assert.Contains("HTTP 400", error.Message);
        Assert.Empty(handler.CreatedPayload!.픽업!.연락처.이름);
        Assert.Empty(handler.CreatedPayload.픽업.연락처.전화번호);
        Assert.Null(handler.CreatedPayload.픽업.시간창);
        Assert.Null(handler.CreatedPayload.하차?.시간창);
        Assert.Null(observer.GetSnapshot("missing-input"));
    }

    private static AuthApiService CreateAuthApi(HttpClient client, IAuthSession auth)
        => new(client, auth,
            new 꾸미기보유권동기화Service(client, auth, new UnusedDecorationStore(), new PlatformCommunityDecorationStateService()),
            new SsalddelMobilePushInstallationClient(client, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));

    private sealed class UnusedDecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecoveryHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public string? Path;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status) { Content = status == HttpStatusCode.OK
                ? JsonContent.Create(new 화주운송의뢰응답 { 의뢰Id = "server-ledger-id" }) : new StringContent("not available") });
        }
    }

    private sealed class ConsentHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public 화주운송의뢰생성요청? CreatedPayload { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-a", request.Headers.Authorization?.Parameter);
            Paths.Add(request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath == "/api/v1/common/application-privacy-consents")
            {
                var body = await request.Content!.ReadFromJsonAsync<신청개인정보동의기록Request>(cancellationToken);
                Assert.True(body!.수집이용동의); Assert.True(body.연령요건확인);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(ShipperConsentFixture.Response(body)) };
            }
            Assert.Equal("/api/v1/shipper/requests", request.RequestUri.AbsolutePath);
            CreatedPayload = await request.Content!.ReadFromJsonAsync<화주운송의뢰생성요청>(cancellationToken);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 화주운송의뢰응답 { 의뢰Id = "server-ledger-id" }) };
        }
    }

    private sealed class LedgerHandler : HttpMessageHandler
    {
        public 화주운송의뢰생성요청? CreatedPayload { get; private set; }
        public 화주운송의뢰수정요청? UpdatedPayload { get; private set; }
        public List<string> Methods { get; } = [];
        public bool RejectWrites { get; set; }
        private 화주운송의뢰응답? _ledger;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Methods.Add(request.Method.Method);
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/api/v1/shipper/requests", request.RequestUri?.AbsolutePath);
                CreatedPayload = await request.Content!.ReadFromJsonAsync<화주운송의뢰생성요청>(cancellationToken);
                if (RejectWrites) return new(HttpStatusCode.BadRequest) { Content = new StringContent("상차 시간창을 확인해 주세요.") };
                _ledger = new()
                {
                    의뢰Id = "server-ledger-id", 의뢰상태 = "접수", 결제상태 = "대기", 배차상태 = "매칭중",
                    생성일시 = DateTime.UtcNow, 픽업지 = CreatedPayload!.픽업!.주소.도로명주소,
                    하차지 = CreatedPayload.하차!.주소.도로명주소, 픽업 = CreatedPayload.픽업, 하차 = CreatedPayload.하차,
                    화물 = CreatedPayload.화물, 요금옵션 = CreatedPayload.요금옵션,
                    결제수단 = CreatedPayload.정산조건!.결제수단.ToString()
                };
            }
            else
            {
                Assert.Equal("/api/v1/shipper/requests/server-ledger-id", request.RequestUri?.AbsolutePath);
                if (request.Method == HttpMethod.Put)
                {
                    UpdatedPayload = await request.Content!.ReadFromJsonAsync<화주운송의뢰수정요청>(cancellationToken);
                    _ledger!.픽업 = UpdatedPayload!.픽업;
                    _ledger.하차 = UpdatedPayload.하차;
                    _ledger.픽업지 = UpdatedPayload.픽업!.주소.도로명주소;
                    _ledger.하차지 = UpdatedPayload.하차!.주소.도로명주소;
                }
                else Assert.Equal(HttpMethod.Get, request.Method);
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(_ledger) };
        }
    }
}
