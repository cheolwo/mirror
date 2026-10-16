# 추천 카드 여백과 응답 색 정리 r73

2026-10-16. 사용자는 추천 카드의 흰 여백을 줄이고 거절은 파랑, 수락은 주황으로 구분하도록 요청했다. 카드 높이를 줄이고 금액·주소를 나란히 배치했으며 Fast·전체 Task·APK190 설치 일치와 실제 두/세 건 카드·색·선택 지도·확인 창 취소·수신 OFF 복귀를 확인했다. 긴 실제 주소 스크롤과 실제 수락/거절 확정은 검증하지 않았다. [r72 APK189](2026-10-16-direct-recommendation-card-actions-r72.md)의 전체 시험과 기기 결과는 이전 판본 이력으로 유지한다.

## 범위와 기준

[페이지 책임 카드 r23](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#추천-카드-공백과-응답-색-정리-r23--r73), [페이지 단일 책임 기준](../Architecture/WholeRoadmapPagePrinciple.md), [역할 앱 시각 기준](../Architecture/RoleAppVisualDesignStandard.md)을 대조했다. 화면의 질문은 선택한 추천 한 건을 배달료·픽업지·전달지로 판단해 거절하거나 수락하는 것이다. 새 정보·기능·상세 진입·별도 업무를 추가하지 않는다. 응답 색은 이번 사용자 지정이며 모든 역할 앱의 공통 주 강조색을 바꾸지 않는다.

배달료 이름·금액을 왼쪽 104 DIP 열로 옮기고 오른쪽에 픽업지·전달지 두 줄을 배치해 가로 공간을 사용했다. 배달료 이름은 11 DIP, 금액은 굵은 18 DIP, 두 주소는 13 DIP다. 전체 요약의 세로 스크롤과 하단 좌측 거절·우측 수락 48 DIP 조작 영역을 유지한다. 카드 높이는 128에서 116 DIP로 12 DIP 줄였고 내부 여백은 가로8/세로4, 응답 행과 간격4 DIP를 사용한다. 아래 기기 관측에서 작은 카드의 두 열과 합성 한 줄 장소·버튼 표시를 별도로 확인했다.

카드 표면은 `#F8FAFC`, 선택 경계는 중립 `#64748B`, 비선택은 기존 연한 경계를 사용한다. 거절은 파랑 `#2563EB`·흰 글자, 수락은 밝은 주황 `#FB923C`·짙은 글자 `#172B36`로 정했다. 새 그림자·중첩 카드·장식 애니메이션은 추가하지 않는다.

[추천 페이지](../../DriverApp/NativeDriverRecommendationPage.cs) 최종 소스 SHA-256은 `5a59f73fbcb50269b2affa2430dbb89cb012dbb29c91d2bbee779e19218f30a5`다. [수정 전 r72 사본](../../artifacts/local/physical-phone-r67/.private/source-backups/recommendation-card-r73-before-compact/manifest.json)의 `c0bf2aa8c91dd197852a4460f745605061dde2d3394de699f215205ceebb823b`와 실제 파일을 대조해 높이·요약 배치/글자·색·경계 및 분리된 배달료 이름 외 변경이 없음을 확인했다. 응답 메서드·가드·지도·서버/공개 계약은 바꾸지 않았다.

## 독립 검토와 확인할 위험

- 금액과 두 주소는 같은 추천 판단의 근거이므로 한 카드 안의 두 열로 배치할 수 있다. 금액 강조와 주소의 일반 굵기·왼쪽 정렬을 구분하고 정보·문구·금액 바인딩을 보존한다.
- 주소 열이 좁아져 긴 주소가 줄바꿈하면 116 DIP 안에서 다시 잘릴 수 있다. 전체 요약의 기존 세로 스크롤을 유지해 긴 주소를 생략하지 않고 확인하도록 구성했다. 이번 실제 APK에서는 한 줄 픽업·전달 장소와 버튼 비침범을 확인했으며 긴 실제 주소의 스크롤은 검증하지 않았다. 한 줄 장소의 성공을 긴 주소 성공으로 옮기지 않는다.
- 색만으로 응답을 구분하지 않고 `거절`·`수락` 문구와 좌우 위치를 유지한다. 일반 버튼 글자의 대비 기준은 4.5:1이다. 선택한 파랑/흰 글자는 5.169:1, 밝은 주황/짙은 글자는 6.465:1로 계산했다. 본문/카드 배경은 13.984:1, 선택 중립 경계/카드 배경은 4.548:1이다. 이는 RGB 상대 휘도로 계산한 색 대비이며 실제 화면 판독 검증을 대신하지 않는다.
- 선택한 카드의 경계는 중립 배경·일반 경계와 구분돼야 한다. 옆 카드가 보이더라도 해당 버튼은 비활성이고 실제 `CurrentItem`·선택 ID/추천 차수와 같은 한 건만 응답하도록 유지한다.
- 지도 크기·중심·줌·카메라 여백은 카드 높이에 맞춰 바꾸지 않는다. 전체 유효 핀·선택 주황/파랑·다른 회색, 좌우 넘김과 기존 카메라 유지·조회 실패 복구를 보존한다.
- 경고 확인·취소·만료·최신 원장 조회·페이지/작업공간 세대·미확정 수락 보호는 시각 변경의 대상이 아니다. 로딩·오류·현재 상태 확인·운행 지도 복귀도 유지한다.

## 검증 상태

| 단계 | 현재 상태 |
| --- | --- |
| 기준·책임·가독성 검토 | 위 기준과 좁은 폭·주소·대비 위험 검토 완료 |
| 제품 구현·소스 고정 | 116 DIP·두 열·파랑/주황 구현, 위 최종 해시 확인 |
| Fast | [앱 빌드·69개 통과·실패/생략 0개](../../artifacts/local/validation/20261016-compact-card-r73/fast.log) |
| Task | [솔루션 빌드·전체 12,274개 통과·실패0/환경 의존16개 생략](../../artifacts/local/validation/20261016-compact-card-r73/task.log) |
| APK190 제작 | [제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-compact-card/cargo-driver/manifest.json), 0.1.190 / 190 Debug 서명·정렬 확인 |
| APK190 설치·해시 | [기기 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r190.json)의 버전·해시 일치 |
| APK190 실제 카드·색·선택 지도 | V06/V07 두 건, V10/V12 세 건 양방향 넘김·전체 핀·선택 색·카메라 유지 확인 |
| APK190 확인 창·복귀 | 거절/주의사항 창 취소, V15 신규 수신 OFF PNG·UI 확인 |
| 긴 실제 주소 스크롤·수락/거절 확정 | 미검증, 실제 확정은 실행하지 않음 |

Fast는 이번 제품 경로 하나와 `DriverRecommendationMarkerPresentationR71Tests`, `DriverRecommendationSelectionR69Tests`, `CargoNativeHomeWorkflowTests`, `DriverLocationDirectionR68Tests`의 명시 필터로 실행했다. [상세 시험 로그](../../artifacts/local/validation/20261016-195402/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-195402/Ssalddel.Tests.trx)의 total/executed/passed 69·failed/error/timeout/notExecuted 0을 대조했고 소스 해시도 동일했다. 시각 변경을 그대로 복제하는 새 시험은 추가하지 않았다.

Task에도 같은 필터를 전달했으나 기존 [검증 도구](../../eng/validate-changes.ps1)의 Task 분기가 `Mode=Full`·`Filter=null`로 고정해 실제 실행은 솔루션 빌드와 전체 시험이다. 도구를 바꾸지 않고 실제 범위로 완주했다. [최종 전체 시험 로그](../../artifacts/local/validation/20261016-195556/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-195556/Ssalddel.Tests.trx)의 total12,290·executed/passed12,274·failed/error/timeout/notExecuted0을 직접 대조했다. 환경 의존 생략16개는 시험 로그의 별도 관측이고 시험 시간은 2분41초다. 종료 후 소스 해시는 `5a59f73fbcb50269b2affa2430dbb89cb012dbb29c91d2bbee779e19218f30a5`로 동일했다. 이전 r72 전체 통과와 별도 실행 결과다.

## APK190 제작·설치

`phone-r67-compact-card` 판본은 0.1.190 / 190이다. APK SHA-256은 `0679981e3c299a19c6c46a0a08be8f2bbcfcb818e952325caefe8954a933d816`, 제작 원장 SHA-256은 `5172de977dbbb721f0672389d4ab1908c0a54057609bb6327636467f9874e418`이며 설치 기록의 버전·파일 해시 일치와 제작 원장의 소스를 대조했다. 빌드 시점의 기기/서버 미검증 값을 소급 변경하지 않고 아래 실제 화면을 별도 관측으로 남긴다.

[격리 증분 제작 기록](../../artifacts/local/validation/20261016-compact-card-r73/incremental-apk-build-provenance.json)은 이전 APK189 격리 캐시 재사용과 기존 APK189 해시 보존, 소스 before/after 보호·서명/버전 검사 유지를 기록한다. 별도 로컬 제작 도구를 사용했으며 표준 제작 도구나 제품 소스·시험을 바꾸는 변경으로 확대하지 않았다. 제작·설치 일치만으로 색·주소·스크롤·응답의 실제 화면 완료를 주장하지 않는다.

## APK190 실제 기기 확인

[최종 기기 검증 원장](../../artifacts/local/physical-phone-r67/.private/compact-card-r73-r190-validation.json)의 SHA-256은 `3ce2cec336a9d64762f1dfe9b87bf1cb8ecd1ff04cf7672bdad50b41cd951f12`다. 소스·제작/설치·시험·서버 상태를 연결하고 수록 PNG 7개의 파일 해시를 다시 대조했다. 다음 다섯 PNG를 직접 열어 확인했다.

- [V06 첫 카드](../../artifacts/local/physical-phone-r67/screens/V06-r190-first-compact-card.png)와 [UI](../../artifacts/local/physical-phone-r67/.private/ui/V06-r190-first-compact-card.xml)의 1/2·6,000원에서 금액은 왼쪽, 픽업/전달은 오른쪽에 완전하게 보인다. 두 주소 bounds `[492,1883][939,1934]`·`[492,1940][939,1991]`는 각각51px다. 좌측 파랑 거절·우측 밝은 주황 수락과 중립 표면/선택 경계가 보이고 두 버튼 높이135px·활성 상태를 확인했다. 버튼48 DIP와 실제135px 관측은 서로 다른 단위로 기록한다. 원장의 V06 픽셀 표본도 파랑 RGB37/99/235, 주황251/146/60, 표면248/250/252로 지정 색과 같다.
- [V07 두 번째 카드](../../artifacts/local/physical-phone-r67/screens/V07-r190-second-compact-card.png)의 2/2·6,200원에서 W 주황/파랑, V 회색 두 핀과 같은 지도 지형지물 위치를 확인했다. [기존 V/W 만료를 유지한 X 추가](../../artifacts/local/recommendation-map-r69/.private/20261016-110455-394475-r190-offer.json) 뒤 [V10 세 번째 카드](../../artifacts/local/physical-phone-r67/screens/V10-r190-third-compact-card.png)의 3/3·6,400원·X 선택 두 핀/V·W 회색 네 핀, [V12 역방향 넘김](../../artifacts/local/physical-phone-r67/screens/V12-r190-reverse-swipe.png)의 2/3·6,200원·W 선택 색을 확인했다. 세 건의 전체 지도 범위도 유지됐으며 옆 카드 버튼은 비활성이다.
- [V08 거절 사유 창](../../artifacts/local/physical-phone-r67/.private/ui/V08-r190-reject-prompt.xml)과 [V11 주의사항 창](../../artifacts/local/physical-phone-r67/.private/ui/V11-r190-accept-warning.xml)의 `기존 배송 예상 지연 +7분`에서 각각 돌아가기로 취소했다. 실제 수락/거절 확정은 실행하지 않았다. [V15 OFF 정착 PNG](../../artifacts/local/physical-phone-r67/screens/V15-r190-off-settled-final.png)와 [UI](../../artifacts/local/physical-phone-r67/.private/ui/V15-r190-off-settled-final.xml)의 `꺼짐`·switch false로 운행 지도 복귀와 신규 수신 OFF를 확인했다. USB 화면 유지15와 기존 권한·보안 설정은 유지했다.

[최종 V/W/X 상태 원장](../../artifacts/local/recommendation-map-r69/.private/20261016-110708-457059-r190-status.json)은 관측 시점 세 건 유효·queue20/exposure210·round1, 확정 기사 없음·보수 미동결·후불승인완료/결제대기다. [별도 준비](../../artifacts/local/recommendation-map-r69/.private/20261016-105505-599741-r190-prepare.json)와 [V/W 두 건 추천](../../artifacts/local/recommendation-map-r69/.private/20261016-110307-058271-r190-offer.json)을 사용했고 기존 A~U 이력이나 V/W 만료를 되돌리지 않았다. 합성 좌표·격리 host 추천 유효시간300초·worker/FCM/금융 OFF를 유지한다.

긴 실제 주소 스크롤·실제 수락/거절 확정·제품 기본60초 수락·자동 worker·FCM·주행·금융·다른 역할 앱·공개 운영·commit/push는 이번 검증으로 완료를 선언하지 않는다. 이전 r71 전체 시험 시간 초과·r72 APK188 주소 잘림·APK189 전체 시험/기기 성공과 원본 비공개 기기 화면은 모두 보존한다.

원본 화면은 비공개로 유지하며, 검토한 대표 파생 화면과 판본별 설명은 [휴대폰 UI 진행 요약](2026-10-16-phone-ui-progress-summary.md)에서 확인한다.
