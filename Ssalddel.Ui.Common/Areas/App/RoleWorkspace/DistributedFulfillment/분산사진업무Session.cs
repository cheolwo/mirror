using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>사진과 완료 입력의 수명을 결속합니다. 전송 시작 후에는 원래 요청만 재확인합니다.</summary>
public sealed class 분산사진업무Session(I업무사진촬영Adapter capture)
{
    private readonly List<사진항목> photos = [];
    private string? context;
    public DistributedEvidenceWorkRequest? Work { get; private set; }
    public string Title { get; private set; } = "";
    public bool CaptureSupported => capture.IsSupported;
    public IReadOnlyList<업무촬영사진> Photos => photos.Select(item => item.Photo).ToArray();
    public IReadOnlyList<string> EvidenceIds => photos.Where(item => item.Evidence is not null).Select(item => item.Evidence!.EvidenceId).ToArray();
    public bool HasPhotos => photos.Count is > 0 and <= 3;
    public bool UploadStarted => photos.Any(item => item.UploadStarted);
    public bool HasUnresolvedUpload => photos.Any(item => item.UploadStarted && item.Evidence is null);
    public bool FullyUploaded => HasPhotos && photos.All(item => item.Evidence is not null);
    public Guid? CompletionRequestId => Body(Work)?.RequestId;
    public bool Bind(DistributedEvidenceWorkRequest work, string title)
    {
        var newContext = DistributedEvidenceDigests.Payload(work);
        if (context == newContext) return true;
        if (UploadStarted) return false;
        Clear(); context = newContext; Title = title;
        Work = JsonSerializer.Deserialize<DistributedEvidenceWorkRequest>(JsonSerializer.Serialize(work))!;
        var body = Body(Work) ?? throw new InvalidOperationException("사진을 연결할 업무를 확인해 주세요.");
        body.RequestId = Guid.NewGuid();
        return true;
    }
    public async Task<bool> CaptureAsync(CancellationToken ct)
    {
        if (Work is null || UploadStarted || photos.Count >= 3) return false;
        var photo = await capture.CaptureAsync(Title + " 사진", ct); ct.ThrowIfCancellationRequested();
        if (photo is null) return false;
        if (photo.ContentType is not ("image/jpeg" or "image/png") || photo.Bytes.Length is 0 or > 5 * 1024 * 1024)
            throw new InvalidOperationException("JPEG 또는 PNG 사진을 한 장당 5MB 이하로 촬영해 주세요.");
        photos.Add(new(photo with { Bytes = photo.Bytes.ToArray() }, Guid.NewGuid(), DateTime.UtcNow));
        return true;
    }
    public void Remove(int index)
    { if (!UploadStarted && index >= 0 && index < photos.Count) photos.RemoveAt(index); }
    public async Task UploadAsync(분산이행Client client, CancellationToken ct, Action requireCurrent)
    {
        if (Work is null || !HasPhotos) throw new InvalidOperationException("완료 사진을 먼저 촬영하고 확인해 주세요.");
        foreach (var item in photos)
        {
            if (item.Evidence is not null) continue;
            item.UploadStarted = true;
            var response = await client.UploadPhotoAsync(new() { UploadRequestId = item.UploadRequestId, Work = Work,
                ContentType = item.Photo.ContentType, ImageBase64 = Convert.ToBase64String(item.Photo.Bytes), CapturedAtUtc = item.CapturedAtUtc }, ct);
            requireCurrent();
            if (response is null || string.IsNullOrWhiteSpace(response.EvidenceId) || response.Kind != Work.Kind || response.AggregateId != Work.AggregateId
                || response.CompletionRequestId != CompletionRequestId || response.InputDigest != DistributedEvidenceDigests.Payload(Body(Work)!)
                || string.IsNullOrEmpty(response.BindingDigest) || response.BindingDigest.Length != 64 || response.BindingDigest.Any(c => !Uri.IsHexDigit(c))
                || photos.Any(other => other.Evidence is { } stored && (stored.BindingDigest != response.BindingDigest || stored.EvidenceId == response.EvidenceId))
                || response.StatusCode is not ("Uploaded" or "Bound") || !response.ReadAllowed
                || response.ContentType is not ("image/jpeg" or "image/png") || response.Bytes is <= 0 or > 5 * 1024 * 1024)
                throw new InvalidOperationException("사진 저장 응답의 업무 연결을 확인하지 못했습니다. 같은 사진 전송 결과를 다시 확인해 주세요.");
            item.Evidence = response;
        }
    }
    public DistributedCommandRequest CompletionBody()
    {
        if (!FullyUploaded) throw new InvalidOperationException("모든 완료 사진의 저장 결과를 먼저 확인해 주세요.");
        var body = Body(Work)!;
        switch (body)
        {
            case DistributedInboundReceiptBatchRequest receive: receive.PhotoEvidenceIds = EvidenceIds; break;
            case DistributedPickBatchRequest pick: pick.PhotoEvidenceIds = EvidenceIds; break;
            case DistributedDeliveryBatchRequest delivery: delivery.PhotoEvidenceIds = EvidenceIds; break;
            case DistributedReturnBatchRequest returned: returned.PhotoEvidenceIds = EvidenceIds; break;
            case DistributedExistingLotRequest import: import.PhotoEvidenceIds = EvidenceIds; break;
        }
        return body;
    }
    public void Clear() { photos.Clear(); context = null; Work = null; Title = ""; }
    public static DistributedCommandRequest? Body(DistributedEvidenceWorkRequest? work) => work?.Kind switch
    { "Receive" => work.Receive, "Pick" => work.Pick, "Deliver" => work.Deliver, "Return" => work.Return, "ImportLot" => work.ImportLot, _ => null };
    private sealed class 사진항목(업무촬영사진 photo, Guid uploadRequestId, DateTime capturedAtUtc)
    {
        public 업무촬영사진 Photo { get; } = photo;
        public Guid UploadRequestId { get; } = uploadRequestId;
        public DateTime CapturedAtUtc { get; } = capturedAtUtc;
        public bool UploadStarted { get; set; }
        public DistributedPhotoEvidenceDto? Evidence { get; set; }
    }
}
