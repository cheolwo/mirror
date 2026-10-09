# 생활 공간 분산 물류의 모바일·서버 연결 r36

2026-10-08. 상태 `Implemented / LocalValidated / ApkPrepared / DeviceAndOperationalPending`. 사용자의 최신 정정에 따라 Unity는 제외한다. 이번에 작성했던 Unity 신규 소스·메타만 회수했고 해당 저장소의 기존 변경 목록을 작업 전 기준선과 대조했다.

## 변경 내용

기존 창고가 없는 일반 이용자도 확인한 생활 공간을 본인 소유·관리의 동의 대기 거점으로 등록할 수 있다. 공간 사용·픽업 위치 확인과 소유자·관리자 동의는 별도이며 기존 직원 권한은 추가하지 않는다. 공간 아래 층·구역과 고유 적재 위치 바코드를 두며, 기존 입고 자료 또는 본인이 명시한 상품 원천에서 거점별 분할 예정과 실제 부분 수령을 연결한다. 예정 선언만으로 재고를 늘리지 않는다. 소유자·판매자·SKU·lot별 재고를 기존 실재고와 대조한다.

판매자의 기존 화물 의뢰에 물품을 예약하고 운영자가 주문 마감·픽업·전달 창을 설정한다. 서버는 다거점에서 같은 장소의 피킹을 묶거나 나눠 방문하는 후보와 피킹·전달 혼합 순서를 비교한다. 이동·대기·작업 시간을 포함한 예상 총 소요 시간을 우선하고 거리로 동률을 구분한다. 모든 필요 물품을 인수하기 전에는 해당 고객에게 전달하지 않는다.

기사는 기존 화물 배차 수락과 운송 상태를 사용한다. 현재 정차에서 위치·소유자·SKU·수량을 확인하고 부분 피킹 또는 전달을 기록한다. 고객 수령 확인은 전달 완료와 구분한다. 반복 요청·변경 payload·낡은 revision·재고 경합을 차단하고, 응답이 불명확하면 원 요청의 영수증과 같은 원장의 최신 상태를 확인한다.

기존에 예약한 본인 출고 물품은 새 예약과 구분해 가져오며 재고를 다시 차감하지 않는다. 같은 경고 코드라도 다음 화물의 실제 설명을 읽고 확인하기 전에는 수락하지 않는다. 접수 중 일부 주문만 취소해도 남은 묶음은 마감 전 새 주문을 받을 수 있다. 남은 화물이 실제 기사에게 배정돼 있으면 재계획으로 다른 기사에게 자동 교체하지 않는다.

일반 화물 진행 명령이 피킹·전달 기록을 건너뛰는 경로도 차단했다. 기존 출고 예정과 `DLOC-` 적재 위치로 연결된 화물은 출고 완료 뒤에도 전용 순회에서 처리하도록 안내한다. 새 분산 테이블이 없는 기존 DB에서는 기존 테이블만 조회하므로 일반 화물 상태 전이를 보존한다. 공통 HTTP 응답 변환을 거쳐 `409 / DistributedCargoCustodyRequired`가 유지되는지도 시험한다.

취소는 미피킹 예약 해제와 인수한 물품의 반납 대기를 나눈다. 동의 철회·유효 배정 해제·시간 만료에 따라 신규 처리와 상세 정보 조회를 제한한다. 기존 음식 배달·화물·요금·결제·지급 기능은 별도로 유지한다.

기준 문서는 [승인 구현 r30](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/distributed-warehouse.implementation.r30.md), [페이지→코드→원장 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md)이다.

## 자료와 실행 범위

기존 `SagajeongReference.json` r3는 서버의 주소 후보로 읽기 재사용한다. © OpenStreetMap contributors · ODbL 1.0, 지역 서울특별시 중랑구, 도로명·건물번호 단위이며 파일 SHA-256은 `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3`이다. 기존 602개 건물 중 580개에 도로명·건물번호가 있으나 모두 실사용 거점으로 확인됐다는 뜻은 아니다. 해시·판본·지역·정확 주소가 맞는 후보만 선택하며 자료의 건물 형상·소유권·입주·실제 출입구·현행 행정 경계를 새로 확인한 것은 아니다. 파일은 수정하거나 배포본으로 복사하지 않았다.

