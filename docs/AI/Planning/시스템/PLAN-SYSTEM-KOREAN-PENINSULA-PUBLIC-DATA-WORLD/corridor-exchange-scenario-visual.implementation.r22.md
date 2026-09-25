# [개발 인계 · 월드·관찰·표현 · 한반도 교류 생활상 시각 검증 · r22]

## 승인 기준

- 상위 기획: `korean-peninsula-semantic-zoom-information-layers.decision.r19.md`
- 상위 기획 SHA-256: `95662B68FF620152B1B2477AEDA6B4623D000AC3EE384296CB112766F447315D`
- 선행 구현: `semantic-zoom-first-slice.implementation.r20.md`
- 사용자 승인: 2026-09-21, 남측과 북측 사이의 철도·물자 교류 생활상을 게임 비유로 시각 확인
- 상태: `Approved / ReadyToDispatch`

## 목표

기존 한반도 교류축 화면에 실제 운행을 주장하지 않는 `가상 교류 시나리오` 표현을 추가한다. 부산에서 신의주까지의 상대적 거점 순서, 구간별 자료 상태, 남행·북행 열차와 생활 물자 표식을 한 화면에서 관찰하고 실제 Game View로 확인한다.

## 권위와 표현 경계

- 움직이는 열차와 물자 표식은 `SimulationAnalog` 표현이며 현재 통행·실제 운행·교역량·정밀 노선을 뜻하지 않는다.
- 현재 국내 운행, 역사적 연결, 단절, 관계 검토 중, 국제 회랑 기술을 색과 문구로 구분한다.
- 단절 구간을 열차가 실제로 통과하는 것처럼 확정하지 않는다. 전체 왕복 애니메이션은 교류가 성립했을 때의 생활상 비유임을 화면에 항상 표시한다.
- 선택·관찰·애니메이션은 운영·Simulation 권위와 저장 상태를 변경하지 않는다.

## 첫 시각 흐름

```text
대한민국 선택·확대
  → 한반도 서부 교류축
    → 부산–신의주 상대 거점과 구간 상태
      → 북행·남행 가상 열차와 생활 물자 표식 왕복
        → 도라산 선택·근거 카드
          → 축소·지구본 복귀
```

## 허용 쓰기 경로

- `Hongdal/docs/AI/Planning/시스템/PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD/**`
- `Hongdal/docs/AI/PLANNING.md`
- `Hongdal/docs/AI/CURRENT_WORK.md`
- `ssalddel/Assets/Ssalddel/Presentation/WorldMap/한반도교류축View.cs`
- `ssalddel/Assets/Ssalddel/Presentation/WorldMap/세계지구본View.cs`
- `ssalddel/Assets/Ssalddel/Tests/EditMode/세계지구본Tests.cs`
- 검증 캡처 경로

Scene·Builder·카탈로그 원본·서버 권위 코드는 수정하지 않는다.

## 완료 조건

- 8개 거점과 7개 구간이 화면에서 순서대로 판독된다.
- 가상 시나리오 고지와 실제 운행 아님 고지가 항상 보인다.
- 두 방향의 열차 표식이 프레임마다 움직이되 업무 상태를 변경하지 않는다.
- 기존 지구본 시험과 신규 표현 계약 시험이 통과한다.
- 실제 `SimulationWorldShell` Game View 캡처에서 한반도 교류축 화면을 확인한다.
- commit·push와 E 단계 승격은 하지 않는다.

## 구현 결과

- 한반도 교류축 화면을 `ScreenSpaceCamera`로 전환해 전용 지구본 카메라의 실제 Game View 증거에 포함되게 했다.
- 부산–서울–도라산–판문–봉동–개성–평양–신의주 8개 거점과 7개 구간을 남북 상대 순서로 표시한다.
- 현재 국내 운행·역사 연결·현재 단절·관계 검토·국제 회랑 기술을 색과 상태 문구로 분리했다.
- 북행·남행 가상 물자 열차 두 개와 식료품·의약품·생활물자·완성품 관찰 카드를 추가했다.
- 화면 상단에 `SimulationAnalog · 실제 운행/통행/교역량이 아닙니다`를 고정 표시했다.
- 열차 표식은 `Time.unscaledTime`에 따른 화면 보간만 수행하며 원장·Simulation·운영 상태를 쓰지 않는다.

## 검증 결과

- Unity 6000.5.6f1 연결 Editor 재컴파일 뒤 `세계지구본Tests` 13/13 통과.
- 시험에서 관찰 전용 경계, 거점 8개, 구간 7개, 이동 표식 2개와 실제 운행 아님 고지를 검증했다.
- 실제 `SimulationWorldShell` Play Mode에서 대한민국·거리 38·도라산 선택 상태를 조립했다.
- 3초 간격으로 북행 표식 Y가 `-127.04 → -257.19`, 남행 표식 Y가 `-436.96 → -306.81`로 바뀌어 양방향 이동을 확인했다.
- Game View 캡처: `C:/Users/user/ssalddel/Assets/artifacts/korean-peninsula-exchange-scenario-r22.png`.
- 공식 Scene과 Builder는 저장 변경하지 않았다.

## 남은 위험

- 이 화면은 생활상 비유이며 열차 mesh·선로 지형·적재량·교환 원장·NPC 승하차는 아직 없다.
- Play Mode 전체 Scene에서는 이 변경과 무관한 기존 저장 hash 불일치와 로컬 서버 미연결 오류가 다시 관찰됐다. 따라서 Console 0 증거와 전체 Scene 건전성은 확보하지 못했다.
- 실제 DB/API 상태 사본, 북측 상세 자료, 현재 통행 판정은 구현하지 않았다.
- commit·push와 E 단계 승격은 수행하지 않았다. 새 디오라마 규칙 후보 없음.
