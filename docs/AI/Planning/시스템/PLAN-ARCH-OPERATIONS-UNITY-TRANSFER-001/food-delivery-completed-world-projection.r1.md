# 음식 배달 정상 완료 상태 사본 수직 조각 r1

- 상위 기획: `PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001` r7
- 소유 운영체제: `FoodDeliveryOS`
- 상태: `Implemented / DatabaseMigrationPreparedNotApplied / UnityContractVerified / RuntimeNotVerified`

## 확정 범위

첫 수직 조각은 `주문대기 → 조리중 → 픽업대기 → 기사배정 → 픽업완료 → 전달완료 → 수령확인`의 순서가 운영 원장에 모두 남은 정상 완료 건만 다룬다. 주문자 수령 확인을 저장할 때 같은 DB 저장 단위에서 발행 요청을 남기고, 비동기 작업이 완료 판본과 확정 기사 결속을 다시 검증한 뒤 지역 장면용 상태 사본을 만든다.

거절·취소·사고·중단·재조리·재배차와 화물 운송은 이 조각에 포함하지 않는다. Unity Scene·GameObject·Prefab·Animation과 모바일·Web 화면도 변경하지 않는다.

## 개인정보·권위 경계

- 공개 상태 사본에는 주문번호, 실제 주문자·음식점·기사 식별자, 성명, 연락처, 주소, GPS 좌표를 넣지 않는다.
- 역할 식별자는 상태 사본마다 새로 만든 합성 식별자이며 실제 사람을 역추적하는 계약이 아니다.
- 단계별 시각은 시작 뒤 경과 초만 제공한다. 절대 시각은 완료·발행·만료 시각만 제공한다.
- 클라이언트 저장과 재생은 허용하지 않고, 서버 상태 사본은 발행 한 시간 뒤 조회에서 제외하고 비동기 정리한다.
- 법정동명이 운영 주소에 명시된 면목동·중화동만 승인된 지역 고유 식별자로 분류한다. 공간 근거가 없으면 `region:kr:bjd:unclassified`로 남긴다.
- 운영 원장이 최종 권위다. Unity 계약과 지역 조회는 읽기 전용이며 운영 상태를 바꾸지 않는다.

## 구현 결속

- 완료 저장과 발행 요청: `EfSsalddelFoodOrderStore`
- 재처리·멱등 발행: `음식배달완료WorldProjectionService`
- 서버 상태 사본: `음식배달완료_WorldSnapshot`
- 지역 조회: `GET api/v1/food-delivery/world/areas/{areaStableId}/completed-lifecycles`
- Unity 공유 계약: `음식배달완료WorldSnapshotContracts.cs`
- DB 마이그레이션: `20260911093054_AddFoodDeliveryCompletedWorldProjection`

## 검증 상한

서버 빌드, 원장·발행·멱등·부분 실패·만료·개인정보 계약의 집중 시험과 Unity 계약 시험까지 검증한다. 실제 MySQL 마이그레이션 적용, Quartz 주기 실행, 인증 HTTP 호출, Unity 프로젝트 가져오기·Editor·Play Mode·Game View는 별도 실행 증거 전까지 완료로 보지 않는다.
