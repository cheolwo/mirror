# HongdalApp 화면 문서

[전체 화면 문서](../README.md) / [앱 전체 카탈로그](../../app-page-catalog.md)

이 문서는 HongdalApp 에 속한 화면별 README를 모은 색인입니다. 각 화면 문서는 캡처 이미지, 화면 책임, 사용자와 참여자, API/서버 연계, 보안 점검을 별도로 설명합니다.

| 페이지 ID / 제목 | 라우트 | 분류 | 화면 책임 | 캡처 |
| --- | --- | --- | --- | --- |
| [HongdalApp-P00 - 역할 기반 통합 커뮤니티 홈](HongdalApp-P00/) | / | 필수 | 현재 역할에 맞는 홈 선택 | 완료 |
| [HongdalApp-P01 - 화주 업무 홈, 운송 의뢰/상태/창고/판매 업무 진입](HongdalApp-P01/) | /shipper | 필수 | 커뮤니티와 화주 업무 요약 조합 | 완료 |
| [HongdalApp-P01-1 - 화주 프로필과 운영 프로필 설정](HongdalApp-P01-1/) | /shipper/settings/profile | 보조 | 화주 프로필과 운영 프로필 설정 | 완료 |
| [HongdalApp-P01-2 - 화주 앱 메뉴/화면 노출 설정](HongdalApp-P01-2/) | /shipper/settings/views | 보조 | 화주 앱 메뉴/화면 노출 설정 | 완료 |
| [HongdalApp-P01-3 - 공개 화물 또는 공개 의뢰 확인](HongdalApp-P01-3/) | /shipper/public-cargo | 확장 | 공개 화물 또는 공개 의뢰 확인 | 완료 |
| [HongdalApp-P01-4 - 탐색/제안성 업무 수신함](HongdalApp-P01-4/) | /shipper/exploration/inbox | 확장 | 탐색/제안성 업무 수신함 | 완료 |
| [HongdalApp-P02 - 운송 의뢰 작성](HongdalApp-P02/) | /shipper/request | 필수 | 운송 의뢰 작성 | 완료 |
| [HongdalApp-P02-1 - 운송 의뢰 대량 등록](HongdalApp-P02-1/) | /shipper/request/bulk | 보조 | 운송 의뢰 대량 등록 | 완료 |
| [HongdalApp-P02-2 - 배차 주소 입력/검증 폼](HongdalApp-P02-2/) | /dispatch/address-form | 보조 | 배차 주소 입력/검증 폼 | 완료 |
| [HongdalApp-P03 - 의뢰 상세, 결제/배차/상차/하차/정산 타임라인](HongdalApp-P03/) | /shipper/request/{RequestId} | 필수 | 의뢰 상세, 결제/배차/상차/하차/정산 타임라인 | 완료 |
| [HongdalApp-P04 - 화주 입고 업무 대시보드](HongdalApp-P04/) | /shipper/inbound/dashboard | 확장 | 화주 입고 업무 대시보드 | 완료 |
| [HongdalApp-P04-1 - 입고 요청 목록과 처리](HongdalApp-P04-1/) | /shipper/inbound/requests | 확장 | 입고 요청 목록과 처리 | 완료 |
| [HongdalApp-P05 - 화주 관점 창고 업무 허브](HongdalApp-P05/) | /shipper/warehouse/workspace | 확장 | 화주 관점 창고 업무 허브 | 완료 |
| [HongdalApp-P05-1 - 창고 재고 조회](HongdalApp-P05-1/) | /shipper/warehouse/inventory | 확장 | 창고 재고 조회 | 완료 |
| [HongdalApp-P05-2 - 창고 스캔 작업](HongdalApp-P05-2/) | /shipper/warehouse/scan | 확장 | 창고 스캔 작업 | 완료 |
| [HongdalApp-P05-3 - 창고 프로세스별 작업 시작](HongdalApp-P05-3/) | /shipper/warehouse/work/{ProcessCode} | 확장 | 창고 프로세스별 작업 시작 | 완료 |
| [HongdalApp-P06 - 판매채널 연결/관리](HongdalApp-P06/) | /shipper/sales/channels | 확장 | 판매채널 연결/관리 | 완료 |
| [HongdalApp-P06-1 - 상품 등록/리스팅](HongdalApp-P06-1/) | /shipper/sales/listings | 확장 | 상품 등록/리스팅 | 완료 |
| [HongdalApp-P06-2 - 판매 주문 이행/출고 연결](HongdalApp-P06-2/) | /shipper/sales/orders | 확장 | 판매 주문 이행/출고 연결 | 완료 |
| [HongdalApp-P07 - FCL/LCL 해외 물류 계획](HongdalApp-P07/) | /shipper/international/fcl-lcl | 확장 | FCL/LCL 해외 물류 계획 | 완료 |
| [HongdalApp-P07-1 - HS 코드/통관 검토](HongdalApp-P07-1/) | /shipper/customs/hs-reviews | 확장 | HS 코드/통관 검토 | 완료 |
| [HongdalApp-P08 - 재위탁/재운송 주문](HongdalApp-P08/) | /shipper/reconsignment/orders | 확장 | 재위탁/재운송 주문 | 완료 |
| [HongdalApp-P09 - 운송 업무 워크스페이스](HongdalApp-P09/) | /shipper/transport | 필수 | 의뢰별 결제·배차·운송 상태 처리 | 캡처 대기 |
| [HongdalApp-P10 - 꾸미기 상점](HongdalApp-P10/) | /community/decorations | 확장 | 홈 테마·노드·괘상 상품 탐색 | 완료 |
| [HongdalApp-P10-1 - 꾸미기 상품 상세](HongdalApp-P10-1/) | /community/decorations/{ProductKey} | 확장 | 전체 테마 미리보기, 구매·적용 판단 | 완료 |
| [HongdalApp-P10-2 - 꾸미기 FakePG 결제](HongdalApp-P10-2/) | /community/decorations/{ProductKey}/checkout | 개발·확장 | 개발용 구매 승인 흐름 | 완료 |
| [HongdalApp-P10-2-1 - 홈 테마 구매 완료와 적용 선택](HongdalApp-P10-2-1/) | /community/decorations/{ProductKey}/checkout | 개발·확장 | 구매 완료 확인과 명시적 전체 적용 | 완료 |
| [HongdalApp-P10-3 - 내 꾸미기 만들기](HongdalApp-P10-3/) | /community/decorations/create | 확장 | 개인 괘상·노드 이미지 제작 | 캡처 대기 |
| [HongdalApp-P10-4 - 디자이너 홈 테마 패키지 등록](HongdalApp-P10-4/) | /community/decorations/themes/submit | 확장·제작 | 8개 슬롯 패키지 제작과 미리보기 | 완료 |
| [HongdalApp-P90 - 템플릿/샘플성 날씨 화면](HongdalApp-P90/) | /weather | 시스템 | 템플릿/샘플성 날씨 화면 | 완료 |
| [HongdalApp-P91 - 템플릿/샘플성 카운터 화면](HongdalApp-P91/) | /counter | 시스템 | 템플릿/샘플성 카운터 화면 | 완료 |
| [HongdalApp-P99 - 미발견 페이지](HongdalApp-P99/) | /not-found | 시스템 | 미발견 페이지 | 완료 |
