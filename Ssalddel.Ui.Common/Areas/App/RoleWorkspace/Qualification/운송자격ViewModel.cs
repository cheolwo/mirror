using Ssalddel.Contracts.Driver.Profile;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Qualification;

/// <summary>한 자격 원장의 초안과 정본 재확인을 소유합니다. 응답 유실 시 변경 요청을 재전송하지 않습니다.</summary>
public sealed class 운송자격ViewModel(I운송자격Client client, bool administrator = false,
    Func<string>? sessionKey = null) : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly string owner = sessionKey?.Invoke() ?? "local";
    private bool disposed;
    private int minimumRevision;
    public event Action? Changed;
    public bool Administrator => administrator;
    public bool Busy { get; private set; }
    public bool NeedsResultCheck { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public 운송자격상세Dto? Current { get; private set; }
    public bool CanEdit => !Busy && !NeedsResultCheck && Current is not null && IsCurrent;
    public bool CanSubmit => CanEdit && !administrator && Current?.Status != 운송자격Codes.Suspended;
    public bool CanApprove => CanEdit && administrator && Current?.Status == 운송자격Codes.Pending;
    public string VehicleKind { get; private set; } = 운송자격Codes.Motorcycle;
    public string VehicleProfileValue { get; set; } = "";
    public string ActualVehicleReference { get; set; } = "";
    public string TransportOperatorReference { get; set; } = "";
    public string ReviewReason { get; set; } = "";
    public string RecheckDate { get; set; } = "";
    public List<운송자격입력행> Rows { get; private set; } = [];
    private bool IsCurrent => !disposed && !lifetime.IsCancellationRequested
        && owner == (sessionKey?.Invoke() ?? "local");

    public void SetVehicleKind(string kind)
    {
        if (!CanSubmit) return;
        VehicleKind = kind;
        Rows = 운송자격Codes.RequiredFor(kind).Select(code => Rows.FirstOrDefault(row => row.Code == code)
            ?? new 운송자격입력행 { Code = code }).ToList();
        Notify();
    }

    public async Task LoadAsync()
    {
        if (Busy || !IsCurrent) return;
        Busy = true; Error = null; Message = "자격 상태를 확인하고 있습니다."; Notify();
        try
        {
            var value = await client.조회Async(lifetime.Token);
            if (!IsCurrent) return;
            if (value.Revision < minimumRevision) throw new InvalidDataException();
            Apply(value); NeedsResultCheck = false; Message = "최신 자격 상태를 확인했습니다.";
        }
        catch (OperationCanceledException) when (!IsCurrent) { }
        catch
        {
            if (IsCurrent) { Current = null; Error = "자격 상태를 확인하지 못했습니다. 다시 확인해 주세요."; Message = null; }
        }
        finally { if (IsCurrent) { Busy = false; Notify(); } }
    }

    public async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        if (string.IsNullOrWhiteSpace(VehicleProfileValue) || string.IsNullOrWhiteSpace(ActualVehicleReference)
            || VehicleKind == 운송자격Codes.FreightVehicle && string.IsNullOrWhiteSpace(TransportOperatorReference)
            || Rows.Any(row => string.IsNullOrWhiteSpace(row.Reference) || row.Summary.Trim().Length < 3))
        { Error = "차량 정보와 항목별 비공개 증빙 참조코드·3자 이상의 자료 설명을 입력해 주세요."; Notify(); return; }
        var request = new 운송자격제출요청
        {
            ExpectedRevision = Current!.Revision, VehicleKind = VehicleKind,
            VehicleProfileValue = VehicleProfileValue.Trim(), ActualVehicleReference = ActualVehicleReference.Trim(),
            TransportOperatorReference = TransportOperatorReference.Trim(), Evidence = Rows.Select(row => row.ToEvidence()).ToArray()
        };
        await ChangeAsync(ct => client.제출Async(request, ct));
    }

    public async Task ReviewAsync(string decision)
    {
        if (!CanEdit || !administrator || decision == 운송자격Codes.Valid && !CanApprove) return;
        if (ReviewReason.Trim().Length < 10 || decision == 운송자격Codes.Valid
            && (Rows.Count == 0 || Rows.Any(row => !row.Checked || row.Summary.Trim().Length < 3
                || !row.NoStatedExpiry && row.ValidUntilUtc is null)
                || string.IsNullOrWhiteSpace(ActualVehicleReference)))
        { Error = "검토 사유를 10자 이상 입력하고, 유효로 판단하려면 모든 자료·유효기간·실제 차량을 확인해 주세요."; Notify(); return; }
        if (decision == 운송자격Codes.Valid && (!DateTime.TryParse(RecheckDate, out var recheck) || recheck.Date <= DateTime.Today))
        { Error = "미래의 재확인 날짜를 입력해 주세요."; Notify(); return; }
        var request = new 운송자격검토요청
        {
            ExpectedRevision = Current!.Revision, Decision = decision, ReviewReason = ReviewReason.Trim(),
            ActualVehicleReference = ActualVehicleReference.Trim(),
            RecheckAtUtc = DateTime.TryParse(RecheckDate, out var date) ? DateTime.SpecifyKind(date.Date, DateTimeKind.Local).ToUniversalTime() : null,
            Evidence = Rows.Select(row => row.ToEvidence() with
                { VerificationLevel = 운송자격Codes.DocumentReview, ReviewReason = ReviewReason.Trim() }).ToArray()
        };
        await ChangeAsync(ct => client.검토Async(request, ct));
    }

    private async Task ChangeAsync(Func<CancellationToken, Task<운송자격상세Dto>> change)
    {
        Busy = true; NeedsResultCheck = true; Error = null; Message = "요청 결과를 확인하고 있습니다."; Notify();
        try
        {
            var written = await change(lifetime.Token);
            if (!IsCurrent) return;
            minimumRevision = Math.Max(Current!.Revision + 1, written.Revision);
            var latest = await client.조회Async(lifetime.Token);
            if (!IsCurrent) return;
            if (latest.Revision < minimumRevision) throw new InvalidDataException();
            Apply(latest); NeedsResultCheck = false; Message = "변경된 자격 상태를 확인했습니다.";
        }
        catch (OperationCanceledException) when (!IsCurrent) { }
        catch
        {
            if (IsCurrent) { Error = "처리 결과가 아직 확인되지 않았습니다. 최신 상태를 확인한 뒤 다시 진행해 주세요."; Message = null; }
        }
        finally { if (IsCurrent) { Busy = false; Notify(); } }
    }

    private void Apply(운송자격상세Dto value)
    {
        Current = value; minimumRevision = value.Revision;
        VehicleKind = string.IsNullOrWhiteSpace(value.VehicleKind) ? 운송자격Codes.Motorcycle : value.VehicleKind;
        VehicleProfileValue = value.VehicleProfileValue; ActualVehicleReference = value.ActualVehicleReference;
        TransportOperatorReference = value.TransportOperatorReference; ReviewReason = ""; RecheckDate = "";
        var required = value.RequiredEvidenceCodes.Count > 0 ? value.RequiredEvidenceCodes : 운송자격Codes.RequiredFor(VehicleKind);
        Rows = required.Select(code =>
        {
            var evidence = value.Evidence.FirstOrDefault(item => item.RequirementCode == code);
            return new 운송자격입력행 { Code = code, Reference = evidence?.PrivateEvidenceReference ?? "", Summary = evidence?.Summary ?? "",
                ValidFromUtc = evidence?.ValidFromUtc, ValidUntilUtc = evidence?.ValidUntilUtc,
                NoStatedExpiry = evidence?.NoStatedExpiry ?? false };
        }).ToList();
    }

    public void Invalidate()
    { if (disposed) return; Current = null; Rows.Clear(); VehicleProfileValue = ActualVehicleReference = TransportOperatorReference = ReviewReason = RecheckDate = ""; Error = "로그인 상태가 변경됐습니다. 다시 로그인해 주세요."; Busy = false; Notify(); Dispose(); }
    private void Notify() => Changed?.Invoke();
    public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); }
}

