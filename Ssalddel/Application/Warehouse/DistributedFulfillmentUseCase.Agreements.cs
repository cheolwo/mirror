using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    public Task<DistributedCommandResponse> AgreementAsync(DistributedActor actor, string siteId, DistributedStorageAgreementRequest r, CancellationToken ct)
        => Execute(actor, "CreateAgreement", siteId, r, () => agreementCommands.CreateAsync(actor, siteId, r, ct), ct);
    public Task<DistributedCommandResponse> AgreementTermsAsync(DistributedActor actor, string id, DistributedStorageAgreementRequest r, CancellationToken ct)
        => Execute(actor, "AgreementTerms", id, r, () => agreementCommands.TermsAsync(actor, id, r, ct), ct);
    public Task<DistributedCommandResponse> AgreementConsentAsync(DistributedActor actor, string id, DistributedStorageAgreementConsentRequest r, CancellationToken ct)
        => Execute(actor, "AgreementConsent", id, r, () => agreementCommands.ConsentAsync(actor, id, r, ct), ct);
    private Task<분산보관합의Record> Agreement(string id, CancellationToken ct)
        => agreementCommands.Agreement(id, ct);
    private string AgreementStatus(분산보관합의Record agreement) => agreementCommands.AgreementStatus(agreement);
    private Task<분산보관합의Record> ActiveAgreement(분산재고LotRecord lot, CancellationToken ct)
        => agreementCommands.ActiveAgreement(lot, ct);
    private void SnapshotAgreement(분산재고LotRecord lot, 분산보관합의Record agreement, int quantity)
        => agreementCommands.SnapshotAgreement(lot, agreement, quantity);
    private Task AgreementCapacity(분산보관합의Record agreement, int addingQuantity, CancellationToken ct)
        => agreementCommands.AgreementCapacity(agreement, addingQuantity, ct);
}
internal sealed record DistributedAgreementHistory(int TermsVersion, string ActorUserId, string Action, bool SpaceConsented, bool GoodsConsented, string TermsJson, DateTime AtUtc);
internal sealed record DistributedLotAgreementSnapshot(string AgreementId, int TermsVersion, string SpaceOwnerUserId, string GoodsOwnerUserId, string TermsJson, int Quantity, DateTime AtUtc);
