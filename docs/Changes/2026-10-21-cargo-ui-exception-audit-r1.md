# 화물 업무 흐름·예외 안내 검증 r1

- 날짜: 2026-10-21
- 상태: `VerifiedWithFindings / ProductUnchanged`
- [실제 휴대폰 화면](../assets/changes/2026-10-21-cargo-ui-exception-audit-r1/index.html) · [촬영 근거](../assets/changes/2026-10-21-cargo-ui-exception-audit-r1/evidence.json)
- 기준: [APK201 문구 정리](2026-10-21-cargo-concise-ui-r1.md), [앞선 추천→수락→사진→완료 검증](2026-10-21-cargo-ai-phone-verification-r1.md)

정상 흐름의 기본 관문은 마련돼 있다. 다만 예외 안내까지 이상 없다고 결론낼 수는 없다. 휴대폰에서 통신 복구 뒤 오류 잔존과 긴 서버 오류 원문 표시를 재현했다. 제품·시험 코드를 고치지 않고 실제 화면 관찰, 기존 회귀 실행, 소스 검토를 구분해 기록했다. 새 APK는 만들거나 설치하지 않았다.

## 직접 확인한 화면

Samsung SM-A346N의 기존 APK201 두 앱과 로컬 합성 서버를 사용했다. 기존 완료 의뢰/운송9를 조회했으며 수락·도착·사진·완료·결제·신고 명령을 다시 보내지 않았다.

| 검증 | 결과 | 근거 |
| --- | --- | --- |
| 화주 상세 정상 조회 | 현재 상태·금액·다음 이동 표시, 가로 넘침 없음 | baseline/refresh 관측, 화면07 |
| 통신 차단 뒤 상세 조회 | 빨간 인라인 오류와 새로고침/의뢰 조회 표시, 이전 카드 제거 | 콜드 스타트, 화면01 |
| 통신 복구·자동 재조회 | 정상 카드가 돌아오지만 이전 빨간 연결 오류가 계속 표시됨 | 화면03 |
| 수동 새로고침 | 오류가 없어지고 정상 카드 유지 | 화면04/07, refresh 관측 |
| 없는 화주 의뢰 | API 경로·HTTP 상태·응답 JSON·traceId까지 노출되고 긴 원문이 화면 오른쪽에서 잘림 | missing 관측, 화면06 |
| 없는 기사 운송 | ‘운송 정보를 찾지 못했습니다’·다시 불러오기·현재 운송 표시, 완료 행동 없음 | driver-missing 관측, 화면08 |
| 원장 재조회 | 인수완료·차수1·6,200원·AI Applied 동일 | canonical-readback.json |

예외 표시는 별도 팝업이 아니라 화면 안의 `MudAlert`다. 앱 내부 안내를 검증했으며 외부 푸시·백그라운드 알림·소리를 새로 시험하지 않았다.

첫 연결 차단 시도는 기존 HTTP 연결이 유지돼 오류를 만들지 못했다. 이후 화주 앱만 종료·재실행하고 USB 시험 연결을 끊어 오류를 확인했다. 연결은 `finally`에서 원래 `5363→5364`로 복구했다. 휴대폰 Wi-Fi·모바일 데이터·보안 설정·다른 앱·서버는 끄지 않았다.

원본 화면02는 자동 재조회 전 촬영인데 이름이 `auto-recovered`로 잘못 붙었다. 검토용 묶음에서 제외하고 raw에는 남겼다. 일부 탐색 스크립트의 중복 텍스트 selector 오류와 예상 문구 불일치도 제품 오류로 집계하지 않았다. 정정한 selector와 실제 관측을 별도로 남겼다. 화면03의 정상 카드+오류 공존은 실제 PNG로 확인했다.

## 다음 보완 순서

### 1. 실제 재현된 안내 문제

- **복구 후 조회 오류 제거.** [모바일 상세](../../SsalddelApp/ViewModels/Shipper/ShipperRequestDetailPageViewModel.cs)의 성공 처리176~180은 `showMessage:false`이면 메시지를 갱신하지 않는다. 실패187의 오류가 자동 조회360/373 뒤에도 남는다. 조회 성공에 맞춰 해당 오류만 정리해야 한다. 등록 결과 미확인 등 중요한 다른 업무 안내까지 일괄 지우면 안 된다.
- **서버 오류를 짧은 사용자 안내로 변환.** 같은 파일187은 예외 원문을 그대로 화면에 보낸다. 없는 의뢰는 ‘의뢰를 찾을 수 없습니다’, 통신 실패는 ‘연결 확인 필요’처럼 다음 행동과 함께 제공하고 경로·응답 JSON·traceId는 진단 기록으로 분리할 필요가 있다. 실제 고객 정보 유출을 확인한 것은 아니며 이번 입력은 합성이다.

### 2. 상하차의 서버 판단과 화면을 일치

아래는 소스에서 확인한 연결 누락이며 이번 휴대폰에서 보류 업무를 새로 만들지는 않았다.

