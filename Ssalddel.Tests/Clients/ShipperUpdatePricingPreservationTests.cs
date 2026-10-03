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
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 앱 adapter·수정 VM과 모의 HTTP 간의 보존 회귀. 실제 서버/DB/UI 실행 증거는 아닙니다.</summary>
public sealed class ShipperUpdatePricingPreservationTests
{
    [Fact]
    public async Task 조회한의뢰의연락처만수정하면_같은ID의정산조건과견적을보존한다()
    {
        await using var fixture = await Fixture.CreateAsync(KnownResponse());
        var loaded = await fixture.Service.GetRequestAsync(Fixture.RequestId);
        Assert.NotNull(loaded);
        var state = new 화주운송의뢰상태ViewModel();
        state.목록적용([loaded]);
        var model = new 화주운송의뢰수정ViewModel(fixture.Service, state);
        Assert.True(model.선택항목적용());
        Assert.NotSame(loaded, model.초안);
        Assert.Equal("월말정산", model.초안.정산시점);
        Assert.Equal("세금계산서", model.초안.증빙방식);
        Assert.Equal("화주직접", model.초안.수납주체);
        Assert.True(model.초안.세금계산서필요);
        Assert.True(model.초안.현금영수증필요);
        Assert.Equal("기존 월말 정산 메모", model.초안.정산메모);
        model.초안.하차정보!.연락처.전화번호 = "010-5555-6666";

        Assert.True(await model.실행Async());

        var payload = fixture.Handler.UpdatedPayload!;
        Assert.Equal("010-5555-6666", payload.하차?.연락처.전화번호);
        Assert.Equal("010-3333-4444", loaded.하차정보?.연락처.전화번호);
        Assert.Equal("별도정산", payload.결제수단);
        Assert.Equal(97000, payload.결제예정금액);
        var settlement = Assert.IsType<화주운송정산조건DTO>(payload.정산조건);
        Assert.Equal(정산시점.월말정산, settlement.정산시점);
        Assert.Equal(결제수단.별도정산, settlement.결제수단);
        Assert.Equal(증빙방식.세금계산서, settlement.증빙방식);
        Assert.Equal(수납주체.화주직접, settlement.수납주체);
        Assert.True(settlement.세금계산서필요);
        Assert.True(settlement.현금영수증필요);
        Assert.Equal("기존 월말 정산 메모", settlement.정산메모);
        Assert.Equal(95000m, payload.요금옵션?.최종운임);
        Assert.Equal(81000m, payload.요금옵션?.기사지급예정운임);
        Assert.Equal(12.3m, payload.요금옵션?.예상거리Km);
        Assert.Null(payload.요금옵션?.서비스레벨);
        Assert.Null(payload.요금옵션?.요청사항);
        Assert.Null(payload.요금옵션?.알선정책);
        Assert.Equal("식품", payload.화물?.화물종류);
        Assert.Equal(Fixture.RequestId, state.선택된의뢰?.의뢰Id);
        Assert.Equal("월말정산", model.초안.정산시점);
        Assert.Equal(81000m, model.초안.기사지급예정운임);

        var reloaded = await fixture.Service.GetRequestAsync(Fixture.RequestId);
        Assert.Equal(Fixture.RequestId, reloaded?.의뢰Id);
        Assert.Equal("010-5555-6666", reloaded?.하차정보?.연락처.전화번호);
        Assert.Equal(12.3m, reloaded?.예상거리Km);
        Assert.Equal("기존 월말 정산 메모", reloaded?.정산메모);
        Assert.Equal(["GET", "PUT", "GET"], fixture.Handler.Methods);
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData(0d, 90000d, 0d, 0d)]
    [InlineData(93000d, 95000d, 81000d, 12.3d)]
    [InlineData(null, 95000d, null, 12.3d)]
    [InlineData(null, 95000d, null, null)]
    [InlineData(null, null, 0d, null)]
    public async Task 조회와수정은_미확인과실제0및화주기사운임을구분한다(
        double? canonicalFare, double? quoteFare, double? driverFare, double? distance)
    {
        var response = KnownResponse();
        response.결제예정금액 = null;
        response.최종운임 = ToDecimal(canonicalFare);
        response.요금옵션 = new()
        {
            최종운임 = ToDecimal(quoteFare),
            기사지급예정운임 = ToDecimal(driverFare),
            예상거리Km = ToDecimal(distance)
        };
        await using var fixture = await Fixture.CreateAsync(response);
        var item = (await fixture.Service.GetRequestAsync(Fixture.RequestId))!;
        var expectedFare = ToDecimal(canonicalFare);
        Assert.Equal(expectedFare, item.기준운임);
        Assert.Equal(ToDecimal(driverFare), item.기사지급예정운임);
        Assert.Equal(ToDecimal(distance), item.예상거리Km);

        await fixture.Service.UpdateRequestAsync(item);

        var payload = fixture.Handler.UpdatedPayload!;
        Assert.Null(payload.결제예정금액);
        if (expectedFare is null && driverFare is null && distance is null)
        {
            Assert.Null(payload.요금옵션);
        }
        else
        {
            Assert.NotNull(payload.요금옵션);
            Assert.Equal(expectedFare, payload.요금옵션.최종운임);
            Assert.Equal(ToDecimal(driverFare), payload.요금옵션.기사지급예정운임);
            Assert.Equal(ToDecimal(distance), payload.요금옵션.예상거리Km);
            Assert.Null(payload.요금옵션.서비스레벨);
            Assert.Null(payload.요금옵션.요청사항);
        }
    }

