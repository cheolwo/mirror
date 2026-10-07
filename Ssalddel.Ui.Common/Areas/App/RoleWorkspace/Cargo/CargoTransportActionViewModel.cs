using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public sealed record CargoEvidenceUploadResponse(string ObjectName, string? Url);

/// <summary>현장 확인과 실제 사진 업로드를 완료 입력의 독립 수명으로 관리합니다.</summary>
public sealed class CargoTransportActionViewModel : IDisposable
{
    public const long MaxPhotoBytes = 8 * 1024 * 1024;
    private readonly CargoDriverWorkspaceApiClient _client;
    private readonly IRoleWorkspaceApi _api;
    private IBrowserFile? _photo;
    private CargoEvidenceUploadResponse? _uploaded;
    public CargoTransportActionViewModel(CargoDriverWorkspaceApiClient client, IRoleWorkspaceApi api, IRoleWorkspaceAccess access)
    {
        _client = client; _api = api;
        Lifetime = new(access, RoleWorkspaceCatalog.CargoDriver, Clear);
    }
    public CargoInputLifetime Lifetime { get; }
    public long ItemId { get; private set; }
    public string ActionKey { get; private set; } = "";
    public 기사운송상세응답? Source { get; private set; }
    public bool Saved { get; private set; }
    public string? PhotoName => _photo?.Name;
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
    public bool CanSubmit => StageAllowsInput && !Lifetime.IsBusy && !Lifetime.HasError && !Saved && SaveConfirmed && (ActionKey == "issue"
        ? !string.IsNullOrWhiteSpace(IssueReason)
        : _photo is not null && CargoConfirmed && LocationConfirmed && RecipientConfirmed
          && (ActionKey == "pickup" ? ReceiptReady : PaymentConfirmed));
    private bool ReceiptReady => Source?.인수증필요 != true || DocumentPhotoConfirmed
        || (!string.IsNullOrWhiteSpace(RecipientName) && !string.IsNullOrWhiteSpace(RecipientSignature) && !string.IsNullOrWhiteSpace(DriverSignature))
        || (Source.인수증서명필수 != true && ReceiptOmissionConfirmed && !string.IsNullOrWhiteSpace(ReceiptOmissionReason));

    public async Task LoadAsync(long id, string action)
    {
        if (ItemId != id || ActionKey != action) { Lifetime.Reset(); ItemId = id; ActionKey = action; }
        await Lifetime.RunAsync(async ct =>
        {
            _ = CargoWorkspaceRoutes.DriverInput(id, action);
            var source = await _client.TransportAsync(id, ct);
            Lifetime.RequireCurrent(ct); CargoWorkspacePresentation.RequireTransport(source, id);
            if (source.개인정보제공보류) throw new RoleWorkspaceAccessException(403, "운송 정보 제공 조건을 확인해 주세요.");
            Source = source;
        });
    }
    public void SetPhoto(IBrowserFile file)
    {
        if (Lifetime.IsBusy) return;
        if (file.Size <= 0 || file.Size > MaxPhotoBytes || file.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new ArgumentException("8MB 이하의 JPG·PNG·WebP 사진을 선택해 주세요.", nameof(file));
        _photo = file; _uploaded = null; DocumentPhotoConfirmed = false; Lifetime.Notify();
    }
    public async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        await Lifetime.RunAsync(async ct =>
        {
            var latest = await _client.TransportAsync(ItemId, ct);
            Lifetime.RequireCurrent(ct); CargoWorkspacePresentation.RequireTransport(latest, ItemId);
            if (latest.개인정보제공보류) throw new RoleWorkspaceAccessException(403, "운송 정보 제공 조건을 확인해 주세요.");
            Source = latest;
            if (!StageAllowsInput) throw new InvalidOperationException("운송 상태가 바뀌었습니다. 현재 단계를 확인해 주세요.");
            if (ActionKey == "issue")
            {
                var result = await _client.IssueAsync(ItemId, new 기사운송문제신고요청
                {
                    단계 = CargoWorkspacePresentation.CanArrivePickup(latest.상태) || latest.상태 == "상차지도착" ? "상차" : "하차",
                    사유 = IssueReason.Trim(), 메모 = IssueMemo.Trim(), 관리자확인요청 = true, 현장진행불가 = WorkCannotContinue
                }, ct);
                Lifetime.RequireCurrent(ct); CargoWorkspacePresentation.RequireTransport(result, ItemId);
                RequireIssueRecorded(result!);
            }
            else
            {
                if (!ReceiptReady && ActionKey == "pickup") throw new InvalidOperationException("인수증 조건을 다시 확인해 주세요.");
                _uploaded ??= await UploadAsync(ct);
                Lifetime.RequireCurrent(ct);
                var result = ActionKey == "pickup"
                    ? await _client.PickupAsync(ItemId, PickupPayload(_uploaded), ct)
                    : await _client.DropoffAsync(ItemId, new 기사운송하차완료요청 { 하차사진ObjectName = _uploaded.ObjectName, 하차사진Url = _uploaded.Url }, ct);
                Lifetime.RequireCurrent(ct);
                if (result?.Id != ItemId) throw new InvalidOperationException("완료 처리 응답을 확인하지 못했습니다. 운송 상태를 다시 조회해 주세요.");
                if (result.상태 != (ActionKey == "pickup" ? "상차완료" : "인수완료"))
                    throw new InvalidOperationException("완료 응답의 운송 단계가 일치하지 않습니다. 현재 상태를 다시 확인해 주세요.");
            }
            var after = await _client.TransportAsync(ItemId, ct);
            Lifetime.RequireCurrent(ct); CargoWorkspacePresentation.RequireTransport(after, ItemId);
            if (after.개인정보제공보류) throw new RoleWorkspaceAccessException(403, "운송 정보 제공 조건을 확인해 주세요.");
            Source = after;
            if (ActionKey == "issue") RequireIssueRecorded(after);
            else if (!(ActionKey == "pickup"
                ? after.상태 is "상차완료" or "운송중" or "하차지도착" or "인수완료"
                : after.상태 == "인수완료"))
                throw new InvalidOperationException("저장된 완료 상태를 확인하지 못했습니다. 현재 운송과 예외 상태를 다시 확인해 주세요.");
            Saved = true;
            _photo = null; _uploaded = null;
        }, "저장된 운송 상태를 확인했습니다. 지도로 돌아가 다음 업무를 확인해 주세요.");
    }
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
        Lifetime.RequireCurrent(ct);
        var result = await _api.UploadAsync<CargoEvidenceUploadResponse>(RoleWorkspaceCatalog.CargoDriver, "api/v1/files/upload", content, ct);
        Lifetime.RequireCurrent(ct);
        if (string.IsNullOrWhiteSpace(result?.ObjectName)) throw new InvalidOperationException("사진 업로드 결과를 확인하지 못했습니다.");
        return result;
    }
    private void Clear()
    {
        Source = null; Saved = false; _photo = null; _uploaded = null;
        CargoConfirmed = LocationConfirmed = RecipientConfirmed = PaymentConfirmed = SaveConfirmed = false;
        DocumentPhotoConfirmed = ReceiptOmissionConfirmed = WorkCannotContinue = false;
        RecipientName = RecipientSignature = DriverSignature = ReceiptOmissionReason = IssueReason = IssueMemo = "";
    }
    public void Dispose() => Lifetime.Dispose();
}
