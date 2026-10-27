using System.Globalization;
using System.Net;
using System.Text.Json;
using Ssalddel.Contracts.Admin.Management;

namespace SsalddelAdmin.Services;

/// <summary>선택 기사 한 명의 실측 기록과 CAS·정본 재조회를 소유한다. 차량 선택이나 자격을 변경하지 않는다.</summary>
public sealed class 기사실차적재확인ViewModel : IDisposable
{
    private readonly I기사실차적재확인Client client;
    private readonly Func<string> sessionKey;
    private readonly Func<bool> canManage;
    private readonly Func<DateTimeOffset> now;
    private readonly string owner;
    private readonly CancellationTokenSource lifetime = new();
    private 기사실차적재제원응답? current;
    private 기사실차적재제원? attempted;
    private long minimumRevision;
    private bool disposed;
    public 기사실차적재확인ViewModel(I기사실차적재확인Client client, string driverId,
        Func<string>? sessionKey = null, Func<bool>? canManage = null, Func<DateTimeOffset>? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        this.client = client; DriverId = driverId;
        this.sessionKey = sessionKey ?? (() => "local"); this.canManage = canManage ?? (() => true);
        this.now = now ?? (() => DateTimeOffset.UtcNow); owner = this.sessionKey();
    }
    public event Action? Changed;
    public string DriverId { get; }
    public 기사실차적재제원응답? Current => IsCurrent ? current : null;
    public 실차적재입력Draft Draft { get; private set; } = new();
    public bool Busy { get; private set; }
    public bool NeedsResultCheck { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    private bool IsCurrent => !disposed && canManage() && owner == sessionKey();
    public bool SessionCurrent => IsCurrent;
    public string StoredState
    {
        get
        {
            if (Current is not { 제원: { } value } response) return "미등록";
            return value.ActualVehicleReference != response.현재실차참조 || value.VehicleProfileValue != response.현재차량종류 ? "이전 차량 기록"
                : value.Withdrawn ? "철회됨" : value.ValidUntilUtc <= now().UtcDateTime ? "유효기간 만료"
                : value.ConfirmedAtUtc > now().UtcDateTime ? "확인 시각 검토 필요" : "실측 기록 보관 중";
        }
    }
    public bool CurrentVehicleSelected => Current is { } c
        && !string.IsNullOrWhiteSpace(c.현재실차참조) && !string.IsNullOrWhiteSpace(c.현재차량종류);
    public bool CanEdit => IsCurrent && Current is not null && CurrentVehicleSelected && !Busy;
    public bool CanSave => CanEdit && !NeedsResultCheck && ValidationError is null;
    public string? ValidationError
    {
        get
        {
            if (!CurrentVehicleSelected) return "기사 본인이 선택한 현재 차량 정보를 먼저 확인해 주세요.";
            if (string.IsNullOrWhiteSpace(Draft.EvidenceReference)) return "실측 자료의 비공개 참조코드를 입력해 주세요.";
            if (!TryUtc(Draft.ConfirmedLocal, out var confirmed) || !TryUtc(Draft.ValidUntilLocal, out var validUntil)
                || confirmed > now().UtcDateTime || validUntil <= now().UtcDateTime || validUntil <= confirmed)
                return "확인 시각과 미래의 유효 종료 시각을 확인해 주세요.";
            if (new[] { Draft.LengthMm, Draft.WidthMm, Draft.HeightMm, Draft.MaxKg }.Any(x => x is <= 0)
                || Draft.MaxCbm is <= 0 || Draft.Pallets is < 0)
                return "실측 크기·중량·부피는 양수, 팔레트 수는 0 이상으로 입력해 주세요. 미확인은 비워 둡니다.";
            if ((Draft.Refrigerated == true || Draft.Frozen == true || Draft.Lift == true)
                && string.IsNullOrWhiteSpace(Draft.EquipmentReferences)) return "확인한 장비의 비공개 참조코드를 입력해 주세요.";
            return null;
        }
    }

    public async Task LoadAsync()
    {
        if (!IsCurrent || Busy) return;
        Busy = true; Error = null; Message = null; Notify();
        var preserve = NeedsResultCheck;
        try
        {
            var next = await client.실차조회Async(DriverId, lifetime.Token);
            if (!IsCurrent) return;
            ValidateResponse(next, minimumRevision);
            var vehicleChanged = current is not null && (current.현재실차참조 != next.현재실차참조 || current.현재차량종류 != next.현재차량종류);
            current = next; minimumRevision = Math.Max(minimumRevision, next.판본 ?? 0);
            if (vehicleChanged)
            {
                Draft = ReadDraft(next); attempted = null;
                Message = "기사의 현재 선택 차량이 바뀌었습니다. 이전 입력을 새 차량에 적용하지 않고 다시 확인합니다.";
            }
            else if (preserve && attempted is not null && Equivalent(next.제원, attempted))
            {
                Draft = ReadDraft(next); attempted = null;
                Message = "현재 저장된 자료가 작성한 내용과 일치함을 확인했습니다.";
            }
            else if (preserve) Message = "최신 기록을 확인했습니다. 작성값은 유지됩니다. 저장된 자료와 비교한 뒤 다시 저장해 주세요.";
            else { Draft = ReadDraft(next); Message = "현재 실차 선택과 저장된 실측 기록을 확인했습니다."; }
            NeedsResultCheck = false;
        }
        catch (OperationCanceledException) when (!IsCurrent) { }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { if (IsCurrent) RejectAccess(); }
        catch { if (IsCurrent) Error = "실차 기록을 확인하지 못했습니다. 입력은 유지되며 최신 기록을 다시 확인할 수 있습니다."; }
        finally { if (IsCurrent) { Busy = false; Notify(); } }
    }

    public async Task SaveAsync()
    {
        if (!CanSave || Current is not { } before) return;
        attempted = Draft.ToRecord(before.현재실차참조!, before.현재차량종류!);
        var command = new 기사실차적재제원수정요청(before.판본, attempted);
        Busy = true; NeedsResultCheck = true; Error = null; Message = null; Notify();
        try
        {
            var written = await client.실차수정Async(DriverId, command, lifetime.Token);
            if (!IsCurrent) return;
            ValidateResponse(written, checked((before.판본 ?? 0) + 1));
            minimumRevision = Math.Max(minimumRevision, written.판본 ?? 0);
            var latest = await client.실차조회Async(DriverId, lifetime.Token);
            if (!IsCurrent) return;
            ValidateResponse(latest, minimumRevision);
            if (latest.현재실차참조 != before.현재실차참조 || latest.현재차량종류 != before.현재차량종류
                || !Equivalent(latest.제원, attempted)) throw new InvalidDataException();
            current = latest; minimumRevision = Math.Max(minimumRevision, latest.판본 ?? 0);
            Draft = ReadDraft(latest); attempted = null; NeedsResultCheck = false;
            Message = "저장된 실측 자료를 최신 기록에서 확인했습니다. 운송 자격과 배차 가능 여부는 별도로 판단합니다.";
        }
        catch (OperationCanceledException) when (!IsCurrent) { }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { if (IsCurrent) RejectAccess(); }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Conflict)
        { if (IsCurrent) Error = "다른 변경이 먼저 저장됐습니다. 작성값을 유지한 채 최신 기록을 확인해 주세요."; }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        { if (IsCurrent) Error = "자료 참조·실측값·기간을 다시 확인해 주세요. 작성값은 유지됩니다."; }
        catch { if (IsCurrent) Error = "저장 결과를 확인하지 못했습니다. 다시 저장하지 말고 최신 기록부터 확인해 주세요."; }
        finally { if (IsCurrent) { Busy = false; Notify(); } }
    }

