# Mirror (거울)

> 기획·개발 중 발견되는 문제를 계속 수정·보완하고 있습니다. 아래는 구현 완료 목록이 아닌 프로젝트 안내입니다.

사람·사물·환경 사이의 행동과 변화를 이야기·게임 규칙·Unity 공간으로 구성하는 프로젝트입니다.

## 기획과 구조

- [관찰 중심 개인 세계 · 리팩토링 단계](docs/AI/Planning/시스템/PLAN-SYSTEM-OBSERVER-WORLD/README.md)
- [이야기와 플레이 기획](docs/AI/PLANNING.md)
  - [게임의 상위 목적](docs/AI/게임상위목적-오행순환과광복기-기획-2026-09-02.md)
  - [64괘·384효 기획 트리](docs/AI/generated/hexagram-story-tree.md)
    - [수뢰둔 — 한스 농장](docs/AI/generated/hexagram-story-tree.md#hex-03-zhun)
    - [산수몽 — 이데아 맵과 학습](docs/AI/Planning/스토리/PLAN-STORY-IDEA-MAP-LEARNING-001/README.md)
    - [효사별 기획·WI·H 연결](docs/AI/generated/hexagram-line-planning-requirements.md)
    - [효사 기획의 H 공간 참조](docs/AI/generated/hexagram-h-reference-index.md)
  - [괘·효는 진행 규칙이 아닌 영감 자료](docs/Architecture/스토리영감과플레이진행분리.md)
- [주체와 상호작용(WI)](docs/Architecture/주체상호작용중심개발체계.md)
- 세계와 공간
  - [H1 행동 공간 → H2 블록 → H3 경관 → H4 지역 → H5 세계 배치](docs/Architecture/H1-H5공간포함계층조사.md)
  - [관계 지도(Graph Map)와 배치 맵](docs/Architecture/GraphMap기획인계순환체계.md)
- 실행과 표현
  - [웹·MAUI에서 Unity로 — 코드 연결과 음식점 예제](docs/Architecture/UnityClientLayeredArchitecture.md)
  - [Simulation·Unity 코드 지도](docs/AI/generated/simulation-unity-code-map.md)
  - [Unity 프로젝트](https://github.com/cheolwo/unity)
- 개발과 검증
  - [E1~E7 기능 검증 / E8~E10 반복 안정·영역 조화·제한 운영](docs/Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md)
  - [논리와 표현의 검증 구분](docs/Architecture/플레이폐루프논리시각이중순환체계.md)
- 현실 자료와 기존 업무 기반
  - [커뮤니티·운영 서버·웹 도구](docs/ProjectOverview/README.md)
  - [운영 업무에서 Unity로의 이관](docs/AI/Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)
  - [현실 자료의 수집·검토·저장](docs/AI/현실자료-서버MySQL축적-기획과개발인계-2026-08-31.md)

## 현재 상태와 문서

- [개발 통합 상태판](docs/AI/개발통합상태판.md) · [현재 작업](docs/AI/CURRENT_WORK.md)
- [AI 협업 방식과 대화 기반 의도 관찰](docs/AI/ai-collaboration/README.md)
- [전체 문서 안내](docs/README.md) · [화면 카탈로그](docs/ProjectOverview/app-page-catalog.md) · [변경 기록](docs/Changes/README.md)
- [개발 지침](AGENTS.md) · [운영·Simulation·Unity 책임 구분](docs/Architecture/OperationsSimulationUnity작업흐름분리.md)

기존 `Ssalddel` 이름은 코드·저장 호환을 위해 유지합니다. 게임 상태와 실제 운영 상태는 분리합니다.
