using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public sealed class CargoNoticeListViewModel(ICargoNoticeClient client) : IDisposable
{
    private long generation;
    private bool disposed;
    public event Action? Changed;
    public IReadOnlyList<CargoNoticePublicDto> Items { get; private set; } = [];
    public int Page { get; private set; } = 1;
    public bool HasNext { get; private set; }
    public bool IsLoading { get; private set; }
    public bool HasLoaded { get; private set; }
    public bool FeatureDisabled { get; private set; }
    public string? Error { get; private set; }
    public async Task LoadAsync(int page = 1)
    {
        var ticket = ++generation; IsLoading = true; Error = null; FeatureDisabled = false; Changed?.Invoke();
        try
        {
            var response = await client.ListAsync(Math.Max(1, page));
            if (disposed || ticket != generation) return;
            if (response is null) throw new InvalidDataException();
            Items = response.Items; Page = response.Page; HasNext = (long)Page * response.PageSize < response.TotalCount; HasLoaded = true;
        }
        catch (Exception exception)
        {
            if (disposed || ticket != generation) return;
            Items = []; HasNext = false; FeatureDisabled = CargoNoticePresentation.Disabled(exception);
            Error = FeatureDisabled ? null : CargoNoticePresentation.Error(exception);
        }
        finally { if (!disposed && ticket == generation) { IsLoading = false; Changed?.Invoke(); } }
    }
    public void Dispose() { disposed = true; generation++; }
}
