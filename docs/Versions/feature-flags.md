# Feature Flag Policy

기능 플래그는 구현된 코드와 실제 운영 노출을 분리하기 위한 상위 스위치입니다. 버전별 기능이 같은 코드베이스에 존재하더라도, 현재 운영 버전 범위 밖 기능은 기본적으로 꺼져 있어야 합니다.

## 원칙

| 원칙 | 설명 |
| --- | --- |
| 기본값 보수주의 | 현재 안정화 대상이 아닌 버전의 플래그는 `false`로 둡니다. |
| UI와 서버를 함께 제어 | 화면 노출만 끄는 것이 아니라 서버 처리 흐름도 같은 플래그를 참조할 수 있어야 합니다. |
| 필수 기능과 보조 기능 분리 | 상차/하차 증빙처럼 업무 필수 기능은 보조 기능 플래그로 끄지 않습니다. 관계 스냅샷, 알림, 공동 주문 모집 같은 보조/확장 기능은 플래그로 제어합니다. |
| 사용자별 설정과 구분 | 버전 플래그는 플랫폼 운영 범위의 상위 스위치입니다. 사용자별 on/off는 `AuxiliaryFeatureSettings`나 View 가시성 정책에서 별도로 처리합니다. |
| DB 마이그레이션과 별개 | 테이블이 존재해도 플래그가 꺼져 있으면 기본 UI와 업무 흐름에 노출하지 않습니다. |
| 설정 화면의 버전 그룹화 | 관리자/사용자 설정 화면은 기능을 `0.0`, `1.0`, `1.5`, `2.0`, `2.5`, `3.0`, `3.5` 그룹으로 보여줍니다. 현재 릴리즈 대상인 커뮤니티 `0.0`과 실행 모듈을 한 화면에서 섞어 판단하지 않습니다. |

## 권장 플래그

| 키 | 버전 | 기본값 | 목적 |
| --- | --- | --- | --- |
| `CommunityTrustWorkflow` | `0.0` | `true` | 커뮤니티, 공동 원장, 참여 동의와 신뢰 기록 기반 |
| `CargoYongdalV1` | `1.0` | `false` | 국내 화물/용달 운송 핵심 흐름 |
| `WarehouseV15` | `1.5` | `false` | 입고, 적재, 출고, 재위탁 창고 흐름 |
| `CustomsHsV20` | `2.0` | `false` | HS 코드, 통관, 관세사 보정 |
| `OrdererGroupOrderV25` | `2.5` | `false` | 주문자 집단 공동 주문, 해외 선적/통관 조회, 국내 물류대행 입고, 판매채널 출품, 집단 내 분류/배분, 입주민 우선 고용 |
| `FoodDeliveryV30` | `3.0` | `false` | 음식점 일반 음식 배달, 조리/픽업, 고객 배송 |
| `HongdalMartV35` | `3.5` | `false` | 알뜰살뜰 마트, 도심 즉시배송, 피킹/포장 후 음식 배달 기사 픽업 |

## 설정 예시

```json
{
  "VersionFeatureFlags": {
    "CommunityTrustWorkflow": true,
    "CargoYongdalV1": false,
    "WarehouseV15": false,
    "CustomsHsV20": false,
    "OrdererGroupOrderV25": false,
    "FoodDeliveryV30": false,
    "HongdalMartV35": false
  }
}
```

## 서버 API 차단

서버는 버전 범위 밖 기능을 문서나 화면에서만 숨기지 않고, 컨트롤러 진입점에서도 차단합니다. `RequireVersionFeatureAttribute`는 `VersionFeatureFlags` 설정을 조회해 플래그가 꺼진 API에 `404 Not Found`와 `FeatureDisabled` 오류 코드를 반환합니다.

현재 `OrdererGroupOrderV25`가 꺼져 있으면 주문자 집단 공동 주문, 해외 선적/통관 추적, 커머스 출품/출고 연계, 주문자 집단 운영 주체, 4대보험 신고 준비 API는 기본 운영 흐름에서 호출할 수 없습니다. 주소 검색처럼 0.0 커뮤니티에서도 재사용 가능한 기반 조회는 별도 기능으로 남겨두고, 공동주택/주문자 집단 후보와 비용/수익 시뮬레이션 조회만 2.5 플래그로 제한합니다.

`CommunityTrustWorkflow`는 국내 화물/용달과 독립적으로 켤 수 있습니다. 반대로 국내 화물/용달 1.0은 커뮤니티 기반이 꺼진 상태에서 열리지 않습니다.

## API 제품 버전 메타데이터

모든 서버 컨트롤러는 `HongdalApiVersionAttribute`로 제품 로드맵 버전을 기록합니다. 이 버전은 `/api/v1/...`의 HTTP 계약 버전과 다르며, 홍달 기능 로드맵의 `0.0`, `1.0`, `1.5`, `2.0`, `2.5`, `3.0`, `3.5`를 뜻합니다.

버전 값은 문자열을 직접 쓰지 않고 `HongdalProductVersion` enum으로 관리합니다. 사람이 읽는 `1.0`, `2.5` 같은 라벨은 `HongdalProductVersionLabels`에서만 변환합니다.

```csharp
[HongdalApiVersion(HongdalProductVersion.V2_5, FeatureKey = VersionFeatureFlagKeys.OrdererGroupOrderV25)]
[RequireVersionFeature(VersionFeatureFlagKeys.OrdererGroupOrderV25)]
public sealed class 공동구매해외선적추적Controller : ControllerBase
{
}
```

