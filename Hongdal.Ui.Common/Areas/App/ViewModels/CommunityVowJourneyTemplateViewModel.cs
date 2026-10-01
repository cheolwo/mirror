using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record CommunityVowJourneyTemplateDefinition(
    string Key,
    string DisplayName,
    string Purpose,
    IReadOnlyList<string> SectionNames);

public sealed record CommunityVowJourneyTemplateDraft(
    string Title,
    string Body);

public static class CommunityVowJourneyTemplateCatalog
{
    public const string JourneyKey = "journey";
    public const string SourceKey = "source";
    public const string PartnersKey = "partners";

    public static IReadOnlyList<CommunityVowJourneyTemplateDefinition> All { get; } =
    [
        new(
            JourneyKey,
            "여정 세우기",
            "문제를 알아차린 순간부터 다음 작은 행동까지 한 흐름으로 기록합니다.",
            [
                "한 문장 서원",
                "알아차린 문제와 기회",
                "함께할 사람과 업체",
                "지금 그리는 여정",
                "다이어그램과 원장 연결",
                "근거와 확인할 자료",
                "서로에게 돌아갈 이익과 부담",
                "아직 정하지 않은 조건",
                "다음 작은 행동",
                "운영 경계"
            ]),
        new(
            SourceKey,
            "자료에서 시작",
            "YouTube, SNS, 공공자료에서 알아차린 사실과 해석을 나누고 여정으로 확장합니다.",
            [
                "출발한 자료",
                "자료에서 확인한 사실",
                "나의 해석과 서원",
                "함께 확인할 질문",
                "예상 여정과 역할",
                "다이어그램과 원장 연결",
                "가격·통계 근거",
                "아직 정하지 않은 조건",
                "다음 작은 행동",
                "운영 경계"
            ]),
        new(
            PartnersKey,
            "함께할 사람 찾기",
            "공동 목적과 비어 있는 역할을 공개하고 당사자와 전문가의 참여 의사를 모읍니다.",
            [
                "함께 이루고 싶은 일",
                "현재 모인 마음",
                "함께할 역할과 업체",
                "참여 전에 확인할 자격과 책임",
                "참여 뒤 이어질 여정",
                "다이어그램과 원장 연결",
                "서로에게 돌아갈 이익과 부담",
                "아직 정하지 않은 조건",
                "다음 작은 행동",
                "운영 경계"
            ])
    ];

    public static CommunityVowJourneyTemplateDefinition Find(string? key)
        => All.FirstOrDefault(template => string.Equals(
               template.Key,
               key?.Trim(),
               StringComparison.OrdinalIgnoreCase))
           ?? All[0];
}

public sealed class CommunityVowJourneyTemplateViewModel : ObservableObject
{
    private string _selectedKey = CommunityVowJourneyTemplateCatalog.JourneyKey;

    public IReadOnlyList<CommunityVowJourneyTemplateDefinition> Templates { get; } =
        CommunityVowJourneyTemplateCatalog.All;

    public string SelectedKey
    {
        get => _selectedKey;
        set
        {
            var normalized = CommunityVowJourneyTemplateCatalog.Find(value).Key;
            if (SetProperty(ref _selectedKey, normalized))
            {
                OnPropertyChanged(nameof(Selected));
            }
        }
    }

    public CommunityVowJourneyTemplateDefinition Selected
        => CommunityVowJourneyTemplateCatalog.Find(SelectedKey);

    public CommunityVowJourneyTemplateDraft BuildDraft(CommunityVowVersionDefinition version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new(
            BuildTitle(version),
            BuildBody(version));
    }

    private string BuildTitle(CommunityVowVersionDefinition version)
        => Selected.Key switch
        {
            CommunityVowJourneyTemplateCatalog.SourceKey =>
                $"[서원][{version.DisplayName}] 이 자료에서 시작한 여정 ",
            CommunityVowJourneyTemplateCatalog.PartnersKey =>
                $"[서원][{version.DisplayName}] 함께할 사람과 업체를 찾습니다 ",
            _ => $"[서원][{version.DisplayName}] 이루고 싶은 여정 "
        };

