namespace Ssalddel.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private int 현재원장활성노드순서해결(
        현재원장컨텍스트 ledger,
        IReadOnlyList<원장블록노드> nodes)
        => PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex(
            ledger.TemplateKey, ledger.StateLabel, nodes);

    private static int 원장블록노드순서찾기(
        IReadOnlyList<원장블록노드> nodes,
        string nodeTitle)
        => PlatformCommunityLedgerStatePresentation.FindNodeIndex(nodes, nodeTitle);
}
