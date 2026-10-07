using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services.Geo;
using Ssalddel.Client.Infrastructure;
using Ssalddel.Contracts.Driver.Reservation;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Settlement;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Driver.Work;
using Microsoft.Extensions.Options;
using Ssalddel.Client.Infrastructure.Transport;
using System.Net;

namespace DriverApp.Services.Samples;

public sealed class ServerBackedDriverSampleDataService : IDriverSampleDataService
{
    private readonly IAuthSession _authSession;
    private readonly IDriverFreightWorkspaceStore _freightWorkspace;
    private readonly IDriverRecommendationApiService _recommendationApi;
    private readonly IDriverSettlementApiService _settlementApi;
    private readonly IDriverReservationApiService _reservationApi;
    private readonly IDriverWorkApiService _workApi;
    private readonly IDriverTransportApiService _transportApi;
    private readonly 기사샘플데이터Service _sampleFallback;
    private readonly IOptions<ClientDataModeOptions> _dataModeOptions;
    private readonly TransportRequestLedgerRealtimeClient _realtimeClient;
    private readonly ITransportRequestLedgerObserver _ledgerObserver;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private bool _loaded;
    private string? _loadedAccessToken;

    private 기사근무샘플상태 _근무상태 = null!;
    private 기사현재위치샘플 _기사현재위치 = null!;
    private 기사정산샘플요약 _정산요약 = null!;
    private IReadOnlyList<DriverRequestItem> _추천의뢰목록 = [];
    private IReadOnlyList<기사예약샘플항목> _예약목록 = [];
    private IReadOnlyList<기사운송샘플항목> _운송목록 = [];
    private long? _currentTransportId;
    private IReadOnlyList<기사알림샘플항목> _알림목록 = [];

    public ServerBackedDriverSampleDataService(
        IAuthSession authSession,
        IDriverFreightWorkspaceStore freightWorkspace,
        IDriverRecommendationApiService recommendationApi,
        IDriverSettlementApiService settlementApi,
        IDriverReservationApiService reservationApi,
        IDriverWorkApiService workApi,
        IDriverTransportApiService transportApi,
        기사샘플데이터Service sampleFallback,
        IOptions<ClientDataModeOptions> dataModeOptions,
        TransportRequestLedgerRealtimeClient realtimeClient,
        ITransportRequestLedgerObserver ledgerObserver)
    {
        _authSession = authSession;
        _freightWorkspace = freightWorkspace;
        _recommendationApi = recommendationApi;
        _settlementApi = settlementApi;
        _reservationApi = reservationApi;
        _workApi = workApi;
        _transportApi = transportApi;
        _sampleFallback = sampleFallback;
        _dataModeOptions = dataModeOptions;
        _realtimeClient = realtimeClient;
        _ledgerObserver = ledgerObserver;
        _ledgerObserver.RefreshRequested += OnLedgerRefreshRequested;
        ApplyEmptyState();
    }

