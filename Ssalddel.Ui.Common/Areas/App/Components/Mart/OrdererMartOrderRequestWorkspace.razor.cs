using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Mart;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Mart;

public partial class OrdererMartOrderRequestWorkspace
{
    private bool _initialized;
    private bool? _lastAuthenticationStage;

    [Parameter]
    public long? ProductId { get; set; }

    [Parameter]
    public Guid? RequestId { get; set; }

    [Parameter]
    public Guid? ApplicationPrivacyConsentEvidenceId { get; set; }

    [Parameter]
    public string ApplicationSourceCode { get; set; } = string.Empty;

    [Parameter]
    public string CatalogHref { get; set; } = MartProductPageRoutes.Root;

    [Parameter]
    public EventCallback<Guid?> RequestSelected { get; set; }

    [Parameter]
    public EventCallback<bool> AuthenticationModeChanged { get; set; }

    [Parameter]
    public Func<Task<bool>>? BeforeSubmit { get; set; }

    private 주문자앱인증ViewModel Authentication => ViewModel.인증;
    private 마트공개상품상세ViewModel Product => ViewModel.상품;
    private 마트주문작성ViewModel Writer => ViewModel.작성;
    private 마트주문요청상세ViewModel RequestDetail => ViewModel.요청상세;

    private bool AuthenticationStage
        => (ProductId.HasValue || RequestId.HasValue)
           && (Authentication.처리중 || !Authentication.초기화됨 || !Authentication.로그인됨);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var stage = AuthenticationStage;
        if (_lastAuthenticationStage != stage)
        {
            _lastAuthenticationStage = stage;
            await AuthenticationModeChanged.InvokeAsync(stage);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await InitializeAsync();
        _initialized = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized)
        {
            return;
        }

        var tasks = new List<Task>();
        if (ProductId is long productId && Product.요청ProductId != productId)
        {
            Writer.새요청준비();
            tasks.Add(Product.조회Async(productId));
        }
        else if (ProductId is null && Product.요청ProductId.HasValue)
        {
            Product.선택해제();
        }

        if (Authentication.로그인됨 && RequestId is Guid requestId && RequestDetail.요청Id != requestId)
        {
            tasks.Add(RequestDetail.조회Async(requestId));
        }
        else if (RequestId is null && RequestDetail.요청Id.HasValue)
        {
            RequestDetail.선택해제();
        }

        await Task.WhenAll(tasks);
    }

    private async Task InitializeAsync()
    {
        var productTask = ProductId is long productId
            ? Product.조회Async(productId)
            : Task.FromResult(true);
        var authenticationTask = Authentication.초기화됨
            ? Task.FromResult(true)
            : Authentication.복원Async();
        await Task.WhenAll(productTask, authenticationTask);

        if (Authentication.로그인됨 && RequestId is Guid requestId)
        {
            await RequestDetail.조회Async(requestId);
        }
    }

    private async Task LoginAsync(공통로그인요청 request)
    {
        if (await Authentication.로그인Async(request.UserNameOrEmail, request.Password)
            && RequestId is Guid requestId)
        {
            await RequestDetail.조회Async(requestId);
        }
    }

    private async Task LogoutAsync()
    {
        await Authentication.로그아웃Async();
        RequestDetail.선택해제();
        Writer.새요청준비();
    }

    private Task RetryProductAsync(long productId)
        => Product.조회Async(productId);

    private Task RetryRequestAsync(Guid requestId)
        => RequestDetail.조회Async(requestId);

    private async Task SubmitAsync()
    {
        if (BeforeSubmit is not null && !await BeforeSubmit())
        {
            return;
        }

        Writer.신청개인정보동의증적Id = ApplicationPrivacyConsentEvidenceId;
        Writer.신청출처Code = ApplicationSourceCode;
        if (Product.상세 is not { 판매가능여부: true } product
            || !await Writer.등록Async(product.Id)
            || Writer.등록응답 is not { } response)
        {
            return;
        }

        if (RequestSelected.HasDelegate)
        {
            await RequestSelected.InvokeAsync(response.주문요청Id);
        }

        await RequestDetail.조회Async(response.주문요청Id);
    }
}
