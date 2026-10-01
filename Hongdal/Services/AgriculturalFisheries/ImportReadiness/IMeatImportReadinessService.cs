using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.ImportReadiness;

public interface IMeatImportReadinessService
{
    MeatImportReadinessDiagramResponse GetDiagram();

    Task<MeatImportReadinessCaseListResponse> ListMineAsync(
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<MeatImportReadinessCaseResponse?> GetCaseAsync(
        string caseId,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<MeatImportReadinessCaseResponse> CreateCaseAsync(
        CreateMeatImportReadinessCaseRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<MeatImportReadinessCaseResponse> CreateCaseFromCommunityPostAsync(
        long sourceCommunityPostId,
        CreateMeatImportReadinessCaseRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<MeatImportReadinessCaseResponse> UpdateStepStatusAsync(
        string caseId,
        string stepCode,
        UpdateMeatImportReadinessStepStatusRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<MeatImportReadinessCaseResponse> AddEvidenceAsync(
        string caseId,
        string stepCode,
        AddMeatImportReadinessEvidenceRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<MeatImportReadinessCaseResponse> AddDiscussionAsync(
        string caseId,
        string stepCode,
        AddMeatImportReadinessDiscussionRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<MeatImportReadinessCaseResponse> ResolveDiscussionAsync(
        string caseId,
        string stepCode,
        string discussionId,
        ResolveMeatImportReadinessDiscussionRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<MeatImportReadinessCaseResponse> AcknowledgeStepAsync(
        string caseId,
        string stepCode,
        AcknowledgeMeatImportReadinessStepRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);
}

public sealed class MeatImportReadinessConcurrencyException : Exception
{
    public MeatImportReadinessConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
