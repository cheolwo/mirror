# 비공개 APK 시험 준비와 역할별 구현 현황

> r71 후속 코드: [기사 업무 선택과 공통 수락 보호](../Changes/2026-10-15-driver-participation-r71.md)는 아래에 기록된 0.1.70 APK의 동결 소스 이후 구현이다. 기존 APK·배포 준비 manifest는 보존했으며, 이 기능을 휴대폰에서 시험하려면 최신 소스로 별도 APK를 다시 준비해야 한다.

2026-10-15. 첫 시험은 일반 앱의 화주 기능과 화물 기사 앱으로 시작한다. 본인 전용폰에서 의뢰 한 건을 마무리한 뒤 소수 참가자로 넓힌다. 홈의 역할 선택 8개와 공통 Release 제작 도구의 APK 대상 7개는 서로 다른 구분이다.

## 현재 만들어진 기능

아래의 구현은 실제 영업·입금 또는 모든 휴대폰에서의 동작 완료를 뜻하지 않는다. 최신 국내 화물의 코드·격리 HTTP/DB·화면 HTML 결과는 [국내 화물 마감 기록](../Changes/2026-10-14-cargo-closeout-r69.md), 이번 보완은 [비공개 APK 준비 기록](../Changes/2026-10-15-private-apk-preparation-r70.md)에 연결한다.

| 이용 입장 | 연결된 업무 | 다음에 확인할 부분 |
| --- | --- | --- |
| 일반 이용자·화주 | 생활 지도 제공/요청·보관 신청, 화물 작성·견적/조건 검토·명시 동의·접수/진행 조회 | 이번 인수증 입력/전송 연결과 실제폰 완료 확인 |
| 화물기사 | 추천 수락·상하차 사진·운송 완료·본인 월별 운송대금 목록/상세, 응답 유실 재조회·보호 사진 복구 | 촬영 권한·사진 크기·앱 재실행·잠금 알림·업데이트 후 자료 보존 |
| 공간 제공자·창고 | 수용 조건·보관 협의·같은 판본 양측 동의·예약·바코드/사진 입고·재고/피킹/반품 | 공간 종료 후 잔여 재고 반출, 입고 수령/취소 순서에 따른 계획 상태 일관성 |
| 판매·마트·작업자 | 주문·피킹·포장·QR 인계·전달, 작업 완료와 보수 근거, 본인 원가 조회 API, 고객 수령/문제 보류와 지급 관문 | 최근 원가 조회·고객 수령·지급 신고의 앱 화면 연결. 지급 준비와 실제 송금 구분 |
| 음식 주문자·음식점·기사 | 주문·수락·유효 기사 배정 뒤 조리·픽업·전달·주문자 수령/내역 | 수락 후 취소 협의를 실제 주문 종결로 반영, 장기 미수령 예외 정산 |
| 운영자 | 지연·중단·지원 사건 검토, 담당자 인계·안내·사건 종결 | 사건 종결과 실제 주문 취소·재고 반출·정산 결정의 전문 처리 연결 |

입고·마트·음식의 상세 근거는 [역할별 시작과 완료 조사](../ProjectOverview/page-docs/role-work-completion-audit-r66.md), [공급자와 공간 협의](../Changes/2026-10-12-supplier-space-inbound-r63.md), [작업 보수와 원가](../Changes/2026-10-13-mart-order-compensation-r67.md), [마트 고객 수령](../Changes/2026-10-13-mart-customer-receipt-r68.md)를 따른다. 각 결과는 서로 다른 판본의 범위이며 최신 전체 단말 완주로 합치지 않는다.

## APK 보관과 다운로드 기준

MYBOX는 공유받은 이용자의 APK 이용을 제한하므로 앱 배포의 기본 경로로 쓰지 않는다. 기존 비공개 GCS를 대상으로 준비하되 공개 버킷이나 공개 ACL로 바꾸지 않는다. 메뉴 사진 보관과 APK 파일은 경로·원장·권한을 구분한다.

