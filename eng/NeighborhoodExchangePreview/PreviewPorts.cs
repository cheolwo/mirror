using MediatR;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using 살뜰.Infrastructure.Security;

namespace NeighborhoodExchangePreview;

// 루프백 합성 자료 시험 전용. 운영 인증·개인정보 보호 어댑터로 사용하지 않습니다.
internal sealed class AnonymousPreviewUser : ICurrentUserAccessor, ISsalddelAccessTokenProvider, ISsalddel현재사용자Context
{
    public AnonymousPreviewUser(IHttpContextAccessor context, IConfiguration configuration)
    {
        var account = context.HttpContext?.Request.Cookies["preview-account"];
        FixtureAccount = configuration.GetValue<bool>("CollaborationPreview") && account is "owner" or "requester" or "helper" or "driver-a" or "driver-b" ? account : null;
    }
    public string? FixtureAccount { get; }
    public string? UserId => FixtureAccount is null ? null : "preview-" + FixtureAccount;
    public string? Role => UserId is null ? null : FixtureAccount?.StartsWith("driver-") == true ? "기사" : "일반회원";
    public string? AccessToken => null;
    public 현재사용자Snapshot 현재사용자 => new(UserId, FixtureAccount is null ? null : "로컬 확인 계정", []);
}
internal sealed class PreviewDataProtection : IPersonalDataEncryptionService
{ public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
internal sealed class EmptyLedgerContext : I게시글원장표시ContextService
{
    public Task<PlatformCommunityPostLedgerContextResponse?> 조회Async(string? 원장Id, string? 사용자UserId, CancellationToken cancellationToken) => Task.FromResult<PlatformCommunityPostLedgerContextResponse?>(null);
    public Task<PlatformCommunityPostLedgerContextResponse?> 비식별성립사례조회Async(string? 원장Id, CancellationToken cancellationToken) => Task.FromResult<PlatformCommunityPostLedgerContextResponse?>(null);
}
internal sealed class PreviewPublisher : IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
}
