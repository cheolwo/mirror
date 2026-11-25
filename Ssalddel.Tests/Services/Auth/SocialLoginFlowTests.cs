using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common;
using Ssalddel.Controllers.Common;
using Ssalddel.Security;
using Ssalddel.Services.Auth;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Audit;

namespace Ssalddel.Tests.Services.Auth;

public sealed class SocialLoginFlowTests
{
    private const string PublicOrigin = "https://public.test";
    private const string FlowId = "aabbccdd11223344556677889900aabb";
    private const string Password = "Valid123!";
    private const string Subject = "provider-verified-subject";

    [Theory]
    [InlineData("google", "Google", "accounts.google.com", """{"sub":"provider-verified-subject"}""", Subject)]
    [InlineData("kakao", "Kakao", "kauth.kakao.com", """{"id":9007199254740993}""", "9007199254740993")]
    [InlineData("naver", "Naver", "nid.naver.com", """{"response":{"id":"provider-verified-subject"}}""", Subject)]
    public async Task OAuth_uses_fixed_callback_and_only_accepts_its_correlated_browser(
        string provider, string section, string authorizationHost, string profile, string subject)
    {
        var scheme = 소셜로그인OAuth.Scheme(provider);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        var clock = new TestClock();
        services.AddAuthentication()
            .AddCookie(IdentityConstants.ExternalScheme, options => options.TimeProvider = clock)
            .AddSsalddelSocialLogin(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SocialLogin:PublicOrigin"] = PublicOrigin,
                    [$"SocialLogin:{section}:ClientId"] = "synthetic-client",
                    [$"SocialLogin:{section}:ClientSecret"] = "synthetic-secret"
                }).Build());
        var backchannel = new OAuthBackchannel(profile);
        services.Configure<소셜로그인OAuthOptions>(scheme, options => options.BackchannelHttpHandler = backchannel);
        await using var serviceProvider = services.BuildServiceProvider();

        await using var startScope = serviceProvider.CreateAsyncScope();
        var start = RequestContext(startScope.ServiceProvider);
        var controller = new 인증Controller(null!) { ControllerContext = new ControllerContext { HttpContext = start } };
        var schemes = start.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var providers = Assert.IsType<OkObjectResult>(await controller.소셜로그인제공자(schemes));
        Assert.Equal(new[] { provider }, Assert.IsType<string[]>(providers.Value));
        Assert.IsType<NotFoundResult>(await controller.소셜로그인시작("unconfigured", "community", FlowId, schemes));
        Assert.IsType<BadRequestResult>(await controller.소셜로그인시작(provider, "https://outside.test", FlowId, schemes));
        var challenge = Assert.IsType<ChallengeResult>(await controller.소셜로그인시작(provider, "community", FlowId, schemes));
        Assert.Equal(scheme, Assert.Single(challenge.AuthenticationSchemes));
        await start.ChallengeAsync(scheme, challenge.Properties);

        var authorize = new Uri(start.Response.Headers.Location.ToString());
        var query = QueryHelpers.ParseQuery(authorize.Query);
        var callback = $"{PublicOrigin}/api/v1/auth/social/{provider}/callback";
        Assert.Equal(authorizationHost, authorize.Host);
        Assert.Equal(callback, query["redirect_uri"].ToString());
        Assert.NotEqual(FlowId, query["state"].ToString());
        Assert.Equal(provider == "google", query.ContainsKey("code_challenge"));
        Assert.False(query.ContainsKey("client_secret"));
        var correlation = CookieHeader(start.Response.Headers.SetCookie);

        await using var rejectedScope = serviceProvider.CreateAsyncScope();
        var rejected = Callback(rejectedScope.ServiceProvider, provider, query["state"].ToString());
        await RunRemoteHandlerAsync(rejected, scheme);
        Assert.Equal("/roles/01/login?social=failed", rejected.Response.Headers.Location.ToString());
        Assert.Equal(0, backchannel.Calls);

        await using var callbackScope = serviceProvider.CreateAsyncScope();
        var accepted = Callback(callbackScope.ServiceProvider, provider, query["state"].ToString());
        accepted.Request.Headers.Cookie = correlation;
        await RunRemoteHandlerAsync(accepted, scheme);
        Assert.Equal("/roles/01/login?social=complete", accepted.Response.Headers.Location.ToString());
        Assert.Equal(callback, backchannel.TokenRequest["redirect_uri"].ToString());
        Assert.Equal(provider == "naver", backchannel.TokenRequest.ContainsKey("state"));
        if (provider == "naver")
        {
            Assert.Equal(query["state"], backchannel.TokenRequest["state"]);
        }
        Assert.Equal("Bearer synthetic-provider-token", backchannel.ProfileAuthorization);
        var cookieOptions = serviceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ExternalScheme);
        var externalCookie = CookieHeader(accepted.Response.Headers.SetCookie.Where(value =>
            value?.StartsWith($"{cookieOptions.Cookie.Name}=", StringComparison.Ordinal) == true));

        await using var finishScope = serviceProvider.CreateAsyncScope();
        var finish = RequestContext(finishScope.ServiceProvider);
        finish.Request.Headers.Cookie = externalCookie;
        var ticket = await finish.AuthenticateAsync(IdentityConstants.ExternalScheme);
        Assert.True(ticket.Succeeded);
        Assert.Equal(subject, ticket.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(FlowId, ticket.Properties!.GetString(소셜로그인OAuth.FlowIdKey));
        Assert.Null(ticket.Properties.GetTokenValue("access_token"));
        controller.ControllerContext.HttpContext = finish;
        Assert.IsType<BadRequestResult>(await controller.소셜로그인완료(new(new string('c', 32), null, null), schemes));

        clock.Now += TimeSpan.FromMinutes(6);
        await using var expiredScope = serviceProvider.CreateAsyncScope();
        var expired = RequestContext(expiredScope.ServiceProvider);
        expired.Request.Headers.Cookie = externalCookie;
        controller.ControllerContext.HttpContext = expired;
        Assert.IsType<BadRequestResult>(await controller.소셜로그인완료(new(FlowId, null, null), schemes));
    }

    [Theory]
    [InlineData(역할명.커뮤니티회원, false, 200)]
    [InlineData(역할명.서버관리자, false, 403)]
    [InlineData(역할명.관세사, false, 403)]
    [InlineData(역할명.커뮤니티회원, true, 401)]
    public async Task Social_identity_links_only_after_password_and_issues_tokens_for_eligible_accounts(
        string role, bool lockOut, int expectedStatus)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPersonalDataEncryptionService, TestEncryption>();
        services.AddDbContext<SsalddelContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SsalddelContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SsalddelContext>().Database.EnsureCreatedAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = new ApplicationUser { UserName = "synthetic-user", Email = "synthetic@example.test", LockoutEnabled = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        var jwt = Options.Create(new JwtOptions
        {
            Issuer = "synthetic-issuer",
            Audience = "synthetic-audience",
            SecretKey = "test-only-signing-key-that-is-longer-than-thirty-two-bytes",
            AccessTokenMinutes = 5,
            RefreshTokenDays = 1
        });
        var useCase = new 인증UseCase(users, new AuthTokenService(jwt), null!, jwt, new TestAudit(), null!, null!);
        var context = new 인증요청Context("/api/v1/auth/social/complete", "synthetic-trace", "", "");
        var missing = await useCase.소셜로그인Async("google", Subject, null, context);
        Assert.Equal(소셜로그인OAuth.LinkRequiredCode, Assert.Single(missing.Errors).Metadata["Code"]);
        var wrong = await useCase.소셜로그인Async("google", Subject,
            new 로그인요청 { UserNameOrEmail = user.Email!, Password = "Wrong123!" }, context);
        Assert.Equal(401, Assert.Single(wrong.Errors).Metadata["StatusCode"]);
        Assert.Empty(await users.GetLoginsAsync(user));
        if (lockOut)
        {
            Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded);
        }
        var result = await useCase.소셜로그인Async("google", Subject,
            new 로그인요청 { UserNameOrEmail = user.Email!, Password = Password }, context);

        if (expectedStatus != 200)
        {
            Assert.Equal(expectedStatus, Assert.Single(result.Errors).Metadata["StatusCode"]);
            Assert.Empty(await users.GetLoginsAsync(user));
            return;
        }

        Assert.Equal(user.Id, result.Value.UserId);
        var login = Assert.Single(await users.GetLoginsAsync(user));
        Assert.Equal("google", login.LoginProvider);
        Assert.Equal(user.Id, (await useCase.소셜로그인Async("google", Subject, null, context)).Value.UserId);
        var anotherUser = new ApplicationUser { UserName = "other-synthetic-user" };
        Assert.True((await users.CreateAsync(anotherUser, Password)).Succeeded);
        var raced = await useCase.소셜로그인Async("google", Subject,
            new 로그인요청 { UserNameOrEmail = anotherUser.UserName!, Password = Password }, context);
        Assert.Equal(409, Assert.Single(raced.Errors).Metadata["StatusCode"]);
        Assert.Empty(await users.GetLoginsAsync(anotherUser));

        var caseVariant = Subject.ToUpperInvariant();
        var unlinkedVariant = await useCase.소셜로그인Async("google", caseVariant, null, context);
        Assert.Equal(소셜로그인OAuth.LinkRequiredCode, Assert.Single(unlinkedVariant.Errors).Metadata["Code"]);
        var linkedVariant = await useCase.소셜로그인Async("google", caseVariant,
            new 로그인요청 { UserNameOrEmail = anotherUser.UserName!, Password = Password }, context);
        Assert.Equal(anotherUser.Id, linkedVariant.Value.UserId);
        var variantLogin = Assert.Single(await users.GetLoginsAsync(anotherUser));
        Assert.False(StringComparer.OrdinalIgnoreCase.Equals(login.ProviderKey, variantLogin.ProviderKey));
        Assert.Equal(anotherUser.Id, (await useCase.소셜로그인Async("google", caseVariant, null, context)).Value.UserId);
        Assert.Equal(user.Id, (await useCase.소셜로그인Async("google", Subject, null, context)).Value.UserId);

        Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded);
        var locked = await useCase.소셜로그인Async("google", Subject, null, context);
        Assert.Equal(401, Assert.Single(locked.Errors).Metadata["StatusCode"]);
    }

    private static DefaultHttpContext RequestContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Host = new HostString("private-proxy.test");
        context.Request.Scheme = "http";
        return context;
    }

    private static DefaultHttpContext Callback(IServiceProvider services, string provider, string state)
    {
        var context = RequestContext(services);
        context.Request.Path = $"/api/v1/auth/social/{provider}/callback";
        context.Request.QueryString = QueryString.Create(
            new Dictionary<string, string?> { ["code"] = "synthetic-code", ["state"] = state });
        return context;
    }

    private static async Task RunRemoteHandlerAsync(HttpContext context, string scheme)
    {
        var provider = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = Assert.IsAssignableFrom<IAuthenticationRequestHandler>(await provider.GetHandlerAsync(context, scheme));
        Assert.True(await handler.HandleRequestAsync());
    }

    private static string CookieHeader(IEnumerable<string?> setCookies)
        => Assert.Single(setCookies)!.Split(';', 2)[0];

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class OAuthBackchannel(string profile) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Dictionary<string, Microsoft.Extensions.Primitives.StringValues> TokenRequest { get; private set; } = [];
        public string? ProfileAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (request.Method == HttpMethod.Post)
            {
                TokenRequest = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                return Json("""{"access_token":"synthetic-provider-token","token_type":"Bearer"}""");
            }
            ProfileAuthorization = request.Headers.Authorization?.ToString();
            return Json(profile);
        }

        private static HttpResponseMessage Json(string value)
            => new(HttpStatusCode.OK) { Content = new StringContent(value) };
    }

    private sealed class TestEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class TestAudit : I사용자행위로그Service
    {
        public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
