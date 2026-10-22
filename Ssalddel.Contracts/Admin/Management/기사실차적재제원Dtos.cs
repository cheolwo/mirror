namespace Ssalddel.Contracts.Admin.Management;

/// <summary>법정 자격과 별개인 선택 실차·장비의 비공개 적재 확인 자료. 생략값은 미확인이다.</summary>
public sealed record 기사실차적재제원
{
    public string ActualVehicleReference { get; init; } = "";
    public string VehicleProfileValue { get; init; } = "";
    public string PrivateEvidenceReference { get; init; } = "";
    public IReadOnlyList<string> EquipmentReferences { get; init; } = [];
    public DateTime ConfirmedAtUtc { get; init; }
    public DateTime ValidUntilUtc { get; init; }
    public bool Withdrawn { get; init; }
    public int? 적재함길이Mm { get; init; }
    public int? 적재함폭Mm { get; init; }
    public int? 적재함높이Mm { get; init; }
    public int? 최대적재중량Kg { get; init; }
    public decimal? 최대부피Cbm { get; init; }
    public int? 팔레트적재개수 { get; init; }
    // Unknown/Open/Enclosed. Open은 높이나 장비가 확인됐다는 뜻이 아니다.
    public string EntranceKind { get; init; } = "Unknown";
    public bool? 비눈보호가능 { get; init; }
    public bool? 냉장가능 { get; init; }
    public bool? 냉동가능 { get; init; }
    public bool? 측면상하차가능 { get; init; }
    public bool? 리프트가능 { get; init; }
    public bool? 장재물유리 { get; init; }
    public string ReviewerId { get; init; } = "";
}

public sealed record 기사실차적재제원수정요청(long? ExpectedRevision, 기사실차적재제원 제원);
public sealed record 기사실차적재제원응답(string 기사Id, long? 판본, 기사실차적재제원? 제원)
{
    public string? 현재실차참조 { get; init; }
    public string? 현재차량종류 { get; init; }
}
