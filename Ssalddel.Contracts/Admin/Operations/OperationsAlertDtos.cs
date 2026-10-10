namespace Ssalddel.Contracts.Admin.Operations;

public sealed record OperationsAlertDto(string Id, int Episode, long Revision, string Severity,
    string Status, string Title, string Action, string SourceHref, DateTime CreatedAtUtc,
    DateTime LastVerifiedAtUtc, bool SourceCurrent, string[] DeliveryStates);
public sealed record OperationsAlertsPage(bool Enabled, bool ExternalSendEnabled,
    IReadOnlyList<OperationsAlertDto> Items, int Page, bool HasMore);
public sealed record OperationsAlertAcknowledgeRequest(Guid ClientRequestId, long ExpectedRevision);
