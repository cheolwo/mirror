using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>선택한 원장에 속한 읽기 결과만 표시한다. 취소를 무시하는 adapter의 늦은 응답도 폐기한다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.GroupImportTradeReadiness, SsalddelCodeLayer.ViewModel,
    "주문자가 선택한 원장과 자동집단의 최신 읽기 결과만 적용합니다.",
    Effects = SsalddelCodeEffect.NetworkCall,
    Boundary = "조회만 수행하며 이전 선택과 종료된 화면의 성공·오류 응답을 표시하지 않습니다.")]
public sealed class OrdererGroupImportReadinessState(I같이수입준비주문자Client client, Func<string?>? currentOwner = null) : IDisposable
{
    private CancellationTokenSource? current;
    private long generation;
    private bool disposed;
    private string? owner;
    private bool ownerCaptured;
    public string? Key { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public string? BackgroundWarning { get; private set; }
    public DateTime? LastLoadedAtUtc { get; private set; }
    public 같이수입준비주문자조회응답? Readiness { get; private set; }
    public static string SelectionKey(string ledgerId, string? autoGroupId) => $"{ledgerId.Length}:{ledgerId}{autoGroupId?.Length ?? -1}:{autoGroupId}";
    public void SynchronizeOwner()
    {
        if (disposed || currentOwner is null) return;
        var next = currentOwner();
        if (ownerCaptured && next == owner) return;
        ownerCaptured = true; owner = next; ++generation; current?.Cancel(); current = null;
        Key = null; Readiness = null; Error = BackgroundWarning = null; LastLoadedAtUtc = null; IsLoading = false;
    }

    public async Task LoadAsync(string ledgerId, string? autoGroupId, bool force = false, bool preserveCurrentContent = false)
    {
        SynchronizeOwner();
        var key = SelectionKey(ledgerId, autoGroupId);
        if (disposed || currentOwner is not null && owner is null || !force && Key == key) return;
        var previous = current;
        var cancellation = new CancellationTokenSource();
        current = cancellation;
        var requestGeneration = ++generation;
        previous?.Cancel();
        // Only a refresh of this same selection may retain previously confirmed content.
        preserveCurrentContent &= Key == key;
        Key = key;
        if (!preserveCurrentContent) { Readiness = null; LastLoadedAtUtc = null; Error = null; }
        BackgroundWarning = null;
        IsLoading = !preserveCurrentContent && !string.IsNullOrWhiteSpace(ledgerId) && !string.IsNullOrWhiteSpace(autoGroupId);
        bool IsCurrent() => !disposed && requestGeneration == generation && ReferenceEquals(current, cancellation)
            && !cancellation.IsCancellationRequested && (currentOwner is null || owner == currentOwner());
        try
        {
            if (string.IsNullOrWhiteSpace(ledgerId) || string.IsNullOrWhiteSpace(autoGroupId)) return;
            var response = await client.조회Async(ledgerId, autoGroupId, cancellation.Token)
                ?? throw new InvalidOperationException("서버에서 원장 응답을 받지 못했습니다.");
            if (!IsCurrent()) return;
            Readiness = response;
            Error = null;
            LastLoadedAtUtc = DateTime.UtcNow;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsCurrent()) return;
            if (exception is SsalddelApiException { StatusCode: 401 or 403 })
            {
                Readiness = null; LastLoadedAtUtc = null; BackgroundWarning = null;
                Error = "이 원장을 볼 수 있는 로그인과 참여 권한을 다시 확인해 주세요.";
                return;
            }
            var message = $"같이 수입 준비 자료를 불러오지 못했습니다. {exception.Message}";
            if (preserveCurrentContent && Readiness is not null)
                BackgroundWarning = $"{message} 이전에 확인한 내용을 유지합니다.";
            else Error = message;
        }
        finally
        {
            SynchronizeOwner();
            if (IsCurrent()) { IsLoading = false; current = null; }
            cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; ++generation;
        current?.Cancel(); current = null;
        Readiness = null; Error = BackgroundWarning = null; LastLoadedAtUtc = null; IsLoading = false;
    }
}
