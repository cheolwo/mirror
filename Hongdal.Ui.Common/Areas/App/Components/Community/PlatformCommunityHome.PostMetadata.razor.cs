using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private IReadOnlyList<string> BoardCategoryOptions
    {
        get
        {
            var categories = new List<string>
            {
                PlatformCommunityPostCategories.General,
                PlatformCommunityPostCategories.Sales,
                "시스템 다이어그램",
                "운송 실무",
                "업무 질문",
                "업무 기록",
                "생활 원장",
                "개선 제안",
                "신고/분쟁"
            };

            foreach (var board in approvedBoards)
            {
                if (!categories.Contains(board.Title, StringComparer.OrdinalIgnoreCase))
                {
                    categories.Add(board.Title);
                }
            }

            return categories;
        }
    }

    private CommunityWorkClassificationResponse? SelectedWorkClassification
        => CommunityWorkClassificationCatalog.FindByWorkflowTag(form.WorkflowTag);

    private bool SelectedWorkFeatureEnabled
        => SelectedWorkClassification is { } classification
           && classification.LedgerTemplateKeys.Count > 0
           && featureFlagStates.TryGetValue(classification.FeatureFlagKey, out var enabled)
           && enabled;

    private IReadOnlyList<string> RoleTagOptions
    {
        get
        {
            var tags = new List<string>(DefaultRoleTagOptions);
            var resolvedRole = ResolveRoleTag(RoleLabel);
            foreach (var role in SelectedLedgerTemplate.Roles)
            {
                if (!tags.Contains(role.RoleName, StringComparer.OrdinalIgnoreCase))
                {
                    tags.Add(role.RoleName);
                }
            }

            if (!tags.Contains(resolvedRole, StringComparer.OrdinalIgnoreCase))
            {
                tags.Insert(0, resolvedRole);
            }

            return tags;
        }
    }

    private static readonly IReadOnlyList<string> DefaultRoleTagOptions =
    [
        "기사",
        "화주",
        "주문자",
        "주문자 집단 대표",
        "창고 관리자",
        "관세사",
        "운영자",
        "플랫폼 구성원"
    ];

    private static string ResolveRoleTag(string? roleLabel)
    {
        if (string.IsNullOrWhiteSpace(roleLabel))
        {
            return "플랫폼 구성원";
        }

        if (roleLabel.Contains("기사", StringComparison.OrdinalIgnoreCase))
        {
            return "기사";
        }

        if (roleLabel.Contains("화주", StringComparison.OrdinalIgnoreCase) ||
            roleLabel.Contains("판매자", StringComparison.OrdinalIgnoreCase))
        {
            return "화주";
        }

        if (roleLabel.Contains("주문", StringComparison.OrdinalIgnoreCase))
        {
            return "주문자";
        }

        if (roleLabel.Contains("창고", StringComparison.OrdinalIgnoreCase))
        {
            return "창고 관리자";
        }

        if (roleLabel.Contains("관세", StringComparison.OrdinalIgnoreCase))
        {
            return "관세사";
        }

        if (roleLabel.Contains("관리", StringComparison.OrdinalIgnoreCase) ||
            roleLabel.Contains("운영", StringComparison.OrdinalIgnoreCase))
        {
            return "운영자";
        }

        return "플랫폼 구성원";
    }

    private string ResolveLedgerRoleTag(CommunityLedgerTemplateResponse template)
    {
        var resolvedRole = ResolveRoleTag(RoleLabel);
        var matchedRole = template.Roles.FirstOrDefault(role =>
            RoleLabel.Contains(role.RoleName, StringComparison.OrdinalIgnoreCase) ||
            role.RoleName.Contains(resolvedRole, StringComparison.OrdinalIgnoreCase) ||
            resolvedRole.Contains(role.RoleName, StringComparison.OrdinalIgnoreCase));

        return matchedRole?.RoleName
               ?? template.Roles.FirstOrDefault()?.RoleName
               ?? resolvedRole;
    }

    private static string FormatDate(DateTime createdAtUtc)
    {
        return createdAtUtc.ToLocalTime().ToString("yyyy.MM.dd HH:mm");
    }

    private static string FormatForumDate(DateTime createdAtUtc)
    {
        var local = createdAtUtc.ToLocalTime();
        var now = DateTime.Now;
        if (local.Date == now.Date)
        {
            return local.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        return local.Year == now.Year
            ? local.ToString("MM.dd", CultureInfo.InvariantCulture)
            : local.ToString("yy.MM.dd", CultureInfo.InvariantCulture);
    }

    private static bool IsReportPost(PlatformCommunityPostResponse post)
    {
        return post.IsReportBoardPost
            || ContainsReportKeyword(post.Category);
    }

    private static string DisplayPostNickname(PlatformCommunityPostResponse post)
    {
        return IsReportPost(post) ? "익명 신고자" : post.Nickname;
    }

    private static bool ContainsReportKeyword(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        return category.Contains("신고", StringComparison.OrdinalIgnoreCase)
            || category.Contains("분쟁", StringComparison.OrdinalIgnoreCase)
            || category.Contains("report", StringComparison.OrdinalIgnoreCase);
    }
}
