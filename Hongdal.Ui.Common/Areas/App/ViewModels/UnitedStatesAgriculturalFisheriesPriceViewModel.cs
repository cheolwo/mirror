using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 미국농수산가격조회ViewModel(
    I농수산공공데이터Client dataClient) : 농수산가격조회ViewModelBase
{
    public static IReadOnlyList<string> 품목예시 => 농수산가격기본Catalog.미국품목예시;

    private string _품목명 = "APPLES";
    private string _조사Program = "SURVEY";
    private int _시작연도 = DateTime.UtcNow.Year - 3;
    private int _종료연도 = DateTime.UtcNow.Year;
    private 미국농수산가격조회응답? _응답;

    public string 품목명
    {
        get => _품목명;
        set
        {
            if (SetProperty(ref _품목명, value))
            {
                OnPropertyChanged(nameof(정규화품목명));
            }
        }
    }

    public string 조사Program
    {
        get => _조사Program;
        set => SetProperty(ref _조사Program, value);
    }

    public int 시작연도
    {
        get => _시작연도;
        set => SetProperty(ref _시작연도, value);
    }

    public int 종료연도
    {
        get => _종료연도;
        set => SetProperty(ref _종료연도, value);
    }

    public 미국농수산가격조회응답? 응답
    {
        get => _응답;
        private set => SetProperty(ref _응답, value);
    }

    public string 정규화품목명
        => string.IsNullOrWhiteSpace(품목명)
            ? "품목 미선택"
            : 품목명.Trim().ToUpperInvariant();

    public Task 조회Async(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(품목명))
        {
            응답 = null;
            오류메시지 = "미국 공식 품목명을 입력해 주세요.";
            return Task.CompletedTask;
        }

        return 조회실행Async(
            async token =>
            {
                응답 = null;
                응답 = await dataClient.미국가격조회Async(
                    정규화품목명,
                    조사Program,
                    시작연도,
                    종료연도,
                    cancellationToken: token);
            },
            "미국 가격 API에 연결하지 못했습니다.",
            cancellationToken);
    }
}
