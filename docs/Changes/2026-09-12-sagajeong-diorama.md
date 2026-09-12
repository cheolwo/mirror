# 사가정역 생활 디오라마 · 정적 운영 관찰

날짜: 2026-09-12. 커밋 전. [승인 기획](../AI/Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/diorama.r3.md) · [구현·검증 기록](../AI/Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/implementation.r3.md)

## 화면 변화

기존 canonical `SimulationWorldShell` 안에 사가정역 주변 1km 동결 지도의 도로·건물, 전용 관찰 카메라와 여섯 가상 완료 업무 표식을 연결했다. 지도 계산, 지역 배치 자료, 업무 상태 사본과 Unity 표현의 책임을 분리했다. 생성 콘셉트는 색감·구도 참고이며 지도 원자료를 대신하지 않는다.

음식 배달·화물 운송·창고·마트를 역할색과 이름표로 구별한다. 실제 업소·사람·현재 차량 위치가 아니며 움직임은 구현 범위에서 제외했다. 유효기간이 지난 업무는 제거하고 위치 연결이 없는 자료를 임의로 배치하지 않는다.

## 실제 화면 증거

- [최종 전체 UI](../assets/changes/2026-09-12-sagajeong-diorama/game-frame-layout-final.png)
- [최종 업무 상세 UI](../assets/changes/2026-09-12-sagajeong-diorama/game-frame-layout-detail-final.png)
- [최종 전체 귀환 UI](../assets/changes/2026-09-12-sagajeong-diorama/game-frame-layout-return-final.png)
- [전체 지도](../assets/changes/2026-09-12-sagajeong-diorama/whole-final.png)
- [첫 업무 근접 보기](../assets/changes/2026-09-12-sagajeong-diorama/detail-final.png)
- [전체 귀환](../assets/changes/2026-09-12-sagajeong-diorama/return-final.png)

최종 UI 세 PNG는 실제 Play Mode의 ScreenCapture로 OnGUI까지 포함한다. 좌하단 안내 카드와 기존 시점 버튼, 업무 라벨의 겹침 해소를 확인했다. 나머지 지도 세 PNG는 명시한 관찰 카메라의 렌더링이며 OnGUI를 포함하지 않는다. 전환은 검증 메뉴 호출로 수행했으므로 실제 클릭·휠·드래그 입력 검증과 혼동하지 않는다. 검증 종료 후 Editor는 정지·Scene clean 상태이며 캡처 중 Scene 저장과 소스 변경은 없었다.

## 검증 수준과 남은 범위

Unity 관련 EditMode 41/41 및 컴파일 오류 0. 공유 상태 사본 Task 시험은 해당 실행 시점 779/779, solution build 통과다. 전용 합성 실행은 600초 완료, 6/6 발행, Outbox 대기·실패 0이다. 실제 지도 Renderer 5개와 관찰 카메라 1개, 여섯 업무가 전체·선택·귀환 캡처 동안 유지됐다.

초기 TTL 만료, 잘못된 카메라 선택, 환경 표시 등록 누락, 비활성화 후 연결 재생성 결손은 별도 실패 기록과 수정 시험을 남겼다. 다른 영역의 Console 전체 무오류, 실제 운영 전체 업무, Player build 및 생성 콘셉트 수준의 시각 마감은 선언하지 않는다. 상세 자산·이동 애니메이션·실사용자 연결은 후속 범위이며 commit·push·운영 활성화는 하지 않았다.
