# Ssalddel 공통 UI 작업 지침

이 폴더에서는 저장소 루트 `AGENTS.md`와 함께 아래 UI 원칙을 적용한다.

## 구조와 재사용

- 새 페이지나 의미 있는 UI·UX 리팩토링 전에 [전체 로드맵 조화형 페이지 원칙](../docs/Architecture/WholeRoadmapPagePrinciple.md)의 단일 책임 기준을 읽고 대상 상세 README에 [페이지 책임 카드](../docs/ProjectOverview/page-docs/page-responsibility-template.md)를 작성한다. 정보 접기와 독립 업무 분리를 구별하며 판단 기준은 이 기준 문서만 소유한다.
- 새 공용 흐름은 우선 `SsalddelApp`과 `Ssalddel.Ui.Common`에 통합하고, 기존 전문 앱은 명시적 요청 없이 삭제하거나 축소하지 않는다.
- `Ui.Common`에는 여러 역할 앱이 같은 의미로 수행하는 커뮤니티, 공동 원장, 관계와 업무 workflow를 둔다. 단순 기술 재사용만으로 공통 업무로 분류하지 않는다.
- 공통 셸은 커뮤니티, 업무, 다이어그램, 정보 흐름을 연결하며 기본 navigation은 `사방괘 -> 다이어그램 -> 구체 데이터 페이지`다.
- MAUI Blazor는 View, ViewModel, navigation 책임을 나누고 기존 MVVM CommunityToolkit 패턴을 따른다.
- shared 업무 component와 workflow를 먼저 재사용하고 rendering·device·storage 같은 platform 기능은 adapter로 분리한다.

## 사용자 경험과 검증

- 역할 업무 앱의 시각 변경은 [역할 앱 시각 디자인 기준](../docs/Architecture/RoleAppVisualDesignStandard.md)을 먼저 참조한다. 기존 내용·기능·조건을 유지하고 공통 색/글자/여백 토큰을 소비하며 실제 호스트 렌더로 확인한다. Community/Figma 적용 범위와 전수 검증 여부를 구분한다.

- 모바일 목록은 넓은 table보다 compact card와 detail 전환을 우선한다.
- Figma `01 Community`와 `SsalddelApp` MAUI 화면은 [Figma-MAUI 호환성 정책](../docs/Architecture/FigmaMauiCompatibilityPolicy.md)을 따른다. 화면 구조·토큰·route·상태가 바뀌면 같은 작업에서 양쪽 대응과 실제 렌더를 확인한다.
- 초기 필수 데이터를 먼저 표시하고 loading, empty, error, retry, disabled 상태를 제공한다.
- desktop/mobile에서 텍스트 잘림, 겹침, 터치 영역, 고정 navigation, drawer, dialog, diagram 연결선을 확인한다.
- 시각 변경은 실제 렌더링으로 검증하고 개인정보·주소·연락처·계좌·결제 식별자·위치·증빙 원본은 마스킹한다.
- 대표 PNG는 `docs/assets/changes/`로 옮기고 임시 capture와 raw output은 `artifacts/local/`에 둔다.
- 공통 UI 변경은 server build만으로 끝내지 않고 최소 한 소비 client를 함께 검증한다.

화면 구조는 `docs/Architecture/ThreeStageClientNavigation.md`, 화면 색인은 `docs/ProjectOverview/00-첨부문서목차.md`를 따른다.
