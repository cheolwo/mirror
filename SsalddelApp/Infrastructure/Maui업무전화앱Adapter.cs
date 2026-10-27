using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelApp.Infrastructure;

/// <summary>MAUI 전화 앱에 번호만 채웁니다. Android에서는 ACTION_DIAL이며 발신은 사용자가 직접 결정합니다.</summary>
public sealed class Maui업무전화앱Adapter : I업무전화앱Adapter
{
    public bool IsSupported
    {
        get { try { return PhoneDialer.Default.IsSupported; } catch (Exception) { return false; } }
    }

    public async Task<업무전화앱결과> OpenAsync(string number, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!업무전화번호Policy.TryNormalize(number, out var normalized)) return 업무전화앱결과.InvalidNumber;
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (!PhoneDialer.Default.IsSupported) return 업무전화앱결과.NotSupported;
                PhoneDialer.Default.Open(normalized);
                return 업무전화앱결과.Opened;
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (FeatureNotSupportedException) { return 업무전화앱결과.NotSupported; }
        catch (Exception) { return 업무전화앱결과.Failed; }
    }
}
