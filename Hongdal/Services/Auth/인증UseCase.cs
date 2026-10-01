using System.Security.Claims;
using FluentResults;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.ViewSettings;
using Hongdal.Security;
using Hongdal.Services.Orderer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services;
using 홍달.Services.Audit;

namespace Hongdal.Services.Auth;

public interface I인증UseCase
{
    Task<Result<토큰응답>> 로그인Async(로그인요청? request, 인증요청Context context);
    Task<Result<기사회원가입응답>> 기사회원가입Async(기사회원가입요청? request);
    Task<Result<주문자회원가입응답>> 주문자회원가입Async(주문자회원가입요청? request);
    Task<Result<IReadOnlyList<가입인연후보항목응답>>> 가입온보딩인연후보조회Async(가입인연후보조회요청? request, CancellationToken cancellationToken);
    Task<Result<주문자집단자동배정응답>> 주문자집단온보딩Async(주문자집단온보딩요청? request, string? userId);
    Task<Result<토큰응답>> 토큰갱신Async(토큰갱신요청? request);
}

[HongdalUseCaseActor(HongdalActor.Driver)]
[HongdalUseCaseActor(HongdalActor.Orderer)]
[HongdalUseCaseActor(HongdalActor.CommunityMember, HongdalUseCaseActorRole.Supporting)]
public sealed class 인증UseCase : I인증UseCase
{
    private const string TokenProvider = "HongdalAuth";
    private const string RefreshTokenHashName = "RefreshTokenHash";
    private const string RefreshTokenExpiresAtName = "RefreshTokenExpiresAt";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthTokenService _authTokenService;
    private readonly INtsBusinessRegistrationService _ntsBusinessRegistrationService;
    private readonly JwtOptions _jwtOptions;
    private readonly I사용자행위로그Service _activityLogService;
    private readonly I가입온보딩인연후보Service _가입온보딩인연후보Service;
    private readonly I주문자집단자동배정Service _ordererGroupAutoAssignmentService;

    public 인증UseCase(
        UserManager<ApplicationUser> userManager,
        AuthTokenService authTokenService,
        INtsBusinessRegistrationService ntsBusinessRegistrationService,
        IOptions<JwtOptions> jwtOptions,
        I사용자행위로그Service activityLogService,
        I가입온보딩인연후보Service 가입온보딩인연후보Service,
        I주문자집단자동배정Service ordererGroupAutoAssignmentService)
    {
        _userManager = userManager;
        _authTokenService = authTokenService;
        _ntsBusinessRegistrationService = ntsBusinessRegistrationService;
        _jwtOptions = jwtOptions.Value;
        _activityLogService = activityLogService;
        _가입온보딩인연후보Service = 가입온보딩인연후보Service;
        _ordererGroupAutoAssignmentService = ordererGroupAutoAssignmentService;
    }