- 기존 [Release 제작 도구](../../eng/release/publish-mobile-field-test.ps1)를 재사용한다. 공개 HTTPS 주소, 저장소 밖의 기존 서명키, 패키지 ID, 버전 번호와 파일 해시를 확인한다.
- APK는 버전·파일 해시별 경로에 보존한다. 같은 이름을 덮어쓰거나 파일명만 바꿔 새 버전으로 취급하지 않는다. 서명키 백업과 이전판 위 업데이트를 실제폰에서 확인한다.
- GCS 준비는 로컬 파일 검증과 업로드 계획까지만 수행한다. 실제 버킷 접근·공개 차단 확인, 신규 객체 업로드, 원격 파일 재다운로드/해시 대조, 만료 다운로드 링크 발급과 단말 설치는 각각 별도 결과다.
- 이후 다운로드 링크는 초대 참가자에게만 전달하며 짧은 유효기간을 사용한다. 링크 소지자는 유효기간 동안 내려받을 수 있으므로 서버 계정 접근을 별도로 제한한다. 회수한 링크가 이미 설치된 APK를 지우지는 않는다.
- 안내에는 앱 이름·대상 역할·버전·변경 내용·시험 범위·설치 방법·문의 경로를 담는다. 서명키·비밀번호·인증 토큰·서버 개인정보를 APK 묶음에 넣지 않는다.

### 로컬 준비 도구

[GCS 준비 도구](../../eng/release/prepare-private-apk-gcs.ps1)는 실제 업로드 기능을 실행하지 않는다. 기존 GCS 대상은 Git 제외 `artifacts/local/private-apk-preparation-r70/gcs-target.json`에 보존했다. 과거 메뉴 사진 보관 자료에서 대상만 재사용한 기록이며 현재 버킷 권한을 재확인한 결과는 아니다.

새 실제 HTTPS 주소로 제작한 Release manifest와 독립적으로 확인한 서명 인증서 SHA-256이 준비되면 다음처럼 검사한다. 꺾쇠 괄호 값은 실제 값으로 바꾸며 서명키 비밀번호는 넣지 않는다.

```powershell
pwsh -NoProfile -File eng/release/prepare-private-apk-gcs.ps1 `
  -Mode Validate `
  -ManifestPath 'artifacts/local/mobile-field-test/<version>-<code>/release-manifest.json' `
  -BucketName '<existing-private-bucket>' `
  -ExpectedCertificateSha256 '<trusted-certificate-sha256>' `
  -AndroidSdkDirectory '<android-sdk-directory>'
```

`Validate`는 파일도 쓰지 않는다. 같은 입력의 `-Mode Prepare`는 `artifacts/local/private-apk-gcs/<version>-<code>/<planHash>/gcs-upload-plan.json`만 생성한다. 파일 해시·크기·패키지·버전·Release 서명·정렬·ABI와 출력 경로를 확인하고 버전/파일 해시별 GCS 경로를 기록한다. manifest 주소 검사는 APK에 내장된 서버 주소 또는 실제 통신의 확인이 아니며 두 상태를 false로 남긴다.

배포용 Release APK·실제 서버 주소·신뢰 서명값이 지정되지 않았으므로 현재 실제 배포 계획이나 다운로드 URL은 생성하지 않았다. 오프라인 시험의 가짜 APK/CLI와 버킷 이름은 도구의 반례 검증용이며 사용자 설치 파일로 쓰지 않는다.

### 전용폰용 로컬 시험 APK

일반·화주와 화물기사 APK 두 개를 `0.1.70`(버전 코드 170)으로 제작했다. 기존 Debug 서명을 사용하고 ARM64/x64 런타임을 포함하며 서명·정렬·패키지·버전·파일 해시를 확인했다. 보관 위치는 Git 제외 `artifacts/local/private-apk-preparation-r70/apk/`이고 파일명은 `mirror-shipper-local-0.1.70-170.apk`, `mirror-cargo-driver-local-0.1.70-170.apk`다.

두 파일은 PC의 `http://127.0.0.1:5363/` 시험 API를 USB `adb reverse` 등으로 연결해 사용할 후보다. APK 설치·지도·알림·실제 업무 완주를 확인한 파일은 아니며 외부 사용자용 Release도 아니다. 실제 시험 서버·전용폰이 준비되면 설치와 업데이트를 별도로 검증한다. 이 Debug 파일을 GCS 배포용 manifest로 바꾸지 않는다.

## 설치 전에 남은 조건

