using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public sealed record CargoEvidenceUploadResponse(string ObjectName, string? Url);

/// <summary>현장 확인과 실제 사진 업로드를 완료 입력의 독립 수명으로 관리합니다.</summary>
public sealed class CargoTransportActionViewModel : IDisposable
{
    public const long MaxPhotoBytes = 8 * 1024 * 1024;
    private readonly CargoDriverWorkspaceApiClient _client;
    private readonly IRoleWorkspaceApi _api;
    private readonly I업무사진촬영Adapter? _capture;
    private readonly CargoInputRecovery recovery;
    private IBrowserFile? _photo;
    private CargoEvidenceUploadResponse? _uploaded;
    private bool _captureNeedsReview;
    public CargoTransportActionViewModel(CargoDriverWorkspaceApiClient client, IRoleWorkspaceApi api, IRoleWorkspaceAccess access,
        I업무사진촬영Adapter? capture = null, IWorkflowRecoveryStore? recoveryStore = null,
        IWorkflowRecoveryContext? recoveryContext = null)
    {
        _client = client; _api = api; _capture = capture;
        recovery = new(access, recoveryStore, recoveryContext);
        Lifetime = new(access, RoleWorkspaceCatalog.CargoDriver, Clear);
    }
    public CargoInputLifetime Lifetime { get; }
    public long ItemId { get; private set; }
    public string ActionKey { get; private set; } = "";
    public 기사운송상세응답? Source { get; private set; }
    public bool Saved { get; private set; }
    public bool AwaitingResult => recovery.Pending is not null;
    public string? PhotoName => _photo?.Name;
    public string? PhotoCaptureMessage { get; private set; }
    public bool HasCameraSupport => _capture?.IsSupported == true;
    public bool CanCapturePhoto => HasCameraSupport && StageAllowsInput && !AwaitingResult && !Lifetime.IsBusy && !Lifetime.HasError && !Saved;
    public bool CargoConfirmed { get; set; }
    public bool LocationConfirmed { get; set; }
    public bool RecipientConfirmed { get; set; }
    public bool PaymentConfirmed { get; set; }
    public bool SaveConfirmed { get; set; }
    public bool DocumentPhotoConfirmed { get; set; }
    public bool ReceiptOmissionConfirmed { get; set; }
    public string ReceiptOmissionReason { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string RecipientSignature { get; set; } = "";
    public string DriverSignature { get; set; } = "";
    public string IssueReason { get; set; } = "";
    public string IssueMemo { get; set; } = "";
    public bool WorkCannotContinue { get; set; }
    public string ReturnHref => CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.CargoDriver, ItemId > 0 ? CargoWorkspaceRoutes.Number(ItemId) : null);
    public string Title => ActionKey switch { "pickup" => "상차 확인", "dropoff" => "하차 확인", "issue" => "운송 문제 신고", _ => "운송 업무" };
    public bool StageAllowsInput => Source is { 개인정보제공보류: false } && (ActionKey switch
    {
        "pickup" => Source.상태 == "상차지도착" && CargoWorkspacePresentation.CanPerform(Source, "pickup"),
        "dropoff" => Source.상태 == "하차지도착" && CargoWorkspacePresentation.CanPerform(Source, "dropoff"),
        "issue" => CargoWorkspacePresentation.CanPerform(Source, "issue"),
        _ => false
    });
    public bool CanSubmit => recovery.CanSubmit && StageAllowsInput && !Lifetime.IsBusy && !Lifetime.HasError && !_captureNeedsReview && !Saved && SaveConfirmed && (ActionKey == "issue"
        ? !string.IsNullOrWhiteSpace(IssueReason)
        : _photo is not null && CargoConfirmed && LocationConfirmed && RecipientConfirmed
          && (ActionKey == "pickup" ? ReceiptReady : PaymentConfirmed));
    public bool CompletionConfirmed => !Lifetime.IsBusy && !Lifetime.HasError && Source is { 개인정보제공보류: false }
        && (ActionKey switch
        {
            "pickup" => Source.상태 is "상차완료" or "운송중" or "하차지도착" or "인수완료",
            "dropoff" => Source.상태 == "인수완료",
            "issue" => Saved,
            _ => false
        });
    public string CompletionMessage => ActionKey switch
    {
        "pickup" => "현재 운송에서 상차 완료 상태를 확인했습니다.",
        "dropoff" => "현재 운송에서 하차·인수 완료 상태를 확인했습니다.",
        _ => "저장된 문제 신고 상태를 확인했습니다."
    };
    public string CompletionNextStep => Source?.운송진행보류 == true
        ? "운송이 보류되어 있습니다. 지도에서 예외 처리 안내와 다음 담당자를 확인해 주세요."
        : Source?.상태 == "인수완료"
            ? "지도에서 다음 업무를 확인해 주세요. 정산과 입금 상태는 별도로 확인합니다."
            : ActionKey == "pickup" ? "지도에서 하차 장소와 다음 업무를 확인해 주세요."
                : "지도에서 현재 업무와 처리 안내를 확인해 주세요.";
    public string CompletionHref => Source?.상태 == "인수완료"
        ? CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.CargoDriver, null) : ReturnHref;
    public string? SubmissionReadinessMessage
    {
        get
        {
            if (Lifetime.RequiresLogin) return "로그인한 뒤 이 업무를 다시 확인해 주세요.";
            if (Lifetime.IsBusy) return "현재 업무를 처리하고 있습니다.";
            if (AwaitingResult) return "이전 처리 결과를 먼저 확인해 주세요. 다시 저장하지 말고 현재 상태 다시 확인을 눌러 주세요.";
            if (Lifetime.HasError) return "현재 상태 다시 확인을 눌러 처리 결과를 먼저 확인해 주세요.";
            if (Source is null) return "현재 운송 정보를 먼저 확인해 주세요.";
            if (CompletionConfirmed) return null;
            if (!StageAllowsInput) return "지도에서 현재 운송 단계와 처리 안내를 확인해 주세요.";
            if (ActionKey == "issue")
            {
                if (string.IsNullOrWhiteSpace(IssueReason)) return "문제 사유를 입력해 주세요.";
            }
            else
            {
                if (!CargoConfirmed) return "화물과 수량을 확인해 주세요.";
                if (!LocationConfirmed) return "작업 장소를 확인해 주세요.";
                if (!RecipientConfirmed) return "인계 담당자를 확인해 주세요.";
                if (_captureNeedsReview) return PhotoCaptureMessage ?? "사진 촬영을 마치거나 현장 사진을 다시 선택해 주세요.";
                if (_photo is null) return "현장 사진을 선택해 주세요.";
                if (ActionKey == "pickup" && !ReceiptReady)
                    return Source.인수증서명필수
                        ? "서명된 인수증 사진을 확인하거나 인수자명·인수자 서명·기사 서명을 모두 입력해 주세요."
                        : "인수증 사진·직접 서명·서명 생략 확인과 사유 중 한 가지를 완료해 주세요.";
                if (ActionKey == "dropoff" && !PaymentConfirmed) return "인수와 결제·증빙 조건을 확인해 주세요.";
            }
            return !SaveConfirmed ? "확인한 내용으로 저장하겠습니다 항목을 선택해 주세요." : null;
        }
    }
    private bool ReceiptReady => Source?.인수증필요 != true || DocumentPhotoConfirmed
        || (!string.IsNullOrWhiteSpace(RecipientName) && !string.IsNullOrWhiteSpace(RecipientSignature) && !string.IsNullOrWhiteSpace(DriverSignature))
        || (Source.인수증서명필수 != true && ReceiptOmissionConfirmed && !string.IsNullOrWhiteSpace(ReceiptOmissionReason));

    public async Task LoadAsync(long id, string action)
    {
        if (ItemId != id || ActionKey != action) { Lifetime.Reset(); ItemId = id; ActionKey = action; recovery.Reset(CargoWorkspaceRoutes.Number(id), action); }
        await Lifetime.RunAsync(async ct =>
        {
            _ = CargoWorkspaceRoutes.DriverInput(id, action);
            await recovery.LoadAsync(ct);
            var source = await _client.TransportAsync(id, ct);
            RequireCurrent(ct); CargoWorkspacePresentation.RequireTransport(source, id);
            if (source.개인정보제공보류) throw new RoleWorkspaceAccessException(403, "운송 정보 제공 조건을 확인해 주세요.");
            Source = source;
            if (AwaitingResult)
            {
                if (!IsCanonicalCompletion(source))
                    throw new InvalidOperationException("이전 처리 결과를 아직 확인하지 못했습니다. 다시 저장하지 말고 현재 상태를 확인해 주세요.");
                await recovery.ConfirmAsync(ct);
                RequireCurrent(ct);
            }
        });
    }
    public void SetPhoto(IBrowserFile file)
    {
        if (Lifetime.IsBusy || AwaitingResult) return;
        ApplyPhoto(file);
    }
    private void ApplyPhoto(IBrowserFile file)
    {
        if (file.Size <= 0 || file.Size > MaxPhotoBytes || file.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new ArgumentException("8MB 이하의 JPG·PNG·WebP 사진을 선택해 주세요.", nameof(file));
        _photo = file; _uploaded = null; DocumentPhotoConfirmed = false;
        _captureNeedsReview = false; PhotoCaptureMessage = null; Lifetime.Notify();
    }
    public async Task CapturePhotoAsync()
    {
        if (!CanCapturePhoto || ActionKey is not ("pickup" or "dropoff")) return;
        // 이전 파일은 보존하되 취소/실패한 재촬영을 이전 사진으로 조용히 완료하지 않는다.
        _captureNeedsReview = true; PhotoCaptureMessage = null;
        await Lifetime.RunAsync(async ct =>
        {
            try
            {
                var photo = await _capture!.CaptureAsync(Title + " 사진", ct);
                RequireCurrent(ct);
                if (photo is null)
                {
                    PhotoCaptureMessage = "촬영을 취소했습니다. 다시 촬영하거나 현장 사진을 선택해 주세요.";
                    return;
                }
                ApplyPhoto(new CapturedPhotoFile(photo));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                PhotoCaptureMessage = "촬영을 취소했습니다. 다시 촬영하거나 현장 사진을 선택해 주세요.";
            }
            catch (Exception)
            {
                RequireCurrent(ct);
                PhotoCaptureMessage = "사진 촬영을 마치지 못했습니다. 카메라 권한을 확인하거나 현장 사진을 선택해 주세요.";
            }
        });
    }
    public async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        await Lifetime.RunAsync(async ct =>
        {
            var latest = await _client.TransportAsync(ItemId, ct);
            RequireCurrent(ct); CargoWorkspacePresentation.RequireTransport(latest, ItemId);
            if (latest.개인정보제공보류) throw new RoleWorkspaceAccessException(403, "운송 정보 제공 조건을 확인해 주세요.");
            Source = latest;
            if (!StageAllowsInput) throw new InvalidOperationException("운송 상태가 바뀌었습니다. 현재 단계를 확인해 주세요.");
            if (ActionKey == "issue")
            {
                await recovery.BeginAsync(ct);
                RequireCurrent(ct);
                var result = await _client.IssueAsync(ItemId, new 기사운송문제신고요청
                {
                    단계 = CargoWorkspacePresentation.CanArrivePickup(latest.상태) || latest.상태 == "상차지도착" ? "상차" : "하차",
                    사유 = IssueReason.Trim(), 메모 = IssueMemo.Trim(), 관리자확인요청 = true, 현장진행불가 = WorkCannotContinue
                }, ct);
                RequireCurrent(ct); CargoWorkspacePresentation.RequireTransport(result, ItemId);
                RequireIssueRecorded(result!);
            }
            else
            {
                if (!ReceiptReady && ActionKey == "pickup") throw new InvalidOperationException("인수증 조건을 다시 확인해 주세요.");
                _uploaded ??= await UploadAsync(ct);
                RequireCurrent(ct);
                await recovery.BeginAsync(ct);
                RequireCurrent(ct);
                var result = ActionKey == "pickup"
                    ? await _client.PickupAsync(ItemId, PickupPayload(_uploaded), ct)
                    : await _client.DropoffAsync(ItemId, new 기사운송하차완료요청 { 하차사진ObjectName = _uploaded.ObjectName, 하차사진Url = _uploaded.Url }, ct);
                RequireCurrent(ct);
                if (result?.Id != ItemId) throw new InvalidOperationException("완료 처리 응답을 확인하지 못했습니다. 운송 상태를 다시 조회해 주세요.");
                if (result.상태 != (ActionKey == "pickup" ? "상차완료" : "인수완료"))
                    throw new InvalidOperationException("완료 응답의 운송 단계가 일치하지 않습니다. 현재 상태를 다시 확인해 주세요.");
            }
            var after = await _client.TransportAsync(ItemId, ct);
            RequireCurrent(ct); CargoWorkspacePresentation.RequireTransport(after, ItemId);
            if (after.개인정보제공보류) throw new RoleWorkspaceAccessException(403, "운송 정보 제공 조건을 확인해 주세요.");
            Source = after;
            if (ActionKey == "issue") RequireIssueRecorded(after);
            else if (!(ActionKey == "pickup"
                ? after.상태 is "상차완료" or "운송중" or "하차지도착" or "인수완료"
                : after.상태 == "인수완료"))
                throw new InvalidOperationException("저장된 완료 상태를 확인하지 못했습니다. 현재 운송과 예외 상태를 다시 확인해 주세요.");
            Saved = true;
            await recovery.ConfirmAsync(ct);
            RequireCurrent(ct);
            _photo = null; _uploaded = null;
        }, "저장된 운송 상태를 확인했습니다. 지도로 돌아가 다음 업무를 확인해 주세요.",
            ActionKey == "issue"
                ? "신고 처리 결과를 확인하지 못했습니다. 다시 신고하기 전에 현재 상태 다시 확인을 눌러 주세요."
                : "사진 또는 저장 처리 결과를 확인하지 못했습니다. 다시 저장하기 전에 현재 상태 다시 확인을 눌러 주세요.");
    }
    private bool IsCanonicalCompletion(기사운송상세응답 source) => ActionKey switch
    {
        "pickup" => source.상태 is "상차완료" or "운송중" or "하차지도착" or "인수완료",
        "dropoff" => source.상태 == "인수완료",
        _ => false // 기존 신고 존재만으로 이번 신고의 처리 결과를 확정하지 않습니다.
    };
    private void RequireCurrent(CancellationToken ct) { Lifetime.RequireCurrent(ct); recovery.RequireCurrent(ct); }
    private void RequireIssueRecorded(기사운송요약응답 source)
    {
        if (source.개인정보제공보류) throw new RoleWorkspaceAccessException(403, "운송 정보 제공 조건을 확인해 주세요.");
        if (!source.예외신고됨 || !source.관리자확인필요 || source.최근예외메시지 != IssueReason.Trim())
            throw new InvalidOperationException("문제 신고와 관리자 확인 상태가 반영되었는지 확인하지 못했습니다.");
    }
    private 기사운송상차완료요청 PickupPayload(CargoEvidenceUploadResponse evidence)
    {
        var signed = !DocumentPhotoConfirmed && !string.IsNullOrWhiteSpace(RecipientName) && !string.IsNullOrWhiteSpace(RecipientSignature) && !string.IsNullOrWhiteSpace(DriverSignature);
        return new()
        {
            상차사진ObjectName = evidence.ObjectName, 상차사진Url = evidence.Url,
            인수증증빙방식 = DocumentPhotoConfirmed ? "문서사진" : "직접서명",
            인수증확인완료 = DocumentPhotoConfirmed || signed,
            인수자명 = signed ? RecipientName.Trim() : null, 인수자서명 = signed ? RecipientSignature.Trim() : null,
            기사서명 = signed ? DriverSignature.Trim() : null,
            인수증서명생략확인 = !DocumentPhotoConfirmed && !signed && ReceiptOmissionConfirmed,
            인수증서명생략사유 = ReceiptOmissionReason.Trim()
        };
    }
    private async Task<CargoEvidenceUploadResponse> UploadAsync(CancellationToken ct)
    {
        var file = _photo ?? throw new InvalidOperationException("현장 사진이 필요합니다.");
        await using var stream = file.OpenReadStream(MaxPhotoBytes, ct);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
        content.Add(fileContent, "file", file.Name);
        content.Add(new StringContent(ActionKey == "pickup" ? "TransportPickupComplete" : "TransportDropoffComplete"), "commandName");
        content.Add(new StringContent(ItemId.ToString(CultureInfo.InvariantCulture)), "referenceId");
        RequireCurrent(ct);
        var result = await _api.UploadAsync<CargoEvidenceUploadResponse>(RoleWorkspaceCatalog.CargoDriver, "api/v1/files/upload", content, ct);
        RequireCurrent(ct);
        if (string.IsNullOrWhiteSpace(result?.ObjectName)) throw new InvalidOperationException("사진 업로드 결과를 확인하지 못했습니다.");
        return result;
    }
    private void Clear()
    {
        Source = null; Saved = false; _photo = null; _uploaded = null;
        _captureNeedsReview = false; PhotoCaptureMessage = null;
        CargoConfirmed = LocationConfirmed = RecipientConfirmed = PaymentConfirmed = SaveConfirmed = false;
        DocumentPhotoConfirmed = ReceiptOmissionConfirmed = WorkCannotContinue = false;
        RecipientName = RecipientSignature = DriverSignature = ReceiptOmissionReason = IssueReason = IssueMemo = "";
        recovery.Reset(ItemId.ToString(CultureInfo.InvariantCulture), ActionKey);
    }
    public void Dispose() => Lifetime.Dispose();

    /// <summary>기존 선택 파일과 같은 검증·업로드 경로만 사용하며 촬영으로 업무를 완료하지 않는다.</summary>
    private sealed class CapturedPhotoFile : IBrowserFile
    {
        private readonly byte[] _bytes;
        public CapturedPhotoFile(업무촬영사진 photo)
        {
            if (photo.Bytes is null || photo.Bytes.LongLength <= 0 || photo.Bytes.LongLength > MaxPhotoBytes)
                throw new ArgumentException("8MB 이하의 현장 사진이 필요합니다.", nameof(photo));
            Name = photo.FileName; ContentType = photo.ContentType; _bytes = photo.Bytes.ToArray();
        }
        public string Name { get; }
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
        public long Size => _bytes.LongLength;
        public string ContentType { get; }
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Size > maxAllowedSize) throw new IOException("사진의 허용 크기를 초과했습니다.");
            return new MemoryStream(_bytes, writable: false);
        }
    }
}
