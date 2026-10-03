# SsalddelApp 화면 문서

[전체 화면 문서](../README.md) / [앱 전체 카탈로그](../../app-page-catalog.md)

이 문서는 SsalddelApp 에 속한 화면별 README를 모은 색인입니다. 각 화면 문서는 캡처 이미지, 화면 책임, 사용자와 참여자, API/서버 연계, 보안 점검을 별도로 설명합니다.

[MAUI 화주 조회 복귀 보완](../cargo-workflow-recovery-r1.md)은 운송 목록·상세의 최신 선택과 인증 수명을 연결한다. 별도 Web 화주 호스트의 실행 결과와 합산하지 않는다.

[창고 연계 의뢰의 완료 대조](../cargo-warehouse-journey-r1.md)는 MAUI 운송 목록과 공용 상세·타임라인에서 같은 의뢰 관계를 확인하는 책임을 설명한다. 서버 HTTP 완료와 MAUI 목록의 실제 UI 검증은 별도로 판정한다.

| 페이지 ID / 제목 | 라우트 | 분류 | 화면 책임 | 캡처 |
| --- | --- | --- | --- | --- |
| [SsalddelApp-P00 - 역할 기반 통합 커뮤니티 홈](SsalddelApp-P00/) | / | 필수 | 현재 역할에 맞는 홈 선택 | 완료 |
| [SsalddelApp-P01 - 화주 업무 홈, 운송 의뢰/상태/창고/판매 업무 진입](SsalddelApp-P01/) | /shipper | 필수 | 커뮤니티와 화주 업무 요약 조합 | 완료 |
| [SsalddelApp-P01-1 - 화주 프로필과 운영 프로필 설정](SsalddelApp-P01-1/) | /shipper/settings/profile | 보조 | 화주 프로필과 운영 프로필 설정 | 완료 |
| [SsalddelApp-P01-2 - 화주 앱 메뉴/화면 노출 설정](SsalddelApp-P01-2/) | /shipper/settings/views | 보조 | 화주 앱 메뉴/화면 노출 설정 | 완료 |
| [SsalddelApp-P01-3 - 공개 화물 또는 공개 의뢰 확인](SsalddelApp-P01-3/) | /shipper/public-cargo | 확장 | 공개 화물 또는 공개 의뢰 확인 | 완료 |
| [SsalddelApp-P01-4 - 탐색/제안성 업무 수신함](SsalddelApp-P01-4/) | /shipper/exploration/inbox | 확장 | 탐색/제안성 업무 수신함 | 완료 |
| [SsalddelApp-P02 - 운송 의뢰 작성](SsalddelApp-P02/) | /shipper/request | 필수 | 운송 의뢰 작성 | 완료 |
| [SsalddelApp-P02-1 - 운송 의뢰 대량 등록](SsalddelApp-P02-1/) | /shipper/request/bulk | 보조 | 운송 의뢰 대량 등록 | 완료 |
| [SsalddelApp-P02-2 - 배차 주소 입력/검증 폼](SsalddelApp-P02-2/) | /dispatch/address-form | 보조 | 배차 주소 입력/검증 폼 | 완료 |
| [SsalddelApp-P03 - 의뢰 상세, 결제/배차/상차/하차/정산 타임라인](SsalddelApp-P03/) | /shipper/request/{RequestId} | 필수 | 의뢰 상세, 결제/배차/상차/하차/정산 타임라인 | 완료 |
| [SsalddelApp-P04 - 화주 입고 업무 대시보드](SsalddelApp-P04/) | /shipper/inbound/dashboard | 확장 | 화주 입고 업무 대시보드 | 완료 |
| [SsalddelApp-P04-1 - 입고 요청 목록과 처리](SsalddelApp-P04-1/) | /shipper/inbound/requests | 확장 | 입고 요청 목록과 처리 | 완료 |
| [SsalddelApp-P05 - 화주 관점 창고 업무 허브](SsalddelApp-P05/) | /shipper/warehouse/workspace | 확장 | 화주 관점 창고 업무 허브 | 완료 |
| [SsalddelApp-P05-1 - 창고 재고 조회](SsalddelApp-P05-1/) | /shipper/warehouse/inventory | 확장 | 창고 재고 조회 | 완료 |
| [SsalddelApp-P05-2 - 창고 스캔 작업](SsalddelApp-P05-2/) | /shipper/warehouse/scan | 확장 | 창고 스캔 작업 | 완료 |
| [SsalddelApp-P05-3 - 창고 프로세스별 작업 시작](SsalddelApp-P05-3/) | /shipper/warehouse/work/{ProcessCode} | 확장 | 창고 프로세스별 작업 시작 | 완료 |
| [SsalddelApp-P06 - 판매채널 연결/관리](SsalddelApp-P06/) | /shipper/sales/channels | 확장 | 판매채널 연결/관리 | 완료 |
| [SsalddelApp-P06-1 - 상품 등록/리스팅](SsalddelApp-P06-1/) | /shipper/sales/listings | 확장 | 상품 등록/리스팅 | 완료 |
| [SsalddelApp-P06-2 - 판매 주문 이행 Simulation 허브](SsalddelApp-P06-2/) | /shipper/sales/fulfillment | 개발·확장 | 상태 변경 없는 목표 탐색 | desktop·501px 확인 |
| [SsalddelApp-P06-2-1 - 판매 주문 원장 목록](SsalddelApp-P06-2-1/) | /shipper/sales/orders | 확장 | 영속 판매 주문 검색·필터·페이지 조회 | 공용 Screen Web host 확인 |
| [SsalddelApp-P06-2-2 - 판매 주문 원장 상세](SsalddelApp-P06-2-2/) | /shipper/sales/orders/{OrderId} | 확장 | stable order ID의 주문·출고 투영 읽기 | 공용 Screen Web host 390px 확인 |
| [SsalddelApp-P06-2-3 - Simulation 샘플 반영](SsalddelApp-P06-2-3/) | /shipper/sales/fulfillment/samples | 개발·확장 | 비식별 로컬 샘플 준비 | route·build 확인 |
| [SsalddelApp-P06-2-4 - Simulation 주문 목록](SsalddelApp-P06-2-4/) | /shipper/sales/fulfillment/orders | 개발·확장 | 로컬 주문 후보 검색·필터 | desktop·501px 확인 |
| [SsalddelApp-P06-2-4-1 - Simulation 주문 상세](SsalddelApp-P06-2-4-1/) | /shipper/sales/fulfillment/orders/{OrderKey} | 개발·확장 | opaque stable 주문 key 한 건 조회 | route·build 확인 |
| [SsalddelApp-P06-2-5 - 재고·입고 신호](SsalddelApp-P06-2-5/) | /shipper/sales/fulfillment/inventory | 개발·확장 | 로컬 재고 snapshot·입고 검토 신호 | route·build 확인 |
| [SsalddelApp-P06-2-6 - 피킹 task 목록](SsalddelApp-P06-2-6/) | /shipper/sales/fulfillment/picking | 개발·확장 | 피킹 task 목록과 stable ID 선택 | route·build 확인 |
| [SsalddelApp-P06-2-6-1 - 피킹 task Action](SsalddelApp-P06-2-6-1/) | /shipper/sales/fulfillment/picking/{TaskId} | 개발·확장 | 로컬 스캔·보류·취소 | route·build 확인 |
| [SsalddelApp-P06-2-7 - 포장 task 목록](SsalddelApp-P06-2-7/) | /shipper/sales/fulfillment/packing | 개발·확장 | 포장 task 목록과 stable ID 선택 | route·build 확인 |
| [SsalddelApp-P06-2-7-1 - 포장 task Action](SsalddelApp-P06-2-7-1/) | /shipper/sales/fulfillment/packing/{TaskId} | 개발·확장 | 로컬 시작·완료 | route·build 확인 |
| [SsalddelApp-P06-2-8 - 입고 알림 정책](SsalddelApp-P06-2-8/) | /shipper/sales/fulfillment/restock-policy | 개발·확장 | 로컬 동의·발송 의도 편집 | route·build 확인 |
| [SsalddelApp-P07 - FCL/LCL 해외 물류 계획](SsalddelApp-P07/) | /shipper/international/fcl-lcl | 확장 | FCL/LCL 해외 물류 계획 | 완료 |
| [SsalddelApp-P07-1 - HS 코드/통관 검토](SsalddelApp-P07-1/) | /shipper/customs/hs-reviews | 확장 | HS 코드/통관 검토 | 완료 |
| [SsalddelApp-P08 - 재위탁/재운송 주문](SsalddelApp-P08/) | /shipper/reconsignment/orders | 확장 | 재위탁/재운송 주문 | 완료 |
| [SsalddelApp-P09 - 운송 업무 워크스페이스](SsalddelApp-P09/) | /shipper/transport | 필수 | 의뢰별 결제·배차·운송 상태 처리 | 캡처 대기 |
| [SsalddelApp-P10 - 꾸미기 상점](SsalddelApp-P10/) | /community/decorations | 확장 | 홈 테마·노드·괘상 상품 탐색 | 완료 |
| [SsalddelApp-P10-1 - 꾸미기 상품 상세](SsalddelApp-P10-1/) | /community/decorations/{ProductKey} | 확장 | 전체 테마 미리보기, 구매·적용 판단 | 완료 |
| [SsalddelApp-P10-2 - 꾸미기 FakePG 결제](SsalddelApp-P10-2/) | /community/decorations/{ProductKey}/checkout | 개발·확장 | 개발용 구매 승인 흐름 | 완료 |
| [SsalddelApp-P10-2-1 - 홈 테마 구매 완료와 적용 선택](SsalddelApp-P10-2-1/) | /community/decorations/{ProductKey}/checkout | 개발·확장 | 구매 완료 확인과 명시적 전체 적용 | 완료 |
| [SsalddelApp-P10-3 - 내 꾸미기 만들기](SsalddelApp-P10-3/) | /community/decorations/create | 확장 | 개인 괘상·노드 이미지 제작 | 캡처 대기 |
| [SsalddelApp-P10-4 - 디자이너 홈 테마 패키지 등록](SsalddelApp-P10-4/) | /community/decorations/themes/submit | 확장·제작 | 8개 슬롯 패키지 제작과 미리보기 | 완료 |
| [SsalddelApp-P90 - 템플릿/샘플성 날씨 화면](SsalddelApp-P90/) | /weather | 시스템 | 템플릿/샘플성 날씨 화면 | 완료 |
| [SsalddelApp-P91 - 템플릿/샘플성 카운터 화면](SsalddelApp-P91/) | /counter | 시스템 | 템플릿/샘플성 카운터 화면 | 완료 |
| [SsalddelApp-P99 - 미발견 페이지](SsalddelApp-P99/) | /not-found | 시스템 | 미발견 페이지 | 완료 |
