# 모바일 운영 UI 샘플 이미지 대장

> **공통 상태: `SampleOnly / NotApprovedVisual / NotImplementationSpec`**

이 폴더의 PNG는 기획 문답에서 상상과 비교를 돕기 위해 만든 **샘플용 이미지**다. Git에 커밋된 이유는 생성 이력과 문답 맥락을 잃지 않기 위해서이며, 커밋 자체는 시안 승인·화면 확정·구현 지시·실행 증거를 뜻하지 않는다. 실제 앱 화면, 실행 캡처, 코드 구현 완료 또는 서버 연결 증거로 사용할 수 없다.

화면 구조·정보 우선순위·문구·색·수치·버튼·상호작용은 각 `PLAN-*` 문서에서 별도로 확정해야 한다. 이미지와 문서가 다르면 승인된 최신 문서와 현재 route·contract·test를 우선한다.

## 상태 의미

- `샘플`: 화면 구조를 상상하고 비교하기 위한 자료다. 일부 현행 문답을 반영했더라도 확정 시안이 아니다.
- `대체된 샘플`: 후속 문답이나 코드 정리로 의미가 바뀐 자료다. 이력 보존만 하며 신규 구현 기준으로 사용하지 않는다.
- 파일명의 `candidate`, `r1`, `r2`는 생성 이력 식별자일 뿐 승인 단계나 완성도를 뜻하지 않는다.

## 2026-09-17~19 생성 시안

| 파일 | 상태 | 용도·주의 |
| --- | --- | --- |
| `operator-home-overview-r1-candidate.png` | 샘플 | 총괄 진입 화면 탐색. OS별 운영자 화면 우선 원칙과 비교용 |
| `pickup-delay-detail-r1-candidate.png` | 샘플 | 픽업 지연 상세 탐색. 시간·권장 조치는 서버 정책 판본을 따라야 함 |
| `food-delivery-os-overview-r1-superseded.png` | 대체된 샘플 | 기사 무응답을 운영 예외로 직접 묶은 초기 이미지 |
| `food-delivery-os-overview-r2-superseded.png` | 대체된 샘플 | 자동 재배차 대기를 예외 목록에 둔 초기 이미지 |
| `food-delivery-finance-impact-r1-candidate.png` | 샘플 | 음식 배달 OS의 읽기 전용 재무 영향·Simulation 비교 탐색 |
| `platform-operations-finance-impact-r1-candidate.png` | 샘플 | 플랫폼 전체 OS의 읽기 전용 재무 영향·Simulation 비교 탐색 |
| `operator-screen-catalog-r2-candidate.png` | 샘플 | OS별 운영자 화면 선택 대장 탐색 |
| `integrated-operations-ledger-order-detail-r1-candidate.png` | 샘플 | 통합 사건 목록과 주문 원장 상세의 시간순 연결 탐색 |
| `operations-exception-support-flow-r1-superseded.png` | 대체된 샘플 | 중복 조리 지연 명칭과 자동 재배차를 예외로 표현한 초기 상담 흐름 |
| `operations-exception-navigation-r1-superseded.png` | 대체된 샘플 | 중복 조리 지연 명칭과 자동 재배차를 예외로 표현한 초기 탐색 흐름 |

대체된 시안의 현재 기준은 `음식점 준비 지연`과 `배달 진행 지연`을 구분하고, 추천 만료·기사 거절·재배차 대기는 배차 엔진의 자동 회복으로 처리하며 운영자 예외 사건으로 올리지 않는 것이다. 원장·Outbox·투영 실패는 별도의 기술 이상으로 다룬다.

## 기존 샘플 이미지

- `food-sales-order-decision-r1.png`
- `orderer-active-order-home-r1.png`
- `orderer-delivery-tracking-detail-r1.png`
- `food-driver-map-home-r1.png`
- `food-driver-profile-history-r1.png`
- `food-driver-payout-detail-r1-candidate.png`
- `food-driver-payout-detail-r2-candidate.png`
- `food-delivery-os-operator-workspace-r1.png`
- `os-operator-screen-catalog-r1.png`
- `food-delivery-os-dispatch-supply-r1.png`

위 자료도 모두 `SampleOnly`다. 파일명에 `candidate`가 없는 자료 역시 확정 시안이 아니며, 실제 법정 공제·보험·지급·정산이나 앱 상호작용을 확정하지 않는다.
