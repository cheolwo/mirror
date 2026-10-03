using MudBlazor;
using Ssalddel.Ui.Common.Areas.App.Theme;

namespace Ssalddel.Ui.Common.Areas.BackOffice.Theme;

public static class BackOfficeTheme
{
    public static MudTheme Create()
    {
        return RoleAppTheme.Create(역할앱시각역할.관리자);
    }
}
