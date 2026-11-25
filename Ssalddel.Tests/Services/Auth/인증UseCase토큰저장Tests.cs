using System.Globalization;
using System.Security.Claims;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common;
using Ssalddel.Security;
using Ssalddel.Services.Auth;
using 살뜰.Data;
using 살뜰.Services.Audit;

namespace Ssalddel.Tests.Services.Auth;

public sealed class 인증UseCase토큰저장Tests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task 로그인과갱신은_각토큰필드저장실패를_성공토큰으로반환하지않는다(bool refresh, int failAt)
    {
        using var fixture = new Fixture(failAt);

        var result = await fixture.RequestAsync(refresh);

        Assert.True(result.IsFailed);
        Assert.Throws<InvalidOperationException>(() => { _ = result.Value; });
        var error = Assert.Single(result.Errors);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.Metadata["StatusCode"]);
        Assert.Equal("인증 정보를 저장하지 못했습니다. 잠시 후 다시 시도해 주세요.", error.Message);
        Assert.DoesNotContain("internal-store-detail", error.Message);
        Assert.Equal(failAt, fixture.Users.TokenWriteNames.Count);
        Assert.Equal("RefreshTokenHash", fixture.Users.TokenWriteNames[0]);
        if (failAt == 2)
        {
            Assert.Equal("RefreshTokenExpiresAt", fixture.Users.TokenWriteNames[1]);
            // 첫 필드의 성공까지 롤백하거나 기존 토큰을 보존했다는 보장은 추가하지 않습니다.
            Assert.NotEqual(fixture.Users.InitialTokenHash, fixture.Users.StoredTokens["RefreshTokenHash"]);
        }
        else
            Assert.Equal(fixture.Users.InitialTokenHash, fixture.Users.StoredTokens["RefreshTokenHash"]);
        if (refresh)
            Assert.Empty(fixture.Audit.Entries);
        else
        {
            var entry = Assert.Single(fixture.Audit.Entries);
            Assert.False(entry.IsSuccess);
            Assert.Equal("ServerError", entry.ErrorCode);
            Assert.DoesNotContain("internal-store-detail", entry.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 두필드저장이성공하면_실제로저장한토큰과만료시각을반환한다(bool refresh)
    {
        using var fixture = new Fixture(failAt: 0);

        var result = await fixture.RequestAsync(refresh);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
        Assert.True(fixture.Tokens.VerifyRefreshToken(
            result.Value.RefreshToken, fixture.Users.StoredTokens["RefreshTokenHash"]));
        Assert.Equal(result.Value.RefreshTokenExpiresAtUtc, DateTime.Parse(
            fixture.Users.StoredTokens["RefreshTokenExpiresAt"], CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind));
        Assert.Equal(new[] { "RefreshTokenHash", "RefreshTokenExpiresAt" }, fixture.Users.TokenWriteNames);
        if (!refresh)
            Assert.True(Assert.Single(fixture.Audit.Entries).IsSuccess);
    }

    private sealed class Fixture : IDisposable
    {
        private const string OriginalRefresh = "synthetic-original-refresh";
        private readonly 인증UseCase useCase;
        internal FailingTokenUserManager Users { get; }
        internal RecordingAudit Audit { get; } = new();
        internal AuthTokenService Tokens { get; }

        internal Fixture(int failAt)
        {
            var options = Options.Create(new JwtOptions
            {
                Issuer = "Ssalddel.Tests", Audience = "Ssalddel.Tests.Client",
                SecretKey = "test-only-signing-key-that-is-longer-than-thirty-two-bytes",
                AccessTokenMinutes = 5, RefreshTokenDays = 1
            });
            Tokens = new AuthTokenService(options);
            Users = new FailingTokenUserManager(failAt, Tokens.HashRefreshToken(OriginalRefresh));
            useCase = new 인증UseCase(Users, Tokens, null!, options, Audit, null!, null!);
        }

        internal Task<Result<토큰응답>> RequestAsync(bool refresh)
            => refresh
                ? useCase.토큰갱신Async(new 토큰갱신요청 { UserId = Users.User.Id, RefreshToken = OriginalRefresh })
                : useCase.로그인Async(new 로그인요청 { UserNameOrEmail = "synthetic-user", Password = "synthetic-password" },
                    new 인증요청Context("/api/v1/auth/login", "synthetic-trace", string.Empty, string.Empty));

        public void Dispose() => Users.Dispose();
    }

    private sealed class FailingTokenUserManager : UserManager<ApplicationUser>
    {
        private readonly int failAt;
        internal ApplicationUser User { get; } = new() { Id = "synthetic-user", UserName = "synthetic-user" };
        internal Dictionary<string, string> StoredTokens { get; }
        internal string InitialTokenHash { get; }
        internal List<string> TokenWriteNames { get; } = [];

        internal FailingTokenUserManager(int failAt, string originalHash)
            : base(new UnusedUserStore(), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
                [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
                new EmptyServices(), NullLogger<UserManager<ApplicationUser>>.Instance)
        {
            this.failAt = failAt;
            InitialTokenHash = originalHash;
            StoredTokens = new()
            {
                ["RefreshTokenHash"] = originalHash,
                ["RefreshTokenExpiresAt"] = DateTime.UtcNow.AddDays(1).ToString("O")
            };
        }

        public override Task<ApplicationUser?> FindByNameAsync(string userName) => Task.FromResult<ApplicationUser?>(User);
        public override Task<ApplicationUser?> FindByIdAsync(string userId) => Task.FromResult<ApplicationUser?>(User);
        public override Task<bool> CheckPasswordAsync(ApplicationUser user, string password) => Task.FromResult(true);
        public override Task<IList<string>> GetRolesAsync(ApplicationUser user) => Task.FromResult<IList<string>>(["화주"]);
        public override Task<IList<Claim>> GetClaimsAsync(ApplicationUser user) => Task.FromResult<IList<Claim>>([]);
        public override Task<string?> GetAuthenticationTokenAsync(ApplicationUser user, string loginProvider, string tokenName)
            => Task.FromResult(StoredTokens.GetValueOrDefault(tokenName));
        public override Task<IdentityResult> SetAuthenticationTokenAsync(
            ApplicationUser user, string loginProvider, string tokenName, string? tokenValue)
        {
            TokenWriteNames.Add(tokenName);
            if (TokenWriteNames.Count == failAt)
                return Task.FromResult(IdentityResult.Failed(new IdentityError
                    { Code = "ConcurrencyFailure", Description = "internal-store-detail" }));
            StoredTokens[tokenName] = tokenValue!;
            return Task.FromResult(IdentityResult.Success);
        }
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class UnusedUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingAudit : I사용자행위로그Service
    {
        internal List<사용자행위로그기록> Entries { get; } = [];
        public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
