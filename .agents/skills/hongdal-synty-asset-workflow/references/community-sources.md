# 선별한 Synty 커뮤니티 자료

2026-11-11 사용자 요청으로 실제 SKILL.md 본문과 기존 Unity 자산 대장을 대조했다. 아래 방법을 참고해 프로젝트 지침을 새로 작성했으며 외부 SKILL.md·프로그램·자산을 통째 복사하거나 설치하지 않았다. 모두 커뮤니티 자료다. 고정 commit의 출처는 비교 근거이며 현재 최신판·동작 보증이 아니다.

| 출처 | 채택한 판단 방법 | 그대로 사용하지 않은 부분 |
| --- | --- | --- |
| [synty-inventory / SKILL.md](https://github.com/tom-swift-tech/synty-inventory/blob/64be2f51807943dee7fb841e865476d0123b282f/skill/SKILL.md) · [README](https://github.com/tom-swift-tech/synty-inventory/blob/64be2f51807943dee7fb841e865476d0123b282f/README.md) | 용도별 후보를 먼저 좁히고 선택한 자산의 상세 크기·피벗·부착 위치를 확인. 팩 간 동명 충돌과 미측정 값을 구분 | 별도 Python CLI·카탈로그 생성·GLB 준비·Grok/Ollama 분석 명령. 기존 Unity 대장과 중복 도입하지 않음. 확인한 [자동 검사](https://github.com/tom-swift-tech/synty-inventory/actions/runs/41491881681)는 Ruff 실패 뒤 Pytest가 건너뛰어졌으므로 현재 판본을 검증 완료 도구로 취급하지 않음 |
| [Oh My Game Kit / Polygon Generic](https://github.com/tranvietanh0/oh-my-game-kit/blob/ec2cf7889924ba6a254fbbb81c3994b391ea0ba4/modules/rendering/skills/omg-unity-rendering-synty-polygon-generic/SKILL.md) | 환경·건축 부품·소품·FX를 나눠 필요한 범주만 찾고 실제 재질/색 변형을 대조 | Generic·Knights·Fantasy Rivals의 고정 수량·경로·정적 메시/리깅 가정·URP 일괄 변환·DOTS 지침은 현재 설치판에 일반화하지 않음. 팩별 자료 작성 방식만 참고 |
| [UnityPlugin / init-project](https://github.com/newtro/UnityPlugin/blob/e34a88dd19d7ec434202912edb9548085c4a7059/plugins/unity-mcp-setup/skills/init-project/SKILL.md) | Unity 버전·렌더링 방식·설치 패키지·실제 자산 폴더부터 확인 | root CLAUDE.md 재생성·현재 지침 교체·해당 플러그인 설치. 원문의 Assets/Polygon* 탐지만으로 현재 Assets/Synty 하위까지 조사했다고 간주하지 않음 |
| [sleepover / mg-art](https://github.com/Bornqazaq/sleepover/blob/a1fce3918aac99e950ebce36fe78a25f2bf027b5/.claude/skills/mg-art/SKILL.md) | 게임 규칙/충돌 영역과 외형 분리, 실제 크기와 접촉 상태 확인, 생성 결과의 재현·화면 검증 | 특정 게임의 경로·도구·티켓·절차. 원문은 2026-09-15 이후 과거 Synty 작업용으로 보존한다고 명시. 후속 mg-polish의 자체 Blender 아트 전환은 이번 구매 자산 활용 범위에서 제외 |

## 기존 프로젝트 도구를 먼저 사용한다

경로는 현재 Unity 소비 프로젝트 또는 Unity 작업 영역에서 확인한 원 checkout 기준이다. 파일 존재·대장 내용 확인과 오늘의 Unity 실행 결과를 구분한다.

| 필요한 확인 | 기존 진입 | 주의할 실제 범위 |
| --- | --- | --- |
| 자산 후보 | `Assets/Ssalddel/Presentation/World/Catalogs/SyntyPackAssetInventoryCatalog.asset`와 `SyntyPackAssetInventoryCatalog.cs` | 지정 5팩 기술 대장. 추가 설치 팩은 대장 밖일 수 있음. `Entries`의 실제 필드를 읽어 좁게 필터 |
| 대장 갱신 | `Assets/Ssalddel/Editor/SyntyPackAssetInventoryBuilder.cs` | 생성·SaveAssets/Refresh 쓰기. 단순 검색으로 실행하지 않음 |
| 조립용 측정 | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/SyntyCompositionSourceMeasurement.cs` | 정의한 표본의 bounds·pivot·진입 방향 등. `TownCityGridCellSize=5f`는 다른 팩의 보편 값이 아님 |
| 애니메이션 실재 | 같은 연구 Editor 폴더의 `SyntyAnimationSourceInventory.cs` | clip/controller/rig/누락 참조와 추가 애니메이션 팩을 실제 파일로 구분 |
| 사가정 표현 연결 | `Assets/Ssalddel/Presentation/World/사가정Synty표현Planner.cs`, `법정동Synty후단연결기.cs` | 계획/VisualKey와 외형 연결을 분리. 기존 미연결/거절 사유 유지 |

Hongdal의 `eng/world-seedbeds/manage-synty-bottom-up-inventory.ps1` 기본 입력은 과거 catalog.v1·4팩 기준이다. 현재 Unity 5팩 기술 대장이나 추가 팩 전체의 검색·갱신 명령으로 쓰지 않는다. 게임 소스가 별도 검토판이면 원 checkout의 도구를 자동 실행하거나 게임 소스를 덮지 않는다.

## 사용할 수 있는 요청 예

- `보유 자산에서 도심 매장 외벽·간판 후보를 찾아줘. 지금은 후보와 근거만 정리해줘.`
- `선택한 Synty 건물 외형을 현재 매장에 적용해줘. 출입구·선택·기사 경로와 정산 결과는 유지해줘.`
- `이 캐릭터의 실제 rig와 clip을 확인해줘. 예전 설치 기록과 지금 파일이 다른 점도 구분해줘.`

Codex에서는 `$hongdal-synty-asset-workflow`, Claude에서는 같은 이름의 프로젝트 진입으로 호출한다. 현재 세션의 발견 여부와 실제 Claude/Cloud 실행은 파일 배치 검증과 별도다.
