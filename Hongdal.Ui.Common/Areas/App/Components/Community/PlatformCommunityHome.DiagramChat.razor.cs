using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private CommunityDiagramChatContext BuildDiagramChatContext()
    {
        var roomId = string.IsNullOrWhiteSpace(선택현재원장Id)
            ? $"community:{selectedLedgerTemplateKey}:diagram"
            : $"community:ledger:{선택현재원장Id}:diagram";
        return new(
            roomId,
            $"{SelectedLedgerTemplate.DisplayName} 대화방",
            선택현재원장?.Title ?? sharedLedgerDiagramSnapshot?.DiagramName ?? "현재 원장 선택 전",
            AppKey,
            선택현재원장Id,
            SelectedLedgerTemplate.DisplayName,
            selectedLedgerTemplateKey);
    }
}
