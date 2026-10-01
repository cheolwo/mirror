using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 5개 업무 사이의 전환과 5개 역할 관점을 세부 기능 단위로 식별한다.
/// 같은 좌표를 Razor 테이블, 카드, 다이얼로그가 공유할 수 있다.
/// </summary>
public sealed record 업무역할관점좌표(
    string 출발업무코드,
    string 도착업무코드,
    string 기능코드,
    string 역할코드)
{
    public string 업무전환Key => $"{출발업무코드}->{도착업무코드}";

    public string 관점슬롯Key => $"{업무전환Key}:{기능코드}:{역할코드}";
}

/// <summary>
/// 역할별 화면이 현재 서버 API에 어느 정도 연결되어 있는지 나타낸다.
/// 화면 관점과 서버 권한을 혼동하지 않도록 조회 준비 상태만 표현한다.
/// </summary>
public enum 역할관점데이터연결상태
{
    공통조회연결됨 = 0,
    역할별조회연결됨 = 1,
    역할별조회Api필요 = 2
}

/// <summary>
/// 화면에서 노출할 수 있는 행동 후보이다.
/// 후보 노출은 실행 권한 부여가 아니며, 실제 실행 시 서버 권한 검사를 거쳐야 한다.
/// </summary>
public sealed record 역할관점행동후보(
    string 기능Key,
    string 이름,
    string 설명,
    bool 서버권한확인필요 = true);

/// <summary>
/// 하나의 업무 기능을 특정 역할의 시선으로 표시하기 위한 공통 정의이다.
/// 이후 출고·주문·판매에도 같은 구조를 재사용한다.
/// </summary>
public sealed record 역할관점업무정의(
    업무역할관점좌표 좌표,
    BaguaActorRoleDefinition 역할,
    string 화면제목,
    string 핵심질문,
    string 설명,
    IReadOnlyList<string> 핵심정보,
    IReadOnlyList<역할관점행동후보> 행동후보,
    IReadOnlyList<string> 역할별칭,
    역할관점데이터연결상태 데이터연결상태,
    string 데이터연결안내)
{
    public bool 서버조회준비됨 => 데이터연결상태 != 역할관점데이터연결상태.역할별조회Api필요;

    public bool 현재사용자관점(현재사용자Snapshot 현재사용자)
    {
        if (!현재사용자.인증됨)
        {
            return false;
        }

        return 현재사용자.Roles.Any(role =>
            string.Equals(role, 역할.RoleCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, 역할.RoleName, StringComparison.OrdinalIgnoreCase)
            || 역할별칭.Any(alias => string.Equals(role, alias, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed record 역할관점표시값(
    string Key,
    string 이름,
    string 값,
    bool 강조 = false);
