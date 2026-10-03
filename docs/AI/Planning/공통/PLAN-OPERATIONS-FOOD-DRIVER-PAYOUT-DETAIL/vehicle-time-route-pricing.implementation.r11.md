# 기사 차량·시간대별 경로와 배달료 연결 r11

2026-10-03. r9 경로 거리 연결과 r10 운영자 기본 요금 설정 위에 추가한다.

## 동작

추천을 시작할 때 서버가 추천 대상의 배달기사 프로필 차량을 조회한다. 자동차/승용차/Car는 Car, 오토바이/이륜차/Motorcycle은 Motorcycle로 분류한다. 프로필 누락·미지원 수단은 FoodPricingDriverVehicleRequired로 제안 저장·알림을 막는다. 기존 프로필의 기본 문자열은 오토바이이므로 별도 차량 확인 절차를 대신하지 않는다.

자동차는 FoodDriverRoutePricing.CarRouteOption(기본 trafast), 오토바이는 MotorcycleRouteOption(traavoidcaronly)으로 Directions 5를 요청한다. 오토바이에 다른 옵션을 설정하면 제안 산정을 막는다. 자동차 전용도로 회피 우선은 공개 API가 문서화한 옵션이지 이륜차 통행을 보장하는 모드가 아니다.

동일 좌표라도 경로 옵션별로 요청 범위 캐시를 분리한다. 실제 Directions 5의 거리(m)를 km로 변환하여 기존 요금 엔진에 전달한다. 좌표 근사 fallback, 무효 거리, 요청과 다른 경로 옵션은 제안 요금 근거로 쓰지 않는다. API는 조회 당시 실시간 교통을 반영한다. 미래 출발시각 예약 API 또는 실제 주행 실측으로 표현하지 않는다.

제안금액 = 기존 기본/거리/최소지급 계산 + 기상 할증 + 기존 한시 수요 할증 + 차량별 시간대 할증.
기존 픽업비·전달비·거리 요율은 r10 정책을 재사용한다. 소요시간은 근거에 기록하며 분당 요금을 추가하지 않는다. 기존 한시 수요 할증의 Simulation 경계도 유지한다. 시간대 할증은 별도 정규 정책이므로 기존 한시 수요 할증과 함께 설정하면 둘 다 더해진다.

시간 구간은 한국 시간의 하루 분(0~1439)으로 설정하며 시작 포함/종료 제외다. 자정 넘김을 허용한다. 일치 구간 중첩·음수 금액·무효 구간은 차단한다. TimeBands가 비면 시간대 할증은 0원이고 기존 금액을 유지한다. 자동차는 구간별 CarRouteOption으로 경로 옵션을 바꿀 수 있고 오토바이는 회피 우선 옵션을 유지한다.

현재 보관된 제안을 동일 기사에게 다시 제시할 때 금액은 시각·현재 정책이 바뀌어도 유지한다. 거절/만료 뒤 다른 후보에게 새 제안을 만드는 경우에는 해당 기사 차량과 새 제안시각으로 재산정한다. 다른 후보를 거친 뒤 원래 기사로 돌아오는 경우도 새 제안이다. 수락·완료한 원장의 정산 금액을 다시 계산하는 변경이 아니다. 새로운 기사 산정이 실패하면 이전 계산을 덮어쓰지 않고 새 제안을 생성하지 않는다.

## 설정 위치

서버 구성의 FoodDriverRoutePricing 섹션을 사용한다. 비밀 API 키는 기존 NaverCloudDirections 설정에 둔다. 이번 작업에는 관리자 화면에서 시간대 목록 편집, 기사 앱에서 차량 변경 화면, 새 DB migration을 추가하지 않았다. 기존 차량 프로필과 제안 계산 JSON을 사용한다.

아래는 설정 형식의 예시이며 운영 시간·요금을 확정하거나 실제 설정에 적용한 값이 아니다.

```json
{
  "FoodDriverRoutePricing": {
    "CarRouteOption": "trafast",
    "MotorcycleRouteOption": "traavoidcaronly",
    "TimeBands": [
      {
        "Code": "example-lunch",
        "StartMinuteKst": 720,
        "EndMinuteKst": 840,
        "CarRouteOption": "traoptimal",
        "CarSurchargeKrw": 1000,
        "MotorcycleSurchargeKrw": 1000
      }
    ]
  }
}
```

배차 시 기사Id·차량·요청 옵션·거리 근거·소요시간·한국 시간 구간·할증액·제안시각을 기사제안요금계산근거Json에 저장한다. 정책 판본에도 차량/옵션/시간대/할증액을 포함한다. 기사에게는 기존 API의 총 기사지급예정액을 전달한다. 시간대 세부 항목은 계산 근거 JSON에서 확인한다.

## 공식 근거와 한계

- [Directions 5](https://api.ncloud-docs.com/docs/application-maps-directions5): 자동차 경로와 traavoidcaronly 회피 우선, summary.distance(m), 실시간 교통 반영.
- [이륜차 경로 이용 방법](https://help.naver.com/service/5637/contents/8245?lang=ko&osType=MOBILE): 지도 앱의 차종 설정. 앱 기능을 공개 API 모드로 가정하지 않는다.
- 자동차·오토바이 모두 같은 Directions 5 API를 사용하고 옵션/반환 거리/운영자 시간대 요금이 달라진다. 접근 구간(기사→매장) 및 멀티 픽업 경로 합산은 이번 단건 매장→전달지 계산에 포함하지 않는다.

## 검증

관련 시험 40개: mock HTTP로 실제 NAVER client→경로 service→차량/시간대 요금, 옵션별 캐시, 차량 미등록 차단, 한국시간 경계·자정 구간·중첩, 실제 SQLite 배차 추천 저장/재조회·동일 기사 동결·다른 기사 재산정·실패 시 알림 미생성까지 확인했다.
최종 Fast 근거: artifacts/local/validation/20261003-090025/. 관련 40/40 통과(시간대 자동차 옵션 override 포함).
Task: artifacts/local/validation/20261003-085857/. v0.0 전체 build 통과, 전체 시험 5533/5540 통과. 실패 7개는 r10(084500)의 실패 testName 집합과 정확히 일치한다. API 업무 분류/한국어 액션 이름, 업무 책임 문서, WebApp capability, 재료 화면 CSS의 기존 실패다. Task가 모두 통과한 것으로 보고하지 않는다.
실제 NAVER 유료/외부 호출, 운영 DB, 실기기 UI, commit/push는 수행하지 않았다.
