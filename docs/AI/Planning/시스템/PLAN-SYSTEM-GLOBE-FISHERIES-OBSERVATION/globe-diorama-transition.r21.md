# 지구본 → 준비된 사가정 디오라마 전환 r21

상태: 사용자 승인 범위의 작은 로컬 표현 구현·Play 확인. 정식 빌드 검증 아님.

## 범위

- 기존 `세계지구본View`와 `사가정운영디오라마View`, canonical `SimulationWorldShell` 재사용.
- 역세권 선택 뒤 디오라마 열기 또는 최대 확대에서 추가 휠 입력으로 진입한다. 임의 지역을 사가정으로 치환하지 않는다.
- 기존 동결 자료의 `Ready` 및 ModuleHost 활성화 성공을 먼저 확인한다. 실패 시 지구본을 유지한다.
- 약 0.35초 확대·페이드 뒤 카메라 전환, 약 0.45초 디오라마 확대·페이드 해제. 중복 진입과 전환 중 조작을 막는다.
- 복귀 시 진입 전 지구본 회전·거리와 역세권 선택을 복원한다.
- 현재는 구체를 평면으로 변형하거나 두 지형을 지리적으로 정합하는 구현이 아니다. 비동기 신규 지역 자료 수집·스트리밍도 이번 범위 밖이다.
- 서버·NPC·업무 상태 변경, 새 Scene, DB 변경 없음.

## 검증

- Unity 컴파일 및 기존 `세계지구본Tests` 22/22 통과.
- 실제 Play의 기존 공개 검증 호출로 StationArea → 디오라마 → StationArea 확인. 중복 진입 false.
- 복귀 거리 33, 회전 (-0.374527, -0.348595, -0.029666, 0.858680)이 진입 전과 동일.
- 실제 Game View 캡처: [진입 전](../../../../assets/changes/2026-09-23-transport-preview/transition-origin.png), [디오라마](../../../../assets/changes/2026-09-23-transport-preview/transition-diorama.png), [복귀](../../../../assets/changes/2026-09-23-transport-preview/transition-return.png).
- 물리 휠·버튼 입력과 전환 중간 프레임의 부드러움은 별도 사용자 검토 대상. 정지 캡처로 애니메이션 품질을 확정하지 않는다.
- 기존 서버 세션 누락·ConnectionError가 존재한다. 업무 결과 0건/ConnectionUnavailable 화면이며 실운영 연결 완료나 Console 오류 0이 아니다.
- Play 종료, Scene 저장·commit·push 없음.

## 후속

카메라 목적지·지역 원점 정합과 자료 준비 중 표시를 다음 좁은 개선으로 검토한다. 기존 OSM/건축 자료 출처는 원래 디오라마 자료를 그대로 사용한다. 새 디오라마 공통 규칙 후보 없음. E 증거 승격 없음.
