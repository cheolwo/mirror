using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum 농수산가격비교Section
{
    비교,
    국내,
    미국,
    호주,
    출처
}

public sealed class 농수산가격비교PageViewModel : PageViewModelBase
{
    private readonly I농수산공공데이터Client _dataClient;
    private AgriculturalFisheriesInformationOverviewResponse? _overview;
    private 농수산가격비교Section _activeSection = 농수산가격비교Section.비교;
    private string? _initializationMessage;

    public 농수산가격비교PageViewModel(
        I농수산공공데이터Client dataClient,
        국내농수산가격조회ViewModel 국내,
        미국농수산가격조회ViewModel 미국,
        호주농수산가격조회ViewModel 호주)
    {
        _dataClient = dataClient;
        this.국내 = 하위ViewModel등록(국내, 수명소유: true);
        this.미국 = 하위ViewModel등록(미국, 수명소유: true);
        this.호주 = 하위ViewModel등록(호주, 수명소유: true);
    }

    public 국내농수산가격조회ViewModel 국내 { get; }

    public 미국농수산가격조회ViewModel 미국 { get; }

    public 호주농수산가격조회ViewModel 호주 { get; }

    public 농수산가격비교Section ActiveSection
    {
        get => _activeSection;
        private set
        {
            if (SetProperty(ref _activeSection, value))
            {
                OnPropertyChanged(nameof(CurrentSectionEyebrow));
                OnPropertyChanged(nameof(CurrentSectionTitle));
            }
        }
    }

    public string? InitializationMessage
    {
        get => _initializationMessage;
        private set => SetProperty(ref _initializationMessage, value);
    }

    public bool IsLoading => 처리중 || 국내.처리중 || 미국.처리중 || 호주.처리중;

    public IReadOnlyList<AgriculturalFisheriesDataSourceResponse> VisibleSources
        => _overview?.DataSources.Count > 0
            ? _overview.DataSources
            : 농수산가격기본Catalog.출처;

    public string CurrentSectionEyebrow => ActiveSection switch
    {
        농수산가격비교Section.국내 => "KR · aT",
        농수산가격비교Section.미국 => "US · USDA NASS",
        농수산가격비교Section.호주 => "AU · ABS",
        농수산가격비교Section.출처 => "SOURCE",
        _ => "COMPARE"
    };

    public string CurrentSectionTitle => ActiveSection switch
    {
        농수산가격비교Section.국내 => "한국 농수산물 가격",
        농수산가격비교Section.미국 => "미국 농수산물 가격",
        농수산가격비교Section.호주 => "호주 식품 가격지수",
        농수산가격비교Section.출처 => "공식 데이터 출처",
        _ => "한국·미국·호주 가격 비교"
    };

    public Task SelectSectionAsync(HongdalCardinalNavigationOption option)
    {
        ActiveSection = option.TrigramKey switch
        {
            "li" => 농수산가격비교Section.국내,
            "dui" => 농수산가격비교Section.미국,
            "kan" => 농수산가격비교Section.호주,
            _ => 농수산가격비교Section.출처
        };
        return Task.CompletedTask;
    }

    public Task ShowComparisonAsync()
    {
        ActiveSection = 농수산가격비교Section.비교;
        return Task.CompletedTask;
    }

    public Task LoadDomesticAsync(CancellationToken cancellationToken = default)
        => 국내.조회Async(cancellationToken);

    public Task LoadUnitedStatesAsync(CancellationToken cancellationToken = default)
        => 미국.조회Async(cancellationToken);

    public Task LoadAustraliaAsync(CancellationToken cancellationToken = default)
        => 호주.조회Async(cancellationToken);

    public Task LoadComparisonAsync(CancellationToken cancellationToken = default)
        => Task.WhenAll(
            국내.조회Async(cancellationToken),
            미국.조회Async(cancellationToken),
            호주.조회Async(cancellationToken));

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(
            Overview초기화Async(cancellationToken),
            국내.품목초기화Async(cancellationToken),
            호주.Catalog초기화Async(cancellationToken));

        InitializationMessage = results.All(result => result)
            ? null
            : "일부 공공데이터 서버 연결 전에는 기본 품목과 조회 조건을 표시합니다.";
        OnPropertyChanged(nameof(VisibleSources));
        OnPropertyChanged(nameof(IsLoading));
    }

    private Task<bool> Overview초기화Async(CancellationToken cancellationToken)
        => 농수산공공데이터호출정책.초기화시도Async(
            async token => _overview = await _dataClient.개요조회Async(token),
            cancellationToken);
}
