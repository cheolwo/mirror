# 비공개 APK 준비와 화주 인수증 연결

2026-10-15. [승인 범위](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/private-apk-preparation.implementation.r70.md)와 [작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/private-apk-preparation.r70.work-order.json)에 따른 로컬 보완이다.

현재 상태: 구현·로컬 검증·로컬 시험 APK 두 개 제작 완료. [역할별 현황과 배포 순서](../Deployment/PrivateApkPilotReadiness-20261015.md)를 기준으로 실제 APK 설치와 외부 서버 확인을 분리한다.

## 화주 인수증 입력과 등록 결과 확인

기존 증빙 관리 화면에 인수증 번호, 선택 검수 메모, 수령 확인과 등록 버튼을 연결했다. 등록 가능 운송·정산 상태를 먼저 표시하고 서버의 기존 권한·완료 운송·인계 검사를 유지한다. 등록은 한 번만 보내고 응답 유실·거부 뒤 같은 의뢰의 번호와 등록시각을 재조회한다. 결과가 미확인이면 입력과 재전송을 잠그며 명시적인 결과 확인만 허용한다.

처음 조회한 의뢰 ID와 현재 선택을 대조하고, 계정 변경·다른 의뢰 선택·화면 이탈·취소 뒤의 늦은 응답을 폐기한다. 인수증 화면은 sample fallback과 로컬 FakePG 금융 상태 덮어쓰기를 사용하지 않는다. 공통 패널은 표시·콜백만, 모바일 ViewModel은 입력·응답 수명, 기존 서버는 등록 권한을 소유한다.

- [페이지 책임과 코드·원장 연결](../ProjectOverview/page-docs/shipper-receipt-registration-r70.md)
- [모바일 증빙 route](../../SsalddelApp/Components/Pages/ShipperRequestProofsPage.razor) → [공통 등록 패널](../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperReceiptRegistrationPanel.razor) → [등록 ViewModel](../../SsalddelApp/ViewModels/Shipper/ShipperRequestDetailPageViewModel.Receipt.cs) → 기존 `POST api/v1/shipper/requests/{id}/settlement/receipt`

## GCS APK 로컬 준비

[준비 명령](../../eng/release/prepare-private-apk-gcs.ps1)은 기존 manifest와 APK의 해시·크기·Release 서명·패키지·버전·정렬·ABI를 다시 검사한다. 서명은 별도로 입력한 신뢰 인증서와 대조한다. 기본 Validate는 파일도 쓰지 않고 Prepare만 Git 제외 로컬 계획을 만든다. 버전·파일 해시별 객체 경로와 신규 생성 전제조건을 기록하고 기존 파일 덮어쓰기·경로 탈출·junction·시험 서버 주소를 거부한다. 독립 검토에서 writer의 직접 호출 경로도 보호했다.

실제 GCS 호출·업로드·링크 발급·버킷 생성·권한 변경은 하지 않는다. 버킷 비공개·원격 해시·APK 내장 서버 주소·실제 통신·단말 확인도 false로 남긴다. 기존 저장소 대상은 `artifacts/local/private-apk-preparation-r70/gcs-target.json`에만 기록했다. 실제 배포용 서명 APK와 신뢰 인증서가 아직 지정되지 않아 사용자 배포 계획은 생성하지 않았다.

## 검증과 다음 확인

- GCS 도구 오프라인 73/73 통과. 합성 ZIP과 fake CLI 반례 시험이며 실제 APK나 저장소 증거가 아니다. 기록: `artifacts/local/validation/private-apk-gcs-r70/01ccb83ce20d40a991427e40c8dd2269/results.json`.
- Fast 155개 통과, 실패 0개. 인수증 ViewModel·HTTP·실제 공통 패널 정적 렌더 30개를 포함한다. 공통 UI·일반 소비 앱 빌드 오류 0개. 기록: `artifacts/local/validation/20261015-092250/`.
- 전체 소비 앱 빌드 오류 0개·경고 134개. Task 11,425개 통과, 실패 0개·환경 의존 MongoDB 15개 건너뜀. TRX의 결과별 outcome과 console을 대조했다. 기록: `artifacts/local/validation/20261015-092646/`.
- 실제 loopback 화면 호스트에서 공통 등록 패널의 등록 전·결과 미확인·등록 후·완료 전 차단 4개 상태와 공통 CSS 응답을 확인했다. 입력은 합성 자료이며 실제 화주/기사 완주가 아니다. 브라우저 도구의 inventory가 `apps=[]`, `browsers=[]`여서 실제 paint·viewport·클릭·터치 확인은 하지 못했다. 호스트는 확인 뒤 종료했다. 기록: `artifacts/local/private-apk-preparation-r70/ui-preview/ssr-report.json`.
- 일반·화주와 화물기사의 독립 실행 Debug APK 두 개를 기존 Debug 키로 제작했다. 버전 `0.1.70`·코드 `170`, 기존 패키지 ID, 서명·정렬·ARM64/x64·복사 해시와 완료 뒤 소스 지문을 확인했다. 크기는 각각 137,646,704·160,678,525바이트다. 기록: `artifacts/local/private-apk-preparation-r70/apk/local-apk-manifest.json`. PC loopback `http://127.0.0.1:5363/`용이며 서버 시작·물리폰 설치/통신·외부 배포 증거가 아니다. 일반·운영자 앱의 실제 Firebase 푸시 설정과 FDriver 지도 ID가 없는 일반 빌드 경고를 실제 연결 완료로 바꾸지 않는다.
- APK restore에서 기존 SignalR.Common 10.0.9의 NU1903을 확인했고 PC ASP.NET Core/.NET도 10.0.9다. [공식 보안 공지](https://github.com/advisories/GHSA-334f-vvp4-7g6p)의 수정 버전 10.0.13을 외부 배포 전 필수 후속으로 기록했다. 패키지·SDK·런타임을 이번에 일괄 업데이트한 것은 아니다. 준비 도구 통과나 로컬 시험 파일을 보안 갱신·외부 배포 완료로 승격하지 않는다.

기존 변경 기준선 278개 중 270개는 바이트 그대로이며 수정한 8개도 이번 허용 경로다. 기준선과 소스 지문은 `artifacts/local/private-apk-preparation-r70/`에 보존한다. 물리폰 촬영·GPS·잠금 알림·설치/업데이트, 외부 HTTPS·초대 접근과 실제 운영/입금은 후속이다. 실제 업로드·클라우드 설정·운영 활성화·서명키 생성·Unity·commit/push는 실행하지 않았다.
