using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common;

namespace Ssalddel.Services.Auth;

internal static class 소셜로그인OAuth
{
    internal const string FlowIdKey = "social-flow";
    internal const string ProviderKey = "social-provider";
    internal const string SurfaceKey = "social-surface";
    internal const string RoutePrefix = "/api/v1/auth/social";
    internal const string LinkRequiredCode = "social_link_required";

    internal static string Scheme(string providerId) => $"Ssalddel.Social.{providerId}";

    internal static string LoginPath(string? surface) => surface == "integrated" ? "/login" : "/roles/01/login";

    internal static bool IsValidFlowId(string? flowId)
        => flowId is { Length: 32 } && flowId.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static void AddSsalddelSocialLogin(this AuthenticationBuilder authentication, IConfiguration configuration)
    {
        var section = configuration.GetSection("SocialLogin");
        if (!Uri.TryCreate(section["PublicOrigin"], UriKind.Absolute, out var publicOrigin) ||
            publicOrigin.Scheme != Uri.UriSchemeHttps ||
            publicOrigin.AbsolutePath != "/" ||
            publicOrigin.UserInfo.Length != 0 ||
            publicOrigin.Query.Length != 0 ||
            publicOrigin.Fragment.Length != 0)
        {
            return;
        }

        Register(소셜로그인ProviderIds.Google, "Google",
            "https://accounts.google.com/o/oauth2/v2/auth", "https://oauth2.googleapis.com/token",
            "https://openidconnect.googleapis.com/v1/userinfo");
        Register(소셜로그인ProviderIds.Kakao, "Kakao",
            "https://kauth.kakao.com/oauth/authorize", "https://kauth.kakao.com/oauth/token",
            "https://kapi.kakao.com/v2/user/me");
        Register(소셜로그인ProviderIds.Naver, "Naver",
            "https://nid.naver.com/oauth2.0/authorize", "https://nid.naver.com/oauth2.0/token",
            "https://openapi.naver.com/v1/nid/me");

        authentication.Services.ConfigureExternalCookie(options =>
        {
            options.Cookie.Path = RoutePrefix;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
        });

        void Register(string providerId, string name, string authorization, string token, string userInfo)
        {
            var clientId = section[$"{name}:ClientId"];
            var clientSecret = section[$"{name}:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return;
            }

            authentication.AddOAuth<소셜로그인OAuthOptions, 소셜로그인OAuthHandler>(Scheme(providerId), name, options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.ProviderId = providerId;
                options.AuthorizationEndpoint = authorization;
                options.TokenEndpoint = token;
                options.UserInformationEndpoint = userInfo;
                options.CallbackPath = $"{RoutePrefix}/{providerId}/callback";
                options.PublicCallbackUri = new Uri(publicOrigin, options.CallbackPath.Value).AbsoluteUri;
                options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(10);
                options.BackchannelTimeout = TimeSpan.FromSeconds(15);
                options.BackchannelHttpHandler = new HttpClientHandler { AllowAutoRedirect = false };
                options.SaveTokens = false;
                options.UsePkce = providerId == 소셜로그인ProviderIds.Google;
                if (providerId == 소셜로그인ProviderIds.Google)
                {
                    options.Scope.Add("openid");
                }

                options.Events.OnRemoteFailure = context =>
                {
                    var surface = context.Properties?.GetString(SurfaceKey);
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.Redirect($"{LoginPath(surface)}?social=failed");
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }
    }
}

internal sealed class 소셜로그인OAuthOptions : OAuthOptions
{
    public string ProviderId { get; set; } = string.Empty;
    public string PublicCallbackUri { get; set; } = string.Empty;
}

internal sealed class 소셜로그인OAuthHandler(
    IOptionsMonitor<소셜로그인OAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : OAuthHandler<소셜로그인OAuthOptions>(options, logger, encoder)
{
    protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri)
        => base.BuildChallengeUrl(properties, Options.PublicCallbackUri);

    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = Options.ClientId,
            ["client_secret"] = Options.ClientSecret,
            ["code"] = context.Code,
            ["redirect_uri"] = Options.PublicCallbackUri,
            ["grant_type"] = "authorization_code"
        };
        if (context.Properties.Items.Remove(OAuthConstants.CodeVerifierKey, out var verifier) && verifier is not null)
        {
            parameters.Add("code_verifier", verifier);
        }
        if (Options.ProviderId == 소셜로그인ProviderIds.Naver)
        {
            // The base handler has already unprotected this state and checked its correlation cookie.
            parameters.Add("state", Request.Query["state"].ToString());
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await Backchannel.SendAsync(request, Context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            return OAuthTokenResponse.Failed(new AuthenticationFailureException("소셜 로그인 토큰 발급이 거절됐습니다."));
        }

        var payload = await response.Content.ReadAsStringAsync(Context.RequestAborted);
        return OAuthTokenResponse.Success(JsonDocument.Parse(payload));
    }

    protected override async Task<AuthenticationTicket> CreateTicketAsync(
        ClaimsIdentity identity, AuthenticationProperties properties, OAuthTokenResponse tokens)
    {
        var endpoint = Options.ProviderId == 소셜로그인ProviderIds.Kakao
            ? QueryHelpers.AddQueryString(Options.UserInformationEndpoint, "property_keys", "[]")
            : Options.UserInformationEndpoint;
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await Backchannel.SendAsync(request, Context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationFailureException("소셜 계정을 확인하지 못했습니다.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: Context.RequestAborted);
        var subject = ReadSubject(document.RootElement);
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255)
        {
            throw new AuthenticationFailureException("소셜 계정 식별자가 유효하지 않습니다.");
        }

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
        return new AuthenticationTicket(new ClaimsPrincipal(identity), properties, Scheme.Name);
    }

    private string? ReadSubject(JsonElement profile)
    {
        switch (Options.ProviderId)
        {
            case 소셜로그인ProviderIds.Google:
                return profile.TryGetProperty("sub", out var sub) && sub.ValueKind == JsonValueKind.String
                    ? sub.GetString() : null;
            case 소셜로그인ProviderIds.Kakao:
                return profile.TryGetProperty("id", out var kakaoId) && kakaoId.ValueKind == JsonValueKind.Number
                    ? kakaoId.GetRawText() : null;
            case 소셜로그인ProviderIds.Naver:
                return profile.TryGetProperty("response", out var account) && account.ValueKind == JsonValueKind.Object &&
                    account.TryGetProperty("id", out var naverId) && naverId.ValueKind == JsonValueKind.String
                    ? naverId.GetString() : null;
            default:
                return null;
        }
    }
}