`DistributedFulfillment`은 기본 OFF, 정확 profile `distributed-fulfillment-r36`, `Simulation`에서만 사용할 수 있다. 경로 제공자는 별도 명시 설정이다. 실제 고객 주소·연락처·개인 원장은 검증 자료로 쓰지 않는다. 새 테이블 migration은 준비하되 기존·공용 DB에 적용하지 않는다.

## 검증

최종 집중 Fast는 181개 통과·실패 0개이며 전체 솔루션 빌드는 오류 0개·경고 127개다. 전체 Task는 8,868개 통과·실패 0개·기존 격리 Mongo 환경 시험 2개 건너뜀(전체 8,870개)이며 빌드는 오류 0개·경고 67개다. 검증 소스 62개는 각각의 실행 전후 SHA-256이 일치한다. 경고가 없다는 뜻은 아니며 패키지 제약·null·시험 분석 경고는 남는다.

- [Fast 로그](../../artifacts/local/distributed-fulfillment-r36-20261008/root/fast-run.log), [Fast TRX](../../artifacts/local/validation/20261008-132658/Ssalddel.Tests.trx)
- [Task 로그](../../artifacts/local/distributed-fulfillment-r36-20261008/root/task-run.log), [Task TRX](../../artifacts/local/validation/20261008-133140/Ssalddel.Tests.trx)

DB/API 시험은 격리 SQLite에서 실제 기존 배차 수락·운송 상태 실행부와 새 업무 명령, 같은 HTTP 계약의 두 거점→부분 피킹→전달→두 고객 수령 확인을 대조한다. 인증 계정·경로·시각·발행 연속성 일부는 시험용 주입이며 실제 로그인·도로 경로·외부 서버 실행 증거가 아니다. 두 context의 조건부 예약 갱신은 확인했으나 두 실제 MySQL 서버의 동시 운영 시험은 아니다. migration은 SQLite 적용·기존 행 보존과 MySQL SQL/모델 일치를 확인했고 공용 DB에는 적용하지 않았다.

최종 Fast의 SQLite 원장 재조회 15개를 실제 공통 컴포넌트 20화면으로 렌더했다. 320px·390px의 40조합에서 가로 넘침·alert는 0개이며, 버튼·일반 입력·접기 제목·체크 label은 최소 48px이다. 현재 피킹/전달 카드가 보조 순회 선택보다 먼저 표시되고 활성 업무에서 보조 정보는 접힌 상태다. 서버의 허용 행동·부분 피킹·만료 차단을 화면에 대조했으며 두 주문의 확인 체크가 서로를 승인하지 않는 실제 컴포넌트 이벤트 시험도 통과했다. 이는 브라우저의 공통 UI 렌더이며 휴대폰의 네이티브 화면·지도·실제 손 조작 검증은 아니다.

| 로컬 화면 | 확인한 내용 |
| --- | --- |
| [첫 보관 공간 등록](../../artifacts/local/distributed-fulfillment-r36-20261008/ui/preview-rendered-132658/screenshots/warehouse-first-space-390-card.jpg) | 직원 권한 없이 본인 공간 등록, 주소/좌표 직접 확인, 당사자 동의 분리 |
| [현재 피킹](../../artifacts/local/distributed-fulfillment-r36-20261008/ui/preview-rendered-132658/screenshots/driver-pickup-390-card.jpg) | 같은 거점의 기업별 물품과 위치·SKU·수량 확인 |
| [현재 전달](../../artifacts/local/distributed-fulfillment-r36-20261008/ui/preview-rendered-132658/screenshots/driver-delivery-390-card.jpg) | 현재 고객의 전달 업무와 다음 행동 |
| [다음 화물의 주의사항](../../artifacts/local/distributed-fulfillment-r36-20261008/ui/preview-rendered-132658/screenshots/driver-warning-next-cargo-390-warning.jpg) | 실제 별도 설명을 읽고 확인하기 전 수락 비활성 |

