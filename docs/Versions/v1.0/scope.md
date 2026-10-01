# Hongdal 1.0 Scope

| 기능 | 포함 여부 | 관련 앱/모듈 | 메모 |
| --- | --- | --- | --- |
| 커뮤니티 홈·게시판·글쓰기 | 선행 의존 | `Hongdal.Ui.Common`, 역할별 앱 홈 | 0.0 범위이며 1.0에서 재구현하지 않음 |
| 공동 원장·다이어그램 | 선행 의존 | 원장 API, 다이어그램 작업공간 | 0.0 원장에서 운송 의뢰로 인계 |
| 참여 동의·신고·신뢰 기록 | 선행 의존 | 커뮤니티 API, 관리자 화면 | 0.0 게이트 통과가 1.0의 전제 |
| 국내 화물/용달 운송 의뢰 | 제한 포함 | `HongdalApp`, `Hongdal` | 샘플 원장을 이용한 기술 검증 |
| 화물/용달 기사 배차 | 제한 포함 | `DriverApp`, `CargoYongdalDispatchEngine` | 모의 기사·샘플 데이터 전용, 실운영 비활성 |
| 상차/하차 사진 증빙 | 제한 포함 | `DriverApp`, 파일 업로드 서비스 | 원장 상태·증빙 연동 검증 |
| 수령자 인수 확인 | 제한 포함 | 운송 상세 DTO | 샘플 흐름과 노출 범위 검증 |
| 운송 상태 전이 | 제한 포함 | `기사운송상태전이Service` | 배차대기/상차지도착/상차완료/하차지도착/인수완료 기술 검증 |
| 결제/정산 폐쇄 루프 | 제한 포함 | FakePG, 결제/정산 서비스, 인수증 문서 | 실제 금전 이동 없이 상태와 증빙만 검증 |
| 수령자 정보 단계별 마스킹 | 포함 | 운송 상세 DTO, DriverApp | 배차 전/수락 후/하차 직전/완료 후 구분 |
| 유상 배차·주선·운임 수취·정산 | 보류 | 운영 설정, 결제/정산 연동 | 허가·제휴·법률 검토 전 비활성 |
| 창고 입고/출고 고도화 | 보류 | `WarehouseManagerApp` | 1.5 |
| 통관/HS 데이터 | 보류 | `Hongdal.WebApp`, `HongdalAdmin` | 2.0 |
| 음식점 일반 음식 배달 | 보류 | `FDriverApp`, `FoodDeliveryDispatchEngine` | 3.0 |
| 알뜰살뜰 마트 즉시배송 | 보류 | `FDriverApp`, `FoodDeliveryDispatchEngine`, `WarehouseManagerApp` | 3.5 |

실운영 경계와 공식 근거는 [커뮤니티 0.0 기반 제품 원칙](../../Architecture/CommunityFoundationV0Policy.md)을 따릅니다.
