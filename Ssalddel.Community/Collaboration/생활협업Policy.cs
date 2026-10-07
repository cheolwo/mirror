using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Community.Collaboration;

/// <summary>합의 조건의 저장소 독립 경계. 주소·전화번호 등은 공개 summary로 투영하지 않습니다.</summary>
public static class 생활협업Policy
{
    public static bool 조건유효(NeighborhoodCollaborationTerms? terms, string kind, DateTime now)
    {
        if (terms is null || string.IsNullOrWhiteSpace(terms.Summary) || terms.Summary.Length > 120
            || terms.Quantity is < 1 or > 1000 || string.IsNullOrWhiteSpace(terms.Unit) || terms.Unit.Length > 20
            || terms.Notes?.Length > 1000 || terms.AgreedCostKrw is < 0 or > 10000000
            || terms.FromUtc.Kind != DateTimeKind.Utc || terms.UntilUtc.Kind != DateTimeKind.Utc
            || terms.FromUtc < now.AddMinutes(-5) || terms.UntilUtc <= terms.FromUtc
            || terms.UntilUtc > now.AddDays(30)) return false;
        if (terms.TransferMethod is not null && (kind != NeighborhoodCollaborationKinds.Goods
            || !NeighborhoodTransferMethods.IsKnown(terms.TransferMethod) || string.IsNullOrWhiteSpace(terms.HandoverPlace) || terms.HandoverPlace.Length > 500)) return false;
        if (terms.TransferMethod is null && terms.HandoverPlace is not null) return false;
        if (kind != NeighborhoodCollaborationKinds.Storage)
            return terms.StorageSpaceId is null && terms.StorageQuantity is null && terms.StorageUnit is null
                && terms.StorageFromUtc is null && terms.StorageUntilUtc is null;
        return !string.IsNullOrWhiteSpace(terms.StorageSpaceId) && terms.StorageSpaceId.Length <= 100
            && terms.StorageQuantity is >= 1 and <= 1000 && !string.IsNullOrWhiteSpace(terms.StorageUnit)
            && terms.StorageUnit.Length <= 20 && terms.StorageFromUtc is { Kind: DateTimeKind.Utc }
            && terms.StorageUntilUtc is { Kind: DateTimeKind.Utc }
            && terms.StorageFromUtc >= terms.FromUtc && terms.StorageUntilUtc <= terms.UntilUtc
            && terms.StorageUntilUtc > terms.StorageFromUtc;
    }

    public static bool 시작전(string state) => state is NeighborhoodCollaborationStates.Requested or NeighborhoodCollaborationStates.Agreed;
    public static bool 전체합의(bool owner, bool requester) => owner && requester;
}
