using Hongdal.Contracts.Common.Community;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private bool 원함입력됨 => !string.IsNullOrWhiteSpace(원함입력);

    private bool HasAny원장블록입력
        => 원장블록입력값.Values.Any(value => !string.IsNullOrWhiteSpace(value));

    private string 원함전체문장
        => string.Join(" ", new[] { 원함입력, 원함조건입력 }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()));

    private CommunityLedgerFlowCandidateResponse 원함추천후보
        => 원함분석결과?.PrimaryCandidate ?? new CommunityLedgerFlowCandidateResponse();

    private CommunityLedgerTemplateResponse 원함추천템플릿
        => CommunityLedgerTemplateCatalog.Find(string.IsNullOrWhiteSpace(원함추천후보.TemplateKey)
            ? selectedLedgerTemplateKey
            : 원함추천후보.TemplateKey);

    private string 원장화판정
    {
        get
        {
            if (원함분석결과 is null)
            {
                return "원함 입력 전";
            }

            if (홍달처리범위밖신호있음())
            {
                return "홍달 처리 범위 밖";
            }

            if (원함추천후보.RelationCode == CommunityLedgerFlowRelationCodes.StrongFlowMatch &&
                !원함분석결과.RequiresHumanReview)
            {
                return "원장 생성 가능";
            }

            if (원함추천후보.RelationCode == CommunityLedgerFlowRelationCodes.LooseCommunityRequest)
            {
                return "커뮤니티 대화 유지";
            }

            return "추가 정보 필요";
        }
    }

    private Color 원장화판정Color
        => 원장화판정 switch
        {
            "원장 생성 가능" => Color.Success,
            "추가 정보 필요" => Color.Warning,
            "홍달 처리 범위 밖" => Color.Error,
            _ => Color.Info
        };

    private Severity 원장화판정Severity
        => 원장화판정 switch
        {
            "원장 생성 가능" => Severity.Success,
            "추가 정보 필요" => Severity.Warning,
            "홍달 처리 범위 밖" => Severity.Error,
            _ => Severity.Info
        };

    private string 원함판정설명
        => 원장화판정 switch
        {
            "원장 생성 가능" => "참여자와 조건을 조금만 더 확인하면 원장 초안으로 정리할 수 있습니다.",
            "추가 정보 필요" => "홍달이 원장 형태를 제안할 수 있지만, 진행 전에 부족한 블록을 더 채워야 합니다.",
            "홍달 처리 범위 밖" => "홍달은 이 내용을 기록하거나 대화로 정리할 수는 있지만, 보증·강제 이행·법적 판단까지 대신하지는 않습니다.",
            _ => "아직 실행 원장보다 커뮤니티 대화나 추가 질문으로 두는 편이 좋습니다."
        };

    private IReadOnlyList<string> 원함보완안내목록
    {
        get
        {
            if (홍달처리범위밖신호있음())
            {
                return
                [
                    "플랫폼 보증, 법적 판단, 강제 이행, 자동 결제 확정으로 읽히는 부분을 사람 확인 문구로 낮춰야 합니다.",
                    "실제 약속과 책임은 참여자가 직접 확인해야 합니다.",
                    "필요하면 원장보다 커뮤니티 대화나 신고/분쟁 검토로 먼저 남깁니다."
                ];
            }

            var 보완 = new List<string>();

            if (원함분석결과 is not null)
            {
                보완.AddRange(원함추천후보.MissingRequiredSignals.Select(signal => $"{signal} 정보를 더 적어주세요."));
            }

            if (보완.Count == 0)
            {
                보완.AddRange(원함추천템플릿.사용자확인책임안내목록.Take(3));
            }

            return 보완;
        }
    }
}
