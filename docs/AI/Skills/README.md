# 공통 규칙과 작업 스킬

2026-11-11 사용자는 프로젝트의 `AGENTS.md`·`SKILL.md` 역할을 구분하고 기존 앱 UI와 Unity 지도 데이터·2D 객체 조작에 유용한 스킬을 조사·적용하도록 요청했다. 기존 설계 정본은 보존하고, 필요한 문서를 찾아 한 작업에 적용하는 짧은 절차를 스킬로 둔다.

## 파일의 책임

| 파일 | 담을 내용 | 담지 않을 내용 |
| --- | --- | --- |
| `AGENTS.md` | 항상 지킬 공통 경계, 코드·실행 소유, 현재 상태를 찾는 경로 | 매 작업에 필요하지 않은 긴 작업 절차 |
| `CLAUDE.md` | 공통 AGENTS를 읽는 Claude 진입과 필요한 문서 링크 | 공통 규칙의 별도 복제본이나 자동 작업 배정 |
| `SKILL.md` | 어떤 요청에 쓰는지, 입력·절차·산출물·검증을 묶은 반복 작업 | 새 권한·상태 원장·제품 설계의 별도 정본 |
| `agents/openai.yaml` | Codex 스킬 표시 이름·설명·호출 예시 | 앱의 에이전트 역할·게임 NPC·서버 실행 설정 |
| Architecture·작업 README | 업무 의미·계약·시각·배치의 상세 기준 | 계정별 대화 기억 |
| `CURRENT_WORK.md`·검증 영수증 | 현재 판본·실제 완료·미완료·정확한 재개 위치 | 과거 결과를 현재 실행으로 간주한 완료 표시 |

`AGENTS.md`의 복수형 파일명과 스킬 폴더 안의 `SKILL.md`를 사용한다. 별도 도구의 `.claude/agents/*.md`는 하위 에이전트 정의이며 이 작업에서는 만들지 않는다. 스킬을 불렀다는 사실만으로 현재 작업의 범위·승인·checkout 소유가 바뀌지 않는다.

## 적용한 프로젝트 스킬

| 정본 | 쓰는 작업 | 주요 정본 연결 |
| --- | --- | --- |
| [mirror-app-ui-readability](../../../.agents/skills/mirror-app-ui-readability/SKILL.md) | 기존 Web·MAUI·WPF 화면의 글자·위계·간격·밀도·줄바꿈·초점·복귀 개선 | 역할별 화면 기준·전체 로드맵 페이지 원칙·현재 소비 코드·시안 절차 |
| [hongdal-unity-map-data-binding](../../../.agents/skills/hongdal-unity-map-data-binding/SKILL.md) | 검증된 자료를 기존 지도·객체의 조회 결과에 결속 | Data→Interpretation→Presentation·출처/판본·좌표/단위·누락/지연·가상/운영 권위 |
| [hongdal-unity-2d-interaction](../../../.agents/skills/hongdal-unity-2d-interaction/SKILL.md) | 2D 객체 배치·선택·초점·줌·드래그와 UI 입력 충돌 정리 | 배치 계획/조립 분리·실제 대상 Camera/Input/Presenter·Game View 검증 |
| [hongdal-synty-asset-workflow](../../../.agents/skills/hongdal-synty-asset-workflow/SKILL.md) | 구매한 Synty 자산의 후보 검색·크기/피벗/재질 확인·기존 객체 외형 적용 | 실제 자산 대장·원본/wrapper 분리·측정 범위·표현 연결·기존 게임 동작 보존 |

이 스킬들은 지침만 제공하며 새 라이브러리·MCP·Tilemap·게임 규칙을 설치하거나 생성하는 실행기가 아니다. 현재 경영 게임의 별도 검토판과 기본 Unity checkout을 동일 대상으로 고정하지 않는다. 먼저 CURRENT_WORK와 실제 허용 소스를 확인한다.

