namespace RestaurantDeskApp.Services;

public interface IRestaurantReceiptPrinter
{
    bool UsesNativeDialog { get; }
    Task RequestPrintDialogAsync(string title, string html, CancellationToken cancellationToken);
}

public sealed class BrowserRestaurantReceiptPrinter : IRestaurantReceiptPrinter
{
    public bool UsesNativeDialog => false;
    public Task RequestPrintDialogAsync(string title, string html, CancellationToken cancellationToken)
        => throw new NotSupportedException("브라우저 출력 경로를 사용해 주세요.");
}
