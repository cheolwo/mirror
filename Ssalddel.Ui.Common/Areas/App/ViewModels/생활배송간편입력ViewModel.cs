using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using System.Security.Cryptography;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>한 건의 작성 단계와 메모리 전용 엑셀 미리보기입니다. 견적·동의·접수는 기존 작성 VM을 통합니다.</summary>
public sealed class 생활배송간편입력ViewModel(NeighborhoodDeliveryAuthoringViewModel owner, INeighborhoodDeliveryClient client) : IDisposable
{
    private CancellationTokenSource _lifetime = new();
    private long _generation;
    private bool _disposed;
    private readonly Dictionary<string, Guid> _rowRequestIds = [];
    private string? _previewFileDigest;
    private readonly HashSet<string> _missingTimes = [];
    private bool _dropoffTimeUnspecified;
    public static IReadOnlyList<string> Steps { get; } = ["물품", "장소", "시간", "확인"];
    public static string FieldLabel(string key)
    {
        for (var index = 0; index < NeighborhoodDeliveryInputSchema.FieldKeys.Count; index++)
            if (NeighborhoodDeliveryInputSchema.FieldKeys[index] == key) return NeighborhoodDeliveryInputSchema.Headers[index];
        return "입력 항목";
    }
    public bool IsActive { get; set; }
    public bool ShowDetailed { get; private set; }
    public int Step { get; private set; }
    public bool IsBusy { get; private set; }
    public bool TimeReviewed { get; set; }
    public bool DropoffTimeUnspecified
    {
        get => _dropoffTimeUnspecified;
        set
        {
            if (_disposed || !owner.CanEditInput || IsBusy || _dropoffTimeUnspecified == value) return;
            _dropoffTimeUnspecified = value; TimeReviewed = false; owner.InputChanged();
        }
    }
    public bool HasMissingTimes => _missingTimes.Any(key => key.StartsWith("Pickup", StringComparison.Ordinal) || !DropoffTimeUnspecified);
    internal bool IsTimeMissing(string key) => _missingTimes.Contains(key);
    public string? Error { get; private set; }
    public NeighborhoodDeliveryInputPreviewResponse? Preview { get; private set; }
    public NeighborhoodDeliveryInputRow? SelectedRow { get; private set; }
    public NeighborhoodDeliveryInputTemplateResponse? Template { get; private set; }
    public string? TemplateDownloadHref => Template is { } template
        ? $"data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,{template.ContentBase64}" : null;
    public DateTime? PickupStart { get => TimeValue("PickupStart", owner.PickupStart); set => SetTime("PickupStart", value, x => owner.PickupStart = x); }
    public DateTime? PickupEnd { get => TimeValue("PickupEnd", owner.PickupEnd); set => SetTime("PickupEnd", value, x => owner.PickupEnd = x); }
    public DateTime? DropoffStart { get => DropoffTimeUnspecified ? null : TimeValue("DropoffStart", owner.DropoffStart); set => SetTime("DropoffStart", value, x => owner.DropoffStart = x); }
    public DateTime? DropoffEnd { get => DropoffTimeUnspecified ? null : TimeValue("DropoffEnd", owner.DropoffEnd); set => SetTime("DropoffEnd", value, x => owner.DropoffEnd = x); }
    public void ToggleDetailed()
    {
        if (_disposed || owner.IsSending || owner.NeedsSubmissionReview || IsBusy) return;
        ShowDetailed = !ShowDetailed; Error = null; owner.SimpleInputChanged();
    }
    public void Previous()
    {
        if (_disposed || owner.IsSending || owner.NeedsSubmissionReview || IsBusy) return;
        Step = Math.Max(0, Step - 1); Error = null; owner.SimpleInputChanged();
    }
    public bool Next()
    {
        owner.SynchronizeOwner();
        if (_disposed || !owner.CanEditInput || IsBusy) return false;
        var missing = MissingForStep(Step);
        if (missing.Count > 0) { Error = string.Join(" · ", missing); owner.SimpleInputChanged(); return false; }
        Step = Math.Min(Steps.Count - 1, Step + 1); Error = null; owner.SimpleInputChanged(); return true;
    }
    public IReadOnlyList<string> MissingForStep(int step)
    {
        var input = owner.Draft; var missing = new List<string>();
        if (step is 0 or 3)
        {
            if (string.IsNullOrWhiteSpace(input.CargoName) || input.CargoName.Length > 120) missing.Add("물건 이름을 입력해 주세요");
            if (input.Quantity is < 1 or > 1000) missing.Add("수량을 확인해 주세요");
            if (input.WeightKg is <= 0 or > 10000) missing.Add("전체 중량을 입력해 주세요");
            if (string.IsNullOrWhiteSpace(input.VehicleType) || input.VehicleType.Length > 60) missing.Add("배송 차량을 선택해 주세요");
            if (input.PackageSizeCode is not null && !NeighborhoodPackageSizes.IsKnown(input.PackageSizeCode)) missing.Add("포장 크기를 다시 선택해 주세요");
            if (input.Notes?.Length > 1000) missing.Add("요청 사항은 1,000자 이내로 입력해 주세요");
            missing.AddRange(NeighborhoodDeliveryLoadingInput.Calculate(input).오류);
        }
        if (step is 1 or 3)
        {
            foreach (var (title, location) in new[] { ("픽업지", input.Pickup), ("전달지", input.Dropoff) })
            {
                if (string.IsNullOrWhiteSpace(location.주소.도로명주소) || location.주소.도로명주소.Length > 300) missing.Add($"{title} 도로명 주소를 입력해 주세요");
                if (location.주소.상세주소?.Length > 300) missing.Add($"{title} 상세주소를 확인해 주세요");
                if (string.IsNullOrWhiteSpace(location.연락처.이름) || location.연락처.이름.Length > 100) missing.Add($"{title} 담당자 이름을 입력해 주세요");
                if (string.IsNullOrWhiteSpace(location.연락처.전화번호) || location.연락처.전화번호.Length > 40) missing.Add($"{title} 전화번호를 입력해 주세요");
            }
        }
        if (step is 2 or 3)
        {
            if (input.Pickup.작업시간분 < 0 || input.Dropoff.작업시간분 < 0) missing.Add("작업시간은 0분 이상으로 입력해 주세요");
            if (HasMissingTimes) missing.Add("비어 있는 픽업·전달 시각을 입력해 주세요");
            if (owner.PickupEnd <= owner.PickupStart || !DropoffTimeUnspecified && (owner.DropoffEnd <= owner.DropoffStart || owner.DropoffEnd < owner.PickupStart)) missing.Add("시작·종료 시각을 확인해 주세요");
            if (!TimeReviewed) missing.Add("표시된 시간창을 확인해 주세요");
        }
        return missing;
    }
    public async Task ImportAsync(string fileName, byte[] content)
    {
        owner.SynchronizeOwner();
        if (_disposed || !owner.CanEditInput || IsBusy) return;
        Error = null;
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || content.Length is 0 or > NeighborhoodDeliveryInputSchema.MaximumFileBytes)
        { ReportError("2 MiB 이하의 .xlsx 양식 파일을 선택해 주세요."); return; }
        var generation = ++_generation; var consent = owner.ConsentEvidenceId; var requestId = owner.Draft.ClientRequestId;
        var fileDigest = Convert.ToHexString(SHA256.HashData(content));
        IsBusy = true; Preview = null; SelectedRow = null; _previewFileDigest = null; owner.SimpleInputChanged();
        try
        {
            var response = await client.PreviewInputAsync(new()
            {
                FileName = fileName, ContentBase64 = Convert.ToBase64String(content), PrivacyConsentEvidenceId = consent
            }, _lifetime.Token);
            if (!Current(generation) || !owner.CanEditInput || consent != owner.ConsentEvidenceId || requestId != owner.Draft.ClientRequestId) return;
            if (response is null || response.SchemaVersion != NeighborhoodDeliveryInputSchema.Version || response.Rows.Count > NeighborhoodDeliveryInputSchema.MaximumRows)
            { Error = "엑셀 미리보기를 확인하지 못했습니다. 양식을 다시 확인해 주세요."; return; }
            Preview = response; _previewFileDigest = fileDigest;
            if (response.Errors.Count > 0) Error = "파일을 확인해 주세요. 아래 보완 항목이 있으며 자동으로 접수하지 않았습니다.";
        }
        catch (OperationCanceledException) { }
        catch { if (Current(generation)) Error = "엑셀 미리보기를 불러오지 못했습니다. 다시 시도해 주세요."; }
        finally { if (Current(generation)) { IsBusy = false; owner.SimpleInputChanged(); } }
    }
    public bool CanApplyRow(NeighborhoodDeliveryInputRow row) => !_disposed && !IsBusy && owner.CanEditInput
        && Preview is { Errors.Count: 0 } && Preview.Rows.Any(value => ReferenceEquals(value, row))
        && row.SourceRowId != Guid.Empty && !row.Issues.Any(issue => issue.FieldKey == "SourceRowId" && issue.Code is "InvalidRowId" or "DuplicateRowId");
    public bool ApplyRow(NeighborhoodDeliveryInputRow row)
    {
        owner.SynchronizeOwner();
        if (!CanApplyRow(row)) return false;
        var rowKey = $"{_previewFileDigest}:{row.SourceSheet}:{row.RowNumber}";
        var requestId = row.SourceRowId ?? Guid.Empty;
        if (requestId == Guid.Empty && !_rowRequestIds.TryGetValue(rowKey, out requestId))
        {
            if (_rowRequestIds.Count >= 1000) { ReportError("이 작성 화면에서 검토한 행이 많습니다. 행 ID를 보존한 양식으로 확인하거나 화면을 다시 열어 주세요."); return false; }
            requestId = Guid.NewGuid(); _rowRequestIds[rowKey] = requestId;
        }
        if (!owner.ApplyInputRow(row, requestId)) return false;
        _missingTimes.Clear();
        if (row.Draft.Pickup.시간창 is null) { _missingTimes.Add("PickupStart"); _missingTimes.Add("PickupEnd"); }
        if (row.Draft.Dropoff.시간창 is null) { _missingTimes.Add("DropoffStart"); _missingTimes.Add("DropoffEnd"); }
        // A blank optional window stays omitted; a partially entered/rejected window needs repair or explicit omission.
        _dropoffTimeUnspecified = row.Draft.Dropoff.시간창 is null
            && !new[] { "DropoffStart", "DropoffEnd" }.Any(key => row.RawValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            && !row.Issues.Any(issue => issue.FieldKey is "DropoffStart" or "DropoffEnd");
        SelectedRow = row; Step = 0; ShowDetailed = false; TimeReviewed = false; Error = null; owner.SimpleInputChanged(); return true;
    }
    public async Task LoadTemplateAsync()
    {
        owner.SynchronizeOwner();
        if (_disposed || !owner.IsAuthenticated || owner.IsSending || owner.NeedsSubmissionReview || IsBusy) return;
        var generation = ++_generation; IsBusy = true; Error = null; owner.SimpleInputChanged();
        try
        {
            var value = await client.GetInputTemplateAsync(_lifetime.Token);
            if (!Current(generation) || !owner.IsAuthenticated) return;
            if (value is null || value.SchemaVersion != NeighborhoodDeliveryInputSchema.Version || value.ContentType != "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                || !value.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || value.ContentBase64.Length > 4 * NeighborhoodDeliveryInputSchema.MaximumFileBytes / 3 + 4
                || Convert.FromBase64String(value.ContentBase64).Length is 0 or > NeighborhoodDeliveryInputSchema.MaximumFileBytes)
            { Error = "양식 다운로드를 확인하지 못했습니다."; return; }
            Template = value;
        }
        catch (OperationCanceledException) { }
        catch { if (Current(generation)) Error = "양식을 불러오지 못했습니다. 다시 시도해 주세요."; }
        finally { if (Current(generation)) { IsBusy = false; owner.SimpleInputChanged(); } }
    }
    public void ReportError(string message) { if (!_disposed) { Error = message; owner.SimpleInputChanged(); } }
    internal void TimeChanged(string key) { _missingTimes.Remove(key); if (key.StartsWith("Dropoff", StringComparison.Ordinal)) _dropoffTimeUnspecified = false; TimeReviewed = false; }
    private DateTime? TimeValue(string key, DateTime value) => _missingTimes.Contains(key) ? null : value;
    private void SetTime(string key, DateTime? value, Action<DateTime> assign)
    {
        if (!owner.CanEditInput || IsBusy) return;
        if (value.HasValue) { assign(value.Value); _missingTimes.Remove(key); } else _missingTimes.Add(key);
        TimeReviewed = false; owner.InputChanged();
    }
    private bool Current(long generation)
    {
        owner.SynchronizeOwner();
        return !_disposed && generation == _generation;
    }
    public void Clear()
    {
        ++_generation; _lifetime.Cancel(); _lifetime.Dispose(); if (!_disposed) _lifetime = new();
        IsBusy = false; Step = 0; ShowDetailed = false; TimeReviewed = false; _dropoffTimeUnspecified = false; Error = null;
        Preview = null; SelectedRow = null; Template = null; _previewFileDigest = null; _rowRequestIds.Clear(); _missingTimes.Clear();
    }
    internal void Completed()
    {
        Preview = null; SelectedRow = null; Error = null; Step = 0; TimeReviewed = false; _dropoffTimeUnspecified = false; _missingTimes.Clear(); _previewFileDigest = null;
    }
    public void Dispose() { if (_disposed) return; _disposed = true; Clear(); }
}
