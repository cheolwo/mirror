# 음식점 수락 응답 유실의 서버 상태 복구 r27

2026-10-28. 상태: `Implemented / ValidatedLocalUi / DeviceRestartPending / OperationalUnchanged`. 사용자가 수락 요청을 서버가 저장했다면 앱 재실행 뒤 그 상태를 복구하도록 직접 승인해 `ApprovedByUser / ReadyToDispatch` 범위로 진행했다. 통합 1.0의 음식점 주문 처리 마감이며 [공통 제작 절차](../../시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-input-guide.md)와 기존 [빌지 r26](restaurant-receipt-copies.r26.md)을 따른다. [구현·검증 결과](../../../../Changes/2026-10-28-restaurant-acceptance-recovery-r27.md)를 참조한다.

## 처리 기준

- 수락 전 같은 주문을 서버에서 읽고 현재 음식점 권한·AvailableActions를 따른다. 서버의 수락 시각이 확인되면 다시 수락하지 않고 현재 상태·조리시간을 복구한다.
- 수락 응답을 잃었을 때 같은 계정·선택·취소 범위 안에서 읽기만 재확인한다. 수락이 확인되면 복구 결과를 반환한다. 확인되지 않으면 성공/미접수를 단정하지 않고 상태 확인을 안내한다. 자동 POST 재전송은 하지 않는다.
- 재실행·목록·상세·재연결은 기존 GET을 재사용한다. 서버가 미수락이며 수락 행동을 허용할 때만 사용자가 명시적으로 다시 수락한다. 새 acceptance 저장 원장이나 보호 저장 스키마는 만들지 않는다.
- 결과에 기존 수락 복구를 명시한다. 복구에는 전표 draft를 붙이지 않고 화면 상태만 갱신한다. 빌지는 기존 명시적 전표 출력으로 요청하며 신규 수락의 정상 출력·기본 두 장 설정은 유지한다.
- 취소·거절·완료/더 최신 상태, 다른 주문 응답·계정 변경·인증 실패·취소·늦은 응답을 성공이나 새 인쇄로 바꾸지 않는다. 수락 응답의 식별자와 수락 시각을 확인한다.

## 작업 소유권과 허용 경로

현재 checkout `dev/mirror-integration`과 기존 dirty 작업을 보존한다. root는 `RestaurantDeskApp/Services/음식점주문DeskService.cs`, `Services/음식점주문ProgressSession.cs`의 필요한 작은 수명/기존 요청 해제, `Models/Restaurant/음식점주문DeskModels.cs`, `Components/Pages/OrderDetail.razor.cs`와 필요한 기존 테스트 fixture만 변경한다. 신규 회귀는 `Ssalddel.Tests/Clients/RestaurantAcceptanceRecoveryR27Tests.cs`, `RestaurantReceiptAcceptanceRecoveryR27Tests.cs`로 분리한다. 화면 검증에는 `eng/RestaurantPosPreview`의 기존 자료·port·제어 레이아웃에 합성 복구 응답 선택만 추가한다. 서버 API/DB·수락 정책·권한·자동 배차·알림 설정·기기/프린터는 변경하지 않는다. 검증은 `artifacts/local/restaurant-acceptance-recovery-r27/`에 보존하고 관련 기획/현재 작업/변경 기록만 갱신한다.

## 검증과 완료 기준

같은 서버 상태를 유지한 채 클라이언트 재생성, 수락 저장 후 응답 유실·미저장·조회 실패/다른 주문/취소·거절·인증·계정/선택/화면 전환·취소·낡은 응답을 시험한다. 복구와 일반 GET은 추가 POST·문서 생성·인쇄를 만들지 않고, 신규 명시 수락만 기존 출력 경로로 진행해야 한다. Fast 후 Task와 영향 Android/Windows 소비 빌드를 실행한다. 필요하면 실제 Razor 미리보기에서 복구 안내·다음 행동을 확인하되 시험 대역·제품 서버·실기기·실제 출력은 별도 기록한다. 이 승인에는 commit/push·기기 설치·외부 발송·운영 활성화를 포함하지 않는다.

Fast `20261028-094055`의 관련111통과·Task `20261028-094235`의 전체16,669통과/환경17생략·실패0과 전체 소비 빌드를 확인했다. 신규29사례와 실제 제품 Razor의390px 복구/조회 실패·재조회, 별도 브라우저의 갱신된 인쇄 횟수0→명시 출력1을 확인했다. 선택 소스10개의 지문·HEAD 불변, 기존 dirty99 중 허용3개 외96개를 보존했다. 실제 앱 강제 종료/재시작·제품 서버/DB·물리 인쇄는 미검증이고 소유 미리보기·탭·뷰포트는 정리했다.
