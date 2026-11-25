using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Localization;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Contracts.Common.ViewSettings;
using Ssalddel.Security;
using Ssalddel.Services.Orderer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 살뜰.Data;
using 살뜰.Services;
using 살뜰.Services.Audit;

namespace Ssalddel.Services.Auth;

public interface I인증UseCase
{
    Task<Result<토큰응답>> 로그인Async(로그인요청? request, 인증요청Context context);
    Task<Result<토큰응답>> 소셜로그인Async(string providerId, string subject, 로그인요청? credentials, 인증요청Context context);
    Task<Result<커뮤니티회원가입응답>> 커뮤니티회원가입Async(커뮤니티회원가입요청? request);
    Task<Result<기사회원가입응답>> 기사회원가입Async(기사회원가입요청? request);
    Task<Result<주문자회원가입응답>> 주문자회원가입Async(주문자회원가입요청? request);
    Task<Result<IReadOnlyList<가입친구후보항목응답>>> 가입온보딩친구후보조회Async(가입친구후보조회요청? request, CancellationToken cancellationToken);
    Task<Result<주문자집단자동배정응답>> 주문자집단온보딩Async(주문자집단온보딩요청? request, string? userId);
    Task<Result<토큰응답>> 토큰갱신Async(토큰갱신요청? request);
    Task<Result<표시언어설정응답>> 표시언어설정Async(표시언어설정요청? request, string? userId);
}

[SsalddelUseCaseActor(SsalddelActor.Driver)]
[SsalddelUseCaseActor(SsalddelActor.Orderer)]
[SsalddelUseCaseActor(SsalddelActor.CommunityMember, SsalddelUseCaseActorRole.Supporting)]
public sealed class 인증UseCase : I인증UseCase
{
    private const string TokenProvider = "SsalddelAuth";
    private const string RefreshTokenHashName = "RefreshTokenHash";
    private const string RefreshTokenExpiresAtName = "RefreshTokenExpiresAt";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthTokenService _authTokenService;
    private readonly INtsBusinessRegistrationService _ntsBusinessRegistrationService;
    private readonly JwtOptions _jwtOptions;
    private readonly I사용자행위로그Service _activityLogService;
    private readonly I가입온보딩친구후보Service _가입온보딩친구후보Service;
    private readonly I주문자집단자동배정Service _ordererGroupAutoAssignmentService;

    public 인증UseCase(
        UserManager<ApplicationUser> userManager,
        AuthTokenService authTokenService,
        INtsBusinessRegistrationService ntsBusinessRegistrationService,
        IOptions<JwtOptions> jwtOptions,
        I사용자행위로그Service activityLogService,
        I가입온보딩친구후보Service 가입온보딩친구후보Service,
        I주문자집단자동배정Service ordererGroupAutoAssignmentService)
    {
        _userManager = userManager;
        _authTokenService = authTokenService;
        _ntsBusinessRegistrationService = ntsBusinessRegistrationService;
        _jwtOptions = jwtOptions.Value;
        _activityLogService = activityLogService;
        _가입온보딩친구후보Service = 가입온보딩친구후보Service;
        _ordererGroupAutoAssignmentService = ordererGroupAutoAssignmentService;
    }