    [Fact]
    public async Task 배차된의뢰의연락처수정은_견적이있어도미확인최상위운임을만들어전송하지않는다()
    {
        var response = KnownResponse();
        response.최종운임 = null;
        response.의뢰상태 = "배차확정";
        response.배차상태 = "배차확정";
        await using var fixture = await Fixture.CreateAsync(response);
        var loaded = (await fixture.Service.GetRequestAsync(Fixture.RequestId))!;
        Assert.Null(loaded.기준운임);
        var state = new 화주운송의뢰상태ViewModel();
        state.목록적용([loaded]);
        var model = new 화주운송의뢰수정ViewModel(fixture.Service, state);
        Assert.True(model.선택항목적용());
        Assert.Null(model.초안.기준운임);
        model.초안.픽업정보!.연락처.전화번호 = "010-5555-6666";

        Assert.True(await model.실행Async());

        var payload = fixture.Handler.UpdatedPayload!;
        Assert.Equal("010-5555-6666", payload.픽업?.연락처.전화번호);
        Assert.Equal("배차확정", payload.배차상태);
        Assert.NotNull(payload.요금옵션);
        Assert.Null(payload.요금옵션.최종운임);
        Assert.Equal(12.3m, payload.요금옵션.예상거리Km);
        Assert.Equal(81000m, payload.요금옵션.기사지급예정운임);
        Assert.Equal(97000, payload.결제예정금액);
        Assert.Equal("기존 월말 정산 메모", payload.정산조건?.정산메모);
        Assert.Equal(정산시점.월말정산, payload.정산조건?.정산시점);
        Assert.Null(model.초안.기준운임);
        Assert.Equal(Fixture.RequestId, model.초안.의뢰Id);
        Assert.Null(response.최종운임);
        Assert.Equal(95000m, response.요금옵션?.최종운임);
    }

    [Theory]
    [InlineData("정산시점", "")]
    [InlineData("정산시점", "알 수 없는 정산")]
    [InlineData("정산시점", "0")]
    [InlineData("결제수단", "")]
    [InlineData("결제수단", "알 수 없는 결제")]
    [InlineData("결제수단", "0")]
    [InlineData("증빙방식", "")]
    [InlineData("수납주체", "")]
    public async Task 정산필수enum이누락되거나미확인이면_생성기본조건으로대체하지않는다(string field, string value)
    {
        await using var fixture = await Fixture.CreateAsync(KnownResponse());
        var item = (await fixture.Service.GetRequestAsync(Fixture.RequestId))!;
        switch (field)
        {
            case "정산시점": item.정산시점 = value; break;
            case "결제수단": item.결제수단 = value; break;
            case "증빙방식": item.증빙방식 = value; break;
            case "수납주체": item.수납주체 = value; break;
            default: throw new InvalidOperationException("시험 조건을 확인해 주세요.");
        }

        await fixture.Service.UpdateRequestAsync(item);

        Assert.Null(fixture.Handler.UpdatedPayload?.정산조건);
        if (field == "결제수단")
            Assert.Null(fixture.Handler.UpdatedPayload?.결제수단);
        Assert.Equal(12.3m, fixture.Handler.UpdatedPayload?.요금옵션?.예상거리Km);
    }