## 설치 파일 준비

네 역할의 서명된 Debug APK `0.1.36 / 36`을 준비했다. 각 APK의 패키지·판본·서명 인증서·16KiB 정렬, arm64-v8a/x86_64 런타임과 실제 앱·공통 UI·계약 어셈블리의 새 기능 포함을 확인했다. 독립 읽기 검사에서 Blazor WebView host/스크립트·역할 CSS→공통 scoped CSS 연결과 실제 체크 영역 48px, CSS 원본 및 압축 해제본 일치를 대조했다. 검사 전후 APK 해시는 동일하다.

| 역할 | APK | 패키지 |
| --- | --- | --- |
| 주문자 | [설치 파일](../../artifacts/local/distributed-fulfillment-r36-20261008/apk/orderer/com.ssalddel.ordererapp-Signed.apk) | `com.ssalddel.ordererapp` |
| 판매자·화주 | [설치 파일](../../artifacts/local/distributed-fulfillment-r36-20261008/apk/shipper/com.companyname.ssalddelapp-Signed.apk) | `com.companyname.ssalddelapp` |
| 화물 기사 | [설치 파일](../../artifacts/local/distributed-fulfillment-r36-20261008/apk/cargo-driver/kr.hongdal.driver-Signed.apk) | `kr.hongdal.driver` |
| 공간·창고 관리 | [설치 파일](../../artifacts/local/distributed-fulfillment-r36-20261008/apk/warehouse/com.companyname.warehousemanagerapp-Signed.apk) | `com.companyname.warehousemanagerapp` |

패키징에 사용한 앱·공통 의존 소스 3,387개의 실행 전후 해시가 일치한다. 이는 이번 소유 경로 69개만으로 만든 앱이라는 뜻이 아니며 보존된 기존 모바일 변경도 현재 앱 빌드에 포함한다. [APK 명세·해시](../../artifacts/local/distributed-fulfillment-r36-20261008/apk/manifest.json), [앱 소스 동결 기록](../../artifacts/local/distributed-fulfillment-r36-20261008/apk/source-after.json), [네 APK 스타일·host 포함 검토](../../artifacts/local/distributed-fulfillment-r36-20261008/apk/asset-packaging-read-review/all-four-apps.json), [최종 화면 검증](../../artifacts/local/distributed-fulfillment-r36-20261008/ui/preview-rendered-132658/qa-result.json)에 결속한다.

서버 주소는 후속 USB 연결 시험용 `http://127.0.0.1:5363/`이며 이 패키지는 로컬 시험용이다. 휴대폰 설치·로그인·기기 지도·네 역할 통신·실제 입고/배송을 확인한 것은 아니다. 다음 기기 검증 때 같은 서명/현재 설치 판본을 확인한 뒤 서버를 정확한 Simulation profile로 실행하고 USB 전달 연결에서 실제 상태 전이를 대조한다. Azure 주소나 공용 DB를 자동 적용하지 않는다.

임시 자료는 Git 제외 `artifacts/local/distributed-fulfillment-r36-20261008/`에 보관한다. 기존 dirty 작업의 기준선을 보존하고 소유 경로에만 수정한다. 독립 읽기 검토로 취소·기존 예약 인수·기사 고정·화물별 경고 확인·일반 화물 우회 경로를 확인하고 필요한 보완을 반영했다. 검토 자체를 런타임 시험 결과로 표현하지 않는다.

휴대폰·에뮬레이터·Azure·외부 경로 API·실입고·실운행·실금전·commit/push는 실행하지 않았다. 로컬 시험·화면·APK는 각기 다른 확인 사실로 보고한다.
