using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Services;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사하차완료ViewModel(
    IDriverTransportCompletionPhotoService completionService) : ObservableObject
{
    private long _generation;
    [ObservableProperty]
    public partial bool 처리중 { get; private set; }

    [ObservableProperty]
    public partial DriverTransportCompletionPhotoResult? 결과 { get; private set; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task 처리Async(
        DriverTransportCompletionPhoto request,
        CancellationToken cancellationToken)
    {
        if (request.Kind != DriverTransportCompletionPhotoKind.Dropoff)
        {
            throw new InvalidOperationException("하차 완료 ViewModel에는 하차 완료 요청만 전달할 수 있습니다.");
        }

        var generation = _generation;
        try
        {
            처리중 = true;
            결과 = null;
            var result = await completionService.CompleteWithPhotoAsync(request, cancellationToken);
            if (generation == _generation && !cancellationToken.IsCancellationRequested) 결과 = result;
        }
        finally
        {
            if (generation == _generation) 처리중 = false;
        }
    }

    public void 초기화()
    {
        ++_generation;
        처리Command.Cancel();
        처리중 = false;
        결과 = null;
    }
}
