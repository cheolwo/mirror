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
    private readonly TransportRequestLedgerRealtimeClient? _realtimeClient;
    private readonly ITransportRequestLedgerObserver _ledgerObserver;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private bool _loaded;
    private string? _loadedAccessToken;
    private string? _stateOwner;
    private long _stateRevision;
    private CancellationTokenSource? _refreshLifetime;

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
        TransportRequestLedgerRealtimeClient? realtimeClient,
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
        _stateOwner = _authSession.UserId;
        _stateRevision = _authSession.SessionRevision;
        _authSession.Changed += OnSessionChanged;
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
            if (!_authSession.IsAuthenticated || string.IsNullOrWhiteSpace(accessToken))
            {
                ApplyDisconnectedState();
                _loaded = false;
                _loadedAccessToken = null;
                return;
            }

            var owner = _authSession.UserId;
            var revision = _authSession.SessionRevision;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _refreshLifetime = lifetime;
            var token = lifetime.Token;
            var realtimeState = DriverWorkspaceSliceReadState.Unknown("실시간 연결");
            try
            {
                if (_realtimeClient is not null)
                {
                    await _realtimeClient.StartAsync(token);
                    realtimeState = DriverWorkspaceSliceReadState.Current();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // 실시간 연결 실패 시 기존 30초 보완 조회와 수동 새로고침을 유지합니다.
                realtimeState = DriverWorkspaceSliceReadState.Failed("실시간 연결");
            }
            EnsureCurrent(owner, revision, token);

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
                await LoadLiveServerDataAsync(owner, revision, realtimeState, token);
                EnsureCurrent(owner, revision, token);
                _loaded = ReadState.Warnings.Count == 0;
                _loadedAccessToken = accessToken;
            }
            catch
            {
                // An authenticated live read is never replaced by examples, including when preview fallback is allowed.
                if (_authSession.SessionRevision == revision && string.Equals(_authSession.UserId, owner, StringComparison.Ordinal))
                    ApplyEmptyState();
                _loaded = false;
                _loadedAccessToken = null;
                throw;
            }
        }
        finally
        {
            _refreshLifetime = null;
            _refreshGate.Release();
        }
    }

    private void OnLedgerRefreshRequested(TransportRequestLedgerRefreshRequest request)
        => _ = RefreshAfterLedgerAsync();

    private async Task RefreshAfterLedgerAsync()
    {
        try { await RefreshAsync(force: true); }
        catch (Exception) { /* Slice state or the next explicit authenticated read reports the failure. */ }
    }

    private void OnSessionChanged()
    {
        if (_stateRevision == _authSession.SessionRevision && string.Equals(_stateOwner, _authSession.UserId, StringComparison.Ordinal)) return;
        _refreshLifetime?.Cancel();
        _stateOwner = _authSession.UserId; _stateRevision = _authSession.SessionRevision;
        _loaded = false; _loadedAccessToken = null; ApplyEmptyState();
    }

    public DriverWorkspaceReadState ReadState { get; private set; } = new();

    public 기사근무샘플상태 근무상태 => _근무상태;

    public 기사현재위치샘플 기사현재위치 => _기사현재위치;

    public 기사정산샘플요약 정산요약 => _정산요약;

    public IReadOnlyList<DriverRequestItem> 추천의뢰목록 => _추천의뢰목록;

    public IReadOnlyList<기사예약샘플항목> 예약목록 => _예약목록;

    public IReadOnlyList<기사운송샘플항목> 운송목록 => _운송목록;

    public IReadOnlyList<기사알림샘플항목> 알림목록 => _알림목록;

    public DriverRequestItem? 추천의뢰조회(string 의뢰Id)
    {
        if (!ReadState.Recommendations.IsCurrent) return null;
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

    private async Task LoadLiveServerDataAsync(string? owner, long sessionRevision,
        DriverWorkspaceSliceReadState realtimeState, CancellationToken cancellationToken)
    {
        // A new read invalidates actionable offers until this session's latest result is confirmed.
        ReadState = ReadState with { Recommendations = DriverWorkspaceSliceReadState.Unknown("추천 운송") };
        // Core transport is queried independently from provider-dependent recommendations/financial summaries.
        var freight = await ReadSliceAsync("현재 운송", async ct =>
        {
            var value = await _freightWorkspace.RefreshAsync(ct);
            if (value.활성운송목록.Any(x => !string.Equals(x.기사_운송자, owner, StringComparison.Ordinal)))
                throw new InvalidOperationException("현재 기사와 운송 작업공간의 연결이 일치하지 않습니다.");
            return value;
        }, owner, sessionRevision, cancellationToken);
        var work = await ReadSliceAsync("운행 상태", async ct =>
        {
            var status = await _workApi.운행상태조회Async(ct);
            var current = await _workApi.현재근무조회Async(ct);
            if (status is null || current is null || !string.Equals(status.DriverId, owner, StringComparison.Ordinal)
                || !string.Equals(current.DriverId, owner, StringComparison.Ordinal)
                || status.ShiftId != current.ShiftId || status.Status != current.운행상태)
                throw new InvalidOperationException("현재 기사의 운행 상태를 확인하지 못했습니다.");
            return (status, current);
        }, owner, sessionRevision, cancellationToken);
        var freightWorkspace = freight.Value;
        var currentTransport = freightWorkspace?.다음행동운송;
        if (freightWorkspace is not null && (currentTransport is null || !freightWorkspace.활성운송목록.Any(x => x.Id == currentTransport.Id)))
            currentTransport = freightWorkspace.활성운송목록.FirstOrDefault(x =>
                !string.Equals(x.상태, "인수완료", StringComparison.Ordinal));
        기사운송상세응답? currentTransportDetail = null;
        var detailState = freight.State.IsCurrent ? DriverWorkspaceSliceReadState.Current() : DriverWorkspaceSliceReadState.Unknown("운송 상세");
        if (currentTransport is { Id: > 0 })
        {
            var detail = await ReadSliceAsync("운송 상세", async ct =>
            {
                var value = await _transportApi.상세조회Async(currentTransport.Id, ct);
                if (value is null || value.Id != currentTransport.Id || !string.Equals(value.기사_운송자, owner, StringComparison.Ordinal))
                    throw new InvalidOperationException("요청한 운송과 상세 조회 결과가 일치하지 않습니다.");
                return value;
            }, owner, sessionRevision, cancellationToken);
            currentTransportDetail = detail.Value; detailState = detail.State;
        }
        var recommendations = await ReadSliceAsync("추천 운송", ct => _recommendationApi.전체조회Async(ct), owner, sessionRevision, cancellationToken);
        var settlement = await ReadSliceAsync("월 정산", async ct =>
        {
            var value = await _settlementApi.현재월조회Async(ct);
            if (value is not null && !string.Equals(value.DriverId, owner, StringComparison.Ordinal))
                throw new InvalidOperationException("현재 기사의 정산 정보가 아닙니다.");
            return value;
        }, owner, sessionRevision, cancellationToken);
        var reservations = await ReadSliceAsync("예약", async ct =>
        {
            var values = await _reservationApi.목록조회Async(ct);
            if (values.Any(x => !string.Equals(x.DriverId, owner, StringComparison.Ordinal)))
                throw new InvalidOperationException("현재 기사의 예약 정보가 아닙니다.");
            return values;
        }, owner, sessionRevision, cancellationToken);
        EnsureCurrent(owner, sessionRevision, cancellationToken);

        _추천의뢰목록 = recommendations.Value?.Select(ToRequestItem).ToArray() ?? [];
        _운송목록 = freightWorkspace?.활성운송목록
            .Select(item => ToTransportItem(currentTransportDetail is not null && item.Id == currentTransportDetail.Id
                ? currentTransportDetail : item)).ToArray() ?? [];
        _currentTransportId = currentTransport?.Id;
        _정산요약 = settlement.Value is not null ? ToSettlementSummary(settlement.Value) : EmptySettlement();
        _예약목록 = reservations.Value?.Select(ToReservationItem).ToArray() ?? [];
        if (work.State.IsCurrent) ApplyWorkState(work.Value.status, work.Value.current);
        else ApplyUnknownWorkState();
        ReadState = new()
        {
            CurrentTransport = freight.State, TransportDetail = detailState, Work = work.State,
            Recommendations = recommendations.State, Settlement = settlement.State,
            Reservations = reservations.State, Realtime = realtimeState
        };
    }

    private async Task<(T? Value, DriverWorkspaceSliceReadState State)> ReadSliceAsync<T>(string label,
        Func<CancellationToken, Task<T>> read, string? owner, long revision, CancellationToken token)
    {
        try
        {
            var value = await read(token); EnsureCurrent(owner, revision, token);
            return value is null ? (default, DriverWorkspaceSliceReadState.Failed(label)) : (value, DriverWorkspaceSliceReadState.Current());
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) { throw; }
        catch (Exception) { EnsureCurrent(owner, revision, token); return (default, DriverWorkspaceSliceReadState.Failed(label)); }
    }

    private void EnsureCurrent(string? owner, long revision, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!_authSession.IsAuthenticated || _authSession.SessionRevision != revision
            || !string.Equals(_authSession.UserId, owner, StringComparison.Ordinal))
            throw new OperationCanceledException("인증 세션이 변경되었습니다.", token);
    }

    private void ApplyUnknownWorkState()
    {
        _기사현재위치 = new 기사현재위치샘플("위치 미확인", 0m, 0m, DateTime.MinValue) { SourceCode = DriverNativeLocationSources.Unknown };
        _근무상태 = new 기사근무샘플상태("기사", "운행 상태 미확인", "미정", "위치 미확인", null, DateTime.MinValue, 0, 0);
    }

    private static 기사정산샘플요약 EmptySettlement()
        => new(DateTime.Today.Year, DateTime.Today.Month, 0, 0m, 0m, false, []);

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
            WorkKind = source.WorkKind,
            AssignedActivities = source.AssignedActivities,
            Pricing = source.Pricing,
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
        ReadState = new();
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
        ReadState = DriverWorkspaceReadState.Sample;
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