| 순서 | 준비 | 통과 기준 |
| --- | --- | --- |
| 1 | 배포 의존성 보안 업데이트 | 아래 SignalR/.NET 보안 공지를 반영한 패키지·실행 환경으로 재빌드/시험하고 외부 서버 공개 |
| 2 | 화주와 화물기사 한 건 마감 | 등록·수락·촬영·완료·화주 인수증을 같은 의뢰로 재조회하고 중단/재개 반례 확인 |
| 3 | 외부 시험 서버 | 실제 HTTPS DNS/TLS와 로그인/API 왕복 확인, 시험 계정·DB·사진 저장 범위 고정 |
| 4 | 시험 환경과 접근 제한 | 별도 Staging + Simulation 설정, 필요한 화물 기능만 선택, 초대 참가자만 서버 접근 |
| 5 | 전용폰 | 권한 거부/재허용·카메라·5MB 제한·GPS/지도·알림·잠금/절전·통신 단절·뒤로가기·앱 재실행·업데이트 확인 |
| 6 | 소수 비공개 다운로드 | 서명된 최신 파일을 GCS에서 다시 받아 해시·서명 확인 후 설치, 구버전 안내·중단/문의 경로 확인 |

공통 서버 설정의 실행 모드는 현재 `Operational`이고 업무 기능은 기본 OFF다. 기존 `compose.transport-v20.override.yaml`도 Operational이며, 음식 현장 시험 override는 음식·화물·합성 배차를 함께 켠다. 이를 그대로 화물 전용 Simulation 시험이라고 부르지 않는다. 이번 준비는 어느 기본값도 바꾸지 않는다.

화면 경로 제한과 숨겨진 다운로드 링크는 서버의 초대 참가자 제한이 아니다. 외부 시험 전에 네트워크 또는 승인 계정으로 접근을 제한하고 철회할 수 있어야 한다. 아직 실제 HTTPS 서버와 최신 APK의 물리폰 왕복은 확인하지 않았다. 예전 Release 7개 기록은 시험 주소와 내부 서명을 사용했고 현재 참조 APK도 남아 있지 않으므로 새 배포 후보를 만들어야 한다.

### 제작 중 확인한 보안 업데이트

2026-10-15 APK restore에서 `Microsoft.AspNetCore.SignalR.Common 10.0.9`의 NU1903 경고를 확인했다. 중앙 SignalR.Client도 10.0.9이고 PC의 ASP.NET Core/.NET 실행 환경도 10.0.9다. [Microsoft 보안 공지 CVE-2026-70159](https://github.com/advisories/GHSA-334f-vvp4-7g6p)는 SignalR 상태 재연결의 서비스 거부 문제를 설명하며 10.0.13을 수정 버전으로 제시한다. 실제 서비스에서 악용을 재현했다는 뜻은 아니다.

이번 범위는 로컬 준비이며 중앙 패키지나 PC SDK/런타임을 일괄 업데이트하지 않았다. 외부 공개 전에 패키지와 서버 실행 환경을 함께 갱신하고 회귀 시험·APK를 다시 제작해야 한다. 현재 로컬 시험 APK를 GCS 배포용 Release로 승격하지 않는다.

## 공식 배포 자료

- [MYBOX APK 공유 제한](https://help.naver.com/service/5594/contents/19243?lang=ko&osType=COMMONOS)
- [GCS 만료 다운로드 링크](https://docs.cloud.google.com/storage/docs/access-control/signed-urls)
- [GCS 신규 객체 생성 전제조건](https://docs.cloud.google.com/storage/docs/request-preconditions)
- [GCS 공개 접근 차단](https://docs.cloud.google.com/storage/docs/public-access-prevention)
- [Android 스토어 밖 배포](https://developer.android.com/distribute/marketing-tools/alternative-distribution)
- [Android 앱 서명](https://developer.android.com/studio/publish/app-signing), [버전 관리](https://developer.android.com/studio/publish/versioning)
- [Android 개발자 인증](https://developer.android.com/developer-verification/guides): 2026-09-30 일부 국가부터 적용, 2027년 확대 예정. 한국 설치 차단이 이미 적용됐다고 단정하지 않는다. 소규모 학습·취미용 계정의 20기기 경로와 실제 서비스 운영용 등록 조건을 구분한다.
