[기획 · 시스템·월드 투영 · PLAN-SYSTEM-REGION-EXPERIENCE-PACKAGES · r2]

# Steam 무료 본편의 지역 Experience Package

상태: `Approved / MobilityLayerImplemented / SagajeongPrivatePreviewRegistered / UnityStreamingDeferred`

## 목적

Mirror의 무료 Steam 본편은 지역마다 별도 게임을 복제하지 않고, 하나의 클라이언트에서 판본화된 지역 Experience Package를 발견·선택·갱신한다. 패키지는 기존의 공간·생활·시나리오·게임·상업 표시·운영 상태 API를 합치지 않고, 한 지역을 여는 데 필요한 레이어와 로딩 정책을 상위 manifest로 묶는다.

첫 패키지는 `world-region:kr:seoul:jungnang:sagajeong.r1`이며 면목제3·8동 행정동 디오라마와 면목동 법정동 공간 패키지를 연결한다.

```text
Steam 무료 본편
  → 지역 Catalog
  → 지역 Experience Manifest
  → 행정동 지리 / 지역 이동망 / 생활 맥락 / 복구 시나리오 / gameplay / 상업 표시 / 운영 상태
```

## 확정

- `world-region:*`은 출시·탐색 단위이고 행정동·법정동 식별자를 대체하지 않는다. 한 지역 패키지는 여러 행정·법정동을 참조할 수 있다.
- 기존 `neighborhood-package-registry.v2`는 공간 원본·좌표계·법정동 패키지의 정본으로 유지한다. Experience Package는 이를 참조하며 복제하거나 배치 권위를 가져가지 않는다.
- Catalog와 Manifest는 읽기 전용이다. 세부 API의 권한·기능 플래그·schema·상태 권위는 그대로 유지한다.
- 레이어 상태는 `Ready / Disabled / Planned`로 구분한다. 비활성 또는 미구현 레이어는 endpoint를 내보내지 않는다.
- 지리는 hash별 불변 캐시, 표시·운영 상태는 ETag 재검증으로 읽는다. 운영 상태 실패가 지리 패키지 실행을 sample 자료로 대체하지 않는다.
- 이동망은 행정동 경계의 대체물이 아니라 지역 1km 창의 독립 계층이다. 참조 행정동은 분석 축으로만 보존하고 도로를 행정동마다 복제하지 않는다.
- 무료 본편 여부와 지역 패키지 게시 승인을 분리한다. 사가정은 `PrivatePreview`, `DistributionApproved=false`, `GameplayReady=false`다.
- 광고·후원과 실제 서비스는 선택 레이어다. 패키지 등록만으로 상호 공개, 광고 활성화, 주문·배차·결제 또는 외부 서비스를 열지 않는다.
- Unity 프레임 안정화는 Manifest 존재만으로 성립하지 않는다. 다음 절편에서 bootstrap/on-demand/live-refresh 큐, hash 캐시, 타일 예산과 실제 Game View 성능을 별도로 검증한다.

## 첫 구현

- 공유 계약에 지역 Catalog·Manifest·레이어 참조·endpoint template·로딩/캐시/준비 상태를 추가했다.
- `GET api/v1/world/regions`와 `GET api/v1/world/regions/{regionStableId}/experience-manifest`를 추가하고 Catalog/Manifest hash를 ETag로 제공한다.
- 사가정 패키지는 기존 행정동 manifest/tile, 별도 `region-mobility-graph-manifest.v1`/tile, display overlay, 운영 장면 v2를 참조한다. 생활 맥락·초기 복구 정착기·gameplay는 endpoint 없는 `Planned`로 남긴다.
- 이동망은 `OnDemand / ImmutableByHash`이며 `RegionMobilityObservation`과 행정동 디오라마 관찰 기능이 함께 켜져야 endpoint를 노출한다. 기본 설정은 비활성이다.
- 검토 중 이동망 HTTP는 개발 환경의 서버관리자에게만 제공하고 `DistributionApproved=false`, `TraversalReady=false`, `RuntimeAuthorized=false`를 강제한다.
- 기능 플래그 `RegionExperiencePackages`는 기본 비활성이다. 기존 행정동·운영 관찰 플래그가 켜진 경우에만 해당 레이어가 `Ready`와 endpoint를 노출한다.
- 이번 r2 절편은 RDB·MongoDB·Unity Scene·Steamworks·다운로드 CDN을 변경하지 않는다. 합성 주문의 읽기 전용 이동 사본과 Unity 메모리 표현 계층은 [사가정 가상 배달 이동 r1](../../공간/PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY/README.md)이 소유한다.

## 후속 순서

1. Unity 공용 Package Client·Mapper·hash 캐시와 필수 지리 bootstrap을 구현한다.
2. 검토 승인된 이동 간선만 별도 권위 WI에 결속하고 500m 타일을 카메라 주변 예산에 따라 조립·해제한다.
3. 사가정 익명 생활밀도와 초기 복구 정착기 투영을 독립 레이어로 게시한다.
4. 실제 Game View에서 최초 진입·지역 전환·오프라인 캐시·갱신 실패·메모리 회수를 검증한다.
5. 검증된 Claim·후원과 실제 서비스는 별도 운영 승인 뒤 선택 레이어로 연다.

## 검증 상한

공유 계약·서버 조립·기능 플래그·ETag·로컬 동결 파일의 hash 검증과 읽기 전용 Unity 메모리 계층까지만 구현 증거다. 저장 Scene 결속, 지역 다운로드 파일, Steam 배포, Unity 스트리밍, 프레임 개선, 실제 광고·서비스와 공개 출시는 아직 구현·검증되지 않았다.

## 다음 질문 하나

없음. 다음 구현은 새 지역 추가가 아니라 사가정 후보 간선·Entrance/CurbStop의 사람 검토와 Unity bootstrap/on-demand 캐시 절편이다.
