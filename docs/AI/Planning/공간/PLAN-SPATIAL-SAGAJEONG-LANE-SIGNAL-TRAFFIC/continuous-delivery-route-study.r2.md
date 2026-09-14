# 사가정 연속 배달 표시 경로 연구 r2

- 상태: `AcceptedForSyntheticObservation / PendingHumanReview / TraversalReady=false`
- 대상: `station:kr:kric:s1107:0722`, `world-region:kr:seoul:jungnang:sagajeong.r1`
- 원본: 동결 OSM `map.osm`, SHA-256 `3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3`
- 이동망: `sagajeong-mobility-graph.osm-candidate.r1`, projection hash `59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7`
- 이동망 stable ID·content hash: `mobility-graph:kr:seoul:jungnang:sagajeong.r1`, `CD3E8FBD6428F7499F9E313E1AD3CFC04688EFAEF370E6C109F2321D70B11768`

## 목적

기존 저밀도 표현의 배달 오토바이는 골목 way `1256772587` 안에서만 왕복했다. r2는 사가정역 교차로 접근, 합성 신호 대기, 생활도로, 골목 종점을 하나의 기사 표시 여정으로 연결한다. 이 여정은 새 주문·배차·전달 권위가 아니라 화면과 연결 계약을 검증하기 위한 `SyntheticObservationRoute`다.

## 동결 경로

건물 중심 직선이나 가까운 선 자동 연결을 사용하지 않았다. 아래 순서는 동결 이동망의 명시적 공유 노드로만 이어진다.

| 구간 | OSM 근거 | 원천 edge 길이 합 | 표시 도형 길이 | 판정 |
| --- | --- | ---: | ---: | --- |
| 대로 접근·신호 통과 | way `218790209`, `사가정로`, `secondary` | 107.936m | 107.936860m | 우측 차로 오프셋은 합성 표시 |
| 생활도로 접근 | way `1112326984` → `1112326970`(`면목로44길`) → `576294786`(`면목로44가길`) | 146.565m | 146.565300m | 원본 중심선 후보 |
| 골목 마지막 접근 | way `1256772587`, `service`, `면목로44가길` | 22.775m | 22.775228m | 원본 중심선 후보 |
| 합계 | 14개 동결 표시점, 3개 표시 leg | 277.276m | 277.277388m | 사용자 표시는 약 277.3m, `ManualVisualReviewCandidate` |

이동망의 1mm 단위 edge 길이 합과 3자리 좌표로 다시 계산한 표시 도형 길이는 서로 다른 값이다. 상태 사본은 이를 각각 `sourceDistanceMeters`와 `displayGeometryDistanceMeters`로 보존하며 한 값을 다른 값의 검증 근거로 바꾸지 않는다.

### 원천 간선 순서

상태 사본과 표시 경로 지문은 다음 15개 edge의 전체 안정 ID, 순서, 원천 방향 판정과 표시 도형 읽기 순서를 보존한다.

- 대로: `218790209:21p0 → 21p1 → 22p0 → 22p1 → 23p0 → 24p0 → 25p0`, 모두 `AsStored`.
- 생활도로: `1112326984:0p0`은 `AsStored`; `1112326970:1p0 → 0p1 → 0p0`은 세 구간 모두 `ReverseStored`; `576294786:0p0`은 `AsStored`.
- 골목: `1256772587:0p0 → 1p0 → 2p0`, 모두 `AsStored`.

`AsStored`와 `ReverseStored`는 동결 자료의 geometry를 화면 경로가 어느 순서로 읽는지만 뜻한다. 모든 source edge의 통행 방향은 계속 `Unknown`이고, 화면 배우의 `AsDefined` 진행도 OSM 통행 방향 승인이 아니다.

모든 원천 간선은 `candidateModeCodes` 상 Motorcycle을 포함하지만 `directionCode=Unknown`, `accessReviewCode=PendingHumanReview`, `runtimeAuthorized=false`다. 따라서 경로 순서는 합성 관찰을 위해만 동결하며 `ReviewedLaneTraversalGraph`나 운영 길찾기에 주입하지 않는다.

## 상태 계약

- `RouteFingerprint` 와 `CourierDisplayRouteFingerprint`를 분리한다. 전자는 기존 7-leg 합성 음식배달 여정 결속이고, 후자는 이 277.277388m 표시선 순서와 도형 결속이다.
- 상태 사본은 4개 lane의 원천 ID·축·정지선·도형과 3개 route leg를 함께 보낸다. Unity가 로컬에서 다른 경로를 추정하지 않도록 한다.
- 기사는 주변 차량과 같은 동서 접근 lane에서 6m 최소 간격과 합성 신호 gate를 적용받는다. 생활도로 4m/s와 골목 2.5m/s는 실제 제한속도나 관측속도가 아닌 이 판본의 저밀도 합성 표시 속도다.
- 기존 음식배달 `CourierJourneyId`와 `RouteFingerprint`는 `DeclaredSyntheticReferenceOnly`로만 참조한다. `OperationalBindingReady=false`, `CanonicalDeliveryMutationAllowed=false`이며 이 경로가 운영 주문의 도착·전달·수령 상태를 바꾸지 않는다.
- 마지막 점은 주소·건물 Entrance·CurbStop이 확정되지 않았으므로 `StoppedAtCandidateEndpoint`로만 표시한다. `Delivered`, `전달완료`, 주문 상태 전이를 생성하지 않는다.
- 골목 점유가 확보되지 않으면 진입점에서 `WaitingForAlley`로 정차하고 우회나 순간이동을 생성하지 않는다.

## 재개 관문

이 표시 경로를 실행 권위로 승격하려면 차로 방향·회전·접근 제한, way `1256772587` 유효 폭·장애물·주차, 배송지 Entrance·C815류 CurbStop을 사람이 확인해야 한다. 그전까지 `TraversalReady=false`, `GameplayReady=false`, `distributionApproved=false`를 유지한다.
