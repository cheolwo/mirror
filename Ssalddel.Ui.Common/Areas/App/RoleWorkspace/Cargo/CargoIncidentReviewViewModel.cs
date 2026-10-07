using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>기존 관리자 사건 API에 독립 입력·동일 요청 재시도·저장 후 재조회를 연결합니다.</summary>
public sealed class CargoIncidentReviewViewModel : IDisposable
{
    public const string Root = "api/v1/admin/transports/incidents";
    public static IReadOnlyDictionary<string, string> Decisions { get; } = new Dictionary<string, string>
    {
        ["NormalAcceptedAffectedHeld"] = "정상분 인수 · 영향분 보류",
        ["EntireTransportHeld"] = "전체 운송 보류",
        ["TransportResumed"] = "운송 재개",
        ["IncidentClosed"] = "검토 종료"
    };
    private readonly IRoleWorkspaceApi api;
    private PendingReview? pending;
    private long? acknowledgedRevision;
    public CargoIncidentReviewViewModel(IRoleWorkspaceApi api, IRoleWorkspaceAccess access)
    { this.api = api; Lifetime = new(access, RoleWorkspaceCatalog.Operator, Clear); }
    public CargoInputLifetime Lifetime { get; }
    public IReadOnlyList<비정상운송사건운영응답> Items { get; private set; } = [];
    public 비정상운송사건운영응답? Source { get; private set; }
    public string? SelectedId { get; private set; }
    public bool Loaded { get; private set; }
    public bool Saved { get; private set; }
    public bool Confirmed { get; set; }
    public bool IsConfirming => pending is not null;
    public bool AwaitingResult => acknowledgedRevision is not null;
    public Guid? RequestId => pending?.Body.클라이언트요청Id;
    public string PendingDecision => pending is null ? "" : Decisions[pending.Body.결정Code];
    public string PendingReason => pending?.Body.검토사유 ?? "";
    public int? PendingNormalQuantity => pending?.Body.정상확인수량;
    public int? PendingAffectedQuantity => pending?.Body.영향수량;
    public bool PendingInsuranceReview => pending?.Body.보험적용가능성검토요청 == true;
    public string? Message { get; private set; }
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public int? NormalQuantity { get; set; }
    public int? AffectedQuantity { get; set; }
    public bool ReviewInsurance { get; set; }

    public Task LoadAsync(string? incidentId = null)
    {
        Lifetime.Reset(); SelectedId = incidentId;
        return Lifetime.RunAsync(async ct =>
        {
            if (incidentId is not null && RoleWorkspaceNavigation.StableId(incidentId) != incidentId)
                throw new ArgumentException("사건 식별자를 확인해 주세요.");
            var items = await ReadAsync(ct);
            Lifetime.RequireCurrent(ct);
            Items = items;
            if (incidentId is not null)
            {
                Source = items.SingleOrDefault(item => item.사건StableId == incidentId)
                    ?? throw new InvalidOperationException("검토할 사건이 없습니다. 목록을 다시 조회해 주세요.");
                NormalQuantity = Source.정상확인수량; AffectedQuantity = Source.영향수량;
            }
            Loaded = true;
        });
    }

    private async Task<IReadOnlyList<비정상운송사건운영응답>> ReadAsync(CancellationToken ct)
    {
        IReadOnlyList<비정상운송사건운영응답> items;
        try { items = await api.GetAsync<IReadOnlyList<비정상운송사건운영응답>>(RoleWorkspaceCatalog.Operator, Root, ct); }
        catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 404)
        {
            Lifetime.RequireCurrent(ct);
            Message = "화물 사건 검토를 열 수 없습니다. 기능 활성화와 접근 권한을 확인해 주세요.";
            throw;
        }
        Lifetime.RequireCurrent(ct);
        if (items.Any(item => RoleWorkspaceNavigation.StableId(item.사건StableId) != item.사건StableId
            || string.IsNullOrWhiteSpace(item.사건StableId) || item.운송Id <= 0 || string.IsNullOrWhiteSpace(item.운송의뢰Id) || item.Revision <= 0)
            || items.Select(item => item.사건StableId).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw new InvalidOperationException("사건 응답을 확인하지 못했습니다.");
        return items;
    }

