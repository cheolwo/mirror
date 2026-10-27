namespace DriverApp.Services;

public interface IDriverNativeMapNavigator
{
    Task OpenAsync(CancellationToken cancellationToken = default);
}
