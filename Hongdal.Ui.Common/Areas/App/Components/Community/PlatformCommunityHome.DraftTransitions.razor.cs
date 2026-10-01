using Hongdal.Ui.Common.Areas.App.ViewModels;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private void PrepareDiagramCommunityDraft()
    {
        OpenCommunityMode();
        var draft = DiagramWorkspace.CreateDiagramShareDraft(
            form.WorkflowTag,
            form.Title,
            form.Body,
            ResolveRoleTag(RoleLabel));

        selectedBoardFilter = draft.Category;
        ApplyComposerDraft(draft);
    }

    private void PrepareWorkCommunity초안(WorkCommunityDraftKind kind)
    {
        var draft = DiagramWorkspace.CreateWorkDraft(
            kind,
            AppName,
            RoleLabel,
            ResolveRoleTag(RoleLabel));

        ResetComposerTransitionContext();
        ApplyComposerDraft(draft);
        HomeModeState.SetWorkMode(false);
    }

    private void 다이어그램커뮤니티초안준비(
        IReadOnlyList<원장블록노드> flowNodes,
        IReadOnlyList<원장블록연결선> diagramEdges)
    {
        var draft = DiagramWorkspace.CreateCommunityDraft(
            flowNodes.Select(node => new CommunityDiagramDraftNode(
                node.Title,
                node.GroupLabel,
                node.Description,
                node.Kind,
                node.Condition)).ToArray(),
            diagramEdges.Select(edge => new CommunityDiagramDraftEdge(
                edge.FromTitle,
                edge.ToTitle,
                edge.Label)).ToArray(),
            BoardCategoryOptions,
            AppName,
            RoleLabel);

        ResetComposerTransitionContext();
        ApplyComposerDraft(draft);
        HomeModeState.SetWorkMode(false);
        DiagramPalette.SetDiagramMode(false);
    }

    private void ApplyComposerDraft(CommunityComposerDraftTransition draft)
    {
        selectedLedgerTemplateKey = draft.LedgerTemplateKey;
        form.Category = draft.Category;
        form.WorkflowTag = draft.WorkflowTag;
        form.RoleTag = draft.RoleTag;
        form.Title = draft.Title;
        form.Body = draft.Body;
        form.SharedLinkUrl = string.Empty;
        form.IsReportBoardPost = draft.IsReportBoardPost;
        form.ReporterDisplayName = string.Empty;
        form.ReportedDisplayName = string.Empty;
        isComposeOpen = true;
        statusSeverity = Severity.Info;
        statusMessage = draft.StatusMessage;
    }

    private void ResetComposerTransitionContext()
    {
        editingPostId = null;
        selectedFiles.Clear();
    }
}
