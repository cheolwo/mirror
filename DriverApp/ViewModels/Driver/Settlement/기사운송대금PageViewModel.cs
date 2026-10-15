using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DriverApp.Services;
using Ssalddel.Contracts.Driver.Settlement;

namespace DriverApp.ViewModels.Driver.Settlement;

/// <summary>본인 운송대금 조회의 월·선택·인증·페이지 수명만 소유합니다. 지급을 확정하지 않습니다.</summary>
public sealed class 기사운송대금PageViewModel : ObservableObject, IDisposable
{
    private readonly IDriverPayoutPreparationApiService _api;
    private readonly IAuthSession _auth;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _pageToken;
    private CancellationTokenSource? _query;
    private long _generation;
    private long _observedSession;
    private long _rowsSession = -1;
    private IReadOnlyList<기사운송대금항목> _rows = [];
    private bool _disposed;
    private bool _loginRequired;
    private bool _restoringAuthentication;

    public 기사운송대금PageViewModel(IDriverPayoutPreparationApiService api, IAuthSession auth)
    {
        _api = api;
        _auth = auth;
        _observedSession = auth.SessionRevision;
        _pageToken = _lifetime.Token;
        _auth.Changed += AuthenticationChanged;
    }

    public int Year { get; private set; }
    public int Month { get; private set; }
    public long? SelectedTransportId { get; private set; }
    public bool 불러오는중 { get; private set; } = true;
    public bool 로그인필요 => _loginRequired || !_auth.IsAuthenticated;
    public string? 오류메시지 { get; private set; }
    public bool 기간유효 => Year is >= 2000 and <= 2100 && Month is >= 1 and <= 12;
    public IReadOnlyList<기사운송대금항목> 항목목록
        => !_disposed && _auth.IsAuthenticated && _rowsSession == _auth.SessionRevision ? _rows : [];
    public 기사운송대금항목? 선택항목 => 항목목록.SingleOrDefault(x => x.TransportId == SelectedTransportId);

    public Task InitializeAsync(int? year, int? month, long? transportId = null)
    {
        var now = DateTime.UtcNow;
        return LoadAsync(year ?? now.Year, month ?? now.Month, transportId, restoreAuthentication: true);
    }

    public Task RefreshAsync()
        => LoadAsync(Year, Month, SelectedTransportId, restoreAuthentication: false);

