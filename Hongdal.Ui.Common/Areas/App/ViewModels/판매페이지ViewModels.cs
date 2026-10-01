using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Sales;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 판매페이지작성ViewModel(I판매페이지Client client) : ObservableObject
{
    private 판매페이지초안생성요청 _초안 = 새초안();
    private 판매페이지초안응답? _저장결과;
    private bool _처리중;
    private string? _오류메시지;
    private string _상태메시지 = "직접 입력하거나 외부 상품 상세를 참고해 시작할 수 있습니다.";

    public 판매페이지초안생성요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public 판매페이지초안응답? 저장결과
    {
        get => _저장결과;
        private set => SetProperty(ref _저장결과, value);
    }

    public bool 처리중
    {
        get => _처리중;
        private set => SetProperty(ref _처리중, value);
    }

    public string? 오류메시지
    {
        get => _오류메시지;
        private set => SetProperty(ref _오류메시지, value);
    }

    public string 상태메시지
    {
        get => _상태메시지;
        private set => SetProperty(ref _상태메시지, value);
    }

    public void 입력변경알림()
    {
        오류메시지 = null;
        OnPropertyChanged(nameof(초안));
    }

    public async Task<bool> 초안생성Async(CancellationToken cancellationToken = default)
    {
        if (처리중) return false;

        처리중 = true;
        오류메시지 = null;
        상태메시지 = string.IsNullOrWhiteSpace(초안.Amazon상품Url)
            ? "입력한 내용으로 판매 페이지 초안을 저장하고 있습니다."
            : "외부 상품 상세를 참고한 뒤 Hongdal 판매 페이지 초안을 만들고 있습니다.";

        try
        {
            저장결과 = await client.초안생성Async(초안, cancellationToken)
                ?? throw new InvalidOperationException("판매 페이지 초안 생성 응답이 비어 있습니다.");
            상태메시지 = "초안을 저장했습니다. 실제 주문을 받기 전 입고상품 기반 판매상품 연결과 검수가 필요합니다.";
            return true;
        }
        catch (Exception ex)
        {
            오류메시지 = ex.Message;
            상태메시지 = "입력 내용을 유지했습니다. 설정과 입력값을 확인한 뒤 다시 시도해 주세요.";
            return false;
        }
        finally
        {
            처리중 = false;
        }
    }

    public void 새로작성()
    {
        초안 = 새초안();
        저장결과 = null;
        오류메시지 = null;
        상태메시지 = "새 판매 페이지 내용을 입력해 주세요.";
    }

    private static 판매페이지초안생성요청 새초안()
        => new()
        {
            판매자유형 = 판매자유형코드.일반판매자,
            통화코드 = "KRW",
            최소주문수량 = 1,
            개별주문허용 = true,
            공동주문허용 = true,
            공동주문최소수량 = 10
        };
}