    [Fact]
    public async Task 확인된enum첫값과false세금조건도_실제수정값으로전송한다()
    {
        var response = KnownResponse();
        response.정산시점 = 정산시점.선결제;
        response.결제수단 = "카드";
        response.증빙방식 = 증빙방식.없음;
        response.수납주체 = 수납주체.플랫폼;
        response.세금계산서필요 = false;
        response.현금영수증필요 = false;
        response.정산메모 = null;
        await using var fixture = await Fixture.CreateAsync(response);
        var item = (await fixture.Service.GetRequestAsync(Fixture.RequestId))!;

        await fixture.Service.UpdateRequestAsync(item);

        var settlement = Assert.IsType<화주운송정산조건DTO>(fixture.Handler.UpdatedPayload?.정산조건);
        Assert.Equal(정산시점.선결제, settlement.정산시점);
        Assert.Equal(결제수단.카드, settlement.결제수단);
        Assert.Equal(증빙방식.없음, settlement.증빙방식);
        Assert.Equal(수납주체.플랫폼, settlement.수납주체);
        Assert.False(settlement.세금계산서필요);
        Assert.False(settlement.현금영수증필요);
        Assert.Null(settlement.정산메모);
    }

    [Fact]
    public async Task 입력없는수정은_운송결제금액상태를생성기본값으로채우지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(KnownResponse());

        await fixture.Service.UpdateRequestAsync(new() { 의뢰Id = Fixture.RequestId });

