# [실험 기록 · 화면 오차 비교 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 2026-09-22]

> 이 문서는 현행 `PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD` r52를
> 변경하거나 대체하지 않는 Unity 비교 증거의 보조 실험 기록이다.

## 목표

Windows 1920×1080·60fps를 첫 비교 기준으로 두고, 같은 한반도 카메라 구도에서
목표 화면 오차 4/8/16px가 현재 1·4·16개 곡면 Mesh 중 어느 상세도를 선택하는지
계측한다. 이번 구현은 타일 수를 확정하는 단계가 아니라 정식 공간 타일 전환 전의
비교 모판이다.

## 확정

- 의미 단계 `Relief / Rivers / Hubs`와 공간 Mesh 상세도 `L0 / L1 / L2`를 비교
  모드에서 분리한다.
- 현재 Mesh 표본 간격을 지구 반지름, 카메라 표면 거리, 수직 FOV, viewport 높이로
  투영한 값을 `표본 간격 기반 SSE 추정`으로 표시한다.
- 비교 조건은 카메라 중심 거리 33, 지구 반지름 18, 수직 FOV 38도, 계산 높이
  1080px, 한반도 대표 위도 38.5도로 고정한다.
- 16px는 L0 1개, 8px는 L1 4개, 4px는 L2 16개를 선택한다.
- 비교 모드를 해제하면 기존 의미 단계 기반 1/4/16 선택으로 복귀한다.
- Unity는 읽기 전용 표현이며 이 비교가 운영·Simulation 권위를 변경하지 않는다.

## 구현 결과

| 목표 SSE | 선택 | 활성 타일 | 활성 정점 | 활성 삼각형 | 추정 SSE |
| ---: | --- | ---: | ---: | ---: | ---: |
| 4px | L2 | 16 | 13,200 | 5,284 | 3.910px |
| 8px | L1 | 4 | 7,252 | 2,980 | 5.214px |
| 16px | L0 | 1 | 1,813 | 738 | 10.427px |

거리 32에서 4px를 요구하면 L2도 4.189px이므로 현재 Mesh 상세 상한 도달로
표시한다. 실제 1920×1080 비교 캡처와 현재 Editor Game View 합성은 Unity
`Assets/Documentation/Changes/2026-09-22-korean-peninsula-sse-comparison/`에 둔다.

## 검증

- 화면 오차 선택기 집중 EditMode: 6/6 통과
- 기존 세계 지구본 EditMode: 21/21 통과
- Play Mode에서 세 기준 모두 `Hubs` 의미 단계와 하천 자료층 유지
- 상·하위 Mesh 단계 동시 활성 없음
- 지구본·한반도 지형·SSE 관련 Console 오류 0건
- Pipeline 성능 값은 단일 Editor frame snapshot이라 평균·p95·60fps 완료 증거로
  승격하지 않는다.

## 경계와 미정

- 현재 21개 Mesh를 모두 선생성하므로 메모리·streaming 개선은 성립하지 않는다.
- 원본 405×540 파생 이미지와 Natural Earth 경계보다 정보가 정밀해진 것은 아니다.
- 이번 1/4/16 선택은 `WorldCRS84Quad` 주소를 가진 가지별 adaptive quadtree가
  아니다.
- 다음 후보는 8px이며, 확정 전 정식 논리 tile 주소, source maximum level,
  시야별 가지 선택, 지연 생성·해제, 캐시 상한과 Windows Player 평균·p95를 함께
  검증해야 한다.

## 다음 질문 하나

없음. 다음 개발은 8px를 임시 후보로만 사용해 `WorldCRS84Quad` 논리 대장과
지연 생성 모판을 좁게 만드는 단계다.
