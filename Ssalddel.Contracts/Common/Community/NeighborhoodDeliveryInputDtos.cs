using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Contracts.Common.Community;

/// <summary>배송 접수가 아니라 선택해서 보완할 입력 초안의 판본·양식입니다.</summary>
public static class NeighborhoodDeliveryInputSchema
{
    public const string Version = "neighborhood-delivery-input.v1";
    public const string SheetName = "배송입력";
    public const string TemplateResourceName = "NeighborhoodDelivery.InputTemplate.r29.xlsx";
    public const int MaximumFileBytes = 2 * 1024 * 1024;
    public const int MaximumRows = 100;
    public static IReadOnlyList<string> Headers { get; } = Array.AsReadOnly(new[]
    {
        "행ID(선택)", "물품명", "수량", "중량kg", "픽업주소", "픽업상세주소", "픽업담당자", "픽업전화",
        "전달주소", "전달상세주소", "전달담당자", "전달전화", "픽업시작", "픽업종료",
        "크기(선택)", "전달시작(선택)", "전달종료(선택)", "요청사항(선택)", "차량(선택)"
    });
    public static IReadOnlyList<string> FieldKeys { get; } = Array.AsReadOnly(new[]
    {
        "SourceRowId", "CargoName", "Quantity", "WeightKg", "PickupAddress", "PickupDetailedAddress", "PickupContactName", "PickupPhone",
        "DropoffAddress", "DropoffDetailedAddress", "DropoffContactName", "DropoffPhone", "PickupStart", "PickupEnd",
        "PackageSizeCode", "DropoffStart", "DropoffEnd", "Notes", "VehicleType"
    });
}

public sealed class NeighborhoodDeliveryInputPreviewRequest
{
    [IsmsPProtectedData(PersonalDataFieldKey.DisplayName, "개인정보가 포함될 수 있는 배송 입력 파일 이름 확인")]
    public string FileName { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "배송 입력 자료의 픽업·전달 주소 확인")]
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber, "배송 입력 자료의 담당자 연락처 확인")]
    [IsmsPProtectedData(PersonalDataFieldKey.DisplayName, "배송 입력 자료의 담당자 이름 확인")]
    [IsmsPProtectedData(PersonalDataFieldKey.WorkSchedule, "배송 입력 자료의 요청 시간창 확인")]
    public string ContentBase64 { get; set; } = string.Empty;
    public Guid? PrivacyConsentEvidenceId { get; set; }
}

public sealed class NeighborhoodDeliveryInputTemplateResponse
{
    public string SchemaVersion { get; set; } = NeighborhoodDeliveryInputSchema.Version;
    public string FileName { get; set; } = "생활배송입력.r29.xlsx";
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string ContentBase64 { get; set; } = string.Empty;
}

public sealed class NeighborhoodDeliveryInputPreviewResponse
{
    public string SchemaVersion { get; set; } = NeighborhoodDeliveryInputSchema.Version;
    public IReadOnlyList<NeighborhoodDeliveryInputRow> Rows { get; set; } = [];
    public IReadOnlyList<NeighborhoodDeliveryInputIssue> Errors { get; set; } = [];
}

public sealed class NeighborhoodDeliveryInputRow
{
    public int RowNumber { get; set; }
    public Guid? SourceRowId { get; set; }
    public string SourceSheet { get; set; } = NeighborhoodDeliveryInputSchema.SheetName;
    public IReadOnlyDictionary<string, string> RawValues { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> SourceCells { get; set; } = new Dictionary<string, string>();
    public NeighborhoodDeliveryRequest Draft { get; set; } = new() { Quantity = 0, VehicleType = string.Empty };
    public IReadOnlyList<NeighborhoodDeliveryInputIssue> Issues { get; set; } = [];
}

public sealed record NeighborhoodDeliveryInputIssue(string FieldKey, string Code, string Message);
