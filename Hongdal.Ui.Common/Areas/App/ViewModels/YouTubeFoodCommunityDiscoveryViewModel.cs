using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Content;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class YouTubeFoodCommunityDiscoveryViewModel : ObservableObject
{
    public const string AllFilter = "all";

    private readonly YouTubeFoodCommunityDiscoveryService _service;
    private IReadOnlyList<YouTube음식커뮤니티공유후보Dto> _items = [];
    private string _selectedCountryCode = AllFilter;
    private string _selectedCandidateType = AllFilter;
    private bool _isLoading;
    private bool _isExpanded = true;
    private string? _errorMessage;

    public YouTubeFoodCommunityDiscoveryViewModel(YouTubeFoodCommunityDiscoveryService service)
    {
        _service = service;
    }

    public IReadOnlyList<YouTube음식커뮤니티공유후보Dto> Items
    {
        get => _items;
        private set
        {
            if (SetProperty(ref _items, value))
            {
                OnPropertyChanged(nameof(VisibleItems));
                OnPropertyChanged(nameof(CountryCodes));
                OnPropertyChanged(nameof(CandidateTypes));
                OnPropertyChanged(nameof(HasItems));
            }
        }
    }

    public IReadOnlyList<YouTube음식커뮤니티공유후보Dto> VisibleItems
        => Items
            .Where(item => SelectedCountryCode == AllFilter
                           || string.Equals(
                               item.채널국가코드,
                               SelectedCountryCode,
                               StringComparison.OrdinalIgnoreCase))
            .Where(item => SelectedCandidateType == AllFilter
                           || string.Equals(
                               item.후보유형,
                               SelectedCandidateType,
                               StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.영상게시일시Utc)
            .ToArray();

    public IReadOnlyList<string> CountryCodes
        => Items
            .Select(item => item.채널국가코드)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<string> CandidateTypes
        => Items
            .Select(item => item.후보유형)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public string SelectedCountryCode
    {
        get => _selectedCountryCode;
        set
        {
            var normalized = NormalizeFilter(value);
            if (SetProperty(ref _selectedCountryCode, normalized))
            {
                OnPropertyChanged(nameof(VisibleItems));
            }
        }
    }

    public string SelectedCandidateType
    {
        get => _selectedCandidateType;
        set
        {
            var normalized = NormalizeFilter(value);
            if (SetProperty(ref _selectedCandidateType, normalized))
            {
                OnPropertyChanged(nameof(VisibleItems));
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool HasItems => Items.Count > 0;

    public async Task LoadAsync(
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        if (IsLoading || (!forceRefresh && HasItems))
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Items = await _service.GetApprovedCandidatesAsync(cancellationToken: cancellationToken);
            EnsureSelectedFiltersExist();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            Items = [];
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "승인된 음식 영상을 불러오지 못했습니다.";
        }
        catch (Exception)
        {
            ErrorMessage = "음식 영상 응답을 확인하지 못했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SelectCountry(string? countryCode)
        => SelectedCountryCode = countryCode ?? AllFilter;

    public void SelectCandidateType(string? candidateType)
        => SelectedCandidateType = candidateType ?? AllFilter;

    public PlatformCommunityPostDraft CreateShareDraft(YouTube음식커뮤니티공유후보Dto candidate)
        => YouTubeFoodCommunityShareDraftFactory.Create(candidate);

    private static string NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? AllFilter : value.Trim();

    private void EnsureSelectedFiltersExist()
    {
        if (SelectedCountryCode != AllFilter
            && !CountryCodes.Contains(SelectedCountryCode, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCountryCode = AllFilter;
        }

        if (SelectedCandidateType != AllFilter
            && !CandidateTypes.Contains(SelectedCandidateType, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCandidateType = AllFilter;
        }
    }
}