    private async Task LoadAsync(int year, int month, long? transportId, bool restoreAuthentication)
    {
        CancellationTokenSource query;
        CancellationTokenSource? previous;
        long generation;
        lock (_gate)
        {
            if (_disposed) return;
            query = CancellationTokenSource.CreateLinkedTokenSource(_pageToken);
            previous = _query;
            _query = query;
            generation = ++_generation;
            Year = year;
            Month = month;
            SelectedTransportId = transportId;
            ClearRows();
            _loginRequired = false;
            _restoringAuthentication = restoreAuthentication;
            오류메시지 = null;
            불러오는중 = true;
        }
        Cancel(previous);
        Notify();
        long session = -1;
        string? owner = null;
        try
        {
            if (year is < 2000 or > 2100 || month is < 1 or > 12 || transportId is <= 0)
                throw new ArgumentException("조회할 기간과 운송을 확인해 주세요.");
            if (restoreAuthentication) await _auth.RestoreAsync(query.Token);
            lock (_gate)
            {
                if (!IsCurrentQuery(query, generation)) return;
                _restoringAuthentication = false;
                session = _auth.SessionRevision;
                owner = _auth.UserId;
                _observedSession = session;
                if (!_auth.IsAuthenticated) return;
            }
            var response = await _api.월별조회Async(year, month, query.Token)
                ?? throw new InvalidOperationException("운송대금 조회를 사용할 수 없습니다. 기능과 접근 권한을 확인해 주세요.");
            var rows = ReadRows(response, year, month, owner);
            lock (_gate)
            {
                if (!IsCurrentQuery(query, generation) || !_auth.IsAuthenticated || session != _auth.SessionRevision) return;
                _rows = rows;
                _rowsSession = session;
            }
        }
        catch (OperationCanceledException) when (query.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_gate)
            {
                if (!IsCurrentQuery(query, generation) || session >= 0 && session != _auth.SessionRevision) return;
                _loginRequired = ex is UnauthorizedAccessException;
                오류메시지 = _loginRequired ? null : ex is ArgumentException
                    ? "조회할 기간과 운송을 확인해 주세요."
                    : "운송대금을 확인하지 못했습니다. 기능·접근 권한과 서버 연결을 확인한 뒤 다시 시도해 주세요.";
                ClearRows();
            }
        }
        finally
        {
            var changed = false;
            lock (_gate)
            {
                if (IsCurrentQuery(query, generation))
                {
                    _query = null;
                    _restoringAuthentication = false;
                    불러오는중 = false;
                    changed = true;
                }
            }
            query.Dispose();
            if (changed) Notify();
        }
    }

    private static IReadOnlyList<기사운송대금항목> ReadRows(기사지급준비목록응답 source, int year, int month, string? owner)
    {
        if (source.Year != year || source.Month != month || string.IsNullOrWhiteSpace(owner) || source.DriverId != owner || source.Items is null)
            throw new InvalidOperationException("조회 기간의 운송대금 응답을 확인하지 못했습니다.");
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var rows = source.Items.Select(x => x is null || x.TransportId <= 0 || string.IsNullOrWhiteSpace(x.RequestId)
                || x.CompletedAtUtc < start || x.CompletedAtUtc >= end || x.ExpectedPayoutAmount is < 0
                || string.IsNullOrWhiteSpace(x.ReadinessCode) || string.IsNullOrWhiteSpace(x.ReadinessMessage)
                || string.IsNullOrWhiteSpace(x.CurrencyCode)
            ? throw new InvalidOperationException("운송대금 항목을 확인하지 못했습니다.")
            : new 기사운송대금항목(x.TransportId, x.TransportNumber, x.RequestId, x.CompletedAtUtc,
                x.ExpectedPayoutAmount, x.CurrencyCode, x.ReadinessCode, x.ReadinessMessage,
                x.IsReadyForPayoutPreparation, x.SettlementTiming, x.ShipperPaymentStatus, x.FreightSettlementStatus))
            .OrderByDescending(x => x.CompletedAtUtc).ThenByDescending(x => x.TransportId).ToArray();
        if (rows.Select(x => x.TransportId).Distinct().Count() != rows.Length)
            throw new InvalidOperationException("중복 운송대금 응답을 확인하지 못했습니다.");
        return rows;
    }

    private bool IsCurrentQuery(CancellationTokenSource query, long generation)
        => !_disposed && !query.IsCancellationRequested && ReferenceEquals(_query, query) && generation == _generation;

    private void AuthenticationChanged()
    {
        CancellationTokenSource? previous;
        lock (_gate)
        {
            if (_disposed || _auth.IsAuthenticated && _observedSession == _auth.SessionRevision) return;
            _observedSession = _auth.SessionRevision;
            // Restore may establish the initial session. There is no displayed data yet,
            // and the HTTP request captures the session only after Restore completes.
            if (_restoringAuthentication && _rowsSession == -1) return;
            ++_generation;
            previous = _query;
            _query = null;
            _restoringAuthentication = false;
            ClearRows();
            불러오는중 = false;
            _loginRequired = !_auth.IsAuthenticated;
            오류메시지 = _auth.IsAuthenticated ? "로그인 정보가 변경되었습니다. 현재 계정으로 다시 조회해 주세요." : null;
        }
        Cancel(previous);
        Notify();
    }

    private void ClearRows() { _rows = []; _rowsSession = -1; }
    private void Notify() => OnPropertyChanged(string.Empty);
    private static void Cancel(CancellationTokenSource? source)
    { try { source?.Cancel(); } catch (ObjectDisposedException) { } }

    public void Dispose()
    {
        CancellationTokenSource? previous;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_generation;
            previous = _query;
            _query = null;
            ClearRows();
            불러오는중 = false;
            오류메시지 = null;
        }
        _auth.Changed -= AuthenticationChanged;
        Cancel(previous);
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}

public sealed record 기사운송대금항목(long TransportId, string TransportNumber, string RequestId,
    DateTime CompletedAtUtc, decimal? ExpectedPayoutAmount, string CurrencyCode,
    string ReadinessCode, string ReadinessMessage, bool IsReadyForPayoutPreparation,
    string SettlementTiming, string ShipperPaymentStatus, string FreightSettlementStatus)
{
    public string 금액표시 => ExpectedPayoutAmount is not { } amount ? "금액 미확정"
        : CurrencyCode == "KRW" ? $"{amount.ToString("N0", CultureInfo.GetCultureInfo("ko-KR"))}원"
        : $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {CurrencyCode}";
    public string 상태표시 => ReadinessCode switch
    {
        기사지급준비상태코드.지급준비가능 => "지급 준비 가능",
        기사지급준비상태코드.현장수금확인 => "현장수금 확인",
        기사지급준비상태코드.현장수금대기 => "현장수금 대기",
        기사지급준비상태코드.화주수납대기 => "화주 수납 대기",
        기사지급준비상태코드.비정상운송검토보류 => "운영 검토 보류",
        기사지급준비상태코드.정산계좌없음 => "정산계좌 등록 필요",
        기사지급준비상태코드.정산계좌미확인 => "정산계좌 확인 필요",
        기사지급준비상태코드.지급예정운임없음 => "운임 확인 필요",
        기사지급준비상태코드.원천의뢰없음 => "원천 의뢰 확인 필요",
        "SimulationFulfillmentExcluded" => "모의 수행 · 지급 제외",
        "RateTableEvidenceInvalid" => "합의 운임 확인 필요",
        _ => "지급 상태 확인 필요"
    };
}
