namespace Ssalddel.Services.Commerce;

/// <summary>서버 인증기관 adapter만 생성할 수 있는 결과. HTTP 요청 DTO의 확인 체크값은 이 결과가 아닙니다.</summary>
public sealed record 거래신원확인결과(string ActorUserId, string Reference, bool IsAdult, bool PhoneVerified,
    bool EmailVerified, bool DesignatedIdentityVerified, bool BusinessVerified, bool MailOrderStatusVerified,
    string PhoneNumber, string Email, string BusinessRegistrationNumber, DateTime VerifiedAtUtc, DateTime ExpiresAtUtc,
    string ProfileHash = "");

public interface I거래신원확인Gateway
{
    bool IsConfigured { get; }
    Task<거래신원확인결과?> 확인Async(string actorUserId, string reference, CancellationToken cancellationToken);
}

/// <summary>운영용 인증 기관이 연결되기 전에는 인증 결과를 만들지 않습니다. 개발 표본은 테스트 DI에서만 주입합니다.</summary>
public sealed class 미연결거래신원확인Gateway : I거래신원확인Gateway
{
    public bool IsConfigured => false;
    public Task<거래신원확인결과?> 확인Async(string actorUserId, string reference, CancellationToken cancellationToken)
        => Task.FromResult<거래신원확인결과?>(null);
}
