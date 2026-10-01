# Hongdal 3.0 Scope

| 기능 | 포함 여부 | 관련 앱/모듈 | 메모 |
| --- | --- | --- | --- |
| 음식점 주문 | 포함 | `OrdererApp`, `RestaurantDeskApp` | 일반 음식점 주문 |
| 조리/픽업 상태 | 포함 | `RestaurantDeskApp` | 접수, 조리, 픽업 예상 시각 |
| 음식 배달 배차 | 포함 | `FoodDeliveryDispatchEngine`, `FDriverApp` | 음식점 픽업 배달 |
| 묶음 배달 | 포함 | 음식 배달 정책 | 거리/시간 기준 |
| 알뜰살뜰 마트 도심배송 | 보류 | `WarehouseManagerApp`, `OrdererApp`, `FDriverApp` | 3.5 |
| 주문자 집단 공동 주문 | 참조 | `OrdererApp`, 공동 주문 서비스 | 2.5 흐름과 운영 경계 유지 |
| 국내 화물/용달 핵심 변경 | 보류 | `DriverApp`, `HongdalApp` | 1.0 안정성 유지 |
