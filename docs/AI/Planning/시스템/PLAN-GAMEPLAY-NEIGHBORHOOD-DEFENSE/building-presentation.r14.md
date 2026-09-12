# [기획·구현 · 건물 높이 보완층 · PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE · r14]

- 판본: `sagajeong-building-height-presentation.r1`.
- 상태: `ApprovedReadOnlyPresentationSlice / ImplementationInProgress`. 사용자 “어 그렇게 다음 단계를 진행을 해줘”에 따라 [r13 다음 범위](building-evidence.r13.md)의 대응 검토와 작은 높이 반영을 진행한다.
- [준비 구현 r1](README.md), [Accepted 공간 연구 r2](placement-study.r2.md), [기존 E7 명세](preparation.e7-work-order.json)의 WI·주체·옥상 A/B·명령·저장 경계는 변경하지 않는다. 별도 [높이 보완 작업 명세](building-height.e7-work-order.json)로 표현 영향을 추적한다.

## 대응 검토와 적용 상한

| OSM 건물 ID | 주소 | 대장 높이 / 기존 표현 | 윤곽 면적 / 대장 건축면적 |
| --- | --- | --- | --- |
| osm:way:1240882409 | 동일로96길 51 | 8.3m / 4m | 153.79㎡ / 128.77㎡ |
| osm:way:1240882396 | 동일로92길 57 | 19.1m / 4m | 194.45㎡ / 169.50㎡ |
| osm:way:1240882352 | 사가정로39길 79 | 원문0 / 4m 유지 | 93.36㎡ / 88.97㎡ |

세 건 모두 동결 지도 내 동일 도로명·본번 후보가 하나이며, r13의 주소·필지 일치와 필지별 단일 표제부 응답을 재확인했다. 면적은 서로 다르고 측량 방식·기준 시점도 다르므로 면적비로 윤곽을 변형하거나 공식 동일 건물로 확정하지 않는다. 두 양수 높이만 **주소·필지 후보에 연결한 대장 높이 표현**으로 사용한다. 이는 현장 실측 검증이나 공식 건물 식별자 확정이 아니다. DB의 `PendingHumanReview`·`unityApplyAllowed=false`인 원본 검토 기록은 변경하지 않고, 이 판본이 별도 로컬 표현 실험의 승인 범위를 소유한다.

높이0·5층 건물은 [r11 임시 높이 원칙](building-approximation.r11.md)대로 4m를 유지한다. 층당 높이·지하층 가산·형태 추정은 추가하지 않는다.

## 구현 경계

1. `SagajeongReference.json` r3/hash `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3`는 바이트 단위로 보존한다.
2. `SagajeongBuildingHeightOverlay.json`은 같은 지도 hash에 결속한 읽기 전용 높이 보완층이다. 두 ID·높이·주소 대조값·출처 URL/응답 hash·레코드 생성일·후보 연결 수준만 포함한다. 키, 사적 사업체 자료, GIS 원문, 개인 정보는 포함하지 않는다.
3. Unity 조립 직전 지도 판본/hash, 후보의 고유성·주소, 기존 `SymbolicHeight`, 양수 유한 높이, 출처·표현 전용 상태를 검사한다. 불일치 시 보완층은 거부하고 원본 배경은 유지하되 진단을 명시한다. 공급처 호출이나 DB 쓰기는 없다.
4. Mesh의 벽·지붕·접지 그림자만 보완값을 읽는다. 원본 좌표/높이·운영 표식·주소 연결·옥상 배치 자료를 변경하지 않는다. 방어 A/B는 다른 건물이며 44m/40m 기준선을 보존한다. 실제 옥상 접근·NPC 경로·전투·새 배치 허용은 없다.
5. 공식 건물관리번호 교차 확정, GIS 이용조건 검토, 신규 수집·지역 확대·서버 공개·게임 배포는 이번 범위 밖이다. Graph Map은 고정 WI/슬롯 영향 없음이며 전역 완결로 확대하지 않는다.

## 검증 계획

두 값과 5층 임시값, 잘못된 판본/hash/주소/중복/출처/수치의 거부, 원본 불변, 실제 생성 Mesh 높이, 기존 운영·방어 배치 호환을 EditMode로 검증한다. Scene 저장·Play Mode/Game View·E 승격·commit/push는 수행하지 않는다. 결과는 아래에 사실대로 보완한다.
