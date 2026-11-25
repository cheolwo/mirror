# 기사 앱 비동기 수명과 지도 좌표 검증 정리 r29

상태: `ApprovedByUser / Implemented / ScopedTestsPassed / ActualDevice136 / OperationalUnchanged`. 2026-10-28 사용자가 연결 휴대폰의 화면을 켤 수 있으면 켜고 기존 프로젝트 리팩토링을 계속하도록 요청했다. 새 기능 확대보다 현재 기사 앱의 늦은 비동기 결과와 지도 fallback 좌표 검증을 좁게 보완한다. [실제 결과](../../../../Changes/2026-10-28-cargo-client-lifetime-r29.md)에서 구현·시험·기기 확인을 분리한다. 기기 확인은 합성 표시용 HTTP에 한정하며 제품 서버/DB·실GPS/FCM·실운송 완주가 아니다.

## 범위와 기준

- 기준 HEAD `8a97384604e6c68a1b00fc71a612d699a3651825`, 브랜치 `dev/mirror-integration`. 시작 시 기존 변경159파일의 해시와 관련 원본 사본을 로컬 `artifacts/local/cargo-client-lifetime-r29/`에 보존했다. 이전 작업을 초기화하거나 함께 stage하지 않는다.
- 저장된 푸시 토큰 등록이 페이지 이탈·전경 복귀·계정 변경 후 실패해도 현재 운송/조회 안내를 덮지 않게 한다. 수명 취소·지연 결과 무시·한 수명 내 중복 호출 방지를 검증하고 원문 예외나 토큰을 화면에 표시하지 않는다. 실제 FCM 설정·토큰 발급·서버 권한은 바꾸지 않는다.
- 현재 업무 fallback 지도의 좌표도 기존 `DriverNativeLocationPolicy` 범위 검증에 맞춘다. 상차/하차 좌표 쌍을 각각 판정하고 한 쌍이 무효여도 다른 확인된 지점을 보존한다. 잘못된 좌표 사이를 이어 그리거나 새 위치를 추정하지 않는다.
- 연결 휴대폰은 깨우기와 인증이 필요 없는 잠금 대기화면 해제를 수행했다. 검증 중 USB 충전 시 화면 유지 설정은 원래 값을 기록하고 종료 때 복원한다. PIN·생체 인증·보안 설정을 변경하거나 우회하지 않는다.

## 허용 경로와 담당

- 비동기 수명 담당: `DriverApp/NativeDriverHomePage.xaml.cs`, 필요시 같은 페이지 XAML의 별도 연결 안내, `DriverApp/Services/DriverForegroundPushRegistration.cs`, 직접 회귀 `Ssalddel.Tests/Clients/CargoPushRegistrationLifetimeR29Tests.cs`, 시험 프로젝트의 해당 linked source만 추가.
- 통합 담당: `DriverApp/Services/DriverHomeRoutePlanningService.cs`, `Ssalddel.Tests/Clients/CargoFallbackMapCoordinatesR29Tests.cs`, 이 기획과 `CURRENT_WORK.md`·`PLANNING.md`·변경 기록/목차·기사 페이지 책임 문서·통합1.0 마감표/현재 판본의 관련 범위, 실제 합성 기기 PNG와 로컬 검증/기기 기록.
- 서버·API 계약·원장·운영 플래그·결제/지급·실제 배차·배포·커밋/푸시는 변경하지 않는다. 각 담당은 다른 담당의 파일을 수정하지 않으며 빌드·시험·기기 실행은 통합 담당이 순서대로 수행한다.

## 검증

지연 성공/실패와 취소·계정/수명 변경·재진입, 현재 요청의 실패 안내와 중복 호출을 직접 회귀로 확인한다. 지도는 무효 위도/경도·부분 누락·한쪽만 정상·정상 좌표·분기 뒤 다음 지점과 기존 선분/정차순서를 검사한다. 수정한 범위의 Fast와 Task를 수행하고 실패·환경 생략을 별도로 기록한다.

실제 휴대폰에서는 가능한 현재 앱의 깨우기·화면/복귀를 확인하고, 새 APK를 포장/설치한 경우에만 새 소스의 기기 확인으로 보고한다. 합성 자료·지도 fixture·설치/화면·제품 인증 서버·GPS/FCM·실물/운영은 서로 다른 근거다. 기기가 다시 잠겨도 독립 코드/시험 작업은 계속하고 필요한 인증만 별도로 남긴다. 기존 앱과 데이터·사용자 설정을 보존하며 이번 소유 프로세스/연결과 임시 화면 유지 설정만 정리한다.
