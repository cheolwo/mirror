# 화물 기사·MAUI 화주 연결과 조회 복귀 r1

사용자의 전체 앱 보완 우선순위 중 기존 화물 앱의 진입·인증·조회 수명을 먼저 보완한다. 새로운 업무 화면이나 서버/API/DB 계약은 만들지 않는다. 화주의 실제 연락처·시간창 전달, 창고 인계, 실제 운송 한 건 완주는 후속 대상이다.

## 변경과 책임

| 대상 | 기존 결손 | 보완 |
| --- | --- | --- |
| Driver 네이티브 지도 홈 | 메뉴와 현재 운송이 동일한 일반 복귀 | 기존 MainPage로 목적별 경로 인계. 준비된 Blazor 범위는 NavigationManager, 새 화면은 StartPath |
| Driver 로그인·API | 만료 상태 잔존, 늦은 응답이 새 세션을 덮거나 종료, 푸시 실패가 로그인 성공을 막음 | 토큰 판본과 로그인 수명 분리, 현재 판본 조건부 변경, 일시 실패 유지, 부가 등록 실패 후 업무 복귀 |
| Driver 진행 중 운송 | 초기 await 뒤 이탈해도 폴링 시작, 이전 캐시 표시 | 초기 조회 취소·폐기 검사, 세션별 표시와 재조회·로그인 복귀 |
| Driver 상차·하차 | 캐시 없는 직접 진입에서 조회 없음 | 기존 상세 API로 같은 운송 ID 재조회. loading/error/retry/login, 선택 변경·이탈·인증 변경 후 응답 및 입력 차단 |
| MAUI 화주 목록·상세 | 겹친 조회와 선택 중 요청 무시, 늦은 결과 적용 | 조회 직렬화·최신 선택·세션·화면 수명에 따른 적용, 개인 입력·모의 결제 안내 정리 |

상하차의 서버 요약/상세 응답에는 수치 거리가 없다. 기존 모델 호환 필드는 유지하지만 새 하차 화면은 이를 `0.0km`라는 확인된 거리로 표시하지 않고 `거리 미확인`으로 표시한다. 서버가 제공하지 않는 화물 상세를 만들어 채우지 않는다. 사진 촬영/완료·예외 신고의 기존 권한과 서버 상태 전이를 유지한다.

## 코드와 원장

- 기사: 기존 [인증](../../../DriverApp/Services/AuthSession.cs) → [API Client](../../../DriverApp/Services/DriverApiClient.cs), [상세 조회](../../../DriverApp/Services/기사운송상세조회Service.cs) → `GET api/v1/driver/transports/{id}`. [상차](../../../DriverApp/ViewModels/Driver/Transport/기사상차PageViewModel.cs)·[하차](../../../DriverApp/ViewModels/Driver/Transport/기사하차PageViewModel.cs)는 같은 원장을 조회한다.
- 화주: [목록](../../../SsalddelApp/Components/Pages/TransportWorkspace.razor)·[상세 모델](../../../SsalddelApp/ViewModels/Shipper/ShipperRequestDetailPageViewModel.cs) → [조회 수명](../../../SsalddelApp/ViewModels/Shipper/ShipperQueryLifetime.cs) → 기존 `IShipperOperationsService`. 화주 계약/DB 변경 없음.
- 소비 호스트는 `DriverApp`과 MAUI `SsalddelApp`이다. Web 화주의 별도 linked source나 음식 `FDriverApp`의 기존 실행 결과를 이 작업의 UI 증거로 합산하지 않는다.

## 검증 경계

집중 회귀시험은 실제 인증·API·상하차 PageViewModel·화주 상세 소스를 링크한다. HTTP·저장소·카메라의 기기 경계만 시험 어댑터로 대체한다. 실제 기기 사진/네트워크·상하차 완료·실결제·은행 지급 증거가 아니다. 최신 실행 결과와 화면은 [변경 기록](../../Changes/2026-10-03-cargo-workflow-recovery-r1.md)에 정리한다.

현재 격리 food observer의 합성 계정 로그인은 200이나 화물 목록·현재 API는 404다. 화물 데이터/기능 환경 준비 없이 이를 정상 빈 운송 또는 운송 완료로 판정하지 않는다. 원시 로그·시험·APK·소스 지문은 Git 제외 `artifacts/local/cargo-workflow-recovery-r1/`에 보관한다. 영상/MYBOX·커밋·푸시는 이번 작업에 포함하지 않는다.
