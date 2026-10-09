using Ssalddel.Contracts.Common.Warehouse;

namespace Ssalddel.Application.Warehouse;

public interface IDistributedPhotoEvidenceBinder
{
    // Called inside the same SQL transaction as the custody command, before inventory changes.
    Task BindAsync(DistributedEvidenceBinding binding, IReadOnlyList<string> photoEvidenceIds, CancellationToken ct);
    // ImportLot creates lineage only after preflight; attach generated IDs without changing the verified input.
    Task AttachImportedLineageAsync(Guid completionRequestId, string actorUserId, string planId,
        IReadOnlyList<string> lotIds, CancellationToken ct);
}
