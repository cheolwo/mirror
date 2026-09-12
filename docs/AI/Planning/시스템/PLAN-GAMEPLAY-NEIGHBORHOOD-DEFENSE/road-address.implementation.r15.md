# 도로명주소 표현층 r15 구현 결과

- 기준: [승인 범위](road-address-presentation.r15.md), [수직 작업 명세](road-address.e7-work-order.json).
- 상태: `ReadOnlyRoadAddressOverlayImplemented / EditModeVerified / GameViewVerifiedScoped`.

## 후속 실제 화면 재검증 (2026-09-12)

사용자의 실제 적용·재검증 요청에 따라 [Game View 기록](../../../../Changes/2026-09-12-sagajeong-address-gameview.md)으로 후속 검증을 완료했다. 기존 첫6개 제한을6개씩 페이지 탐색으로 보완하고 방어 준비 버튼과 주소 창 겹침, 부모 Transform 미반영을 수정했다. 선택·페이지·귀환·부모이동 회귀를 추가해 최종72/72다. 도로명/마지막 주소/휠/드래그/전체보기/방어 화면 전환은 합성 Game View 입력, 높이 두 후보는 직접 선택 API+화면+실행 Mesh로 구분해 확인했다. 아래는 최초 구현 당시의 검증 이력이며 현재 화면 미검증 상태는 이 절로 대체한다. 전체 E 승격은 없다.

## 결과

`eng/neighborhood/build-sagajeong-road-name-overlay.ps1`가 동결 OSM 원자료와 `SagajeongReference.json`을 함께 검증하고 `SagajeongRoadNameOverlay.json`을 결정적으로 생성한다. 지도 JSON은 바꾸지 않았다. 출력은 도로명78개, 명명 구간1,109개, 연결 건물568개이며 큰길4개와 생활도로74개를 구분한다. 출력 SHA-256은 `26B2C32FD6F89934BF25E3415287486E625C6EF0369E8C2E0BB6FCBDC43B9DAC`이다.

Unity의 `도로명주소표현자료.cs`는 지도/원자료 hash, 출처·귀속·표현 전용 경계, 도로명/way/구간/대표점/중요도/건물 수를 검사하고 변경 가능한 JSON DTO와 분리된 조회 결과를 만든다. `사가정운영디오라마View.cs`는 다음을 제공한다.

- 전체 보기: 사가정로·면목로·용마산로·면목천로를 우선 표시한다.
- 확대 보기: 카메라 중심에 가까운 생활도로를 포함해 최대18개, 근접에서는 최대24개 후보를 고른 뒤 기존 라벨 충돌 검사를 적용한다.
- 도로명 선택: 해당 도로의 공개 OSM 건물번호 최대6개와 나머지 개수를 표시한다. 건물번호 선택은 건물 윤곽 경계상자의 중심으로 카메라를 옮긴다.
- 방어 준비 중: 기존 `GameOverlayVisible` 관문에서 도로명 UI를 표시하지 않는다.
- 오류: 도로명 보완층만 거부하고 지도·업무·높이 표현은 유지하며 상태와 Console 진단을 남긴다.

사업체명·입주·영업 상태·비공개 주소 대장은 새 자산에 포함하지 않았다. 실제 도로 중심선, 출입구, 주소 권위, 길찾기 또는 이동 가능성도 만들지 않는다. 운영 API·DB·Simulation Core·방어 자리·Scene은 변경하지 않았다.

## 검증

- 생성기 재실행 결과 hash 동일: 결정성 통과. 원본 OSM/map hash 불변.
- Unity 6000.5.6f1 컴파일 오류0.
- 새 `도로명주소표현Tests` 19/19 통과: 동결 수량, 큰길/확대 밀도, 주소 조회, 출처·권위7종, 도로 연결·대표점8종, 원본 불변과 View 조립.
- 기존 높이19/19, 사가정 지도7/7, 방어8/8, 운영 표시5/5, 기존 주소 연결8/8, 기존 면목동 표현4/4 통과. 총70/70.
- 연속 필터 실행의 각 완료 상태는 모두 통과였지만 Pipeline 도구의 대기 호출이 겹쳐 `RunFinished` 중복 완료와 상태 파일 공유 위반 진단이 Console에 남았다. 남은 대기 세션 종료와 Console 정리 뒤 기준 cursor 19569 이후 새 오류가 없음을 확인했다. 이를 제품 코드 오류나 깨끗한 최초 Console로 표현하지 않는다.
- 요약 증거: `artifacts/local/validation/sagajeong-road-address-r15/summary.json`.

이는 코드·생성 자산·EditMode 조립 검증이다. Play Mode/Game View의 실제 글자 크기·중첩·클릭·카메라 이동, 작은 화면 가독성은 아직 확인하지 않았다. Scene 저장·E 승격·commit/push도 수행하지 않았다.
