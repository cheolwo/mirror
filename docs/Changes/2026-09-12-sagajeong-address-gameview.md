# 사가정 도로명·주소·높이 Game View 재검증

날짜: 2026-09-12. 관련 구현·화면 증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다. [기획 r15](../AI/Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/road-address-presentation.r15.md) · [구현 기록](../AI/Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/road-address.implementation.r15.md)

## 결론

canonical `SimulationWorldShell`의 실제 Play Mode에서 공개 지도·도로명·건물번호·후보 높이가 표시된다. 다만 자료 전체가 실측으로 완성된 상태는 아니다. 건물602개 중 도로명+번호580개, 현재 도로명 표현층과 연결568개이며 도로2,397구간 중 명명1,109구간이다. 원 OSM 높이30개와 대장 후보 높이2개 외570개는 임시4m다. 차로 수 수집 자료를 도로 폭에 적용하거나 건물 동일성을 공식 확정한 검증은 아니다.

## 보완

- 첫6개만 노출하던 건물번호 목록에 이전/다음 페이지를 추가했다. 사가정로64개를11쪽으로 탐색한다.
- 주소 창을 y96 아래로 옮겨 기존 방어 준비 버튼과 겹치지 않도록 했다.
- 주소·도로 선택 좌표에 부모 Transform을 반영했다. 실제 World의 X3200 오프셋에서도 선택 건물이 화면 중심에 온다.
- 선택한 주소를 표시하고 전체 보기·도로 변경·비활성화 시 선택을 초기화한다. 원자료·지도·Scene·서버/DB·업무/방어 계약은 변경하지 않았다.

## 실제 화면과 입력

모두 `ScreenCapture.CaptureScreenshot`으로 실제 Game View의 OnGUI까지 포함했다. 카메라 단독 렌더를 UI 증거로 사용하지 않았다. 1399×628 Game View에서 확인했다.

| 경로 | 결과 |
| --- | --- |
| [전체 지도](../assets/changes/2026-09-12-sagajeong-address-gameview/01-whole.png) → [사가정로](../assets/changes/2026-09-12-sagajeong-address-gameview/02-road.png) | 큰길 라벨 클릭, 도로 상세64곳/11쪽 |
| [마지막 페이지](../assets/changes/2026-09-12-sagajeong-address-gameview/03-last-page.png) → [건물 선택](../assets/changes/2026-09-12-sagajeong-address-gameview/04-address.png) | 다음10회, 마지막 항목 사가정로68 선택·확대 |
| 휠·오른쪽 드래그 → [전체 복귀](../assets/changes/2026-09-12-sagajeong-address-gameview/05-return.png) | 확대크기90→72.95258, 중심이동 확인, 전체버튼으로516.6791과 선택해제 |
| [동일로96길51](../assets/changes/2026-09-12-sagajeong-address-gameview/06-height-8m.png) / [동일로92길57](../assets/changes/2026-09-12-sagajeong-address-gameview/07-height-19m.png) | 주소 API로 대상 선택 후 실제 표시·실행 Mesh 꼭짓점8.3m/19.1m 확인. 결손 사가정로39길79는4m 유지 |
| [방어 준비 분리](../assets/changes/2026-09-12-sagajeong-address-gameview/08-defense-boundary.png) → [최종 운영 관찰](../assets/changes/2026-09-12-sagajeong-address-gameview/09-final.png) | 방어 준비·운영 관찰 버튼으로 도로명 UI 숨김/복귀 |

도로·주소·페이지·전체보기·휠·드래그·방어 진입/복귀는 `EditorGUIUtility.QueueGameViewInputEvent`를 통한 합성 입력으로 실제 GUI 이벤트 경로를 검증했다. 물리 마우스 수동 입력 또는 모바일 터치 검증은 아니다. 초기 `EditorWindow.SendEvent`는 처리 반환값만 true였고 상태 전이가 없어 성공 증거에서 제외했다. 높이 두 대상의 직접 선택 API 검증은 입력 검증과 구분한다.

## 시험과 제한

- Unity6000.5.6f1 컴파일 오류0, 도로명주소21·높이19·지도7·방어8·운영5·주소연결8·면목동표현4 = 72/72 통과. 새2개 시험은 EditMode에서 Initialize를 빠뜨려 최초 실패했고 시험 초기화 보완 뒤 통과했다.
- 생성기 재실행 출력 hash `26B2C32FD6F89934BF25E3415287486E625C6EF0369E8C2E0BB6FCBDC43B9DAC` 동일. 원 지도 hash `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3`, Scene hash `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55` 검증 전후 동일.
- 실행 상태는 지도Ready, 도로명78/주소568, 대장후보2건, 관찰 Controller Success/업무0건. 이전 업무 표본의 만료 후 상태이며 이번에 여섯 업무를 새 실행하거나 새 표본으로 대체하지 않았다. 지도 검증과 운영 업무 전체 검증을 구별한다.
- 기존 다른 CompositionRoot의 `SimulationReplayHashMismatch`, 서버 ConnectionError/SessionMissing 등이 재현됐다. 지도·도로명 입력 중 추가 오류는 없었고 마지막 cursor19781 이후 오류0이지만 Console 전체 무오류를 뜻하지 않는다.
- 증거: `artifacts/local/validation/sagajeong-gameview-r15/`의 필터별 결과·초기실패·Console. Unity 쪽 PNG 원본은 `Documentation/Changes/2026-09-12-sagajeong-address-gameview/`.
- 범위 지정 문서 Fast/Task와 수정 문서 링크 검사는 통과했다(`20260912-225928`, `20260912-225943`). 문서 profile은 build/test를 생략하므로 위 Unity 실제 컴파일·72개 시험과 구분한다. 승인 기획 hash도 작업 명세와 일치한다.
- 검증 후 Editor stopped/Scene clean. Scene 저장·E승격·DB/API 쓰기·새 수집은 없었다. 관련 구현·화면 증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다. 작은 화면, 모든78개 도로 개별 클릭, 실제 차로 폭/건물 간격·전체 높이 보완, 실운영·Player build는 미검증/후속 범위다.
