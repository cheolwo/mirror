using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>동일 계정의 화면 복귀에만 사용하는 메모리 초안입니다. 주소·토큰을 저장하지 않습니다.</summary>
public sealed class NeighborhoodCollaborationDraftSession
{
    public string? Owner { get; private set; }
    public long? PostId { get; set; }
    public string? SpaceId { get; set; }
    public string? EditId { get; set; }
    public long Revision { get; set; }
    public NeighborhoodCollaborationCreateRequest Draft { get; set; } = NewDraft();
    public DateTime From { get; set; } = DateTime.Now.AddHours(1);
    public DateTime Until { get; set; } = DateTime.Now.AddDays(1);
    public bool ConditionsConfirmed { get; set; }
    public NeighborhoodCollaborationCreateRequest? PendingCreate { get; set; }
    public NeighborhoodCollaborationCommandRequest? PendingCommand { get; set; }
    public string? PendingTarget { get; set; }
    public void Bind(string? owner)
    {
        if (Owner == owner) return;
        Owner = owner; ResetDraft(); PendingCreate = null; PendingCommand = null; PendingTarget = null;
    }
    public void ResetDraft()
    {
        Draft = NewDraft(); PostId = null; SpaceId = null; EditId = null; Revision = 0;
        From = DateTime.Now.AddHours(1); Until = DateTime.Now.AddDays(1); ConditionsConfirmed = false;
    }
    private static NeighborhoodCollaborationCreateRequest NewDraft() => new() { ClientRequestId = Guid.NewGuid() };
}
