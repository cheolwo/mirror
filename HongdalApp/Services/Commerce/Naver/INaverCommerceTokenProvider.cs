namespace HongdalApp.Services.Commerce.Naver;

public interface INaverCommerceTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
