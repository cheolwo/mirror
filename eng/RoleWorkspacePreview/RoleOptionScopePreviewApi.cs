using Microsoft.AspNetCore.WebUtilities;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace RoleWorkspacePreview;

// 범위 표시만 확인하는 합성 GET 응답입니다. 실제 업무 저장·외부 조회·지급은 제공하지 않습니다.
public static class RoleOptionScopePreviewApi
{
    public static bool IsPreview(string uri) => new Uri(uri).AbsolutePath.StartsWith(
        "/role-option-scope-preview/", StringComparison.Ordinal);

    public static object? Read(string role, string path, IRoleWorkspaceAccess access, string uri)
    {
        if (!IsPreview(uri) || role != RoleWorkspaceCatalog.CargoDriver) return null;
        if (!access.GetIdentity(role).IsAuthenticated) throw new RoleWorkspaceAccessException(401, "로그인 필요");
        var query = QueryHelpers.ParseQuery(new Uri(uri).Query);
        query.TryGetValue("scenario", out var scenario);
        if (scenario == "failure") throw new InvalidOperationException("예시 조회 실패");
        var now = DateTime.UtcNow;
        return path switch
        {
            "api/v1/driver/recommendations" => new 기사배차추천항목응답[]
            {
                new() { 의뢰Id = "option-preview-request", 화물종류 = "상온 보관 물품", 차량적합여부 = true,
                    추천만료시각 = scenario == "expired" ? now.AddMinutes(-1) : now.AddMinutes(10) }
            },
            "api/v1/driver/requests/option-preview-request" => new 기사운송의뢰상세응답
            {
                의뢰Id = "option-preview-request", 화물종류 = "상온 보관 물품",
                픽업지 = "상차 장소 1", 하차지 = "하차 장소 1", 결제예정금액 = 34000,
                차량적합여부 = true, 배차상태 = "추천중"
            },
            _ => throw new InvalidOperationException("미리보기에서 지원하지 않는 조회입니다.")
        };
    }
}