    public void Prepare()
    {
        if (Lifetime.IsBusy || Lifetime.HasError || !Loaded || Source is null || Saved || pending is not null) return;
        Message = null;
        if (Source.상태Code == "Closed") Message = "종료된 사건은 목록에서 기록을 확인해 주세요.";
        else if (!Decisions.ContainsKey(Decision)) Message = "검토 결정을 선택해 주세요.";
        else if (Reason.Trim().Length is 0 or > 1000) Message = "검토 사유를 1~1,000자로 입력해 주세요.";
        else if (Decision == "NormalAcceptedAffectedHeld"
            && (NormalQuantity is not > 0 || AffectedQuantity is not > 0
                || Source.전체수량 is { } total && (long)NormalQuantity.Value + AffectedQuantity.Value != total))
            Message = "정상·영향 수량을 확인해 주세요. 두 수량의 합은 전체 수량과 같아야 합니다.";
        else
        {
            pending = new(Source.운송Id, Source.운송의뢰Id, new()
            {
                클라이언트요청Id = Guid.NewGuid(), 예상Revision = Source.Revision, 결정Code = Decision,
                정상확인수량 = Decision == "NormalAcceptedAffectedHeld" ? NormalQuantity : null,
                영향수량 = Decision == "NormalAcceptedAffectedHeld" ? AffectedQuantity : null,
                보험적용가능성검토요청 = ReviewInsurance, 검토사유 = Reason.Trim()
            });
            Confirmed = false;
        }
        Lifetime.Notify();
    }

    public void CancelConfirmation()
    {
        if (Lifetime.IsBusy || AwaitingResult) return;
        pending = null; Confirmed = false; Message = null; Lifetime.Notify();
    }

    public Task SubmitAsync()
    {
        if (AwaitingResult) return CheckResultAsync();
        if (!Confirmed || pending is not { } request || SelectedId is not { } id || Saved) return Task.CompletedTask;
        return Lifetime.RunAsync(async ct =>
        {
            Message = null;
            비정상운송사건운영응답? response;
            try { response = await api.PutAsync<비정상운송사건운영응답>(RoleWorkspaceCatalog.Operator,
                Root + "/" + Uri.EscapeDataString(id) + "/decision", request.Body, ct); }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 409)
            {
                Lifetime.RequireCurrent(ct);
                pending = null; Confirmed = false; Source = null; Loaded = false;
                Message = "다른 검토에서 사건이 변경되었습니다. 다시 조회한 뒤 결정해 주세요.";
                return;
            }
            Lifetime.RequireCurrent(ct);
            if (response is null || response.사건StableId != id || response.운송Id != request.TransportId
                || response.운송의뢰Id != request.RequestId || response.Revision <= request.Body.예상Revision
                || response.해결결과Code != request.Body.결정Code)
                throw new InvalidOperationException("검토 결과를 확인하지 못했습니다.");
            acknowledgedRevision = response.Revision;
            Message = "결정 응답을 받았습니다. 저장된 사건을 다시 확인합니다.";
            await VerifySavedAsync(ct);
        });
    }

    public Task CheckResultAsync() => !AwaitingResult ? Task.CompletedTask : Lifetime.RunAsync(VerifySavedAsync);
    private async Task VerifySavedAsync(CancellationToken ct)
    {
        var items = await ReadAsync(ct);
        var source = items.SingleOrDefault(item => item.사건StableId == SelectedId);
        Lifetime.RequireCurrent(ct);
        if (source is null || pending is null || source.운송Id != pending.TransportId || source.운송의뢰Id != pending.RequestId)
            throw new InvalidOperationException("저장한 사건을 다시 확인하지 못했습니다.");
        Items = items; Source = source;
        Saved = acknowledgedRevision is { } revision && source.Revision >= revision
            && source.해결결과Code == pending.Body.결정Code
            && (pending.Body.결정Code != "NormalAcceptedAffectedHeld"
                || source.정상확인수량 == pending.Body.정상확인수량 && source.영향수량 == pending.Body.영향수량);
        Message = Saved ? "검토 결정이 반영된 사건을 확인했습니다." : "결정 응답 이후 사건의 상태가 달라졌습니다. 최신 기록을 다시 확인해 주세요.";
    }

    private void Clear()
    {
        Items = []; Source = null; pending = null; acknowledgedRevision = null;
        Loaded = Saved = Confirmed = ReviewInsurance = false; Decision = Reason = "";
        NormalQuantity = AffectedQuantity = null; Message = null;
    }
    public void Dispose() => Lifetime.Dispose();
    private sealed record PendingReview(long TransportId, string RequestId, 비정상운송사건검토요청 Body);
}