## Codex와 Claude가 읽는 위치

Codex의 저장소 스킬 정본은 `.agents/skills/<name>/SKILL.md`에 둔다. Claude의 프로젝트 탐색 위치인 `.claude/skills/<name>/SKILL.md`에는 같은 정본을 읽는 짧은 진입 파일만 둔다. 작업 절차 본문은 한 곳에서 관리한다. 두 위치의 이름·설명이 어긋나지 않는지 함께 확인한다. 루트 `CLAUDE.md`의 AGENTS 연결도 유지한다.

현재 별도 Unity checkout에는 Unity용 세 스킬의 Codex·Claude 진입을 연결한다. 이 연결은 이 PC의 두 checkout 사이 상대 경로다. 다른 PC·클라우드에서는 정본과 연결 파일이 함께 존재하는지 먼저 확인하고, 없으면 필요한 코드·문서 묶음을 인계한다. 로컬 파일의 존재를 클라우드 가용성으로 승계하지 않는다.

Codex에서는 `$mirror-app-ui-readability`, `$hongdal-unity-map-data-binding`, `$hongdal-unity-2d-interaction`, `$hongdal-synty-asset-workflow`로 명시할 수 있다. Claude에서는 대응하는 `/이름`으로 사용한다. 실제 자동 발견은 도구 버전·세션·프로젝트 위치와 설정에서 확인한다. 현재 채팅의 작업 폴더가 다른 프로젝트이면 정본 경로를 직접 전달하거나 대상 프로젝트에서 시작한다. Claude 설치·구독·실제 실행은 이번 파일 배치 검증과 별도다.