    private void ValidateResponse(기사실차적재제원응답 response, long min)
    {
        if (response.기사Id != DriverId || (response.판본 ?? 0) < min) throw new InvalidDataException();
    }
    private void RejectAccess()
    {
        current = null; Draft = new(); attempted = null; NeedsResultCheck = false;
        Error = "관리자 권한을 확인하지 못했습니다. 다시 로그인한 뒤 기록을 확인해 주세요.";
    }
    private static 실차적재입력Draft ReadDraft(기사실차적재제원응답 response)
        => response.제원 is { } value && value.ActualVehicleReference == response.현재실차참조
            && value.VehicleProfileValue == response.현재차량종류 ? 실차적재입력Draft.From(value) : new();
    private static bool Equivalent(기사실차적재제원? a, 기사실차적재제원? b)
        => a is not null && b is not null && JsonSerializer.Serialize(a with { ReviewerId = "" }) == JsonSerializer.Serialize(b with { ReviewerId = "" });
    internal static bool TryUtc(string text, out DateTime value)
    {
        if (DateTime.TryParseExact(text, ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        { value = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime; return true; }
        value = default; return false;
    }
    private void Notify() => Changed?.Invoke();
    public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); current = null; Draft = new(); attempted = null; }
}

public sealed class 실차적재입력Draft
{
    public string EvidenceReference { get; set; } = "";
    public string EquipmentReferences { get; set; } = "";
    public string ConfirmedLocal { get; set; } = "";
    public string ValidUntilLocal { get; set; } = "";
    public int? LengthMm { get; set; }
    public int? WidthMm { get; set; }
    public int? HeightMm { get; set; }
    public int? MaxKg { get; set; }
    public decimal? MaxCbm { get; set; }
    public int? Pallets { get; set; }
    public string EntranceKind { get; set; } = "Unknown";
    public bool? RainProtected { get; set; }
    public bool? Refrigerated { get; set; }
    public bool? Frozen { get; set; }
    public bool? SideLoading { get; set; }
    public bool? Lift { get; set; }
    public bool? LongCargo { get; set; }
    public bool Withdrawn { get; set; }
    internal 기사실차적재제원 ToRecord(string vehicle, string profile)
    {
        if (!기사실차적재확인ViewModel.TryUtc(ConfirmedLocal, out var confirmed) || !기사실차적재확인ViewModel.TryUtc(ValidUntilLocal, out var until))
            throw new InvalidOperationException();
        return new()
        {
            ActualVehicleReference = vehicle, VehicleProfileValue = profile, PrivateEvidenceReference = EvidenceReference.Trim(),
            EquipmentReferences = EquipmentReferences.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray(),
            ConfirmedAtUtc = confirmed, ValidUntilUtc = until, 적재함길이Mm = LengthMm, 적재함폭Mm = WidthMm,
            적재함높이Mm = HeightMm, 최대적재중량Kg = MaxKg, 최대부피Cbm = MaxCbm, 팔레트적재개수 = Pallets,
            EntranceKind = EntranceKind, 비눈보호가능 = RainProtected, 냉장가능 = Refrigerated, 냉동가능 = Frozen,
            측면상하차가능 = SideLoading, 리프트가능 = Lift, 장재물유리 = LongCargo, Withdrawn = Withdrawn
        };
    }
    public static 실차적재입력Draft From(기사실차적재제원 value) => new()
    {
        EvidenceReference = value.PrivateEvidenceReference, EquipmentReferences = string.Join('\n', value.EquipmentReferences ?? []),
        ConfirmedLocal = KoreanTime(value.ConfirmedAtUtc), ValidUntilLocal = KoreanTime(value.ValidUntilUtc),
        LengthMm = value.적재함길이Mm, WidthMm = value.적재함폭Mm, HeightMm = value.적재함높이Mm,
        MaxKg = value.최대적재중량Kg, MaxCbm = value.최대부피Cbm, Pallets = value.팔레트적재개수,
        EntranceKind = value.EntranceKind, RainProtected = value.비눈보호가능, Refrigerated = value.냉장가능,
        Frozen = value.냉동가능, SideLoading = value.측면상하차가능, Lift = value.리프트가능, LongCargo = value.장재물유리, Withdrawn = value.Withdrawn
    };
    private static string KoreanTime(DateTime value) => value == default ? "" : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
}
