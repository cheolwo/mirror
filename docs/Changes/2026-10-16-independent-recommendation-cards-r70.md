# 화물 추천별 독립 카드 r70

2026-10-16. APK185에서 옆 카드 탭 뒤 중앙 카드와 선택 지도 불일치를 재현했고, 탭 선택을 제거한 APK186을 설치했다. 최종 Fast·Task와 설치 해시 일치, 두 장·세 장 독립 카드·탭 전후 선택 유지·양방향 넘김·건별 경고 확인·수신 OFF 복원을 확인했다. 이번 APK186 확인에서는 수락을 실행하지 않았다. [페이지 책임 카드 r20](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#화물-추천별-독립-카드-r20--r70)이 표시 책임을 소유한다.

[이전 r69](2026-10-16-recommendation-map-carousel-r69.md)는 별도 추천 지도·선택과 한 건 수락·서버 재조회·응답 불명확 보호를 연결했다. 이전 큰 흰 하단 영역의 시각 표현만 이번 정정으로 대체하며 당시 APK184와 실행 기록은 보존한다.

## 변경 범위

- 큰 공통 흰 배경 대신 투명한 하단 목록 영역을 둔다.
- 추천별 카드가 각각 흰 배경·둥근 테두리·간격·순번과 해당 건의 정보·응답 버튼을 갖는다.
- 옆 카드 일부를 보이고 좌우 스와이프로 이동해 선택한다. 추천 2건은 2장, 3건은 3장이며 반복 복제하지 않는다. 카드 탭과 별도 선택 버튼은 제거했다.
- 선택 카드만 지도와 한 건 응답에 연결한다. 현재 업무·만료·라운드·경고 확인·응답 불명확 보호·전체 지도 크기를 유지한다.

제품 변경은 [기존 추천 페이지](../../DriverApp/NativeDriverRecommendationPage.cs) 한 파일이다. 탭 선택 제거 후 최종 소스 SHA-256은 `2ebda7bd041960774a469568205b62092b23a02c46cb5930ddc4b663892b91a3`다. `CarouselView`의 양옆 미리보기 40·카드 간격 12와 개별 `Border`를 사용하며, 선택한 카드에만 수락·보류·거절을 연결한다. 부분 노출 카드 전체를 표시 ACK로 처리하지 않는다. 경고 확인은 의뢰 ID·추천 차수와 경고 내용에 연결하고 명령 직전 정본을 다시 확인한다.

이번 작업은 기존 화물 기사 추천 페이지의 표현 변경이다. 서버 배차 정책·음식 기사 앱·요금·정산 원장·권한·Unity를 변경하지 않는다. [APK184 당시 소스 사본](../../artifacts/local/physical-phone-r67/.private/source-backups/recommendation-card-r70-before/NativeDriverRecommendationPage.cs)과 r69의 설치·화면·원장 기록을 보존한다.

## 검증 상태

최초 [Fast](../../artifacts/local/validation/20261016-independent-cards-r70/fast.log)는 `UpdateActions`의 지역 변수 `selected` 이름 충돌 `CS0136`으로 빌드에 실패했고 첫 APK 제작 시도도 완료하지 못했다. 충돌한 변수만 `isSelectedSurface`로 바꿨으며 최초 [APK 작업 로그](../../artifacts/local/validation/20261016-independent-cards-r70/apk-build.log)는 별도 보존한다. 실패 기록을 후속 성공으로 덮어쓰지 않는다.

변수명 수정 후 APK185 소스의 [Fast-r2](../../artifacts/local/validation/20261016-independent-cards-r70/fast-r2.log)는 앱·시험 프로젝트 빌드와 **53개 통과·실패 0개·생략 0개**다. [상세 시험 로그](../../artifacts/local/validation/20261016-175316/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-175316/Ssalddel.Tests.trx)를 보존한다. 이 판본의 [Task](../../artifacts/local/validation/20261016-independent-cards-r70/task.log)는 솔루션 빌드와 **12,258개 통과·실패 0개·환경 의존 16개 생략**이며 [상세 로그](../../artifacts/local/validation/20261016-180026/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-180026/Ssalddel.Tests.trx)를 보존한다.

기기에서 발견한 선택 불일치 수정 후 [Fast-final](../../artifacts/local/validation/20261016-independent-cards-r70/fast-final.log)은 앱·시험 프로젝트 빌드와 **53개 통과·실패 0개·생략 0개**다. [최종 집중 시험 로그](../../artifacts/local/validation/20261016-180912/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-180912/Ssalddel.Tests.trx)를 보존한다. 이어 최종 소스의 [Task-final](../../artifacts/local/validation/20261016-independent-cards-r70/task-final.log)은 솔루션 빌드와 **12,258개 통과·실패 0개·환경 의존 16개 생략**이다. [최종 전체 시험 로그](../../artifacts/local/validation/20261016-181544/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-181544/Ssalddel.Tests.trx)를 보존한다. 이전 판본의 결과와 합산하지 않으며, 자동 회귀 시험은 실제 카드 표시·제스처·선택 정합을 대신하지 않는다.

| 단계 | 현재 확인 상태 |
| --- | --- |
| APK185 | 제작·기기 설치·해시 일치 확인, 아래 탭 선택 불일치로 기기 검증 미완료 |
| 탭 제거 최종 소스·Fast | 위 최종 소스 해시와 53개 시험 통과 |
| 최종 Task | 솔루션 빌드와 12,258개 통과·실패 0개·환경 의존 16개 생략 |
| APK186 | [최종 제작](../../artifacts/local/validation/20261016-independent-cards-r70/apk-build-final.log)·기기 설치·버전/해시 일치 확인 |
| APK186 두 장·세 장 카드·탭 선택 유지·양방향 넘김·건별 경고 | 아래 실제 기기 결과 확인 |
| APK186 마지막 설정 복원 | 운행 지도로 복귀·신규 수신 원래 OFF 복원 확인 |

## APK185 실제 기기 결과와 발견한 문제

**0.1.185 / 185**의 [제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-independent-cards-r2/cargo-driver/manifest.json)과 [기기 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r185.json)은 버전·해시가 일치한다. APK SHA-256은 `0b4b1a078b35eb7c6e7e090bb4cf8d052d5fd9fdda6eaad57df5729b44afbeea`다. 설치 성공과 아래 기기 결함은 별도 사실이다.

| 단계 | 확인한 결과 | 근거 |
| --- | --- | --- |
| 두 장 | 추천별 흰 배경·둥근 테두리와 옆 카드 일부를 확인하고 H 2/2로 이동했다. | [F08](../../artifacts/local/physical-phone-r67/screens/F08-r185-two-independent-cards.png), [F09](../../artifacts/local/physical-phone-r67/screens/F09-r185-second-of-two.png) |
| 세 장 | H 2/3을 가운데 두고 양옆 카드 일부를 확인했다. I 3/3에는 6,400원이 표시됐다. | [F10](../../artifacts/local/physical-phone-r67/screens/F10-r185-three-cards-arriving.png), [F14](../../artifacts/local/physical-phone-r67/screens/F14-r185-three-cards-settled.png), [F11](../../artifacts/local/physical-phone-r67/screens/F11-r185-third-independent-card.png) |
| 건별 경고 확인 | I의 확인 체크 후 수락 활성, H로 돌아오면 미확인·수락 비활성을 UI 관측으로 구분했다. 수락은 실행하지 않았다. | [F12 화면](../../artifacts/local/physical-phone-r67/screens/F12-r185-third-card-warning-confirmed.png), [F12 UI](../../artifacts/local/physical-phone-r67/.private/ui/F12-r185-third-card-warning-confirmed.xml), [F14 UI](../../artifacts/local/physical-phone-r67/.private/ui/F14-r185-three-cards-settled.xml) |
| 탭 선택 실패 | 옆 카드를 누른 뒤 중앙에는 G 6,000원·비선택 회색 테두리가 남고, 순번 2/3과 지도는 H 전달 장소 2를 표시했다. 중앙 카드와 선택 지도 불일치를 확인했다. | [F15](../../artifacts/local/physical-phone-r67/screens/F15-r185-select-neighbor-card.png), [F16](../../artifacts/local/physical-phone-r67/screens/F16-r185-third-card-map.png) |
| 복귀·수신 복원 | 운행 지도로 돌아가 신규 배차 수신을 원래 OFF로 복원했다. G/H/I는 수락하지 않았다. | [F19](../../artifacts/local/physical-phone-r67/screens/F19-r185-reception-restored-off.png) |

이 문제는 APK185 완료 주장에 포함할 수 없다. [수정 전 소스·사본 해시](../../artifacts/local/physical-phone-r67/.private/source-backups/recommendation-card-r70-before-tap-removal/manifest.json)를 보존한 뒤 카드 탭·`이 추천 보기` 버튼과 프로그램식 탭 선택 경로를 제거하고 좌우 스와이프 선택만 남겼다. APK186의 별도 검증 결과를 아래에 기록하며 APK185 실패 화면을 덮어쓰지 않는다.

## 최종 APK186 실제 기기 확인

**0.1.186 / 186**의 [제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-independent-cards-final/cargo-driver/manifest.json)과 [기기 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r186.json)은 버전·해시가 일치한다. APK SHA-256은 `3ce3c0f28f45fe2b832ad2738c6f97ad8e133801505d24194ae831455aed4d5f`다. 제작 원장의 빌드 시점 기기 미검증 항목을 소급 변경하지 않고 별도 설치·화면 기록으로 결과를 연결한다.

| 단계 | 확인한 결과 | 근거 |
| --- | --- | --- |
| 두 독립 카드 | 첫 J 카드 1/2·6,000원과 옆 K 카드 일부를 확인했다. 카드별 흰 배경·둥근 테두리가 분리돼 있다. | [R07](../../artifacts/local/physical-phone-r67/screens/R07-r186-two-independent-cards.png), [R07 UI](../../artifacts/local/physical-phone-r67/.private/ui/R07-r186-two-independent-cards.xml) |
| 옆 카드 탭 | 옆 카드를 눌러도 1/2·중앙 카드·선택 지도가 유지됐다. 별도 탭 선택은 제공하지 않는다. | [R08](../../artifacts/local/physical-phone-r67/screens/R08-r186-adjacent-tap-unchanged.png), [R08 UI](../../artifacts/local/physical-phone-r67/.private/ui/R08-r186-adjacent-tap-unchanged.xml) |
| 좌우 넘김 | 스와이프하면 K 2/2·6,200원·전달 장소 2로 바뀌었다. | [R09](../../artifacts/local/physical-phone-r67/screens/R09-r186-second-card.png) |
| 세 독립 카드 | K 2/3·6,200원을 가운데 두고 양옆 J/L 카드 일부를 확인했다. | [R10](../../artifacts/local/physical-phone-r67/screens/R10-r186-three-independent-cards.png), [R10 UI](../../artifacts/local/physical-phone-r67/.private/ui/R10-r186-three-independent-cards.xml) |
| 세 카드에서 옆 카드 탭 | 오른쪽 옆 카드를 눌러도 중앙 K·2/3·선택 지도는 유지됐다. | [R11](../../artifacts/local/physical-phone-r67/screens/R11-r186-third-peek-tap-unchanged.png), [R11 UI](../../artifacts/local/physical-phone-r67/.private/ui/R11-r186-third-peek-tap-unchanged.xml) |
| 세 번째 카드·지도 | 스와이프로 L 3/3·6,400원·전달 장소 3으로 이동하고 해당 파랑 도착 핀을 확인했다. | [R12](../../artifacts/local/physical-phone-r67/screens/R12-r186-third-card.png) |
| 경고 확인·역방향 넘김 | L 경고 확인 뒤 수락 활성, 역방향 넘김 후 K 2/3·6,200원·미확인·수락 비활성을 확인했다. 옆 L의 체크는 유지되지만 비선택 카드의 버튼은 비활성이다. 수락은 실행하지 않았다. | [R13](../../artifacts/local/physical-phone-r67/screens/R13-r186-third-warning-confirmed.png), [R13 UI](../../artifacts/local/physical-phone-r67/.private/ui/R13-r186-third-warning-confirmed.xml), [R15](../../artifacts/local/physical-phone-r67/screens/R15-r186-second-card-settled.png), [R15 UI](../../artifacts/local/physical-phone-r67/.private/ui/R15-r186-second-card-settled.xml) |
| 복귀·수신 복원 | 운행 지도로 돌아가 진행 중 운송 없음과 신규 배차 수신 OFF를 확인했다. | [R18](../../artifacts/local/physical-phone-r67/screens/R18-r186-reception-restored-off.png) |

R07과 R08 PNG SHA-256은 모두 `1b7ff59df517aff11d7c63137bd64c345e02fcb2f70962fcdbe68cf2f0f8a9ca`, R10과 R11은 모두 `24bbcaf8206594e9e67daa5fada41185cff71a137723f52db84a26d36905f4a9`로 각각 같다. 이 동일성과 각 UI 관측은 탭 전후 선택 유지 근거다. R14는 역방향 넘김 중 과도 프레임이므로 최종 선택 상태 근거로 사용하지 않고 정착 후 R15와 구분한다.

[마지막 J/K/L 원장 조회](../../artifacts/local/recommendation-map-r69/.private/20261016-092340-915068-r186-status.json)에서 세 건 모두 확정 기사와 동결 기사 보수가 없고 결제는 `결제대기`다. 수락·수행·실제 지급을 실행한 결과가 아니다. J/K는 자연 만료했고 L이 남은 상태에서 신규 수신 OFF로 복원했으며 추천 배너는 제거됐다. 기존 원장을 되돌리거나 추천 만료를 연장하지 않았다. PIN 등 보안 잠금 설정은 변경하지 않았다.

[APK186 기기 증거 원장](../../artifacts/local/recommendation-map-r69/.private/device-independent-cards-r186.json)은 설치·소스 해시와 R06~R18 원본 PNG의 해시를 연결한다. 화면 유지 설정은 원래 값 15를 그대로 유지했고 보안 잠금 설정도 변경하지 않았다. 기기 UI 확인과 합성 서버 자료, 미수락·미지급 상태를 각각 기록한다.

## 별도 기기 시험 준비와 남은 범위

[G/H/I 준비 절차](../../artifacts/local/recommendation-map-r69/prepare-r185.md)는 기존 격리 서버·DB·실행 식별자에서 별도 의뢰만 사용한다. [준비 호출 기록](../../artifacts/local/recommendation-map-r69/.private/20261016-085440-822975-r185-prepare.json)은 세 의뢰 생성과 후불 승인 준비의 성공이다. [두 건 추천](../../artifacts/local/recommendation-map-r69/.private/20261016-090244-139221-r185-offer.json) 뒤 [세 번째 추천 추가](../../artifacts/local/recommendation-map-r69/.private/20261016-090425-366247-r185-offer.json)에서는 유효한 G/H의 만료 값을 그대로 유지하고 I만 추가했다. 추천 원장 확인과 위 기기 화면·수락 여부는 구분한다. 기존 A~F의 완료·미수락·만료 기록을 되돌리거나 유효시간을 연장하지 않는다.

시험용 host의 G/H/I 허용 목록과 선언한 합성 좌표 준비는 제품 배차 정책 변경과 구분한다. [변경 전 host 사본·해시](../../artifacts/local/recommendation-map-r69/.private/source-before-r185/source-sha256.json)와 [준비 점검](../../artifacts/local/recommendation-map-r69/.private/preparation-checks-r185.json)을 보존한다. 새 helper·journal만 사용하며 불확실한 POST는 재전송하지 않고 정본을 조회한다. 두 건을 관측한 뒤 유효한 G/H는 재추천하지 않고 I만 더해 세 건을 관측하도록 준비했다.

[APK186용 J/K/L 절차](../../artifacts/local/recommendation-map-r69/prepare-r186.md)에 따라 [별도 준비](../../artifacts/local/recommendation-map-r69/.private/20261016-091205-494742-r186-prepare.json)와 [J/K 두 건 추천](../../artifacts/local/recommendation-map-r69/.private/20261016-091804-824871-r186-offer.json), [L 추가 후 세 건 정본 확인](../../artifacts/local/recommendation-map-r69/.private/20261016-092100-376471-r186-offer.json)을 실행했다. 서버의 유효 추천, 위 기기 화면·제스처와 마지막 미수락 원장을 각각 구분한다. 기존 A~I·실패 화면·helper·journal을 보존하고 새 의뢰로 검증하도록 분리했다.

추천 유효시간은 격리 host의 300초이고 자동 worker·FCM·금융 효과는 OFF다. 제품 기본 60초 수락·실제 주행 경로·실제 금융·공개 운영을 검증한 것으로 확대하지 않는다. 위치가 포함된 원본 화면은 비공개 로컬 증거로 보관하고 문서 이미지 자산으로 복사하지 않는다. APK185와 APK186의 수신 OFF 복원을 각각 확인했다. 이번 APK186은 카드 표시·선택·주의 확인·복귀 검증이며, 수락·사진 수행·취소·거절을 다시 실행한 것으로 확대하지 않는다. commit/push는 이번 요청 범위가 아니다.