    private string BuildBody(CommunityVowVersionDefinition version)
    {
        var lines = Selected.Key switch
        {
            CommunityVowJourneyTemplateCatalog.SourceKey => BuildSourceTemplate(version),
            CommunityVowJourneyTemplateCatalog.PartnersKey => BuildPartnersTemplate(version),
            _ => BuildJourneyTemplate(version)
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> BuildJourneyTemplate(CommunityVowVersionDefinition version)
        =>
        [
            $"{version.DisplayName}을 향한 서원",
            string.Empty,
            "한 문장 서원",
            "- [내가 이루고 싶은 모습을 한 문장으로 적습니다.]",
            string.Empty,
            "알아차린 문제와 기회",
            "- 지금 본 장면:",
            "- 함께 바꾸고 싶은 이유:",
            "- 이 일을 미루면 남는 어려움:",
            string.Empty,
            "함께할 사람과 업체",
            "- 이미 마음을 보탠 사람:",
            "- 더 알아차리고 싶은 사람·업체:",
            "- 아직 비어 있는 역할:",
            string.Empty,
            "지금 그리는 여정",
            $"- 이번 버전의 초점: {version.Focus}",
            "1. 글과 자료로 문제를 함께 확인합니다.",
            "2. 참여 의사와 필요한 역할을 모읍니다.",
            "3. 조건이 드러나면 가원장과 다이어그램으로 기록합니다.",
            "4. 당사자와 전문가가 수량·가격·일정·책임을 확인합니다.",
            "5. 별도 동의가 있을 때만 실원장과 실제 행동으로 전환합니다.",
            "- 이번 글에서 더 구체화할 단계:",
            string.Empty,
            "다이어그램과 원장 연결",
            "- 다이어그램으로 그릴 단계:",
            "- 각 단계에 붙일 업체·자료:",
            "- 가원장으로 기록할 참여 의사와 합의:",
            string.Empty,
            "근거와 확인할 자료",
            "- 참고한 영상·공공자료·경험:",
            "- 자료의 기준일·지역·단위:",
            "- 아직 확인하지 못한 점:",
            string.Empty,
            "서로에게 돌아갈 이익과 부담",
            "- 기대하는 공동 이익:",
            "- 특정 사람에게 몰릴 수 있는 부담:",
            "- Win-Win이라고 보기 위해 확인할 조건:",
            string.Empty,
            "아직 정하지 않은 조건",
            "- 수량·가격·일정:",
            "- 허가·계약·결제·통관·운송:",
            "- 당사자와 전문가에게 물을 것:",
            string.Empty,
            "다음 작은 행동",
            "- 지금 참여자가 할 수 있는 일:",
            "- 다음 글이나 확인 시점:",
            "- 참여 의사를 남기는 방법:",
            string.Empty,
            .. BuildBoundary(version)
        ];

    private static IReadOnlyList<string> BuildSourceTemplate(CommunityVowVersionDefinition version)
        =>
        [
            $"{version.DisplayName}을 향한 자료 기반 서원",
            string.Empty,
            "출발한 자료",
            "- 영상·글·공공자료 제목:",
            "- 원문 주소:",
            "- 출처·게시일·자료 기준일:",
            string.Empty,
            "자료에서 확인한 사실",
            "- 원문이 직접 말하는 내용:",
            "- 지역·통화·단위·표본:",
            "- 자료가 말하지 않는 범위:",
            string.Empty,
            "나의 해석과 서원",
            "- 이 자료에서 알아차린 문제와 기회:",
            "- 함께 이루고 싶은 모습:",
            "- 사실과 구분한 나의 해석:",
            string.Empty,
            "함께 확인할 질문",
            "- 당사자에게 물을 것:",
            "- 전문가에게 확인할 것:",
            "- 추가로 찾을 자료:",
            string.Empty,
            "예상 여정과 역할",
            $"- 이번 버전의 초점: {version.Focus}",
            "1. 자료의 출처와 한계를 함께 확인합니다.",
            "2. 관심 있는 사람과 필요한 역할의 참여 의사를 모읍니다.",
            "3. 가능한 흐름을 다이어그램과 가원장으로 기록합니다.",
            "4. 가격·수량·일정·책임을 검토한 뒤 다음 행동을 정합니다.",
            "- 예상 참여자와 업체:",
            string.Empty,
            "다이어그램과 원장 연결",
            "- 자료를 붙일 노드:",
            "- 업체·전문가를 붙일 노드:",
            "- 가원장에 남길 참여 의사와 질문:",
            string.Empty,
            "가격·통계 근거",
            "- 비교할 기간·지역·품목:",
            "- 그래프로 보여 줄 주장:",
            "- 서로 다른 기준이라 직접 비교할 수 없는 값:",
            string.Empty,
            "아직 정하지 않은 조건",
            "- 수량·가격·일정·역할:",
            "- 허가·계약·결제·통관·운송:",
            string.Empty,
            "다음 작은 행동",
            "- 지금 확인할 원문 또는 당사자:",
            "- 다음 글이나 확인 시점:",
            "- 참여 의사를 남기는 방법:",
            string.Empty,
            .. BuildBoundary(version)
        ];

    private static IReadOnlyList<string> BuildPartnersTemplate(CommunityVowVersionDefinition version)
        =>
        [
            $"{version.DisplayName}을 향한 참여 서원",
            string.Empty,
            "함께 이루고 싶은 일",
            "- 공동 목적:",
            "- 이 일이 필요한 사람과 지역:",
            "- 성공했다고 볼 수 있는 모습:",
            string.Empty,
            "현재 모인 마음",
            "- 참여 의사를 밝힌 사람:",
            "- 현재까지 확인한 수량·관심·자료:",
            "- 아직 의견이 갈리는 점:",
            string.Empty,
            "함께할 역할과 업체",
            "- 구매자·판매자·생산자:",
            "- 수출자·수입자:",
            "- 운송·창고·라스트마일:",
            "- 통관·검사·문서 등 확인된 전문가:",
            "- 아직 비어 있는 역할:",
            string.Empty,
            "참여 전에 확인할 자격과 책임",
            "- 자격·허가·보험 확인이 필요한 역할:",
            "- 각 당사자가 직접 결정할 범위:",
            "- 플랫폼이 대신 결정하거나 중개하지 않을 범위:",
            string.Empty,
            "참여 뒤 이어질 여정",
            $"- 이번 버전의 초점: {version.Focus}",
            "1. 역할별 참여 의사와 질문을 공개합니다.",
            "2. 비어 있는 역할을 알리고 자격 있는 당사자를 확인합니다.",
            "3. 조건이 모이면 가원장과 다이어그램으로 공유합니다.",
            "4. 당사자들이 조건과 책임에 별도로 동의한 뒤 다음 단계로 갑니다.",
            string.Empty,
            "다이어그램과 원장 연결",
            "- 각 역할을 붙일 노드:",
            "- 역할별 제안·질문·동의를 남길 가원장 블록:",
            "- 실원장 전환 전에 충족할 조건:",
            string.Empty,
            "서로에게 돌아갈 이익과 부담",
            "- 역할별 기대 이익:",
            "- 역할별 비용·시간·책임:",
            "- 한쪽에 이익이나 위험이 몰리지 않게 확인할 점:",
            string.Empty,
            "아직 정하지 않은 조건",
            "- 수량·가격·일정·역할:",
            "- 허가·계약·결제·통관·운송:",
            string.Empty,
            "다음 작은 행동",
            "- 지금 열어 둘 역할 슬롯:",
            "- 다음으로 연락하거나 확인할 대상:",
            "- 다음 글이나 확인 시점:",
            string.Empty,
            .. BuildBoundary(version)
        ];

    private static IReadOnlyList<string> BuildBoundary(CommunityVowVersionDefinition version)
        =>
        [
            "운영 경계",
            $"- 앞선 기반: {version.InheritedFoundation}",
            $"- {version.OperationalBoundary}",
            "- 주문·계약·결제·배차·보관·정산은 당사자와 자격 있는 사업자가 별도로 확인하고 결정합니다.",
            "- 이 글은 마음과 정보를 모으는 비구속적 서원이며, 실행 기능의 활성화나 계약 체결을 뜻하지 않습니다."
        ];
}