    public async Task RefreshAsync(
        CancellationToken cancellationToken = default,
        bool force = false)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var accessToken = _authSession.AccessToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                ApplyDisconnectedState();
                _loaded = false;
                _loadedAccessToken = null;
                return;
            }

            try
            {
                await _realtimeClient.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // 실시간 연결 실패 시 기존 30초 보완 조회와 수동 새로고침을 유지합니다.
            }

            if (!string.Equals(_loadedAccessToken, accessToken, StringComparison.Ordinal))
            {
                ApplyEmptyState();
                _loaded = false;
                _loadedAccessToken = null;
            }

            if (_loaded && !force)
            {
                return;
            }

            try
            {
                await LoadLiveServerDataAsync(cancellationToken);
                _loaded = true;
                _loadedAccessToken = accessToken;
            }
            catch (Exception exception) when (exception is not OperationCanceledException && CanUseSampleFallback())
            {
                ApplySampleFallback();
                _loaded = false;
                _loadedAccessToken = null;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void OnLedgerRefreshRequested(TransportRequestLedgerRefreshRequest request)
        => _ = RefreshAsync(force: true);

    public 기사근무샘플상태 근무상태 => _근무상태;

    public 기사현재위치샘플 기사현재위치 => _기사현재위치;

    public 기사정산샘플요약 정산요약 => _정산요약;

    public IReadOnlyList<DriverRequestItem> 추천의뢰목록 => _추천의뢰목록;

    public IReadOnlyList<기사예약샘플항목> 예약목록 => _예약목록;

    public IReadOnlyList<기사운송샘플항목> 운송목록 => _운송목록;

    public IReadOnlyList<기사알림샘플항목> 알림목록 => _알림목록;

    public DriverRequestItem? 추천의뢰조회(string 의뢰Id)
    {
        return _추천의뢰목록.FirstOrDefault(x => string.Equals(x.의뢰Id, 의뢰Id, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<추천의뢰표시항목> 거리포함추천의뢰목록조회()
        => DriverNativeLocationPolicy.RecommendationsWithDistance(
            _추천의뢰목록, 기사현재위치, DateTime.UtcNow, 거리계산Service.직선거리Km);

    public 기사운송샘플항목? 운송조회(long 운송Id)
    {
        return _운송목록.FirstOrDefault(x => x.Id == 운송Id);
    }

    public 기사운송샘플항목? 현재운송조회()
    {
        if (_currentTransportId is { } id)
            return _운송목록.FirstOrDefault(x => x.Id == id);
        return _운송목록.FirstOrDefault(x =>
            !string.Equals(x.현재단계, "인수완료", StringComparison.Ordinal));
    }

    private async Task LoadLiveServerDataAsync(CancellationToken cancellationToken)
    {
        var sessionRevision = _authSession.SessionRevision;
        var recommendations = await _recommendationApi.전체조회Async(cancellationToken);
        var freightWorkspace = await _freightWorkspace.RefreshAsync(cancellationToken);
        var settlement = await _settlementApi.현재월조회Async(cancellationToken);
        var reservations = await _reservationApi.목록조회Async(cancellationToken);
        var workStatus = await _workApi.운행상태조회Async(cancellationToken);
        var currentWork = await _workApi.현재근무조회Async(cancellationToken);
        var currentTransport = freightWorkspace.다음행동운송;
        if (currentTransport is null || !freightWorkspace.활성운송목록.Any(x => x.Id == currentTransport.Id))
            currentTransport = freightWorkspace.활성운송목록.FirstOrDefault(x =>
                !string.Equals(x.상태, "인수완료", StringComparison.Ordinal));
        기사운송상세응답? currentTransportDetail = null;
        if (currentTransport is { Id: > 0 })
        {
            try
            {
                currentTransportDetail = await _transportApi.상세조회Async(currentTransport.Id, cancellationToken);
            }
            catch (HttpRequestException exception) when (
                exception.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
            {
                // 상세 좌표의 조회 실패는 요약 업무를 막지 않는다. 미확인 좌표를 추정하지 않는다.
            }
            if (currentTransportDetail is not null && currentTransportDetail.Id != currentTransport.Id)
                throw new InvalidOperationException("요청한 운송과 상세 조회 결과가 일치하지 않습니다.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!_authSession.IsAuthenticated || _authSession.SessionRevision != sessionRevision)
            throw new OperationCanceledException("인증 세션이 변경되었습니다.", cancellationToken);

        if (recommendations is not null)
        {
            _추천의뢰목록 = recommendations.Select(ToRequestItem).ToArray();
        }

        _운송목록 = freightWorkspace.활성운송목록
            .Select(item => ToTransportItem(currentTransportDetail is not null && item.Id == currentTransportDetail.Id
                ? currentTransportDetail : item)).ToArray();
        _currentTransportId = currentTransport?.Id;

        if (settlement is not null)
        {
            _정산요약 = ToSettlementSummary(settlement);
        }

        if (reservations is not null)
        {
            _예약목록 = reservations.Select(ToReservationItem).ToArray();
        }

        ApplyWorkState(workStatus, currentWork);
    }

    private void ApplyWorkState(기사운행상태응답? workStatus, 기사현재근무응답? currentWork)
    {
        _기사현재위치 = DriverNativeLocationPolicy.FromServer(workStatus, currentWork);

        _근무상태 = new 기사근무샘플상태(
            string.IsNullOrWhiteSpace(_authSession.UserName) ? "기사" : _authSession.UserName!,
            currentWork?.운행상태 ?? workStatus?.Status ?? "서버 연결",
            string.IsNullOrWhiteSpace(currentWork?.시작모드) ? "서버 조회" : currentWork!.시작모드,
            string.IsNullOrWhiteSpace(currentWork?.시작위치) ? _기사현재위치.위치명 : currentWork!.시작위치,
            currentWork?.복귀지 ?? currentWork?.오늘의복귀지주소,
            currentWork?.시작시각 ?? workStatus?.UpdatedAt ?? DateTime.Now,
            _추천의뢰목록.Count,
            _예약목록.Count);
    }

    private static DriverRequestItem ToRequestItem(기사배차추천항목응답 source)
    {
        return new DriverRequestItem
        {
            의뢰Id = source.의뢰Id,
            화물종류 = source.화물종류,
            운송방식 = "서버 추천",
            운송의뢰유형코드 = source.운송의뢰유형코드,
            운송의뢰유형표시 = source.운송의뢰유형표시,
            차량톤수 = "조건 확인",
            차량형태 = source.차량적합여부 ? "적합" : "부적합",
            인수증필요 = true,
            공동주문운송여부 = source.공동주문운송여부,
            세대배송포함여부 = source.세대배송포함여부,
            세대배송건수 = source.세대배송건수,
            세대배송업무표시 = source.세대배송업무표시,
            결제방식 = "서버 정산",
            픽업지 = source.픽업지,
            하차지 = source.하차지,
            픽업_위도 = source.픽업_위도,
            픽업_경도 = source.픽업_경도,
            하차_위도 = source.하차_위도,
            하차_경도 = source.하차_경도,
            직선거리Km = source.직선거리Km,
            픽업거리Km = source.픽업거리Km,
            공차거리Km = source.공차거리Km,
            운송거리Km = source.운송거리Km,
            복귀예상거리Km = source.복귀예상거리Km,
            지금바로복귀거리Km = source.지금바로복귀거리Km,
            복귀우회증가거리Km = source.복귀우회증가거리Km,
            총공차거리Km = source.총공차거리Km,
            주행거리Km = source.주행거리Km,
            예상톨비 = source.예상톨비,
            예상연료비 = source.예상연료비,
            예상총비용 = source.예상총비용,
            예상수익 = source.예상수익,
            추천점수 = source.추천점수,
            추천사유 = source.추천사유,
            복귀지기준추천여부 = source.복귀지기준추천여부,
            복귀지출처 = source.복귀지출처,
            복귀추천사유 = source.복귀추천사유,
            요약설명 = $"{source.화물종류} 운송, {source.픽업지}에서 {source.하차지}까지",
            상세설명 = "살뜰 서비스의 기사 추천 API에서 내려온 의뢰입니다.",
            상태 = source.상태,
            배차상태 = source.배차상태,
            추천시작시각 = source.추천시작시각,
            추천만료시각 = source.추천만료시각,
            추천라운드 = source.추천라운드,
            경고목록 = source.경고.Concat(source.차량경고).Distinct(StringComparer.Ordinal).ToArray(),
            확인필요경고코드 = source.차량경고.Length > 0
                ? ["FreightVehicleAdvisory"]
                : []
        };
    }

    private static 기사운송샘플항목 ToTransportItem(기사운송요약응답 source)
        => 기사운송표시Mapper.Map(source);

    private static 기사예약샘플항목 ToReservationItem(기사예약목록응답 source)
    {
        return new 기사예약샘플항목(
            source.Id,
            source.StartTime ?? DateTime.Now,
            string.IsNullOrWhiteSpace(source.StartMode) ? "예약 운행" : source.StartMode,
            source.StartLocation,
            source.ReturnDestination,
            source.IsFuture ? "확정" : "완료",
            "살뜰 서비스 예약 API에서 조회됨");
    }

    private static 기사정산샘플요약 ToSettlementSummary(기사정산응답 source)
    {
        return new 기사정산샘플요약(
            source.Year,
            source.Month,
            source.DispatchCount,
            source.UsageFee,
            source.MonthlyFeeCap,
            source.IsPaid,
            [
                new("이용료", "서버 정산 API 기준", source.UsageFee),
                new("월 상한", "정책상 월 이용료 상한", source.MonthlyFeeCap),
                new("상한 잔여", "월 상한까지 남은 금액", source.RemainingUntilCap)
            ]);
    }

    private void ApplyEmptyState()
    {
        _근무상태 = new 기사근무샘플상태(
            "기사",
            "서버 연결 대기",
            "미정",
            "위치 미확인",
            null,
            DateTime.Now,
            0,
            0);
        _기사현재위치 = new 기사현재위치샘플("위치 미확인", 0m, 0m, DateTime.MinValue)
        {
            SourceCode = DriverNativeLocationSources.Unknown
        };
        _정산요약 = new 기사정산샘플요약(
            DateTime.Today.Year,
            DateTime.Today.Month,
            0,
            0m,
            0m,
            false,
            []);
        _추천의뢰목록 = [];
        _예약목록 = [];
        _운송목록 = [];
        _currentTransportId = null;
        _알림목록 = [];
    }

    private void ApplyDisconnectedState()
    {
        if (CanUseSampleFallback())
        {
            ApplySampleFallback();
            return;
        }

        ApplyEmptyState();
    }

    private bool CanUseSampleFallback()
    {
        return _dataModeOptions.Value.CanUseSampleFallback;
    }

    private void ApplySampleFallback()
    {
        _근무상태 = _sampleFallback.근무상태;
        _기사현재위치 = _sampleFallback.기사현재위치;
        _정산요약 = _sampleFallback.정산요약;
        _추천의뢰목록 = _sampleFallback.추천의뢰목록;
        _예약목록 = _sampleFallback.예약목록;
        _운송목록 = _sampleFallback.운송목록;
        _currentTransportId = null;
        _알림목록 = _sampleFallback.알림목록;
    }

}