    public async Task<Result<토큰응답>> 로그인Async(로그인요청? request, 인증요청Context context)
    {
        if (request == null) return Result.Fail<토큰응답>("request body is required");
        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail)) return Result.Fail<토큰응답>("userNameOrEmail is required");
        if (string.IsNullOrWhiteSpace(request.Password)) return Result.Fail<토큰응답>("password is required");

        var userNameOrEmail = request.UserNameOrEmail.Trim();
        var user = await 사용자조회Async(userNameOrEmail);
        if (user == null)
        {
            await 로그인로그기록Async(userNameOrEmail, null, false, "UserNotFound", "아이디 또는 비밀번호가 올바르지 않습니다.", context);
            return 인증실패<토큰응답>("아이디 또는 비밀번호가 올바르지 않습니다.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await 로그인로그기록Async(userNameOrEmail, user, false, "PasswordInvalid", "아이디 또는 비밀번호가 올바르지 않습니다.", context);
            return 인증실패<토큰응답>("아이디 또는 비밀번호가 올바르지 않습니다.");
        }

        var response = await 토큰발급Async(user);
        await 로그인로그기록Async(userNameOrEmail, user, true, string.Empty, string.Empty, context);
        return response;
    }

    public async Task<Result<기사회원가입응답>> 기사회원가입Async(기사회원가입요청? request)
    {
        if (request == null) return Result.Fail<기사회원가입응답>("request body is required");
        if (string.IsNullOrWhiteSpace(request.UserName)) return Result.Fail<기사회원가입응답>("userName is required");
        if (string.IsNullOrWhiteSpace(request.Email)) return Result.Fail<기사회원가입응답>("email is required");
        if (string.IsNullOrWhiteSpace(request.Password)) return Result.Fail<기사회원가입응답>("password is required");
        if (string.IsNullOrWhiteSpace(request.BusinessRegistrationNumber)) return Result.Fail<기사회원가입응답>("businessRegistrationNumber is required");

        var businessCheck = await _ntsBusinessRegistrationService.CheckStatusAsync(request.BusinessRegistrationNumber.Trim());
        if (!businessCheck.IsValid)
        {
            return Result.Fail<기사회원가입응답>([
                "사업자등록번호를 확인할 수 없습니다.",
                businessCheck.Message,
                businessCheck.BusinessRegistrationNumber
            ]);
        }

        var duplicate = await 중복사용자검증Async(request.UserName, request.Email);
        if (duplicate.IsFailed)
        {
            return Result.Fail<기사회원가입응답>(duplicate.Errors);
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            BusinessRegistrationNumber = businessCheck.BusinessRegistrationNumber
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result.Fail<기사회원가입응답>(["회원가입에 실패했습니다.", .. createResult.Errors.Select(x => x.Description)]);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, 역할명.기사);
        if (!roleResult.Succeeded)
        {
            return Result.Fail<기사회원가입응답>(["기사 역할 부여에 실패했습니다.", .. roleResult.Errors.Select(x => x.Description)]);
        }

        return new 기사회원가입응답
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            BusinessRegistrationNumber = user.BusinessRegistrationNumber ?? string.Empty
        };
    }

    public async Task<Result<주문자회원가입응답>> 주문자회원가입Async(주문자회원가입요청? request)
    {
        if (request == null) return Result.Fail<주문자회원가입응답>("request body is required");
        if (string.IsNullOrWhiteSpace(request.UserName)) return Result.Fail<주문자회원가입응답>("userName is required");
        if (string.IsNullOrWhiteSpace(request.Email)) return Result.Fail<주문자회원가입응답>("email is required");
        if (string.IsNullOrWhiteSpace(request.Password)) return Result.Fail<주문자회원가입응답>("password is required");

        var duplicate = await 중복사용자검증Async(request.UserName, request.Email);
        if (duplicate.IsFailed)
        {
            return Result.Fail<주문자회원가입응답>(duplicate.Errors);
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result.Fail<주문자회원가입응답>(["회원가입에 실패했습니다.", .. createResult.Errors.Select(x => x.Description)]);
        }

        var assignedScope = _ordererGroupAutoAssignmentService.Resolve(request);
        if (assignedScope is not null)
        {
            var claimResult = await 주문자집단클레임저장Async(user, assignedScope);
            if (!claimResult.Succeeded)
            {
                return Result.Fail<주문자회원가입응답>(["주문자 집단 온보딩 저장에 실패했습니다.", .. claimResult.Errors.Select(x => x.Description)]);
            }
        }

        return new 주문자회원가입응답
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            OrdererGroupScope = assignedScope
        };
    }

    public async Task<Result<IReadOnlyList<가입인연후보항목응답>>> 가입온보딩인연후보조회Async(
        가입인연후보조회요청? request,
        CancellationToken cancellationToken)
    {
        if (request == null) return Result.Fail<IReadOnlyList<가입인연후보항목응답>>("request body is required");
        if (string.IsNullOrWhiteSpace(request.주문참조번호)) return Result.Fail<IReadOnlyList<가입인연후보항목응답>>("orderReference is required");

        return Result.Ok(await _가입온보딩인연후보Service.후보조회Async(request, cancellationToken));
    }

    public async Task<Result<주문자집단자동배정응답>> 주문자집단온보딩Async(주문자집단온보딩요청? request, string? userId)
    {
        if (request == null) return Result.Fail<주문자집단자동배정응답>("request body is required");
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 인증실패<주문자집단자동배정응답>("로그인 사용자 정보를 확인할 수 없습니다.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return 인증실패<주문자집단자동배정응답>("로그인 사용자 정보를 확인할 수 없습니다.");
        }

        var assignedScope = _ordererGroupAutoAssignmentService.Resolve(request);
        if (assignedScope is null)
        {
            return Result.Fail<주문자집단자동배정응답>("주소 또는 아파트 단지 정보로 주문자 집단을 계산할 수 없습니다.");
        }

        var claimResult = await 주문자집단클레임저장Async(user, assignedScope);
        if (!claimResult.Succeeded)
        {
            return Result.Fail<주문자집단자동배정응답>(["주문자 집단 온보딩 저장에 실패했습니다.", .. claimResult.Errors.Select(x => x.Description)]);
        }

        return assignedScope;
    }

    public async Task<Result<토큰응답>> 토큰갱신Async(토큰갱신요청? request)
    {
        if (request == null) return Result.Fail<토큰응답>("request body is required");
        if (string.IsNullOrWhiteSpace(request.UserId)) return Result.Fail<토큰응답>("userId is required");
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return Result.Fail<토큰응답>("refreshToken is required");

        var user = await _userManager.FindByIdAsync(request.UserId.Trim());
        if (user == null)
        {
            return 인증실패<토큰응답>("유효하지 않은 토큰입니다.");
        }

        var storedHash = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenHashName);
        var storedExpiresAt = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenExpiresAtName);

        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedExpiresAt))
        {
            return 인증실패<토큰응답>("유효하지 않은 토큰입니다.");
        }

        if (!DateTime.TryParse(storedExpiresAt, out var refreshTokenExpiresAtUtc) || refreshTokenExpiresAtUtc <= DateTime.UtcNow)
        {
            return 인증실패<토큰응답>("리프레시 토큰이 만료되었습니다.");
        }

        if (!_authTokenService.VerifyRefreshToken(request.RefreshToken, storedHash))
        {
            return 인증실패<토큰응답>("유효하지 않은 토큰입니다.");
        }

        return await 토큰발급Async(user);
    }

    private async Task<Result> 중복사용자검증Async(string userName, string email)
    {
        var existingUser = await _userManager.FindByNameAsync(userName.Trim());
        if (existingUser != null)
        {
            return 상태실패("이미 사용 중인 아이디입니다.", StatusCodes.Status409Conflict);
        }

        var existingEmail = await _userManager.FindByEmailAsync(email.Trim());
        if (existingEmail != null)
        {
            return 상태실패("이미 사용 중인 이메일입니다.", StatusCodes.Status409Conflict);
        }

        return Result.Ok();
    }

    private async Task<ApplicationUser?> 사용자조회Async(string userNameOrEmail)
    {
        var user = await _userManager.FindByNameAsync(userNameOrEmail);
        if (user != null)
        {
            return user;
        }

        return await _userManager.FindByEmailAsync(userNameOrEmail);
    }

    private async Task<Result<토큰응답>> 토큰발급Async(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _authTokenService.CreateAccessToken(user, roles, out var accessTokenExpiresAtUtc, await _userManager.GetClaimsAsync(user));
        var refreshToken = _authTokenService.GenerateRefreshToken();
        var refreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        await 리프레시토큰저장Async(user, refreshToken, refreshTokenExpiresAtUtc);

        return new 토큰응답
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Roles = roles.ToArray()
        };
    }

    private async Task<IdentityResult> 주문자집단클레임저장Async(
        ApplicationUser user,
        주문자집단자동배정응답 scope)
    {
        var existingClaims = await _userManager.GetClaimsAsync(user);
        var managedClaims = existingClaims
            .Where(x => x.Type is 주문자집단배송권ClaimTypes.ScopeKey
                or 주문자집단배송권ClaimTypes.DisplayName
                or 주문자집단배송권ClaimTypes.Basis
                or 주문자집단배송권ClaimTypes.AddressHint
                or 주문자집단배송권ClaimTypes.ApartmentComplexCode
                or 주문자집단배송권ClaimTypes.ApartmentComplexName)
            .ToArray();

        if (managedClaims.Length > 0)
        {
            var removeResult = await _userManager.RemoveClaimsAsync(user, managedClaims);
            if (!removeResult.Succeeded)
            {
                return removeResult;
            }
        }

        var claims = new List<Claim>
        {
            new(주문자집단배송권ClaimTypes.ScopeKey, scope.ScopeKey),
            new(주문자집단배송권ClaimTypes.DisplayName, scope.DisplayName),
            new(주문자집단배송권ClaimTypes.Basis, scope.Basis),
            new(주문자집단배송권ClaimTypes.AddressHint, scope.AddressHint)
        };

        if (!string.IsNullOrWhiteSpace(scope.ApartmentComplexCode))
        {
            claims.Add(new Claim(주문자집단배송권ClaimTypes.ApartmentComplexCode, scope.ApartmentComplexCode));
        }

        if (!string.IsNullOrWhiteSpace(scope.ApartmentComplexName))
        {
            claims.Add(new Claim(주문자집단배송권ClaimTypes.ApartmentComplexName, scope.ApartmentComplexName));
        }

        return await _userManager.AddClaimsAsync(user, claims);
    }

    private async Task 리프레시토큰저장Async(ApplicationUser user, string refreshToken, DateTime refreshTokenExpiresAtUtc)
    {
        var refreshTokenHash = _authTokenService.HashRefreshToken(refreshToken);
        await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenHashName, refreshTokenHash);
        await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenExpiresAtName, refreshTokenExpiresAtUtc.ToString("O"));
    }

    private async Task 로그인로그기록Async(
        string userNameOrEmail,
        ApplicationUser? user,
        bool isSuccess,
        string errorCode,
        string errorMessage,
        인증요청Context context)
    {
        var roles = user is null ? [] : await _userManager.GetRolesAsync(user);
        var roleName = roles.FirstOrDefault() ?? string.Empty;
        await _activityLogService.기록Async(new 사용자행위로그기록
        {
            AppKey = roleName switch
            {
                역할명.기사 => App식별자.DriverApp,
                역할명.화주 => App식별자.HongdalApp,
                역할명.관세사 => App식별자.HongdalAdmin,
                역할명.서버관리자 => App식별자.HongdalAdmin,
                _ => "Hongdal.Server"
            },
            UserId = user?.Id ?? string.Empty,
            UserName = user?.UserName ?? userNameOrEmail,
            RoleName = roleName,
            Email = user?.Email ?? userNameOrEmail,
            PhoneNumber = user?.PhoneNumber ?? string.Empty,
            ActionType = "Auth",
            ActionName = "Login",
            Route = context.Route,
            TraceId = context.TraceId,
            IsSuccess = isSuccess,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ClientIp = context.ClientIp,
            UserAgent = context.UserAgent,
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = $"{{\"userNameOrEmail\":\"{userNameOrEmail}\"}}"
        });
    }

    private static Result<T> 인증실패<T>(string message)
        => 상태실패<T>(message, StatusCodes.Status401Unauthorized);

    private static Result 상태실패(string message, int statusCode)
        => Result.Fail(new Error(message).WithMetadata("StatusCode", statusCode));

    private static Result<T> 상태실패<T>(string message, int statusCode)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", statusCode));
}

public sealed record 인증요청Context(
    string Route,
    string TraceId,
    string ClientIp,
    string UserAgent);
