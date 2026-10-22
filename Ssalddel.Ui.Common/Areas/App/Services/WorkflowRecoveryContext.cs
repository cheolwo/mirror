using System.Security.Cryptography;
using System.Text;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>복구 IO 전후의 계정·세션·서버 일치만 확인합니다. 서버 권한이나 업무 성공을 판정하지 않습니다.</summary>
public interface IWorkflowRecoveryContext
{
    WorkflowRecoverySnapshot Capture();
    bool IsCurrent(WorkflowRecoverySnapshot snapshot);
}

/// <summary>토큰과 서버 주소 원문을 포함하지 않는 현재 업무 문맥입니다.</summary>
public sealed record WorkflowRecoverySnapshot(string Owner, long SessionRevision, string EndpointKey)
{
    public string Scope(string logicalScope)
    {
        if (string.IsNullOrWhiteSpace(logicalScope) || logicalScope.Length > 80
            || logicalScope.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_' or ':'))
            || EndpointKey is not { Length: 32 } || EndpointKey.Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidDataException("이전 요청의 업무 범위를 확인하지 못했습니다.");
        return logicalScope + ".server-" + EndpointKey;
    }
}

public sealed class WorkflowRecoveryContext : IWorkflowRecoveryContext
{
    private readonly ISsalddel현재사용자Context? user;
    private readonly ISsalddelAccessTokenProvider? tokens;
    private readonly HttpClient? http;
    private readonly Func<RoleWorkspaceIdentity>? nativeIdentity;
    private readonly Func<Uri?>? nativeEndpoint;
    private readonly object gate = new();
    private string? lastTokenFingerprint;
    private string? lastOwner;
    private string? lastEndpoint;
    private bool initialized;
    private long epoch;

    /// <summary>Web 호스트의 관측된 토큰 변경을 로컬 판본으로 구분합니다. 단말은 실제 세션 판본 생성자를 사용합니다.</summary>
    public WorkflowRecoveryContext(ISsalddel현재사용자Context user, ISsalddelAccessTokenProvider tokens, HttpClient http)
    {
        this.user = user ?? throw new ArgumentNullException(nameof(user));
        this.tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        this.http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>전문 단말 호스트의 실제 세션 판본을 사용하여 같은 계정으로 재로그인한 경우도 구분합니다.</summary>
    public WorkflowRecoveryContext(Func<RoleWorkspaceIdentity> identity, Func<Uri?> endpoint)
    {
        nativeIdentity = identity ?? throw new ArgumentNullException(nameof(identity));
        nativeEndpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    public WorkflowRecoverySnapshot Capture()
    {
        lock (gate)
        {
            if (nativeIdentity is not null)
            {
                var current = nativeIdentity();
                if (!current.IsAuthenticated || !ValidOwner(current.OwnerId) || current.Revision < 0)
                    throw new InvalidOperationException("로그인한 계정의 요청만 확인할 수 있습니다.");
                var nativeEndpointKey = EndpointFingerprint(nativeEndpoint!());
                if (nativeIdentity() != current || EndpointFingerprint(nativeEndpoint!()) != nativeEndpointKey)
                    throw new InvalidOperationException("로그인 정보가 변경되었습니다. 같은 업무를 다시 확인해 주세요.");
                return new(current.OwnerId!, current.Revision, nativeEndpointKey);
            }

            var token = tokens!.AccessToken;
            var owner = user!.현재사용자.UserId;
            var providerOwner = tokens.AuthenticationOwnerId;
            var tokenFingerprint = string.IsNullOrWhiteSpace(token) ? null : Hash(token);
            string? endpointKey = null;
            try { endpointKey = EndpointFingerprint(http!.BaseAddress); }
            finally { Observe(owner, tokenFingerprint, endpointKey); }
            if (!ValidOwner(owner) || tokenFingerprint is null
                || (!string.IsNullOrWhiteSpace(providerOwner) && !string.Equals(owner, providerOwner, StringComparison.Ordinal)))
                throw new InvalidOperationException("로그인한 계정의 요청만 확인할 수 있습니다.");
            if (!string.Equals(token, tokens.AccessToken, StringComparison.Ordinal)
                || !string.Equals(providerOwner, tokens.AuthenticationOwnerId, StringComparison.Ordinal)
                || !string.Equals(owner, user.현재사용자.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("로그인 정보가 변경되었습니다. 같은 업무를 다시 확인해 주세요.");
            return new(owner!, epoch, endpointKey);
        }
    }

    public bool IsCurrent(WorkflowRecoverySnapshot snapshot)
    {
        if (snapshot is null) return false;
        try { return Capture() == snapshot; }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or InvalidDataException)
        { return false; }
    }

    private void Observe(string? owner, string? tokenFingerprint, string? endpointKey)
    {
        if (!initialized || !string.Equals(lastOwner, owner, StringComparison.Ordinal)
            || !string.Equals(lastTokenFingerprint, tokenFingerprint, StringComparison.Ordinal)
            || !string.Equals(lastEndpoint, endpointKey, StringComparison.Ordinal))
        {
            epoch++;
            initialized = true;
            lastOwner = owner;
            lastTokenFingerprint = tokenFingerprint;
            lastEndpoint = endpointKey;
        }
    }

    private static bool ValidOwner(string? owner)
        => !string.IsNullOrWhiteSpace(owner) && owner.Length <= 200 && !owner.Any(char.IsControl);

    private static string EndpointFingerprint(Uri? endpoint)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https")
            || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new InvalidDataException("요청을 보낸 서버 주소를 확인하지 못했습니다.");
        var normalized = new UriBuilder(endpoint)
        {
            Scheme = endpoint.Scheme.ToLowerInvariant(),
            Host = endpoint.IdnHost.ToLowerInvariant(),
            Port = endpoint.IsDefaultPort ? -1 : endpoint.Port,
            Path = endpoint.AbsolutePath.TrimEnd('/') + "/"
        }.Uri.AbsoluteUri;
        return Hash(normalized)[..32];
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
