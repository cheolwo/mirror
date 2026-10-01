namespace HongdalApp.Services;

public enum HongdalClientRole
{
    Unspecified,
    Shipper,
    WarehouseManager
}

public sealed class HongdalClientRoleService
{
    private const string ActiveRoleKey = "hongdal.client.active_role";

    public HongdalClientRoleService()
    {
        var savedRole = Preferences.Default.Get(ActiveRoleKey, nameof(HongdalClientRole.Unspecified));
        CurrentRole = Enum.TryParse<HongdalClientRole>(savedRole, out var parsedRole)
            ? parsedRole
            : HongdalClientRole.Unspecified;
    }

    public event Action? Changed;

    public HongdalClientRole CurrentRole { get; private set; }

    public bool HasRole => CurrentRole != HongdalClientRole.Unspecified;

    public bool IsUnspecified => CurrentRole == HongdalClientRole.Unspecified;

    public bool IsShipper => CurrentRole == HongdalClientRole.Shipper;

    public bool IsWarehouseManager => CurrentRole == HongdalClientRole.WarehouseManager;

    public string RoleLabel => CurrentRole switch
    {
        HongdalClientRole.Shipper => "화주",
        HongdalClientRole.WarehouseManager => "창고 관리자",
        _ => "역할 미설정"
    };

    public string RoleDescription => CurrentRole switch
    {
        HongdalClientRole.Shipper => "운송 의뢰와 판매·물류 흐름을 연결하는 화주 운영",
        HongdalClientRole.WarehouseManager => "입고부터 판매와 운송까지 이어지는 창고 운영",
        _ => "정보와 대화에서 시작하는 공통 커뮤니티 홈"
    };

    public void SetRole(HongdalClientRole role)
    {
        if (CurrentRole == role)
        {
            return;
        }

        CurrentRole = role;
        Preferences.Default.Set(ActiveRoleKey, role.ToString());
        Changed?.Invoke();
    }
}
