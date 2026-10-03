using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Community;
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
                    하차지 = CreatedPayload.하차!.주소.도로명주소, 픽업 = CreatedPayload.픽업, 하차 = CreatedPayload.하차
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
