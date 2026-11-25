namespace RestaurantDeskApp.Services;

public interface IRestaurantReceiptPrinter
{
    bool UsesNativeDialog { get; }
    // 기존 native 분기/메서드 이름은 호환을 유지하며 Windows 직접 출력도 포함한다.
    bool PrintsWithoutDialog => false;
    Task RequestPrintDialogAsync(string title, string html, CancellationToken cancellationToken);
}

public sealed class BrowserRestaurantReceiptPrinter : IRestaurantReceiptPrinter
{
    public bool UsesNativeDialog => false;
    public Task RequestPrintDialogAsync(string title, string html, CancellationToken cancellationToken)
        => throw new NotSupportedException("브라우저 출력 경로를 사용해 주세요.");
}
