[구현 수용·검증 · 공간 탐색 마무리 · PLAN-SYSTEM-FRANCHISE-OPERATIONS · r55]

# 행정 경계 관찰의 한 단계 상위 복귀

- 승인 근거: 2026-09-26 사용자 “여기까지만 어느 정도 이렇게 마무리”와 후속 “아무튼 뭐 계속 진행해 줘.” 기존 연결의 작은 결손만 같은 개발 스레드에서 `Approved / ReadyToDispatch / Accepted`로 수용한다. 새 공간·자료·게임 기능을 확대하는 승인이 아니다.
- 선행: [r54](visit-review-map-entry.implementation.r54.md), SHA-256 `D5B3093AB7529425D0F5A72B280736D4EE0C4A21C137A395C2DFA3151EF1A1A5`.
- 상태: `Accepted_ImplementationInProgress`. 검증 결과는 아래에 별도로 기록하며 새 Goal/WI·Graph Map·E 승격은 하지 않는다.

## 현재 연결과 제외

현재 코드에는 지구본 → 광역 참고/서울 → 구 → 행정동 선택 경로가 있다. 그러나 준비된 동 진입은 역사 코드 `11260570`의 **사가정 역세권 일부 보기**뿐이며 행정동 전체를 재현하지 않는다. 다른 동은 “디오라마 준비 중”으로 현재 지도를 유지한다. r54의 면목제3·8동 정밀 검토 지도는 Editor의 별도 명시적 진입이다. 두 지도를 통합했다고 해석하지 않는다.

기존 디오라마 복귀는 선택 구·단계·카메라 초점을 보존하지만 선택 동 ID를 별도로 저장하지 않는다. 경계 자료는 Editor Loader의 파일·hash 설정에 의존한다. 배포 앱의 자동 로드·전체 동 연결·현재 행정 경계 정확성은 이번에 완료하지 않는다.

확정 결손은 `서울중간공간View.Update`의 축소 한도에서 현재 단계와 무관하게 `ReturnGlobe()`를 호출하는 것이다. 새 시스템 없이 기존 `ShowDistrict`, `ShowLevel`, `ShowContext`를 재사용한다.

## 이번 계약

- 사가정 단계 → 기존 선택 구 → 서울 → 준비된 광역 참고 → 지구본 순으로 한 단계씩 복귀한다. 광역 자료가 없으면 서울에서 지구본으로 복귀한다.
- 축소 한도와 “상위로” 버튼은 같은 복귀 동작을 사용한다. 광역 화면의 명시적인 “지구본” 버튼은 직접 복귀를 유지한다.
- 새 단계로 이동하면 이전 단계의 준비 중 안내를 지운다. 준비되지 않은 동을 임의로 사가정에 연결하지 않는다.
- 카메라의 기존 보간·자료·고유 식별자·Scene·운영 상태는 바꾸지 않는다. 자동 시험은 단계 전이의 증거이며 실제 마우스 조작·시각적 자연스러움의 증거가 아니다.
- 다음 우선순위를 자동 활성화하지 않는다. 전국 확대·행정동 추가·새 촬영/수집·인접 동 조립·도로 정밀화·r54 지도 통합·기존 Replay 오류 수리는 제외한다.

## 담당과 정확한 쓰기 경로

통합 담당은 Unity 실행을 단독 점유하고 다음 View를 수정한다. 시험 담당은 기존 시험 파일만 수정하며 Unity를 실행하지 않는다.

- Unity `Assets/Ssalddel/Presentation/WorldMap/서울중간공간View.cs` — 시작 SHA-256 `B5D64A750CAE0EE8BBA0137622F30D6104F34B34027C529E4B22C8B5FFCAEF41`.
- Unity `Assets/Ssalddel/Tests/EditMode/서울중간공간Tests.cs` — 시작 SHA-256 `2124229784D9796FF50691C2F1E6B20A5C08956E0D8442B9FEBA778E898D5C3A`.
- Hongdal 이 문서·동일 PLAN `README.md`·`docs/AI/PLANNING.md`·`docs/AI/CURRENT_WORK.md`.
- 비공개 시험 산출물은 Unity `artifacts/globe-parent-navigation-r55/`만 사용한다. 기존 `.meta`를 유지한다.

Scene 시작 SHA-256은 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`, 프로젝트 설정은 `1173FB0729CBD9720435656B7B3EB0DDAF11F43010D4D02E641897698ED7D18D`다. 시험이 만든 설정 변화만 시작값과 대조하여 복구한다. 다른 dirty 작업·Scene은 수정하지 않는다. commit·push·배포 없음.

## 검증 범위

기존 관찰 권위·공간 의미(E1/E2)를 유지하고 부모 전이·준비 중 상태·반복 복귀를 EditMode에서 검증한다(E3 관련). 기존 지구본/행정 계층 시험을 함께 회귀한다. 표현(E4 이후)의 새 자산·카메라 튜닝·실제 입력·Game View·서버 연결 증거는 이번 자동 시험으로 승격하지 않는다. `evidenceStageClaimed=null`.

## 실행 결과

구현·집중 시험 후 기록한다. r54의 합성 Game View와 기존 `SimulationReplayHashMismatch`는 이번 경계 탐색의 실행 증거로 대체하지 않는다.

## 디오라마 규칙 점검

`cross-station-fallback-forbidden`, `missing-coverage-remains-explicit`, `game-view-is-presentation-evidence-only`의 기존 경계를 유지한다. 미준비 동을 다른 모듈에 연결하지 않으며 코드 시험을 화면 증거로 확대하지 않는다. 이번 변경은 새 자료·형상 규칙을 만들지 않는다. **새 디오라마 규칙 후보 없음**.
