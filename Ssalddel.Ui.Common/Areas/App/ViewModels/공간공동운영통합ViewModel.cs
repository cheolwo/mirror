using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>같은 계정·서버의 화면 왕복 동안 초안과 원 명령을 보존합니다. 영구 저장이나 자동 재전송이 아닙니다.</summary>
public sealed class 공간공동운영통합Session
{
    private string? owner, endpoint;
    private readonly Dictionary<string, 공간공동운영예약초안> drafts = new(StringComparer.Ordinal);
    public 공간공동운영예약대기요청? Pending { get; internal set; }
    public void Bind(WorkflowRecoverySnapshot scope)
    {
        if (owner == scope.Owner && endpoint == scope.EndpointKey) return;
        owner = scope.Owner; endpoint = scope.EndpointKey; Pending = null; drafts.Clear();
    }
    public 공간공동운영예약초안 Draft(string ledgerId, DateTimeOffset now)
    {
        if (!drafts.TryGetValue(ledgerId, out var value))
            drafts[ledgerId] = value = new()
            {
                StartsAtKst = now.ToOffset(TimeSpan.FromHours(9)).DateTime.AddHours(1),
                EndsAtKst = now.ToOffset(TimeSpan.FromHours(9)).DateTime.AddHours(2)
            };
        return value;
    }
}

public sealed class 공간공동운영예약초안
{
    public string CycleId { get; set; } = "";
    public string KindCode { get; set; } = 공간공동운영예약종류Codes.Inbound;
    public DateTime StartsAtKst { get; set; }
    public DateTime EndsAtKst { get; set; }
    public HashSet<string> PartyKeys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ReferenceKeys { get; } = new(StringComparer.Ordinal);
    public bool ConfirmNonBinding { get; set; }
    public Guid? ReplacesBookingId { get; set; }
    public bool RequiredForCycle { get; set; }
}

public sealed record 공간공동운영예약대기요청(string LedgerId, Guid BookingId,
    공간공동운영예약명령Request Command, WorkflowRecoverySnapshot Scope);

/// <summary>통합 관계와 내 약속을 읽고, 본인의 명시 결정만 보냅니다. 전문 작업·금액·외부 일정을 추정하지 않습니다.</summary>
public sealed class 공간공동운영통합ViewModel : ObservableObject, IDisposable
{
    private readonly I공간공동운영통합Client client;
    private readonly ISsalddel현재사용자Context user;
    private readonly IWorkflowRecoveryContext recovery;
    private readonly 공간공동운영통합Session session;
    private readonly TimeProvider clock;
    private readonly CancellationTokenSource lifetime = new();
    private long generation;
    private bool disposed;
    private string? owner;
    private WorkflowRecoverySnapshot? viewScope;
    public 공간공동운영통합ViewModel(I공간공동운영통합Client client, ISsalddel현재사용자Context user,
        IWorkflowRecoveryContext recovery, 공간공동운영통합Session session, TimeProvider? clock = null)
    {
        this.client = client; this.user = user; this.recovery = recovery; this.session = session;
        this.clock = clock ?? TimeProvider.System; owner = CurrentOwner;
        if (user is ISsalddel현재사용자변경알림 changes) changes.Changed += AuthenticationChanged;
    }
    private string? CurrentOwner => user.현재사용자.인증됨 ? user.현재사용자.UserId : null;
    public string LedgerId { get; private set; } = "";
    public long ParentRevision { get; private set; }
    public bool IsOperator { get; private set; }
    public bool IsAuthenticated => CurrentOwner is not null && CurrentOwner == owner;
    public bool IsLoading { get; private set; }
    public bool IsSending { get; private set; }
    public bool IsLoadingImpact { get; private set; }
    public bool Loaded { get; private set; }
    public bool CanRetryPending { get; private set; }
    public bool HasPending => session.Pending is { } pending && pending.Scope.Owner == CurrentOwner;
    public bool PendingBelongsHere => session.Pending?.LedgerId == LedgerId;
    public string? PendingReturnHref => HasPending && session.Pending is { } pending
        ? 공간공동운영Presentation.DetailRoute(pending.LedgerId) : null;
    public string? Error { get; private set; }
    public string? ReservationError { get; private set; }
    public string? CandidateError { get; private set; }
    public string? ImpactError { get; private set; }
    public string? Notice { get; private set; }
    public 공간공동운영통합Response? Integrated { get; private set; }
    public 공간공동운영예약목록Response? Reservations { get; private set; }
    public 공간공동운영예약후보Response? Candidates { get; private set; }
    public 공간공동운영예약취소영향Dto? Impact { get; private set; }
    public Guid? ConfirmedBookingId { get; private set; }
    public bool CancelConfirmed { get; set; }
    public string CancelReason { get; set; } = "";
    public long CanonicalRevision => Integrated?.Revision ?? 0;
    public bool SnapshotConsistent => Integrated is not null && Reservations is not null
        && Integrated.LedgerId == LedgerId && Reservations.LedgerId == LedgerId
        && Integrated.Revision == Reservations.Revision;
    public bool CanAct => IsAuthenticated && Loaded && SnapshotConsistent && !IsLoading
        && !IsSending && !IsLoadingImpact && !HasPending;
    public bool CanPropose => CanAct && Reservations!.CanPropose && Candidates is not null
        && Candidates.LedgerId == LedgerId && Candidates.Revision == Reservations.Revision
        && Candidates.TermsRevision == Reservations.TermsRevision;
    public 공간공동운영예약초안 Draft => session.Draft(LedgerId, clock.GetUtcNow());
    public 공간공동운영예약회차후보Dto? DraftCycle => Candidates?.Cycles.SingleOrDefault(c => c.CycleId == Draft.CycleId);
    public IReadOnlyList<공간공동운영예약Dto> ReplacementChoices => Reservations?.Items
        .Where(item => item.Terms.CycleId == Draft.CycleId && item.Terms.KindCode == Draft.KindCode
            && (item.StatusCode is "cancelled" or "declined")
            && (item.Parties.Any(party => party.IsMe) || IsOperator && item.Terms.RequiredForCycle)
            && (IsOperator || !item.Terms.RequiredForCycle)
            && !Reservations.Items.Any(replacement => replacement.Terms.ReplacesBookingId == item.BookingId))
        .OrderByDescending(item => item.Terms.StartsAtUtc).ToArray() ?? [];

