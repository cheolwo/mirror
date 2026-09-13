# [기획 · 월드·공간·배치 · PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY · r1]

상태: `Approved / LogicE3Verified / PresentationE4Verified / SyntheticFixturePlayModeGameViewObserved / SceneBindingDeferred / AuthorityPromotionBlocked`

## 목표

사가정역 1km 디오라마에서 가상 음식 주문 한 건의 `음식점 이동 → 도보 픽업 → 배송지 이동 → 도보 전달 → 복귀`를 도로와 건물 접근점에 맞춰 관찰할 수 있게 한다. 기존 디오라마 도형은 보존하고 지역 Experience Package에 독립 `Mobility` 계층을 추가한다.

주소 문자열을 경로로 직접 사용하지 않는다. 서버와 Simulation은 `주소 근거 → 건물 고유 식별자 → Entrance → CurbStop → 이동망 노드` 결속을 읽고 판본화된 경로와 진행 상태 사본을 만든다. Unity는 이를 보간·표현할 뿐 주문·픽업·전달 완료를 확정하지 않는다.

## 확정

- 첫 주문은 실제 운영 주문이 아니라 `SyntheticFixture` 가상 주문이다. 실제 상호·거주자·연락처·상세 주소를 게임 표시에 사용하지 않는다.
- 전체 보기에서는 일정한 화면 크기의 기사·오토바이 표식과 선택 경로를, 확대 보기에서는 차량·하차·도보·픽업·전달을 표현한다.
- 지역 규모 길찾기는 검토된 명시 간선의 결정적 경로 계산이 소유한다. Unity NavMesh는 향후 근거리 회피 보조일 뿐 경로 권위가 아니다.
- 기존 OSM 디오라마 도로는 표시 근거다. 원본 node ID, `oneway`, `access`, `crossing`, `entrance`를 보존한 별도 500m 이동망 타일을 만들며 임의 연결이나 직선 fallback을 만들지 않는다.
- 이동 수단의 기존 값 `Vehicle=0`, `Pedestrian=1`을 보존하고 `Motorcycle=2`를 뒤에 추가한다.
- 경로·타일·출입구·hash가 없거나 불일치하면 마지막 확인 위치에서 `RouteUnresolved`로 멈춘다.
- 1차는 같은 가상 주문 상태를 읽는 표현 사본이며 도착이 주문 상태를 바꾸지 않는다. 실제 사가정 이동망과 접근점 검증 뒤 별도 권위 WI에서만 도착을 Simulation 상태 전이 조건으로 승격한다.
- 기능은 기본 비활성이고 사가정 패키지는 계속 `PrivatePreview / DistributionApproved=false`다.

## 지역 계층

```text
RegionExperienceRoot
├─ Geography
├─ Mobility
│  ├─ MobilityGraphTiles
│  ├─ BuildingAccessAnchors
│  ├─ ActiveRouteVisuals
│  └─ CourierActors
├─ PlacesAndBusinesses
├─ OperationalSnapshot
└─ Gameplay
```

정적 이동망은 `region-mobility-graph-manifest.v1`과 `region-mobility-graph-tile.v1`, 동적 가상 배달은 `food-delivery-journey-snapshot.v1`을 사용한다. 정적 자료는 원본·그래프·디오라마 투영 hash를 함께 보존하고, 동적 사본은 경로 ID·그래프 판본·경로 지문·구간·진행 거리·차단·revision·기사/차량 좌표를 보존한다.

정적 후보의 범위는 행정동 하나로 오인하지 않는 `world-region:kr:seoul:jungnang:sagajeong.r1` 1km 창이다. 행정동 고유 식별자는 분석 참조로 남기며 이동망을 행정동별로 복제하지 않는다.

## r1 구현 결과

- OSM 동결 원본에서 기존 디오라마와 같은 `WorldOffset=(550,8)`을 적용해 500m 4타일, 노드 2,807개, 간선 2,574개를 결정적으로 생성했다. projection hash는 `59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7`이다.
- 방향 미확정 간선 2,430개와 외곽 portal 115개를 포함하므로 전부 검토 후보이며 실행 간선이 아니다. `DistributionApproved`, `TraversalReady`, `RuntimeAuthorized`는 모두 `false`다.
- 음식점 후보 A는 OSM 출입구가 없고 도로까지 1.437m 검토 후보만 있다. 후보 B는 출입구 node `11683813177`이 있으나 도로까지 1.404m 연결도 사람 검토 대기이며 생성 간선 수는 0개다.
- 서버에는 개발 환경·서버관리자 전용 manifest/tile GET, ETag, 원본/내용/파일 hash·좌표 프레임·타일 stitch 검증을 추가했다. 기능 플래그는 기본 비활성이다.
- Simulation은 기존 합성 이동망에서 7구간 Motorcycle/Pedestrian 여정 사본을 만들고 Unity는 polyline 진행률, 전체 보기 표식, 확대 보기 기사·스쿠터, 선택 경로를 읽기 전용으로 해석한다. 도착은 주문 상태를 바꾸지 않는다.
- Unity `6000.5.6f1`의 canonical Scene Play Mode에서 저장하지 않는 임시 검증 Root로 합성 7구간·116m 경로를 자동 진행시켰다. 시간 경과에 따른 사본·revision·Transform 변화를 확인하고 전체 보기·Motorcycle 확대·Pedestrian 인계 확대 Game View 3개를 남겼으며 저장 Scene은 변경하지 않았다. 세 사진은 연속 프레임이 아닌 고정 판독 지점이다. 이는 합성 상태 사본의 위치 갱신과 거리별 표현에 대한 화면 증거이며, 실제 OSM 길찾기·운영 주문 권위·Scene 영속 결속의 증거는 아니다.

## 첫 폐루프

`주문 대기 → 음식점까지 Motorcycle → CurbStop 정차 → Entrance까지 Pedestrian → 대기·픽업 → 차량 복귀 → 배송지 CurbStop → Entrance까지 Pedestrian → 전달·수령 표시 → 차량 복귀 → 대기 위치 귀환`

현재 가상 음식점·가상 배송지가 결속된 두 OSM 건물을 후보로 삼되, 공개 이름은 가상 명칭으로 유지한다. 실제 출입구 또는 정차점이 검토되지 않으면 건물 중심점으로 대체하지 않는다.

## 제외

실제 주문·실시간 GPS·개인 주소 공개, 교통량·물리 충돌·동적 우회, 전 건물 출입구 완결, NavMesh 권위, 결제·광고 효과, Steam 공개, 새 Scene은 r1 범위가 아니다.

## 구현 명세와 검증

[E1~E7 수직 작업 명세](implementation.md)를 따른다. 현재 행정동 지리 관문과 기존 디오라마의 `TraversalReady=false`는 이동망 후보만으로 자동 승격하지 않는다.