        var payload = fixture.Handler.UpdatedPayload!;
        Assert.Null(payload.운송방식);
        Assert.Null(payload.차량종류);
        Assert.Null(payload.결제수단);
        Assert.Null(payload.결제예정금액);
        Assert.Null(payload.정산조건);
        Assert.Null(payload.요금옵션);
        Assert.Null(payload.픽업);
        Assert.Null(payload.하차);
        Assert.Null(payload.결제상태);
        Assert.Null(payload.상태);
        Assert.Null(payload.배차상태);
    }

    [Fact]
    public async Task 최초등록의기존기본정산과서비스값은유지한다()
    {
        await using var fixture = await Fixture.CreateAsync(KnownResponse());

        await fixture.Service.AddRequestAsync(new()
        {
            의뢰Id = Fixture.RequestId,
            정산시점 = "월말정산", 증빙방식 = "세금계산서", 수납주체 = "화주직접",
            기준운임 = 95000m, 기사지급예정운임 = 81000m, 예상거리Km = 12.3m
        });

        var payload = fixture.Handler.CreatedPayload!;
        Assert.Equal("shipper-a", payload.화주Id);
        Assert.Equal("일반", payload.운송방식);
        Assert.Equal("1톤 카고", payload.차량종류);
        Assert.Equal("카드", payload.결제수단);
        Assert.Equal(95000, payload.결제예정금액);
        Assert.Equal(정산시점.선결제, payload.정산조건?.정산시점);
        Assert.Equal(결제수단.카드, payload.정산조건?.결제수단);
        Assert.Equal(증빙방식.인수증, payload.정산조건?.증빙방식);
        Assert.Equal(수납주체.플랫폼, payload.정산조건?.수납주체);
        Assert.Equal("SsalddelApp 서버 API 생성", payload.정산조건?.정산메모);
        Assert.Equal("standard", payload.요금옵션?.서비스레벨);
        Assert.Equal("SsalddelApp 서버 API 생성", payload.요금옵션?.요청사항);
        Assert.Equal(12.3m, payload.요금옵션?.예상거리Km);
        Assert.Equal(81000m, payload.요금옵션?.기사지급예정운임);
        Assert.NotNull(payload.요금옵션?.알선정책);
    }

    private static decimal? ToDecimal(double? value) => value.HasValue ? (decimal)value.Value : null;

    private static 화주운송의뢰응답 KnownResponse()
        => new()
        {
            의뢰Id = Fixture.RequestId,
            운송방식 = "일반", 차량종류 = "1톤 카고",
            의뢰상태 = "접수", 결제상태 = "대기", 배차상태 = "매칭중",
            결제수단 = "별도정산", 결제예정금액 = 97000, 최종운임 = 95000m,
            정산시점 = 정산시점.월말정산, 증빙방식 = 증빙방식.세금계산서, 수납주체 = 수납주체.화주직접,
            세금계산서필요 = true, 현금영수증필요 = true, 정산메모 = "기존 월말 정산 메모",
            요금옵션 = new()
            {
                예상거리Km = 12.3m, 최종운임 = 95000m, 기사지급예정운임 = 81000m,
                서비스레벨 = "기존 특약 서비스", 요청사항 = "기존 상차 요청", 알선정책 = new() { 알선단계 = 2 }
            },
            요약 = new() { 화물종류 = "식품" },
            픽업지 = "기존 상차 주소", 하차지 = "기존 하차 주소",
            픽업 = new() { 주소 = new() { 도로명주소 = "기존 상차 주소" }, 연락처 = new() { 이름 = "상차 담당", 전화번호 = "010-1111-2222" } },
            하차 = new() { 주소 = new() { 도로명주소 = "기존 하차 주소" }, 연락처 = new() { 이름 = "하차 담당", 전화번호 = "010-3333-4444" } }
        };

    private sealed class Fixture : IAsyncDisposable
    {
        public const string RequestId = "server-existing-request";
        public LedgerHandler Handler { get; }
        public ServerBackedShipperOperationsService Service { get; }
        private readonly HttpClient _client;
        private readonly TransportRequestLedgerRealtimeClient _realtime;

        private Fixture(화주운송의뢰응답 response, AuthSession auth)
        {
            Handler = new(response);
            _client = new(Handler) { BaseAddress = new("https://test.invalid/") };
            var observer = new TransportRequestLedgerObserver();
            _realtime = new(_client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
            var authApi = new AuthApiService(_client, auth,
                new 꾸미기보유권동기화Service(_client, auth, new UnusedDecorationStore(), new PlatformCommunityDecorationStateService()),
                new SsalddelMobilePushInstallationClient(_client, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));
            Service = new(_client, auth, authApi, observer, _realtime);
        }

        public static async Task<Fixture> CreateAsync(화주운송의뢰응답 response)
        {
            var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
            await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
            return new(response, auth);
        }

        public async ValueTask DisposeAsync()
        {
            await _realtime.DisposeAsync();
            _client.Dispose();
        }
    }

    private sealed class LedgerHandler(화주운송의뢰응답 ledger) : HttpMessageHandler
    {
        public 화주운송의뢰수정요청? UpdatedPayload { get; private set; }
        public 화주운송의뢰생성요청? CreatedPayload { get; private set; }
        public List<string> Methods { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-a", request.Headers.Authorization?.Parameter);
            Methods.Add(request.Method.Method);
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/api/v1/shipper/requests", request.RequestUri?.AbsolutePath);
                CreatedPayload = await request.Content!.ReadFromJsonAsync<화주운송의뢰생성요청>(cancellationToken);
            }
            else
            {
                Assert.Equal($"/api/v1/shipper/requests/{Fixture.RequestId}", request.RequestUri?.AbsolutePath);
                if (request.Method == HttpMethod.Put)
                {
                    UpdatedPayload = await request.Content!.ReadFromJsonAsync<화주운송의뢰수정요청>(cancellationToken);
                    if (UpdatedPayload!.픽업 is not null) ledger.픽업 = UpdatedPayload.픽업;
                    if (UpdatedPayload.하차 is not null) ledger.하차 = UpdatedPayload.하차;
                }
                else Assert.Equal(HttpMethod.Get, request.Method);
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(ledger) };
        }
    }

    private sealed class UnusedDecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