    public async Task<Result<토큰응답>> 로그인Async(로그인요청? request, 인증요청Context context)
    {
        if (request == null) return Result.Fail<토큰응답>("request body is required");
        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail)) return Result.Fail<토큰응답>("userNameOrEmail is required");
        if (string.IsNullOrWhiteSpace(request.Password)) return Result.Fail<토큰응답>("password is required");

        var userNameOrEmail = request.UserNameOrEmail.Trim();
        var verified = await 비밀번호검증Async(userNameOrEmail, request.Password, context);
        return verified.IsFailed
            ? Result.Fail<토큰응답>(verified.Errors)
            : await 로그인완료Async(verified.Value, userNameOrEmail, context);
    }

    public async Task<Result<토큰응답>> 소셜로그인Async(
        string providerId, string subject, 로그인요청? credentials, 인증요청Context context)
    {
        const string linkFailedMessage = "소셜 계정을 연결하지 못했습니다. 다시 로그인해 주세요.";
        // The database collation ignores case, but the provider's subject does not.
        var providerKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)));
        var linkedUser = await _userManager.FindByLoginAsync(providerId, providerKey);
        var user = linkedUser;
        if (credentials is not null)
        {
            var verified = await 비밀번호검증Async(credentials.UserNameOrEmail.Trim(), credentials.Password, context);
            if (verified.IsFailed)
            {
                return Result.Fail<토큰응답>(verified.Errors);
            }
            user = verified.Value;
            if (linkedUser is not null && linkedUser.Id != user.Id)
            {
                return 상태실패<토큰응답>(linkFailedMessage, StatusCodes.Status409Conflict);
            }
        }
        if (user is null)
        {
            return Result.Fail<토큰응답>(
                new Error("기존 살뜰 계정에 소셜 계정을 연결해 주세요.")
                    .WithMetadata("StatusCode", StatusCodes.Status409Conflict)
                    .WithMetadata("Code", 소셜로그인OAuth.LinkRequiredCode));
        }

        if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
        {
            return 인증실패<토큰응답>("현재 계정으로 소셜 로그인할 수 없습니다.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Any(role => role is 역할명.서버관리자 or 역할명.관세사))
        {
            await 로그인로그기록Async(providerId, user, false, "SocialRoleNotAllowed", "이 역할은 소셜 로그인을 사용할 수 없습니다.", context);
            return 상태실패<토큰응답>("이 역할은 소셜 로그인을 사용할 수 없습니다.", StatusCodes.Status403Forbidden);
        }

        if (linkedUser is null)
        {
            try
            {
                var linked = await _userManager.AddLoginAsync(user, new UserLoginInfo(providerId, providerKey, providerId));
                if (!linked.Succeeded)
                {
                    return 상태실패<토큰응답>(linkFailedMessage, StatusCodes.Status409Conflict);
                }
            }
            catch (DbUpdateException)
            {
                // The identity store enforces a unique provider/subject pair when callbacks race.
                return 상태실패<토큰응답>(linkFailedMessage, StatusCodes.Status409Conflict);
            }
        }

        return await 로그인완료Async(user, providerId, context, roles);
    }

    private async Task<Result<ApplicationUser>> 비밀번호검증Async(
        string userNameOrEmail, string password, 인증요청Context context)
    {
        var user = await 사용자조회Async(userNameOrEmail);
        if (user == null)
        {
            await 로그인로그기록Async(userNameOrEmail, null, false, "UserNotFound", "아이디 또는 비밀번호가 올바르지 않습니다.", context);
            return 인증실패<ApplicationUser>("아이디 또는 비밀번호가 올바르지 않습니다.");
        }

        if (_userManager.SupportsUserLockout)
        {
            if (!await _userManager.GetLockoutEnabledAsync(user))
            {
                var enableLockoutResult = await _userManager.SetLockoutEnabledAsync(user, true);
                if (!enableLockoutResult.Succeeded)
                {
                    await 로그인로그기록Async(
                        userNameOrEmail,
                        user,
                        false,
                        "LockoutStateUpdateFailed",
                        "로그인 보호 상태를 갱신하지 못했습니다.",
                        context);
                    return 인증실패<ApplicationUser>("아이디 또는 비밀번호가 올바르지 않습니다.");
                }
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                await 로그인로그기록Async(
                    userNameOrEmail,
                    user,
                    false,
                    "LockedOut",
                    "로그인 시도 제한 상태입니다.",
                    context);
                return 인증실패<ApplicationUser>("아이디 또는 비밀번호가 올바르지 않습니다.");
            }
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            var errorCode = "PasswordInvalid";
            if (_userManager.SupportsUserLockout)
            {
                var accessFailedResult = await _userManager.AccessFailedAsync(user);
                if (!accessFailedResult.Succeeded)
                {
                    errorCode = "AccessFailureNotRecorded";
                }
                else if (await _userManager.IsLockedOutAsync(user))
                {
                    errorCode = "LockedOut";
                }
            }

            await 로그인로그기록Async(userNameOrEmail, user, false, errorCode, "아이디 또는 비밀번호가 올바르지 않습니다.", context);
            return 인증실패<ApplicationUser>("아이디 또는 비밀번호가 올바르지 않습니다.");
        }

        if (_userManager.SupportsUserLockout)
        {
            var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
            if (!resetResult.Succeeded)
            {
                await 로그인로그기록Async(
                    userNameOrEmail,
                    user,
                    false,
                    "AccessFailureResetFailed",
                    "로그인 보호 상태를 초기화하지 못했습니다.",
                    context);
                return 인증실패<ApplicationUser>("아이디 또는 비밀번호가 올바르지 않습니다.");
            }
        }

        return Result.Ok(user);
    }

    private async Task<Result<토큰응답>> 로그인완료Async(
        ApplicationUser user, string identifier, 인증요청Context context, IList<string>? roles = null)
    {
        var response = await 토큰발급Async(user, roles);
        if (response.IsFailed)
        {
            await 로그인로그기록Async(identifier, user, false, "ServerError", "인증 정보를 저장하지 못했습니다.", context);
            return response;
        }
        await 로그인로그기록Async(identifier, user, true, string.Empty, string.Empty, context);
        return response;
    }

    public async Task<Result<표시언어설정응답>> 표시언어설정Async(
        표시언어설정요청? request,
        string? userId)
    {
        if (request is null)
        {
            return Result.Fail<표시언어설정응답>("request body is required");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return 인증실패<표시언어설정응답>("로그인 세션을 확인해 주세요.");
        }

        if (!DisplayLanguageCodes.TryNormalize(request.LanguageCode, out var languageCode))
        {
            return 상태실패<표시언어설정응답>(
                "현재 ko-KR과 en-US만 화면 언어로 선택할 수 있습니다.",
                StatusCodes.Status400BadRequest);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return 상태실패<표시언어설정응답>(
                "언어 설정을 저장할 사용자를 찾을 수 없습니다.",
                StatusCodes.Status404NotFound);
        }

        var claims = await _userManager.GetClaimsAsync(user);
        var existingClaims = claims
            .Where(claim => string.Equals(
                claim.Type,
                SsalddelDisplayLanguageClaimTypes.PreferredLanguage,
                StringComparison.Ordinal))
            .ToArray();
        if (existingClaims.Length > 0)
        {
            var removal = await _userManager.RemoveClaimsAsync(user, existingClaims);
            if (!removal.Succeeded)
            {
                return 상태실패<표시언어설정응답>(
                    "기존 화면 언어 설정을 갱신하지 못했습니다.",
                    StatusCodes.Status500InternalServerError);
            }
        }

        var addition = await _userManager.AddClaimAsync(
            user,
            new Claim(SsalddelDisplayLanguageClaimTypes.PreferredLanguage, languageCode));
        if (!addition.Succeeded)
        {
            return 상태실패<표시언어설정응답>(
                "화면 언어 설정을 저장하지 못했습니다.",
                StatusCodes.Status500InternalServerError);
        }

        return new 표시언어설정응답 { LanguageCode = languageCode };
    }

    [SsalddelCommunityV0Module(
        SsalddelCommunityV0ModuleKeys.Safety,
        SsalddelModuleKind.Application,
        "선택 회원가입의 최소 개인정보 동의를 검증하고 동의 버전과 시각을 계정에 기록",
        ReleaseStage = SsalddelCommunityV0ReleaseStages.SafetyAndOperations,
        Boundary = "회원가입을 거부해도 익명 허용 게시판 이용은 유지하며, 계정 생성에 필요한 최소 정보만 저장")]
    public async Task<Result<커뮤니티회원가입응답>> 커뮤니티회원가입Async(커뮤니티회원가입요청? request)
    {
        if (request == null) return Result.Fail<커뮤니티회원가입응답>("request body is required");
        if (string.IsNullOrWhiteSpace(request.UserName)) return Result.Fail<커뮤니티회원가입응답>("아이디를 입력해 주세요.");
        if (string.IsNullOrWhiteSpace(request.Email)) return Result.Fail<커뮤니티회원가입응답>("이메일을 입력해 주세요.");
        if (string.IsNullOrWhiteSpace(request.Password)) return Result.Fail<커뮤니티회원가입응답>("비밀번호를 입력해 주세요.");
        if (!커뮤니티회원가입개인정보동의문.유효한동의(
                request.PrivacyConsentAccepted,
                request.PrivacyConsentVersion))
        {
            return Result.Fail<커뮤니티회원가입응답>("현재 개인정보 수집·이용 안내를 확인하고 동의해 주세요.");
        }

        var userName = request.UserName.Trim();
        var email = request.Email.Trim();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsedEmail)
            || !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<커뮤니티회원가입응답>("올바른 이메일 주소를 입력해 주세요.");
        }

        var duplicate = await 중복사용자검증Async(userName, email);
        if (duplicate.IsFailed)
        {
            return Result.Fail<커뮤니티회원가입응답>(duplicate.Errors);
        }

        var consentedAtUtc = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = false,
            PrivacyConsentVersion = 커뮤니티회원가입개인정보동의문.현재버전,
            PrivacyConsentedAtUtc = consentedAtUtc
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result.Fail<커뮤니티회원가입응답>(["회원가입에 실패했습니다.", .. createResult.Errors.Select(x => x.Description)]);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, 역할명.커뮤니티회원);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return Result.Fail<커뮤니티회원가입응답>(["커뮤니티 회원 역할 부여에 실패했습니다.", .. roleResult.Errors.Select(x => x.Description)]);
        }

        return new 커뮤니티회원가입응답
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PrivacyConsentVersion = user.PrivacyConsentVersion,
            PrivacyConsentedAtUtc = consentedAtUtc
        };
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

    public async Task<Result<IReadOnlyList<가입친구후보항목응답>>> 가입온보딩친구후보조회Async(
        가입친구후보조회요청? request,
        CancellationToken cancellationToken)
    {
        if (request == null) return Result.Fail<IReadOnlyList<가입친구후보항목응답>>("request body is required");
        if (string.IsNullOrWhiteSpace(request.주문참조번호)) return Result.Fail<IReadOnlyList<가입친구후보항목응답>>("orderReference is required");

        return Result.Ok(await _가입온보딩친구후보Service.후보조회Async(request, cancellationToken));
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

    private async Task<Result<토큰응답>> 토큰발급Async(ApplicationUser user, IList<string>? roles = null)
    {
        roles ??= await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var accessToken = _authTokenService.CreateAccessToken(user, roles, out var accessTokenExpiresAtUtc, claims);
        var refreshToken = _authTokenService.GenerateRefreshToken();
        var refreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        var saved = await 리프레시토큰저장Async(user, refreshToken, refreshTokenExpiresAtUtc);
        if (!saved.Succeeded)
        {
            return 상태실패<토큰응답>(
                "인증 정보를 저장하지 못했습니다. 잠시 후 다시 시도해 주세요.",
                StatusCodes.Status500InternalServerError);
        }

        return new 토큰응답
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Roles = roles.ToArray(),
            PreferredLanguageCode = claims
                .LastOrDefault(claim => string.Equals(
                    claim.Type,
                    SsalddelDisplayLanguageClaimTypes.PreferredLanguage,
                    StringComparison.Ordinal))?.Value
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

    private async Task<IdentityResult> 리프레시토큰저장Async(ApplicationUser user, string refreshToken, DateTime refreshTokenExpiresAtUtc)
    {
        // 두 Identity 저장은 별도입니다. 부분 저장을 되돌렸다고 간주하거나 자동 재시도하지 않습니다.
        var refreshTokenHash = _authTokenService.HashRefreshToken(refreshToken);
        var hashResult = await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenHashName, refreshTokenHash);
        if (!hashResult.Succeeded)
        {
            return hashResult;
        }
        return await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenExpiresAtName, refreshTokenExpiresAtUtc.ToString("O"));
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
                역할명.화주 => App식별자.SsalddelApp,
                역할명.관세사 => App식별자.SsalddelAdmin,
                역할명.서버관리자 => App식별자.SsalddelAdmin,
                _ => "Ssalddel.Server"
            },
            UserId = user?.Id ?? string.Empty,
            UserName = user?.UserName ?? string.Empty,
            RoleName = roleName,
            Email = user?.Email ?? string.Empty,
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
            MetadataJson = userNameOrEmail.Contains('@')
                ? "{\"identifierKind\":\"Email\"}"
                : "{\"identifierKind\":\"UserName\"}"
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
