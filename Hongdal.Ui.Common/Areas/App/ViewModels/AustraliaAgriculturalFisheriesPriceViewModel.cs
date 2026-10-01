using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 호주농수산가격조회ViewModel(
    I농수산공공데이터Client dataClient) : 농수산가격조회ViewModelBase
{
    private 호주농수산식품가격Catalog응답 _catalog = 농수산가격기본Catalog.호주Catalog;
    private string _선택IndexCode = 호주식품가격지수Codes.FoodAndNonAlcoholicBeverages;
    private string _선택MeasureCode = 호주식품가격지수측정Codes.IndexNumber;
    private string _선택RegionCode = 호주식품가격지수지역Codes.Australia;
    private 호주농수산식품가격조회응답? _응답;

    public 호주농수산식품가격Catalog응답 Catalog
    {
        get => _catalog;
        private set => SetProperty(ref _catalog, value);
    }

    public string 선택IndexCode
    {
        get => _선택IndexCode;
        set
        {
            if (SetProperty(ref _선택IndexCode, value))
            {
                OnPropertyChanged(nameof(선택Index명));
            }
        }
    }

    public string 선택MeasureCode
    {
        get => _선택MeasureCode;
        set => SetProperty(ref _선택MeasureCode, value);
    }

    public string 선택RegionCode
    {
        get => _선택RegionCode;
        set => SetProperty(ref _선택RegionCode, value);
    }

    public 호주농수산식품가격조회응답? 응답
    {
        get => _응답;
        private set => SetProperty(ref _응답, value);
    }

    public string 선택Index명
        => Catalog.Indexes.FirstOrDefault(item => item.Code == 선택IndexCode)?.Label
           ?? 선택IndexCode;

    public Task<bool> Catalog초기화Async(CancellationToken cancellationToken = default)
        => 농수산공공데이터호출정책.초기화시도Async(
            async token =>
            {
                var catalog = await dataClient.호주가격원천Catalog조회Async(token);
                if (catalog.Indexes.Count > 0
                    && catalog.Measures.Count > 0
                    && catalog.Regions.Count > 0)
                {
                    Catalog = catalog;
                    OnPropertyChanged(nameof(선택Index명));
                }
            },
            cancellationToken);

    public Task 조회Async(CancellationToken cancellationToken = default)
        => 조회실행Async(
            async token =>
            {
                응답 = null;
                응답 = await dataClient.호주식품가격지수조회Async(
                    new 호주농수산식품가격조회요청
                    {
                        IndexCode = 선택IndexCode,
                        MeasureCode = 선택MeasureCode,
                        RegionCode = 선택RegionCode,
                        MaxItems = 36
                    },
                    token);
            },
            "호주 가격지수 API에 연결하지 못했습니다.",
            cancellationToken);
}
