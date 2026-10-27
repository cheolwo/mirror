using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Qualification;

public interface I운송자격Client
{
    Task<운송자격상세Dto> 조회Async(CancellationToken ct);
    Task<운송자격상세Dto> 제출Async(운송자격제출요청 request, CancellationToken ct);
    Task<운송자격상세Dto> 검토Async(운송자격검토요청 request, CancellationToken ct);
}

/// <summary>검토 화면은 서버의 자격 원장만 호출하며 역할·기관확인 결과를 만들지 않습니다.</summary>
public sealed class 운송자격Client : I운송자격Client
{
    public const string OwnPath = "api/v1/drivers/me/transport-qualification";
    private readonly Func<CancellationToken, Task<운송자격상세Dto>> read;
    private readonly Func<object, CancellationToken, Task<운송자격상세Dto>> send;
    private readonly bool administrator;

    public 운송자격Client(Func<CancellationToken, Task<운송자격상세Dto>> read,
        Func<object, CancellationToken, Task<운송자격상세Dto>> send, bool administrator = false)
    { this.read = read; this.send = send; this.administrator = administrator; }

    public static 운송자격Client ForRole(IRoleWorkspaceApi api, string role) => new(
        ct => api.GetAsync<운송자격상세Dto>(role, OwnPath, ct),
        async (request, ct) => await api.PostAsync<운송자격상세Dto>(role, OwnPath, request, ct)
            ?? throw new InvalidDataException("제출 결과를 확인하지 못했습니다."));

    public static 운송자격Client ForAdministrator(ISsalddelJsonApiClient api, string driverId)
    {
        var path = "api/v1/admin/drivers/" + Uri.EscapeDataString(driverId) + "/transport-qualification";
        return new(ct => Required(api.GetAsync<운송자격상세Dto>(path, "기사 자격 확인", false, ct)),
            (request, ct) => Required(api.SendAsync<object, 운송자격상세Dto>(HttpMethod.Post,
                path + "/review", request, "기사 자격 검토", false, ct)), true);
    }

    private static async Task<운송자격상세Dto> Required(Task<운송자격상세Dto?> result)
        => await result ?? throw new InvalidDataException("자격 자료를 확인하지 못했습니다.");
    public Task<운송자격상세Dto> 조회Async(CancellationToken ct) => read(ct);
    public Task<운송자격상세Dto> 제출Async(운송자격제출요청 request, CancellationToken ct)
        => !administrator ? send(request, ct) : throw new InvalidOperationException("본인 자료만 제출할 수 있습니다.");
    public Task<운송자격상세Dto> 검토Async(운송자격검토요청 request, CancellationToken ct)
        => administrator ? send(request, ct) : throw new InvalidOperationException("관리자 검토가 필요합니다.");
}