    public void DraftChanged()
    {
        Draft.ConfirmNonBinding = false;
        if (Draft.ReplacesBookingId is { } id && !ReplacementChoices.Any(item => item.BookingId == id)) Draft.ReplacesBookingId = null;
        Changed();
    }
    public void SelectReplacement(string? value)
    {
        var previous = Guid.TryParse(value, out var id) ? ReplacementChoices.SingleOrDefault(item => item.BookingId == id) : null;
        Draft.ReplacesBookingId = previous?.BookingId;
        if (previous is not null) Draft.RequiredForCycle = previous.Terms.RequiredForCycle;
        DraftChanged();
    }

    public void ConfirmBooking(Guid bookingId, bool confirmed)
    { ConfirmedBookingId = confirmed ? bookingId : ConfirmedBookingId == bookingId ? null : ConfirmedBookingId; Changed(); }
    public void SelectCycle(string cycleId)
    {
        if (IsSending || HasPending || Candidates?.Cycles.Any(c => c.CycleId == cycleId) != true) return;
        if (Draft.CycleId != cycleId) { Draft.CycleId = cycleId; Draft.ReferenceKeys.Clear(); Draft.ReplacesBookingId = null; Draft.ConfirmNonBinding = false; }
        Changed();
    }
    public void SelectParty(공간공동운영예약당사자Dto party, bool selected)
    {
        if (!CanPropose || Candidates!.Participants.Any(p => p.UserId == party.UserId && p.RoleCode == party.RoleCode) != true) return;
        foreach (var other in Candidates.Participants.Where(p => p.UserId == party.UserId)) Draft.PartyKeys.Remove(PartyKey(other));
        if (selected) Draft.PartyKeys.Add(PartyKey(party));
        Draft.ConfirmNonBinding = false; Changed();
    }
    public void SelectReference(공간공동운영연결후보Dto candidate, bool selected)
    {
        if (!CanPropose || DraftCycle?.References.Any(r => ReferenceKey(r.Reference) == ReferenceKey(candidate.Reference)) != true) return;
        if (selected) Draft.ReferenceKeys.Add(ReferenceKey(candidate.Reference)); else Draft.ReferenceKeys.Remove(ReferenceKey(candidate.Reference));
        Draft.ConfirmNonBinding = false; Changed();
    }
    public static string PartyKey(공간공동운영예약당사자Dto party) => Uri.EscapeDataString(party.UserId) + ":" + party.RoleCode;
    public static string ReferenceKey(공간공동운영전문참조Dto source) => source.KindCode + ":" + Uri.EscapeDataString(source.ReferenceId);
    public static DateTimeOffset KstToUtc(DateTime local)
        => new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).ToUniversalTime();

    public async Task LoadAsync(공간공동운영상세Response? detail)
    {
        if (disposed || IsSending && detail?.LedgerId == LedgerId) return;
        if (owner != CurrentOwner) AuthenticationChanged();
        if (detail is null) { ClearView(); return; }
        LedgerId = detail.LedgerId; ParentRevision = detail.Revision; IsOperator = detail.IsOperator;
        var scope = Capture(); if (scope is null) { ClearView(); Error = "로그인한 계정의 통합 원장을 확인해 주세요."; Changed(); return; }
        session.Bind(scope); viewScope = scope; if (!IsOperator) Draft.RequiredForCycle = false;
        var current = ++generation; ClearData(); IsSending = IsLoadingImpact = false; IsLoading = true; Error = null; Notice = null; Draft.ConfirmNonBinding = false; Changed();
        try { await LoadData(current, scope, detail.Revision); }
        catch (Exception exception) { if (Current(current, scope)) Error = ReadError(exception); }
        finally { if (Current(current, scope)) { IsLoading = false; Changed(); } }
    }

    private async Task LoadData(long current, WorkflowRecoverySnapshot scope, long minimumRevision)
    {
        var integrated = await client.ReadAsync(LedgerId, lifetime.Token);
        if (!Current(current, scope)) return;
        if (integrated.LedgerId != LedgerId || integrated.Revision < minimumRevision)
            throw new InvalidDataException("통합 원장 판본을 확인하지 못했습니다.");
        Integrated = integrated; Loaded = true;
        try
        {
            var reservations = await client.ReservationsAsync(LedgerId, lifetime.Token);
            if (!Current(current, scope)) return;
            if (reservations.LedgerId != LedgerId || reservations.Revision < minimumRevision) throw new InvalidDataException();
            Reservations = reservations; ReservationError = null;
        }
        catch (Exception exception) { if (Current(current, scope)) { Reservations = null; ReservationError = ReadError(exception); } }
        if (!Current(current, scope)) return;
        if (Reservations is not null && !SnapshotConsistent) ReservationError = "기록이 먼저 바뀌었어요. 다시 불러온 뒤 약속을 확인해 주세요.";
        Candidates = null; CandidateError = null;
        if (Reservations?.CanPropose == true && SnapshotConsistent)
        {
            try
            {
                var candidates = await client.CandidatesAsync(LedgerId, lifetime.Token);
                if (!Current(current, scope)) return;
                if (candidates.LedgerId != LedgerId || candidates.Revision != Reservations.Revision
                    || candidates.TermsRevision != Reservations.TermsRevision || candidates.Cycles is null || candidates.Participants is null
                    || candidates.Cycles.Any(c => c.References is null)
                    || candidates.Cycles.Select(c => c.CycleId).Distinct(StringComparer.Ordinal).Count() != candidates.Cycles.Count)
                    throw new InvalidDataException();
                Candidates = candidates;
                if (string.IsNullOrEmpty(Draft.CycleId)) Draft.CycleId = candidates.Cycles.OrderByDescending(c => c.Number).FirstOrDefault()?.CycleId ?? "";
            }
            catch (Exception) { if (Current(current, scope)) CandidateError = "현재 제안할 수 있는 참여자와 회차를 확인하지 못했어요. 다시 불러와 주세요."; }
        }
    }

    public async Task ProposeAsync()
    {
        if (!WriteReady() || !CanPropose) return;
        var draft = Draft; var cycle = DraftCycle;
        var parties = Candidates!.Participants.Where(p => draft.PartyKeys.Contains(PartyKey(p))).ToArray();
        var references = cycle?.References.Where(r => draft.ReferenceKeys.Contains(ReferenceKey(r.Reference))).Select(r => Copy(r.Reference)).ToArray() ?? [];
        var starts = KstToUtc(draft.StartsAtKst); var ends = KstToUtc(draft.EndsAtKst);
        if (!draft.ConfirmNonBinding || cycle is null || !공간공동운영예약종류Codes.All.Contains(draft.KindCode)
            || parties.Length is < 2 or > 12 || parties.Select(p => p.UserId).Distinct(StringComparer.Ordinal).Count() != parties.Length
            || draft.PartyKeys.Count != parties.Length || draft.ReferenceKeys.Count != references.Length
            || !IsOperator && draft.RequiredForCycle
            || draft.ReplacesBookingId is { } replacement && !ReplacementChoices.Any(item => item.BookingId == replacement && item.Terms.RequiredForCycle == draft.RequiredForCycle)
            || !parties.Any(p => p.IsMe) || starts <= clock.GetUtcNow() || ends <= starts)
        { Error = "회차·당사자 두 명 이상·한국 시간과 조건 확인을 살펴봐 주세요. 본인도 당사자로 선택해야 해요."; Changed(); return; }
        var scope = Capture(); if (scope is null) return;
        session.Pending = new(LedgerId, Guid.NewGuid(), new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = Reservations!.Revision,
            ExpectedBookingRevision = 0, ExpectedTermsRevision = Reservations.TermsRevision,
            Action = "propose", ConfirmNonBinding = true,
            Terms = new() { CycleId = cycle.CycleId, KindCode = draft.KindCode, StartsAtUtc = starts, EndsAtUtc = ends,
                Capacity = parties.Length, Parties = parties.Select(p => new 공간공동운영예약당사자Request { UserId = p.UserId, RoleCode = p.RoleCode }).ToArray(), References = references,
                ReplacesBookingId = draft.ReplacesBookingId, RequiredForCycle = draft.RequiredForCycle }
        }, scope);
        await SendPending(scope);
    }

    public async Task ExecuteAsync(Guid bookingId, string action)
    {
        if (!WriteReady()) return;
        var booking = Reservations!.Items.SingleOrDefault(b => b.BookingId == bookingId);
        if (booking is null || !booking.AllowedActions.Contains(action) || action is not ("accept" or "decline" or "cancel"))
        { Error = "현재 가능한 약속 결정을 다시 확인해 주세요."; Changed(); return; }
        if (action == "cancel")
        {
            if (CancelReason.Length > 300 || CancelReason.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            { ImpactError = "취소 사유는 제어문자 없이 300자 이내로 입력해 주세요."; Changed(); return; }
            if (!CancelConfirmed || Impact is not { CanCancel: true } impact || impact.BookingId != bookingId
                || impact.LedgerId != LedgerId || impact.ParentRevision != Reservations.Revision || impact.BookingRevision != booking.Revision
                || string.IsNullOrWhiteSpace(impact.Digest))
            { ImpactError = "이 약속의 현재 취소 영향을 먼저 확인해 주세요."; Changed(); return; }
        }
        else if (ConfirmedBookingId != bookingId)
        { Error = "이 약속의 당사자·시간·조건을 확인한 뒤 선택해 주세요."; Changed(); return; }
        var scope = Capture(); if (scope is null) return;
        session.Pending = new(LedgerId, bookingId, new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = Reservations.Revision,
            ExpectedBookingRevision = booking.Revision, ExpectedTermsRevision = Reservations.TermsRevision,
            Action = action, ConfirmNonBinding = true, ImpactDigest = action == "cancel" ? Impact!.Digest : null,
            Reason = action == "cancel" && !string.IsNullOrWhiteSpace(CancelReason) ? CancelReason.Trim() : null
        }, scope);
        await SendPending(scope);
    }

    public async Task PreviewCancelAsync(Guid bookingId)
    {
        if (!WriteReady()) return;
        var booking = Reservations!.Items.SingleOrDefault(b => b.BookingId == bookingId && b.AllowedActions.Contains("cancel"));
        if (booking is null) return;
        var scope = Capture(); if (scope is null) return;
        var current = ++generation; IsLoadingImpact = true; Impact = null; ImpactError = null; CancelConfirmed = false; CancelReason = ""; Changed();
        try
        {
            var value = await client.ImpactAsync(LedgerId, bookingId, lifetime.Token);
            if (!Current(current, scope)) return;
            if (value.LedgerId != LedgerId || value.BookingId != bookingId || value.ParentRevision != Reservations!.Revision
                || value.BookingRevision != booking.Revision || value.TermsRevision != Reservations.TermsRevision || string.IsNullOrWhiteSpace(value.Digest))
            { ImpactError = "영향 기록이 먼저 바뀌었어요. 다시 불러와 확인해 주세요."; return; }
            Impact = value;
        }
        catch (Exception exception) { if (Current(current, scope)) ImpactError = ReadError(exception); }
        finally { if (Current(current, scope)) { IsLoadingImpact = false; Changed(); } }
    }
    public void DismissImpact()
    { if (IsSending || IsLoadingImpact) return; Impact = null; ImpactError = null; CancelConfirmed = false; CancelReason = ""; Changed(); }

    public async Task RecoverAsync()
    {
        if (disposed || IsSending || session.Pending is not { } pending) return;
        var scope = Capture(); if (scope is null || !SamePendingScope(scope)) return;
        viewScope = scope;
        var current = ++generation; IsSending = true; CanRetryPending = false; Error = null; Changed();
        try
        {
            var value = await client.ReceiptAsync(pending.LedgerId, pending.Command.ClientRequestId, lifetime.Token);
            if (!Current(current, scope)) return;
            if (value is null) { CanRetryPending = true; Notice = "원래 요청의 처리 기록이 아직 없어요. 결과를 다시 확인하거나 같은 요청을 직접 다시 보낼 수 있어요."; return; }
            await ApplyCanonical(value, pending, current, scope);
        }
        catch (Exception exception) { if (Current(current, scope)) Error = ReadError(exception); }
        finally { if (Current(current, scope)) { IsSending = false; Changed(); } }
    }
    public Task RetryPendingAsync()
    {
        var scope = Capture();
        return !CanRetryPending || IsSending || scope is null || !SamePendingScope(scope) ? Task.CompletedTask : SendPending(scope);
    }
    private async Task SendPending(WorkflowRecoverySnapshot scope)
    {
        if (session.Pending is not { } pending || !SamePendingScope(scope)) return;
        var current = ++generation; IsSending = true; CanRetryPending = false; Error = null; Notice = null; Changed();
        try
        {
            var value = await client.CommandAsync(pending.LedgerId, pending.BookingId, Copy(pending.Command), lifetime.Token);
            if (!Current(current, scope)) return;
            if (value is null) { Error = "처리 결과를 확인하지 못했어요. 원래 요청의 결과를 먼저 확인해 주세요."; return; }
            await ApplyCanonical(value, pending, current, scope);
        }
        catch (Exception exception)
        {
            if (!Current(current, scope)) return;
            if (exception is SsalddelApiException { StatusCode: 400 or 403 or 409 or 422 })
            {
                session.Pending = null; Reservations = null; Candidates = null; Impact = null;
                Draft.ConfirmNonBinding = false; ConfirmedBookingId = null; CancelConfirmed = false;
            }
            Error = ReadError(exception);
        }
        finally { if (Current(current, scope)) { IsSending = false; Changed(); } }
    }
    private async Task ApplyCanonical(공간공동운영예약Dto response, 공간공동운영예약대기요청 pending, long current, WorkflowRecoverySnapshot scope)
    {
        if (response.LedgerId != pending.LedgerId || response.BookingId != pending.BookingId || response.Revision < 1
            || response.ParentRevision <= pending.Command.ExpectedRevision)
        { Error = "원래 요청과 다른 약속 응답이에요. 처리 결과를 다시 확인해 주세요."; return; }
        var canonical = await client.ReservationAsync(pending.LedgerId, pending.BookingId, lifetime.Token);
        if (!Current(current, scope)) return;
        if (canonical is null || canonical.LedgerId != pending.LedgerId || canonical.BookingId != pending.BookingId
            || canonical.Revision < response.Revision || canonical.ParentRevision < response.ParentRevision)
        { Error = "저장한 약속의 현재 상태를 확인하지 못했어요. 원 요청을 보존했어요."; return; }
        await LoadData(current, scope, canonical.ParentRevision);
        if (!Current(current, scope)) return;
        if (!SnapshotConsistent || Reservations!.Items.SingleOrDefault(b => b.BookingId == pending.BookingId) is not { } item
            || item.Revision < canonical.Revision)
        { Error = "현재 약속 목록을 다시 확인하지 못했어요. 원 요청을 보존했어요."; return; }
        session.Pending = null; Draft.ConfirmNonBinding = false; ConfirmedBookingId = null; Impact = null; CancelConfirmed = false; CancelReason = "";
        Notice = "약속의 현재 상태를 다시 확인했어요."; CanRetryPending = false;
    }
    private bool WriteReady()
    {
        if (disposed || IsLoading || IsSending || IsLoadingImpact) return false;
        if (CurrentOwner is null) { Error = "로그인 후 원래 약속을 확인해 주세요."; Changed(); return false; }
        if (viewScope is null || owner != CurrentOwner || !recovery.IsCurrent(viewScope))
        { AuthenticationChanged(); Error = "계정이나 서버가 바뀌었어요. 원래 약속을 다시 불러와 주세요."; Changed(); return false; }
        if (HasPending) { Error = "이전 요청의 결과를 먼저 확인해 주세요."; Changed(); return false; }
        if (!SnapshotConsistent) { Error = "최신 통합 원장과 약속 목록을 다시 불러와 주세요."; Changed(); return false; }
        return true;
    }
    private WorkflowRecoverySnapshot? Capture()
    {
        try { var scope = recovery.Capture(); if (scope.Owner != CurrentOwner) throw new InvalidOperationException(); return scope; }
        catch (Exception) { Error = "로그인한 계정과 원래 서버를 확인해 주세요."; Changed(); return null; }
    }
    private bool SamePendingScope(WorkflowRecoverySnapshot scope)
    {
        if (session.Pending is { } p && p.LedgerId == LedgerId && p.Scope.Owner == scope.Owner && p.Scope.EndpointKey == scope.EndpointKey) return true;
        Error = "원래 요청을 보낸 계정·서버·공동운영에서 결과를 확인해 주세요."; Changed(); return false;
    }
    private bool Current(long value, WorkflowRecoverySnapshot scope)
    {
        if (disposed || value != generation) return false;
        if (scope.Owner == CurrentOwner && recovery.IsCurrent(scope)) return true;
        AuthenticationChanged(); return false;
    }
    private void AuthenticationChanged()
    {
        if (disposed) return;
        ++generation; owner = CurrentOwner; ClearData(); IsLoading = IsSending = IsLoadingImpact = false;
        viewScope = null;
        try { var scope = recovery.Capture(); if (scope.Owner == CurrentOwner) session.Bind(scope); } catch (Exception) { }
        Error = null; Notice = null; CanRetryPending = false; CancelReason = ""; Changed();
    }
    public void ClearView()
    { if (disposed) return; ++generation; ClearData(); LedgerId = ""; ParentRevision = 0; IsOperator = false; viewScope = null; IsLoading = IsSending = IsLoadingImpact = false; Error = null; Notice = null; CanRetryPending = false; Changed(); }
    private void ClearData()
    {
        Integrated = null; Reservations = null; Candidates = null; Impact = null; Loaded = false;
        ReservationError = CandidateError = ImpactError = null; ConfirmedBookingId = null; CancelConfirmed = false;
    }
    private static string ReadError(Exception exception) => exception is SsalddelApiException { StatusCode: 401 }
        ? "로그인이 만료됐어요. 원래 계정으로 돌아와 결과를 확인해 주세요."
        : exception is SsalddelApiException { StatusCode: 403 or 404 }
            ? "현재 계정에서 이 기록을 확인할 수 없어요."
            : exception is SsalddelApiException { StatusCode: 409 }
                ? "조건이나 일정이 먼저 바뀌었어요. 다시 불러와 확인해 주세요."
                : "기록을 확인하지 못했어요. 연결을 확인하고 다시 불러와 주세요.";
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private void Changed() { if (!disposed) OnPropertyChanged(string.Empty); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; ++generation;
        if (user is ISsalddel현재사용자변경알림 changes) changes.Changed -= AuthenticationChanged;
        lifetime.Cancel(); lifetime.Dispose();
    }
}