이 구분은 [OpenAI 스킬 안내](https://developers.openai.com/codex/skills), [Claude 스킬 안내](https://code.claude.com/docs/en/skills), [Agent Skills 형식](https://agentskills.io/specification)에 근거한다. Claude의 일반 프로젝트 스킬·개인 스킬·계정 동기화 스킬은 클라우드 세션 유형에 따라 가용 범위가 다르므로 저장소 폴더만 만들고 클라우드 등록 완료로 표시하지 않는다.

## 공식·보유 스킬 조사

공식 `openai/skills` curated 목록 39개와 아래 원문을 2026-11-11 대조했다. 목록의 개인 폴더 설치 표시는 현재 연결된 플러그인 전체 목록과 다르다. 외부 스킬을 일괄 설치하지 않고 현재 승인된 도구와 프로젝트 정본을 우선한다.

| 후보 | 판단과 적용 경계 |
| --- | --- |
| [OpenAI aspnet-core](https://github.com/openai/skills/tree/main/skills/.curated/aspnet-core) | Blazor·Razor·DI·라우팅·SignalR·서버 시험에 유용한 후속 후보. UI 미적 기준이나 WPF/MAUI/Unity 자체 기준을 대신하지 않는다. 현재 프레임워크를 바꾸는 근거가 아니다. |
| [Anthropic frontend-design](https://github.com/anthropics/skills/tree/main/skills/frontend-design) | 시각적 위계·타이포·구도 참고. 새 미적 방향을 강하게 선택하는 일반 웹 절차를 그대로 적용하기보다 기존 역할 화면과 디자인 값을 보존하도록 프로젝트 스킬로 좁혔다. 원문을 복제·설치하지 않았다. |
| [webapp-testing](https://github.com/anthropics/skills/tree/main/skills/webapp-testing) · [playwright-interactive](https://github.com/openai/skills/tree/main/skills/.curated/playwright-interactive) · [screenshot](https://github.com/openai/skills/tree/main/skills/.curated/screenshot) | 실제 화면 검증에 관련되지만 별도 실행기·환경 전제가 있다. 현재 보유 computer-use와 기존 화면 조작 절차를 우선하며 중복 설치하지 않았다. 정적 HTML이나 API 시험을 실제 라우터·입력 검증으로 바꾸어 보고하지 않는다. |
| Figma 관련 스킬 | 현재 Figma 플러그인의 디자인·라이브러리·design-to-code 기능을 필요한 요청에서 재사용한다. Figma 파일이 없는 작업에 새 도구 연결을 필수 조건으로 만들지 않는다. |
| [winui-app](https://github.com/openai/skills/tree/main/skills/.curated/winui-app) | WinUI3/Windows App SDK용이며 현재 WPF·MAUI·Unity를 다듬는 용도로는 부적합하다. 기존 앱의 기술 전환을 시작하지 않았다. |
| Unity 전용 범용 스킬 | 이번 공식 curated 목록에는 전용 Unity 스킬이 없었다. 기존 프로젝트 지침과 Unity 공식 문서에 맞춘 두 절차를 작성했다. 임의의 외부 플러그인이나 Editor 접근 도구를 설치하지 않았다. |

Unity API를 확인할 때는 실제 프로젝트 버전의 [화면 좌표 변환](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera.ScreenToWorldPoint.html), [Grid 좌표 변환](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/GridLayout.WorldToCell.html) 같은 공식 자료를 필요한 부분만 읽는다. 이 예시는 새 Grid·Tilemap 도입을 요구하지 않는다.

## Synty 커뮤니티 자료 선별과 사용 준비

2026-11-11 후속 요청으로 Synty 전용 자산 검색, 팩별 분류, 프로젝트 실재 확인, 게임 규칙을 유지하는 외형 교체 방법을 추려 `hongdal-synty-asset-workflow`로 연결했다. [선별 출처와 채택/제외 범위](../../../.agents/skills/hongdal-synty-asset-workflow/references/community-sources.md)는 고정 GitHub commit에서 대조할 수 있다. 외부 원문이나 프로그램의 통째 설치가 아니라 기존 자산 대장과 도구를 사용하는 프로젝트 절차다.

기존 Unity 대장의 5팩 범위와 실제 추가 설치 팩을 구분한다. 단순 검색은 대장을 읽고, 재생성·측정 출력·Prefab/Scene 변경은 요청과 현재 실행 소유 안에서만 진행한다. 외부 스킬의 고정 자산 수·경로·그리드·리깅·재질 변환·DOTS 가정을 현재 팩에 일반화하지 않는다. 기존 게임의 2D 표시·입력·배차·정산은 외형 활용 준비로 바뀌지 않는다.

사용 예: `hongdal-synty-asset-workflow로 보유 자산에서 도심 매장 외벽·간판 후보를 찾아줘. 현재는 후보와 근거만 정리해줘.` 실제 사용 사례와 형식·연결 검증은 [Synty 준비 결과](../../../artifacts/local/synty-skill-preparation-20261111/completion.json)에 연결한다.

## 검증과 유지

스킬 생성기의 형식 검증, 정본·진입 링크와 메타데이터 일치, 현재 업무를 가정한 독립 읽기 평가를 수행한다. 파일 형식 통과와 평가 결과를 실제 앱·Unity 화면 개선 완료로 표현하지 않는다. 새 절차가 실제 작업에서 어긋나면 해당 정본만 좁게 보완하고 진입 파일도 대조한다.

공통 변경 검증기는 `.agents/skills/`·`.claude/skills/`를 개발 도구 지침으로 분류한다. 제품 소스가 함께 바뀌면 기존 제품 검증을 유지한다. 스킬에 실행 스크립트를 추가할 때는 그 도구의 의미 있는 시험을 별도로 실행하며 지침 분류를 스크립트 검증 완료로 쓰지 않는다.

이번 판본·조사 목록·검증 결과·실행하지 않은 범위는 [마감 기록](../../../artifacts/local/skills-separation-20261111/completion.json)에 남긴다. 기존 예약·공유 원본·게임 소스·Scene·권한·배포는 이번 스킬 구분 작업으로 변경하지 않는다.
