using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 국내농수산가격조회ViewModel(
    I농수산공공데이터Client dataClient) : 농수산가격조회ViewModelBase
{
    private IReadOnlyList<AgriculturalFisheriesItemResponse> _품목 = 농수산가격기본Catalog.국내품목;
    private string _선택HsCode = "080810";
    private AgriculturalFisheriesDomesticPriceResponse? _응답;

    public IReadOnlyList<AgriculturalFisheriesItemResponse> 품목
    {
        get => _품목;
        private set => SetProperty(ref _품목, value);
    }

    public string 선택HsCode
    {
        get => _선택HsCode;
        set
        {
            if (SetProperty(ref _선택HsCode, value))
            {
                OnPropertyChanged(nameof(선택품목명));
            }
        }
    }

    public AgriculturalFisheriesDomesticPriceResponse? 응답
    {
        get => _응답;
        private set => SetProperty(ref _응답, value);
    }

    public string 선택품목명
        => 품목.FirstOrDefault(item => item.HsPrefix == 선택HsCode)?.ProductName
           ?? 선택HsCode;

    public Task<bool> 품목초기화Async(CancellationToken cancellationToken = default)
        => 농수산공공데이터호출정책.초기화시도Async(
            async token =>
            {
                var catalog = await dataClient.국내품목조회Async(cancellationToken: token);
                품목 = catalog.Items
                    .Concat(농수산가격기본Catalog.국내품목)
                    .GroupBy(item => item.HsPrefix, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(item => item.CategoryLabel, StringComparer.Ordinal)
                    .ThenBy(item => item.ProductName, StringComparer.Ordinal)
                    .ToArray();
                OnPropertyChanged(nameof(선택품목명));
            },
            cancellationToken);

    public Task 조회Async(CancellationToken cancellationToken = default)
        => 조회실행Async(
            async token =>
            {
                응답 = null;
                응답 = await dataClient.국내가격조회Async(선택HsCode, cancellationToken: token);
            },
            "한국 가격 API에 연결하지 못했습니다.",
            cancellationToken);
}
