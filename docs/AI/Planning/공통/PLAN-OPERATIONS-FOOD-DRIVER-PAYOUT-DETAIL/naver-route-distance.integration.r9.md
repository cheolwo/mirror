# 네이버 경로 거리와 음식배달 제안요금 연결

[운영 · PLAN-OPERATIONS-FOOD-DRIVER-PAYOUT-DETAIL · r9]

기준일: 2026-10-03. 사용자 요청은 시작지·도착지로 네이버 경로 거리를 구해 기존 배달료 엔진에 전달하는 것이다. 선행은 [제안 계산 동결 r8](dispatch-calculation.integration.r8.md)다.

## 구현

기존 `음식배달기사제안요금Service`는 좌표 간 `CalculateDistanceKm`를 호출하던 부분을 비동기 `EstimateRouteAsync`로 교체했다. 기존 네이버 typed HTTP client가 반환하는 `summary.distance`(m)를 1,000으로 나눈 km를 기존 요금 Policy에 전달한다. 음식점 픽업→고객 전달 구간이며 기사 현재 위치→음식점 접근 거리나 멀티 배차 전체 경로를 합산하지 않는다. 유효 위경도·조회 거리0~1000km를 확인하며 미확인 값은0으로 대체하지 않는다.

좌표 누락/무효는 `FoodPricingCoordinatesRequired / FoodPricingCoordinatesInvalid`, 외부 조회 실패·근사 fallback·유효 거리 부재는 `FoodPricingRouteUnavailable`로 요금 확정을 중단한다. 기존 배차 후보 추천의 fallback 자체를 제거한 것은 아니다. 예외 시 새 제안 동결은 성공으로 취급하지 않는다. 사용자에게 보여주는 복구 안내·재시도 UI는 이번 범위가 아니다.

제안 산정 결과의 `산정거리Km / 거리근거Code / 경로차량Code`는 기존 원장의 계산근거 JSON 직렬화에 포함된다. `NaverDirections5CarRouteEstimate / Car`로 자동차 경로 추정이라는 근거를 보존한다. 이미 동결된 이전 제안은 재계산하지 않으며 고객 좌표를 새 공개 계약이나 로그에 추가하지 않는다.

기존 두 좌표 메서드는 유지하고 cancellation overload를 추가했다. 실제 HTTP 요청으로 취소 토큰을 전달한다. 네이버 응답은 요청한 option 순서로 선택하며 자동차 전용도로 회피 등 기존에 파싱하지 못하던 option도 처리한다. 응답 오류 code·음수/누락 거리·요청하지 않은 option을 성공 경로로 취급하지 않는다. 캐시는 기존 요청 scope 내에서만 재사용한다.

## 공식 자료와 적용 한계

[NAVER Cloud Directions 5 공식 API](https://api.ncloud-docs.com/docs/application-maps-directions5), 2026-10-03 확인: 시작/도착 좌표는 경도,위도 순서이며 거리 단위는m, 소요시간은ms다. 자동차 경로이며 경유지는 최대5개, 실시간 교통에 따라 같은 입력의 경로도 바뀔 수 있다. `traavoidcaronly`는 회피 우선 옵션이지 오토바이 통행 가능 경로 보증이 아니다.

API 경로 예상값을 실제 주행 실측값이나 오토바이 전용 거리로 표시하지 않는다. 실제 오토바이 배달 운영 전에는 통행 가능한 경로 제공자·요금 거리 기준을 별도 확정해야 한다. 실제 API 키 권한/외부 응답/운영 실행/휴대폰 화면 검증은 수행하지 않았다. mock key는 시험 전용이며 실서비스 키를 파일에 넣지 않는다.

픽업/전달 각각1,000원·100m당100원이라는 사용자 검토 가정은 기존 요율을 자동 변경하는 근거로 사용하지 않았다. 현재 원장의 정책 판본·거리 올림/포함 거리·최소 지급액·기상/수요 할증을 그대로 사용한다. 신규 요율과 시간대·동시 픽업 귀속은 별도 정책 판본으로 구현해야 한다.

## 검증

관련34/34 시험과 Fast의 서버/시험 프로젝트 build·diff 검증을 통과했다. `artifacts/local/validation/20261003-083253/`에 TRX와 로그를 보존한다. Task(`20261003-083326`)의 `Ssalddel.v0.0.slnx` build 통과. 전체 시험5,527/5,534 통과·7실패로 전체 게이트는 미통과다. 실패 시험명7개는 이전 `20261002-152909/Ssalddel.Tests.trx`와 집합 비교로 동일함을 확인했다. API 업무명/역할 분류·업무 책임 문구·WebApp capability 분류·재료 화면 CSS이며 이번 수정 범위 밖이다. 모의 HTTP→경로 service→제안요금 Policy→직렬화 근거를 확인하며 실제 NAVER 조회·DB 저장·기사 제안 UI 실행 증거와 구분한다.

새 경로 기준은 기존 요금 정책 판본에 `distance:naver-directions5-car.r1`로 결속한다. 기존 배차 전환이 처리하는 입력 오류 형식(ArgumentException)을 유지하여 경로 실패도 배차구성오류 응답으로 반환하고 저장/알림 없이 재시도할 수 있다.
