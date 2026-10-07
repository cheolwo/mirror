using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Commerce;

namespace Ssalddel.Services.Commerce;

public sealed class 거래보호Exception(string code, string message, int status = 409) : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed class 판매자확인Service(I판매자확인Store store, I거래신원확인Gateway gateway, TimeProvider clock)
{
    public Task<판매자확인Record?> 원본Async(string userId, CancellationToken cancellationToken)
        => store.조회Async(Actor(userId), cancellationToken);

    public async Task<판매자확인Response> 내상태Async(string userId, CancellationToken cancellationToken)
        => 응답(await 원본Async(userId, cancellationToken));

    public async Task<판매자확인Response> 등록Async(string userId, 판매자등록Request request, CancellationToken cancellationToken)
    {
        var actor = Actor(userId);
        if (request.ClientRequestId == Guid.Empty || !판매자유형Codes.유효(request.SellerKind)
            || !request.CollectionConsentAccepted || request.NoticeVersion != 통신판매안내Service.Version)
            throw new 거래보호Exception("SellerInputInvalid", "판매자 유형과 현재 정보 이용 안내를 확인해 주세요.", 400);
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 120
            || request.PhoneNumber.Length is < 8 or > 24 || !request.PhoneNumber.All(x => char.IsAsciiDigit(x) || x is '+' or '-' or ' ')
            || request.Email.Length > 254 || !MailAddress.TryCreate(request.Email, out var email) || email.Address != request.Email.Trim()
            || request.RepresentativeName.Length > 120 || request.BusinessAddress.Length > 500
            || request.BusinessRegistrationNumber.Length > 24 || request.MailOrderRegistrationNumber.Length > 100
            || request.MailOrderRegistrationExemptionReason.Length > 500)
            throw new 거래보호Exception("SellerInputInvalid", "표시명·전화번호·이메일과 사업자 정보를 확인해 주세요.", 400);
        if (request.SellerKind == 판매자유형Codes.사업자
            && (string.IsNullOrWhiteSpace(request.RepresentativeName) || string.IsNullOrWhiteSpace(request.BusinessAddress)
                || string.IsNullOrWhiteSpace(request.BusinessRegistrationNumber)
                || (string.IsNullOrWhiteSpace(request.MailOrderRegistrationNumber) && string.IsNullOrWhiteSpace(request.MailOrderRegistrationExemptionReason))))
            throw new 거래보호Exception("BusinessInformationRequired", "사업자 신원과 신고 또는 적용 제외의 검토 근거가 필요합니다.", 400);
        // 개인 판매자의 주거지·사업자번호를 일반 신원 필드로 보존하지 않습니다.
        if (request.SellerKind == 판매자유형Codes.개인)
        {
            request.RepresentativeName = request.BusinessAddress = request.BusinessRegistrationNumber = string.Empty;
            request.MailOrderRegistrationNumber = request.MailOrderRegistrationExemptionReason = string.Empty;
        }
        var json = JsonSerializer.Serialize(request); var hash = Hash(json);
        var prior = await store.조회Async(actor, cancellationToken);
        if (prior?.LastRequestId == request.ClientRequestId)
        {
            if (prior.LastRequestHash != hash) throw new 거래보호Exception("IdempotencyConflict", "같은 요청 번호로 다른 정보를 보낼 수 없습니다.");
            return 응답(prior);
        }
        if ((prior?.Revision ?? 0) != request.ExpectedRevision)
            throw new 거래보호Exception("RevisionConflict", "최신 판매자 정보를 다시 확인해 주세요.");
        var next = new 판매자확인Record
        {
            UserId = actor, Revision = request.ExpectedRevision + 1, LastRequestId = request.ClientRequestId,
            LastRequestHash = hash, SellerKind = request.SellerKind, DisplayName = request.DisplayName.Trim(),
            ProfileJson = json, NoticeVersion = request.NoticeVersion, ConsentedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        if (!await store.저장Async(next, request.ExpectedRevision, cancellationToken))
            throw new 거래보호Exception("RevisionConflict", "정보가 먼저 변경되었습니다. 다시 확인해 주세요.");
        return 응답(next);
    }