- **운송 보류 반영.** 서버 [업무 상태](../../Ssalddel/Application/Driver/Transport/Services/기사운송업무상태Projector.cs)45~67은 `운송진행보류`·`다음행동안내`·`가능한행동`을 제공하고 [상태 변경](../../Ssalddel/Application/Driver/Transport/Services/기사운송상태변경CommandExecutor.cs)112는 보류 전이를 거부한다. 그러나 [기사 표시 변환](../../DriverApp/Services/기사운송표시Mapper.cs)9~48과 표시 모델이 이를 전달하지 않는다. 전용 기사 상하차 화면은 다른 완료 조건을 충족하면 버튼이 켜질 수 있어 서버 거절 후에야 보류를 알게 된다. 서버의 최종 거절 자체가 없는 것은 아니다.
- **상차의 미확정 경고 유지.** [상차 화면](../../DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor)27~35는 완료 요청 뒤 재조회 실패/운송 없음 분기에서 기본 오류만 보인다. 일반 분기의 ‘완료 여부 미확인·사진 보관’ 안내까지 도달하지 못한다. 하차에는 별도 경고가 있다. 실패 때도 결과 확인 행동과 사진 보관 안내가 보여야 한다.

### 3. 신고·수락의 중복/늦은 응답 보호

- [신고 서비스](../../DriverApp/Services/DriverTransportExceptionService.cs)53~59는 연결 끊김·시간 초과를 ‘신고가 저장되지 않았습니다’로 단정하고 재시도를 안내한다. 서버가 저장한 뒤 응답만 유실될 가능성을 구별해 결과 확인 절차가 필요하다.
- [신고 ViewModel](../../DriverApp/ViewModels/Driver/Transport/기사운송예외신고ViewModel.cs)66~82는 응답을 기다린 후 운송·계정·페이지 판본을 다시 검사하지 않는다. 화면 이동/로그아웃 뒤 늦은 응답을 막는 회귀가 필요하다. 실제 혼입을 이번 기기에서 재현한 것은 아니다.
- [기존 추천 상세](../../DriverApp/Components/Pages/Driver/02_Recommendation/추천상세Page.razor)117/211과 [배차 처리](../../DriverApp/Components/Pages/Driver/02_Recommendation/배차처리Page.razor)73/165의 수락/거절에는 대기 중 busy guard·비활성이 없다. 기본 지도 추천 경로와 구분해 보완해야 한다. 서버가 실제 중복 운송을 만들었다는 결론은 아니다.

### 4. 화주 Web·테스트 결제 예외

- [Web 상세](../../Ssalddel.WebApp/ViewModels/ShipperRequestDetailPageViewModel.cs)41은 조회 중 새 요청을 버리고 80~82에서 응답을 적용할 때 현재 의뢰/계정을 재검사하지 않는다. A 조회 중 B로 이동하거나 로그아웃하는 지연 사례의 보호가 필요하다. 모바일의 `ShipperQueryLifetime`과 다른 경로다. 실제 Web 오표시·개인정보 노출은 이번 실행에서 재현하지 않았다.
- [테스트 결제창](../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperRequestPaymentScreen.razor)84~130에는 실패 표시가 없고 오류는 외부 Frame에 표시된다. 모바일 VM252에서 창이 열린 채라 오류가 배경 뒤에 가려질 수 있다. 실패 주입·시각 재현은 후속이다. 실결제/송금에 대한 검증이 아니다.

## 회귀와 한계

이번 실행은 13개 기존 클래스의 **184개 시험 통과·실패0·생략0**과 정상 빌드를 확인했다. 도착/완료 응답 유실, 503·상세 재조회, 사진 보관, 늦은 조회/계정 변경, 인수증 미확정 결과/중복 누름, 추천 만료/차수, 안내 렌더를 포함한다. 이 시험들이 위 신규 발견 분기를 모두 다룬다는 뜻은 아니다. 전체 Task/Release는 이번에 반복하지 않았다.

APK201에 포함된 모바일·공유 소스3,330개 파일의 목록·SHA가 제작 입력과 일치한다. Web·서버는 현재 소스를 별도로 검토했으며 위 모바일 제작 입력 대조에 포함되지 않는다. 사진 누락 분기·촬영 취소·실제 보류 건·신고 응답 유실·수락 연타·Web 계정 전환·테스트 결제 실패는 이번 단말에서 재현하지 않았고 코드/기존 시험 수준을 구분한다.

대표 PNG5장은 원본 바이트·SHA를 대조했다. 비교 HTML은 로컬 파일로 1280px·390px에서 이미지5장 로드·가로 넘침0·스크립트 오류0·외부 요청0을 확인했다. 두 앱의 현재 PID에 해당하는 남아 있는 AndroidRuntime 오류 로그에서 FATAL EXCEPTION은0건이었다. 이전 프로세스·다른 태그·전체 장애 이력을 전수 검사한 결과는 아니다.

실행 자료는 `artifacts/local/cargo-ui-exceptions/20261021-r1/`에 있다. `tests/execution.json`·`result.json`·TRX, `probes.jsonl`, `offline-boot.json`, `screens/manifest.jsonl`, `canonical-readback.json`을 보존한다. 제품 수정·새 APK·새 주문·운영 활성화·금융·가중치 학습·클라우드·commit/push는 하지 않았다.
