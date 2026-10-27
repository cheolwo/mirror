using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.WebApp.Services;

// 공개 snapshot이나 질문 게시와 별개인, 선택 marker의 사용자 전용 조회 상태입니다.
internal sealed class 커뮤니티지도내원장Session(
    Func<string, string?, CancellationToken, Task<IReadOnlyList<지도신청가원장Response>>> read,
    Func<string?> currentMarker,
    Func<string?> currentLedger,
    Action clearMissingLedger,
    Func<Task> changed)
{
    public IReadOnlyList<지도신청가원장Response> Ledgers { get; set; } = [];
    public bool Loading { get; private set; }
    public bool PendingRefresh { get; set; }
    public string? Error { get; set; }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var markerId = currentMarker();
        if (string.IsNullOrWhiteSpace(markerId))
        {
            Ledgers = [];
            Loading = false;
            Error = null;
            PendingRefresh = false;
            return;
        }

        Loading = true;
        Error = null;
        await changed();
        try
        {
            var ledgers = await read(markerId, currentLedger(), cancellationToken);
            if (!string.Equals(markerId, currentMarker(), StringComparison.Ordinal))
            {
                return;
            }

            Ledgers = ledgers;
            if (!string.IsNullOrWhiteSpace(currentLedger())
                && ledgers.All(ledger => !string.Equals(ledger.원장Id, currentLedger(), StringComparison.Ordinal)))
            {
                clearMissingLedger();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Ledgers = [];
            Error = exception.Message;
        }
        finally
        {
            Loading = false;
            PendingRefresh = false;
            await changed();
        }
    }
}
