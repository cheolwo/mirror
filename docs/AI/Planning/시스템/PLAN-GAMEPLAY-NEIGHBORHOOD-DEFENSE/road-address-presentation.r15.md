# [기획·구현 · 도로명주소 표현층 · PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE · r15]

- 판본: `sagajeong-road-address-presentation.r1`.
- 상태: `ApprovedReadOnlyPresentationSlice / ImplementationInProgress`.
- 승인 근거: 사용자가 도로명주소 기준의 정리 상태를 확인한 뒤 그 방식으로 진행하도록 요청했다.
- 기존 [사가정 생활 디오라마 r3](../PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/diorama.r3.md)의 주소 패널 제외 기준을 이 작은 공개 지리 표현 범위에서 대체한다. 비공개 사업체 주소 검토 패널을 합치는 것이 아니라 OSM 도로명과 OSM 건물번호만 현재 관찰 화면에 연결한다.
- [방어 준비 r1](README.md), [Accepted 공간 연구 r2](placement-study.r2.md), [높이 보완 r14](building-presentation.r14.md)의 WI·방어 자리·높이·업무 표식·지도 hash는 변경하지 않는다.

## 동결 자료 점검

```text
SagajeongReference r3
├─ 도로 2,397구간
│  ├─ 원 OSM 도로명 연결 1,109구간
│  └─ 도로명 미연결 1,288구간
├─ 도로명 78개
│  ├─ 큰길 4개: 사가정로·면목로·용마산로·면목천로
│  └─ 생활도로 74개
└─ 건물 602개
   ├─ 도로명+건물번호 580개
   ├─ 위 78개 도로명과 연결 568개
   └─ 도로명이 없거나 현재 도로 자료와 미연결 34개
```

원자료는 `artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm`, SHA-256 `3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3`이다. 지도는 `sagajeong-reference.r3`, SHA-256 `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3`을 유지한다. 둘 모두 OSM 공개 자료이며 ODbL 1.0 귀속을 유지한다.

## 표현 계약

1. 별도 `SagajeongRoadNameOverlay.json`은 원 OSM way의 `name/highway`와 동결 지도 segment ID를 대조하여 도로명별 way ID·대표 좌표·방향·구간 수·연결 건물 수를 보존한다. 원본 지도 JSON을 다시 생성하거나 hash를 바꾸지 않는다.
2. 대표 좌표는 해당 도로명에 속한 현재 지도 구간 중 가장 긴 구간의 중앙이며 **라벨 배치용**이다. 도로 중심선·주소 권위·길찾기·이동 가능성의 증거가 아니다.
3. 전체 보기에서는 큰길 4개를 우선 표시한다. 확대 시 가까운 생활도로를 제한된 개수만 표시하고, 기존 업무 라벨·상세 패널과 겹치면 생략한다.
4. 도로명을 선택하면 지도에 이미 포함된 `street + houseNumber`를 건물번호 목록으로 보여주고, 항목 선택 시 해당 건물 윤곽의 경계상자 중심으로 관찰 카메라를 옮긴다. 실제 출입구·입주·우편 전달 지점은 뜻하지 않는다.
5. 사업체명·영업 상태·비공개 검토 대장·개인 위치는 이 표현층에 포함하지 않는다. 기존 `면목동주소연결View`와 DB 원장은 읽거나 변경하지 않는다.
6. 방어 준비 화면에서는 도로명 UI를 숨겨 기존 선택·확인 흐름을 보존한다. Scene·운영 API·DB·Simulation Core·서버 권위는 변경하지 않는다.

## 실패와 검증

지도/원자료 hash, way ID, 도로명 중복, 대표 좌표, 중요도, 구간·건물 수가 맞지 않으면 도로명 표현층만 거부하고 지도·업무 관찰은 유지한다. 생성 결과 결정성, 78개/1,109구간/568건물, 확대 수준별 라벨 제한, 주소 선택 좌표, 원본 불변과 기존 지도·높이·방어·운영 표시 회귀를 EditMode로 검증한다. Play Mode/Game View·실제 클릭·E 승격·commit/push는 별도다.
