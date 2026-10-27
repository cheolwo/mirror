using Ssalddel.Contracts.Driver.Transport;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Driver.Transport;

/// <summary>수락 때 저장한 기사 보수만 읽습니다. 고객 운임이나 현재 약정으로 대체하지 않습니다.</summary>
public static class 기사운송보수Projector
{
    public static void 투영(기사운송요약응답 response, 운송원장 transport)
    {
        var amount = transport.기사지급예정액;
        var valid = amount is > 0m && decimal.Truncate(amount.Value) == amount.Value;
        response.기사지급예정액 = valid ? amount : null;
        response.기사지급예정액근거 = valid ? "운송원장.기사지급예정액" : null;
    }
}
