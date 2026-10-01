using Hongdal.Ui.Common.Areas.App.Services;

namespace HongdalApp.Services;

public static class HongdalClientContentPolicy
{
    private const string HongikHakdangChannelName = "홍익학당";

    public static bool IsVisibleToGeneralClient(CommunityDecorationProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return !string.Equals(
            product.ScriptureSource?.ChannelName,
            HongikHakdangChannelName,
            StringComparison.OrdinalIgnoreCase);
    }
}