    public async Task<판매자확인Response> 확인Async(string userId, 판매자확인Request request, CancellationToken cancellationToken)
    {
        var actor = Actor(userId);
        if (!gateway.IsConfigured) throw new 거래보호Exception("IdentityProviderUnavailable", "운영용 신원 확인이 연결되면 인증할 수 있습니다.", 503);
        if (string.IsNullOrWhiteSpace(request.VerificationReference) || request.VerificationReference.Length > 512)
            throw new 거래보호Exception("VerificationReferenceRequired", "서버 인증기관이 발급한 확인 참조가 필요합니다.", 400);
        var prior = await store.조회Async(actor, cancellationToken);
        if ((prior?.Revision ?? 0) != request.ExpectedRevision) throw new 거래보호Exception("RevisionConflict", "최신 정보를 다시 확인해 주세요.");
        var verified = await gateway.확인Async(actor, request.VerificationReference, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (verified is null || verified.ActorUserId != actor || verified.Reference != request.VerificationReference
            || verified.VerifiedAtUtc.Kind != DateTimeKind.Utc || verified.ExpiresAtUtc.Kind != DateTimeKind.Utc
            || verified.VerifiedAtUtc > now || verified.VerifiedAtUtc == default || verified.ExpiresAtUtc <= now
            || !verified.IsAdult || !verified.PhoneVerified || (!verified.DesignatedIdentityVerified && !verified.EmailVerified))
            throw new 거래보호Exception("IdentityNotVerified", "본인·성년 여부와 현재 신원 확인 결과가 필요합니다.");
        var next = prior ?? new 판매자확인Record { UserId = actor };
        if (next.SellerKind != 판매자유형Codes.미확인)
        {
            var profile = JsonSerializer.Deserialize<판매자등록Request>(next.ProfileJson)!;
            if (verified.ProfileHash != Hash(next.ProfileJson) || NormalizePhone(verified.PhoneNumber) != NormalizePhone(profile.PhoneNumber)
                || !string.Equals(verified.Email, profile.Email, StringComparison.OrdinalIgnoreCase)
                || (next.SellerKind == 판매자유형Codes.사업자 && (!verified.BusinessVerified || !verified.MailOrderStatusVerified
                    || verified.BusinessRegistrationNumber != profile.BusinessRegistrationNumber)))
                throw new 거래보호Exception("SellerInformationNotVerified", "확인한 신원과 현재 판매자 정보가 일치해야 합니다.");
        }
        next.Verification = verified; next.Revision = request.ExpectedRevision + 1;
        if (!await store.저장Async(next, request.ExpectedRevision, cancellationToken)) throw new 거래보호Exception("RevisionConflict", "확인 결과를 다시 조회해 주세요.");
        return 응답(next);
    }

    public 판매자확인Response 응답(판매자확인Record? record)
    {
        var proof = record?.Verification;
        var now = clock.GetUtcNow().UtcDateTime;
        var current = proof is not null && proof.ActorUserId == record?.UserId
            && proof.VerifiedAtUtc.Kind == DateTimeKind.Utc && proof.ExpiresAtUtc.Kind == DateTimeKind.Utc
            && proof.VerifiedAtUtc != default && proof.VerifiedAtUtc <= now && proof.ExpiresAtUtc > now
            && proof.PhoneVerified && (proof.EmailVerified || proof.DesignatedIdentityVerified);
        var missing = new List<string>();
        if (!current || !proof!.IsAdult) missing.Add("서버 성년 확인");
        if (!current || !proof!.PhoneVerified) missing.Add("전화번호 확인");
        if (!current || (!proof!.DesignatedIdentityVerified && !proof.EmailVerified)) missing.Add("이메일 또는 지정 본인확인기관 확인");
        if (record?.SellerKind is null or 판매자유형Codes.미확인) missing.Add("판매자 유형·정보 등록");
        if (record?.SellerKind == 판매자유형Codes.사업자 && (!current || !proof!.BusinessVerified || !proof.MailOrderStatusVerified)) missing.Add("사업자 신원·신고 여부 검토");
        return new()
        {
            Revision = record?.Revision ?? 0, SellerKind = record?.SellerKind ?? 판매자유형Codes.미확인,
            DisplayName = record?.DisplayName ?? string.Empty, StatusCode = missing.Count == 0 ? "Verified" : current ? "IdentityVerified" : "Unverified",
            IsAdult = current && proof!.IsAdult, PhoneVerified = current && proof!.PhoneVerified, EmailVerified = current && proof!.EmailVerified,
            VerifiedAtUtc = proof?.VerifiedAtUtc, VerificationExpiresAtUtc = proof?.ExpiresAtUtc, MissingRequirements = missing
        };
    }

    public async Task<판매자공개정보Response?> 공개Async(string sellerId, CancellationToken cancellationToken)
    {
        var record = await store.조회Async(sellerId, cancellationToken);
        if (record is null || 응답(record).StatusCode != "Verified") return null;
        var result = new 판매자공개정보Response { Revision = record.Revision, SellerKind = record.SellerKind, DisplayName = record.DisplayName, StatusCode = "Verified" };
        if (record.SellerKind == 판매자유형Codes.사업자)
        {
            var profile = JsonSerializer.Deserialize<판매자등록Request>(record.ProfileJson)!;
            result.RepresentativeName = profile.RepresentativeName; result.BusinessAddress = profile.BusinessAddress;
            result.PhoneNumber = profile.PhoneNumber; result.Email = profile.Email;
            result.BusinessRegistrationNumber = profile.BusinessRegistrationNumber;
            result.MailOrderRegistrationNumber = profile.MailOrderRegistrationNumber;
            result.MailOrderRegistrationExemptionReason = string.IsNullOrWhiteSpace(profile.MailOrderRegistrationExemptionReason) ? null : "신고 적용 여부 검토 완료";
        }
        return result;
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string NormalizePhone(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
    private static string Actor(string value) => string.IsNullOrWhiteSpace(value) || value.Length > 160
        ? throw new 거래보호Exception("AuthenticationRequired", "로그인이 필요합니다.", 401) : value;
}