한 컨트롤러 안에 여러 제품 버전 API가 섞이는 경우에는 컨트롤러에 기본 버전을 붙이고 action에 더 구체적인 버전을 붙입니다. 예를 들어 주소 검색은 1.0 기반 조회로 남기고, 공동주택/주문자 집단 조회 action은 2.5로 표시합니다.

테스트는 모든 컨트롤러에 제품 버전 메타데이터가 있는지, `RequireVersionFeatureAttribute`로 막는 API가 같은 feature key를 `HongdalApiVersionAttribute`에도 기록하는지 확인합니다.

## API 성장 트랙 메타데이터

제품 버전은 특정 API가 어느 릴리즈 범위에서 안정화되는지를 나타냅니다. 반면 커뮤니티처럼 여러 버전에 걸쳐 계속 자라야 하는 기능은 `HongdalApiGrowthTrackAttribute`로 별도 성장 트랙을 기록합니다.

```csharp
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
public sealed class 커뮤니티게시글Controller : ControllerBase
{
}
```

커뮤니티 트랙은 0.0에서 후기, 문의, 감사, 인연 연결, 관계 스냅샷, 개인정보 보호 활동 신호, 투표와 결의문 초안 같은 기본 신뢰 기록으로 시작합니다. 이후 1.0 운송, 2.5 주문자 집단 공동 주문, 3.0 음식점·배달, 3.5 알뜰살뜰 마트가 같은 커뮤니티 기반으로 합류합니다.

## 클라이언트 샘플 데이터 정책

DriverApp과 HongdalApp은 `ClientDataMode` 옵션으로 서버 데이터 호출 실패 시 샘플 데이터를 사용할지 결정합니다.

```json
{
  "ClientDataMode": {
    "AllowSampleFallback": false,
    "AllowDevelopmentSnapshotFallback": false
  }
}
```

`AllowSampleFallback=false`이면 앱은 서버 인증 또는 서버 API 실패를 샘플 데이터로 조용히 덮지 않습니다. 읽기 화면은 빈 상태를 보여주고, 운송 의뢰 등록 같은 쓰기 요청은 실패를 드러내도록 처리합니다. `DEBUG` 빌드에서 설정이 없을 때만 개발 편의를 위해 샘플 fallback과 개발 스냅샷 fallback을 기본 허용합니다.

## 기존 설정과의 관계

| 기존 장치 | 역할 | 버전 플래그와의 관계 |
| --- | --- | --- |
| `View가시성Service` | 앱/역할/사용자별 화면 노출 제어 | 버전 플래그가 꺼진 기능은 View 가시성보다 먼저 숨기는 것이 원칙입니다. |
| `CommandProcessingOptions` | Command 후처리, 감사로그, 알림, 관계 스냅샷 제어 | 버전 플래그가 켜진 기능 안에서 세부 후처리를 조정합니다. |
| `AuxiliaryFeatureSettings` | 보조 기능의 전체/사용자별 on/off | 버전 플래그가 켜진 뒤 사용자별 선택권을 줄 때 사용합니다. 항목은 버전 그룹 메타데이터를 포함해야 합니다. |

## 설정 화면 그룹 기준

| 버전 그룹 | 설정 화면에서의 의미 |
| --- | --- |
| `0.0` | 현재 릴리즈 대상입니다. 커뮤니티, 공동 원장, 동의, 신고, 알림과 신뢰 기록 설정을 둡니다. |
| `1.0` | 국내 화물/용달 실행 모듈입니다. 0.0 게이트를 통과한 환경에서만 엽니다. |
| `1.5` 이후 | 구현 또는 실험 코드가 있어도 기본적으로 확장 후보입니다. 관리자 화면에서는 별도 버전 그룹으로 접어두고, 현재 1.0 운영 판단과 섞지 않습니다. |

새로운 부가 기능을 추가할 때는 기능명, 대상 command/service/event, 필수 여부, 사용자 설정 가능 여부와 함께 버전 그룹을 같이 지정합니다.

## 운영 예시

1. `0.0` 안정화 중에는 `CommunityTrustWorkflow=true`, `CargoYongdalV1=false`, 나머지 실행 모듈 플래그도 `false`로 둡니다.
2. `1.0` 운송 검증을 시작할 때만 커뮤니티 기반을 유지한 채 `CargoYongdalV1=true`로 바꿉니다.
3. `1.5` 창고 기능을 내부 테스트할 때 `WarehouseV15=true`로 바꾸되, 운영 사용자 View 가시성은 제한합니다.
4. `2.5` 주문자 집단 공동 주문, 해외 선적/통관 조회, 물류대행 입고, 판매채널 출품, 집단 내 고용 흐름은 API/DB가 먼저 들어와도 `OrdererGroupOrderV25=false`이면 기본 화면에 노출하지 않습니다.
5. `3.0` 음식점 일반 배달은 `FoodDeliveryV30=true`가 되기 전까지 음식 배달 기사 배차 흐름에 실운영 주문을 흘려보내지 않습니다.
6. `3.5` 알뜰살뜰 마트는 `HongdalMartV35=true`가 되기 전까지 도심 즉시배송 주문을 피킹/포장/배차 실운영 흐름에 흘려보내지 않습니다.

기존 `ApartmentGroupOrderV25` 설정 키는 공동주택 중심 명칭이므로 새 문서와 운영 화면에서는 사용하지 않습니다. 다만 기존 로컬 설정 호환을 위해 서버는 해당 키가 `true`인 경우에도 `OrdererGroupOrderV25`가 켜진 것으로 해석합니다.