public sealed class 운송자격입력행
{
    public string Code { get; init; } = "";
    public string Reference { get; set; } = "";
    public string Summary { get; set; } = "";
    public bool Checked { get; set; }
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public bool NoStatedExpiry { get; set; }
    public bool Insurance => Code is 운송자격Codes.CompulsoryInsurance or 운송자격Codes.DeliveryCoverage;
    public string ValidFromDate { get => ValidFromUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "";
        set => ValidFromUtc = DateTime.TryParse(value, out var date) ? DateTime.SpecifyKind(date.Date, DateTimeKind.Local).ToUniversalTime() : null; }
    public string ValidUntilDate { get => ValidUntilUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "";
        set => ValidUntilUtc = DateTime.TryParse(value, out var date) ? DateTime.SpecifyKind(date.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime() : null; }
    public 운송자격근거Dto ToEvidence() => new() { RequirementCode = Code,
        PrivateEvidenceReference = Reference.Trim(), Summary = Summary.Trim(), ValidFromUtc = ValidFromUtc,
        ValidUntilUtc = NoStatedExpiry && !Insurance ? null : ValidUntilUtc, NoStatedExpiry = NoStatedExpiry && !Insurance };
}

public static class 운송자격Presentation
{
    public static string Status(string code) => code switch
    { "NotSubmitted" => "미제출", "Pending" => "검토 중", "Valid" => "내부 검토 유효", "Rejected" => "보완 필요", "Expired" => "효력 만료", "Suspended" => "이용 정지", _ => "미확인" };
    public static string Requirement(string code) => code switch
    {
        "DriverLicense" => "운전면허", "FreightDriverQualification" => "화물운송 종사자격",
        "TransportOperatorPermit" => "운송사업자 허가", "OperatorRelationship" => "운송사업자 소속·위수탁 관계",
        "VehicleRegistration" => "실제 차량 등록", "VehicleUseRelationship" => "차량 사용 관계",
        "CompulsoryInsurance" => "의무보험", "PaidDeliveryCoverage" => "유상 배송 보장", _ => "추가 검토 자료"
    };
}
