using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodStorageDraftSession
{
    public string? Owner { get; private set; }
    public string? SpaceId { get; set; }
    public long Revision { get; set; }
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string RegionKey { get; set; } = "";
    public string GoodsKind { get; set; } = "생활 물품";
    public decimal Capacity { get; set; } = 1;
    public DateTime From { get; set; } = DateTime.Now.AddHours(1);
    public DateTime Until { get; set; } = DateTime.Now.AddDays(1);
    public string Address { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Instructions { get; set; } = "";
    public bool Publish { get; set; }
    public bool PrivacyConfirmed { get; set; }
    public NeighborhoodStorageSpaceRequest? PendingSave { get; set; }
    public Guid? PendingRequestId { get; set; }
    public string? PendingTarget { get; set; }
    public string? PendingAction { get; set; }
    public string? PendingCollaboration { get; set; }
    public object? PendingMutation { get; set; }
    public void Bind(string? owner)
    {
        if (Owner == owner) return;
        Owner = owner; ResetDraft(); ClearPending();
    }
    public void ResetDraft()
    {
        SpaceId = null; Revision = 0; RequestId = Guid.NewGuid(); Title = ""; Description = ""; RegionKey = "";
        GoodsKind = "생활 물품"; Capacity = 1; From = DateTime.Now.AddHours(1); Until = DateTime.Now.AddDays(1);
        Address = ""; Contact = ""; Instructions = ""; Publish = false; PrivacyConfirmed = false;
    }
    public void ClearPending()
    {
        PendingSave = null; PendingRequestId = null; PendingTarget = null; PendingAction = null; PendingCollaboration = null; PendingMutation = null;
    }
}
