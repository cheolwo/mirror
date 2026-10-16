# 추천 카드에서 바로 응답 r72

2026-10-16. 카드에서 바로 거절·수락하도록 구현한 APK188의 전달 주소 잘림을 발견해 카드 내부 여백만 조정했다. 최종 Fast·전체 Task와 APK189 제작·설치·해시 일치, 실제 두/세 건 카드의 세 정보·양방향 넘김·선택 지도·거절/수락 확인 창 취소·운행 지도 복귀와 신규 수신 OFF를 확인했다. 실제 수락·거절 확정은 실행하지 않았으며 APK188 결함과 이전 통과를 별도로 보존한다. [책임 카드 r22](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#추천-카드에서-바로-응답-r22--r72)가 현행 화면 책임을 소유한다. [r71 APK187](2026-10-16-map-first-recommendation-cards-r71.md)의 이력도 보존한다.

## 구현 범위

[추천 페이지](../../DriverApp/NativeDriverRecommendationPage.cs) 한 파일에서 카드를 128 DIP로 조정했다. 카드 내용은 배달료·픽업지 주소·전달지 주소이며 하단 왼쪽은 거절, 오른쪽은 수락이다. 본문 13·금액 16, 응답 버튼 높이 48 DIP를 사용하고 긴 주소는 카드 안에서 스크롤하도록 구성했다. 순번은 상단 header에 표시하며 상세 진입·상세 overlay·보류·상시 경고 체크박스를 제거했다. 지도 노출과 카드의 약 5:1 비율은 설계 목표이며 실제 화면의 지도 우선 배치를 확인했다. 모든 기기의 동일 비율이나 긴 실제 주소의 스크롤 검증을 뜻하지 않는다.

APK188 페이지 소스 SHA-256은 `9bcc782ddb295aafbaa83a6dd05b5f8e81f787d433cd7ad438646887ee92d3cc`이며 [여백 수정 전 사본](../../artifacts/local/physical-phone-r67/.private/source-backups/recommendation-card-r72-before-spacing/manifest.json)으로 보존한다. 여백 수정 후 소스 SHA-256은 `c0bf2aa8c91dd197852a4460f745605061dde2d3394de699f215205ceebb823b`다. [r72 구현 전 사본](../../artifacts/local/physical-phone-r67/.private/source-backups/recommendation-card-r72-before-direct-response/manifest.json)도 유지한다. 전체 유효 핀·선택 주황/파랑·다른 회색, 전체 좌표 집합 변화 시에만 카메라 갱신, 미확인 좌표 생략과 실패 후 캐시 복구는 r71 지도 코드를 유지한다. 서버 계약·배차 정책·요금/정산·음식 기사 앱·현재 수행 운송·Unity는 변경하지 않는다.

## 응답 보호와 읽기 검토

- 버튼은 해당 카드의 `(의뢰Id, 추천라운드)`와 실제 `CurrentItem`·선택 키가 같은 경우에만 응답한다.
- 주의사항이 있으면 수락 시 그 건의 경고 확인 창을 띄운다. 동의는 이번 클릭의 경고 fingerprint에만 사용하며 다른 카드·차수·다음 수락에 재사용하지 않는다.
- 첫 비동기 대기 전에 `_prompting`으로 잠가 연속 탭·카드 전환·뒤로가기·새 조회의 중복을 막는다. 확인 창 취소는 명령을 보내지 않는다.
- 확인 후 현재 카드·키/차수·페이지/작업공간 세대·만료를 다시 검사하고 서버 정본을 조회한다. 새 경고나 변경된 fingerprint는 다시 확인하도록 멈춘다.
- 실제 수락 호출 직전 `acceptanceSubmitted`를 기록한다. 확인 창·선행 조회 오류와 이미 전송한 수락의 응답 불명확을 구분하고, 후자의 재전송 대신 정본 확인을 유지한다.

응답 보호의 읽기 검토에서 위 경계에 추가 확정 결함은 찾지 못했다. 이후 여백 수정은 아래 세 속성뿐이고 응답 가드는 유지된다. 이 검토는 실제 표시·확인 창·서버 명령·기기 제스처 시험의 완료를 뜻하지 않는다. 기존 거절 API에 추천 차수 CAS가 없는 한계도 이번 UI 수정으로 바뀌지 않는다.

## 검증 상태와 격리 준비

여백 수정 전 APK188 소스의 [Fast](../../artifacts/local/validation/20261016-direct-card-r72/fast.log)는 앱·시험 프로젝트 빌드와 **69개 통과·실패 0개·생략 0개**다. [상세 시험 로그](../../artifacts/local/validation/20261016-190937/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-190937/Ssalddel.Tests.trx)를 보존한다. 실제 확인 창·버튼·기기 비율 검증을 대신하지 않는다.

같은 APK188 소스의 [Task](../../artifacts/local/validation/20261016-direct-card-r72/task.log)는 솔루션 빌드와 **12,274개 통과·실패 0개·환경 의존 16개 생략**이다. [전체 시험 로그](../../artifacts/local/validation/20261016-191732/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-191732/Ssalddel.Tests.trx)를 보존한다. 이 결과를 여백 수정 후 소스에 옮기지 않는다. r71의 시간 초과 1개·단일 재시험 통과도 이전 판본 이력으로 유지한다.

여백 수정 후 [Fast-spacing](../../artifacts/local/validation/20261016-direct-card-r72/fast-spacing.log)은 앱·시험 프로젝트 빌드와 **69개 통과·실패 0개·생략 0개**다. [상세 시험 로그](../../artifacts/local/validation/20261016-192406/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-192406/Ssalddel.Tests.trx)의 실제 counters를 대조했다. 최종 소스의 읽기 재검토에서도 카드 세 정보·좌측 거절/우측 수락과 경고 확인 취소·선택/세대 보호에 추가 확정 기능 결함은 찾지 못했다. 아래 APK189 기기 관측으로 이번 합성 한 줄 장소 표시의 잘림 해결도 별도로 확인했다.

최종 소스 `c0bf2aa8c91dd197852a4460f745605061dde2d3394de699f215205ceebb823b`의 [Task-spacing](../../artifacts/local/validation/20261016-direct-card-r72/task-spacing.log)은 지정한 7개 경로로 솔루션 빌드와 전체 시험을 실행해 **12,274개 통과·실패 0개·환경 의존 16개 생략**을 확인했다. [최종 전체 시험 로그](../../artifacts/local/validation/20261016-193524/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-193524/Ssalddel.Tests.trx)의 total 12,290·executed/passed 12,274·failed/error/timeout/notExecuted 0을 직접 대조했다. 환경 의존 생략 16개는 시험 로그의 별도 관측이며 로그의 시험 시간은 2분 30초다. 완료 후 소스 해시도 동일했다. 이전 APK188 Task 및 r71 실패/단일 재시험 이력과 별도 실행 결과로 보존한다.

| 단계 | 현재 상태 |
| --- | --- |
| APK188 소스 Fast·Task | 69개·12,274개 통과, 위 이전 판본 기록 |
| APK188 제작 | [제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-direct-card/cargo-driver/manifest.json) 확인, 0.1.188 / 188 Debug 서명·정렬 검사 통과 |
| APK188 설치·해시 | [기기 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r188.json)의 버전·해시 일치 확인 |
| APK188 실제 카드 | 전달 주소 잘림을 재현해 완료로 처리하지 않음 |
| 여백 수정 후 Fast | 앱·시험 빌드와 69개 통과·실패/생략 0개 |
| 여백 수정 후 전체 Task | 솔루션 빌드와 12,274개 통과·실패 0개·환경 의존 16개 생략 |
| APK189 제작 | [제작 로그](../../artifacts/local/validation/20261016-direct-card-r72/apk-build-spacing.log)·[제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-direct-card-spacing/cargo-driver/manifest.json), 0.1.189 / 189 |
| APK189 설치·해시 | [기기 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r189.json)의 버전·해시 일치 확인 |
| APK189 카드·지도 | U08/U09 두 건·네 핀, U13/U15 세 건·여섯 핀과 양방향 선택 색·카메라 범위 유지 확인 |
| APK189 확인 창·복귀 | 거절 사유/주의사항 확인 창에서 각각 돌아가기, 운행 지도 복귀·U19 신규 수신 OFF 확인 |
| APK189 실제 수락/거절 확정 | 실행하지 않음, 최종 원장도 세 건 미확정·보수 미동결 |

APK188 SHA-256은 `4c8abcff7166ca9af09b096bc52e6d1b823c55131fcb97b24a68a00241eb0fd0`이고 제작 원장 SHA-256은 `807bb8aa0f6938ba7abd8ec1e4608c61565a235131408df19de243eb666f445f`다. 설치 기록과 대조했으며 제작 원장의 빌드 시점 기기/서버 통신 미검증 항목은 소급 변경하지 않는다.

최종 APK189 SHA-256은 `e9ca3c163621432c50255a560227422e7fff15951d4b551b778023035de64f3c`, 제작 원장 SHA-256은 `d9df42d7b942a5192f03c89534906cf46f7310e10403ea64841dc9cc13ef26f1`이다. `phone-r67-direct-card-spacing` 판본의 버전 0.1.189 / 189와 기기에 설치된 파일 해시 일치를 별도로 확인했다. 제작 원장의 빌드 시점 미검증 값은 유지하고 실제 화면은 아래 후속 관측으로 구분한다.

## APK188 주소 잘림과 최소 수정

[T06 실제 PNG](../../artifacts/local/physical-phone-r67/screens/T06-r188-first-direct-card.png)에서 배달료·픽업 주소는 보였지만 전달 주소의 아래쪽이 잘렸다. [같은 화면 UI 관측](../../artifacts/local/physical-phone-r67/.private/ui/T06-r188-first-direct-card.xml)의 전달 주소 bounds는 `[177,1974][939,1993]`로 높이 19px이며 픽업 주소 51px보다 작다. 설치·시험 통과와 실제 표시 결함을 별개로 기록한다.

수정 전 소스 사본과 실제 파일을 대조해 `summary.Spacing` 2→0, 본문 Grid의 `RowSpacing` 4→2, 카드 `Padding` 8→`Thickness(8,4)` 세 변경만 확인했다. 총 14 DIP의 내부 세로 공간을 확보하도록 조정했으며 카드 128 DIP·본문/금액 글자·버튼 48 DIP·지도·응답 가드는 바꾸지 않았다. 실제 해결 확인은 아래 APK189 PNG·UI에 한정하며 코드 변경만으로 해결 완료를 주장하지 않는다.

APK188 시험 뒤 신규 배차 수신은 [T08 후속 UI](../../artifacts/local/physical-phone-r67/.private/ui/T08-r188-reception-restored-off.xml)의 `꺼짐`·switch false로 OFF 복원을 확인했다. 앞선 [T08 PNG](../../artifacts/local/physical-phone-r67/screens/T08-r188-reception-restored-off.png)는 변경 중 화면이므로 OFF 정착 화면으로 사용하지 않는다.

## APK189 실제 기기 확인

[최종 기기 검증 원장](../../artifacts/local/physical-phone-r67/.private/direct-card-r72-r189-validation.json)의 SHA-256은 `0892b7660a730bd330afe1714c2a45350a38e5c3baf2be8ed2a23439df583655`다. 소스·APK/설치·시험·원본 화면·최종 상태 참조를 대조했고 아래 PNG 다섯 장을 직접 열어 확인했다.

- [U08 첫 카드 정착 PNG](../../artifacts/local/physical-phone-r67/screens/U08-r189-first-card-settled.png)와 [UI](../../artifacts/local/physical-phone-r67/.private/ui/U08-r189-first-card-settled.xml)에서 1/2·6,000원·픽업 장소 4·전달 장소 4와 좌측 거절/우측 수락이 모두 표시된다. 픽업 bounds `[177,1899][939,1950]`와 전달 bounds `[177,1950][939,2001]`는 각각 51px로 APK188 전달 19px 잘림이 해소됐다. 두 버튼도 각각 활성이다. 이번 증거는 합성 한 줄 장소 표기이며 긴 실제 주소의 전체 표시 검증은 아니다.
- [U09 두 번째 카드 PNG](../../artifacts/local/physical-phone-r67/screens/U09-r189-second-card-swipe.png)에서 2/2·6,200원, 선택한 T의 주황/파랑 두 핀과 S의 회색 두 핀을 확인했다. U08과 지도 지형지물 위치가 유지돼 카드 넘김으로 카메라가 다시 맞춰지지 않았다.
- [S/T 뒤 U 추가](../../artifacts/local/recommendation-map-r69/.private/20261016-103724-191204-r189-offer.json)는 기존 S/T 만료를 유지하고 새 U만 추천했다. [U13 세 번째 카드 정착 PNG](../../artifacts/local/physical-phone-r67/screens/U13-r189-third-card-settled.png)의 3/3·6,400원·U 주황/파랑과 S/T 회색 네 핀, [U15 역방향 넘김 PNG](../../artifacts/local/physical-phone-r67/screens/U15-r189-reverse-swipe-two-of-three.png)의 2/3·6,200원·T 선택 색과 다른 회색을 직접 확인했다. 세 건의 전체 지도 범위도 유지됐다. U07은 로딩, U12는 넘김 중간 화면이므로 정착 증거로 사용하지 않는다.
- [U10 거절 사유 창](../../artifacts/local/physical-phone-r67/.private/ui/U10-r189-reject-prompt.xml)에서 돌아가기로 취소한 뒤 [U11 카드 복귀](../../artifacts/local/physical-phone-r67/.private/ui/U11-r189-reject-cancel-three.xml)를 확인했다. [U14 주의사항 창](../../artifacts/local/physical-phone-r67/.private/ui/U14-r189-warning-confirmation.xml)의 `기존 배송 예상 지연 +7분`도 돌아가기로 취소했다. 실제 수락/거절 확정은 실행하지 않았다.
- 운행 지도로 돌아온 뒤 [U19 OFF 정착 PNG](../../artifacts/local/physical-phone-r67/screens/U19-r189-off-settled-final.png)와 [UI](../../artifacts/local/physical-phone-r67/.private/ui/U19-r189-off-settled-final.xml)의 `꺼짐`·switch false로 신규 배차 수신 OFF 복원을 확인했다. U17 fresh UI 시간 초과 때는 입력을 재전송하지 않고 새 관측 뒤 전환했으며 U17 PNG의 ON 상태를 OFF 완료로 사용하지 않는다. USB 화면 유지 값 15는 기존 15를 유지했다.

[마지막 S/T/U 원장](../../artifacts/local/recommendation-map-r69/.private/20261016-104101-129494-r189-status.json)은 모두 확정 기사 없음·보수 미동결·후불승인완료/결제대기다. S/T의 19:40:02 만료를 되돌리지 않았으며 마지막 관측에서 시간이 지났어도 queue20/exposure210 값이 남아 있었다. 이는 worker OFF 격리 원장의 관측이며 제품 만료 worker의 성공이나 실제 금융 완료로 해석하지 않는다.

[APK188 시험 host 빌드](../../artifacts/local/validation/20261016-direct-card-r72/host-build.log)는 경고 0개·오류 0개로 성공했고 기존 ready·실행 식별자·DB·계정을 유지해 재시작했다. 이는 제품 APK나 기기 완료 증거가 아니다.

[P/Q/R 준비 절차](../../artifacts/local/recommendation-map-r69/prepare-r188.md)와 [host 변경 전 사본·해시](../../artifacts/local/recommendation-map-r69/.private/source-before-r188/source-sha256.json)를 보존한다. [별도 준비 호출](../../artifacts/local/recommendation-map-r69/.private/20261016-100844-679662-r188-prepare.json)로 의뢰 생성·후불 승인을 준비했고 [P/Q 두 건 추천](../../artifacts/local/recommendation-map-r69/.private/20261016-102053-579046-r188-offer.json)의 유효 정본을 확인했다. 서버의 추천 확인은 실제 카드·응답 화면 확인을 대신하지 않는다. APK188에는 별도 P/Q/R을 사용했고 기존 A~O·만료·실패·helper·journal을 되돌리지 않았다.

APK189은 [별도 S/T/U 준비](../../artifacts/local/recommendation-map-r69/prepare-r189.md)와 [준비 호출](../../artifacts/local/recommendation-map-r69/.private/20261016-102858-521462-r189-prepare.json), [S/T 두 건 추천](../../artifacts/local/recommendation-map-r69/.private/20261016-103502-413270-r189-offer.json)을 사용했다. 이전 P/Q/R과 실패·소스 사본·추천 유효시간을 보존했다.

시험 자료는 선언된 합성 좌표·300초 추천 유효시간이며 자동 worker·FCM·금융 효과는 OFF다. 제품 기본 60초·실제 주행·실제 금융·공개 운영 검증으로 확대하지 않는다. 원본 기기 화면은 비공개 로컬에 두고 문서 자산으로 복사하지 않는다. 이번 요청은 commit/push가 아니다.
