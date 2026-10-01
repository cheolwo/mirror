# 코드 탐색 메타데이터

## 목적

`HongdalCodeMetadataAttribute`는 기능 하나가 계약, 화면, API, Application, 저장소와 외부 adapter를 어떻게 통과하는지 소스에서 바로 찾기 위한 코드 지도다. 기존 `HongdalApiVersion`, `HongdalApiWorkflow`, `HongdalUseCase`를 대체하지 않는다. 이들은 제품 버전과 업무 흐름을 설명하고, 코드 메타데이터는 개별 구현 타입의 책임과 부수효과를 설명한다.

## 필드

| 필드 | 의미 |
| --- | --- |
| `FeatureKey` | 같은 세로 기능 흐름을 묶는 안정적인 검색 키 |
| `Layer` | `Contract`, `Api`, `Application`, `Domain`, adapter, `ViewModel`, `View` 중 실제 책임 계층 |
| `Responsibility` | 해당 타입이 맡는 한 가지 주 책임 |
| `ContractType` | 구현이 따르는 주 interface 또는 ViewModel 타입 |
| `FlowOrder` | 사용자의 입력에서 외부 경계까지 탐색할 때의 대략적인 순서 |
| `Effects` | 네트워크, 외부 API, 영속화, object storage, UI 상태 변경, 비용 발생 가능성 |
| `Boundary` | 호출 전에 알아야 할 권한·비용·개인정보·증빙·상태 변경 경계 |

`Effects`는 직접 작성한 코드 한 줄만이 아니라 그 타입의 공개 동작이 하위 서비스에 위임해 발생시키는 효과까지 표시한다. 예를 들어 API Controller가 Application service에 생성을 위임하더라도 외부 비용 가능성을 숨기지 않는다.

## 적용 기준

- 새 기능은 여러 프로젝트를 통과해 찾아야 할 때만 하나의 `FeatureKey`를 만든다.
- 하나의 타입이 여러 기능을 실제로 조율하면 특성을 여러 개 붙일 수 있지만, 단순 참조만으로 기능 소유권을 표시하지 않는다.
- `Responsibility`에는 구현 방법을 나열하지 말고 타입이 소유한 결과를 한 문장으로 적는다.
- 외부 호출, 영속 상태 변경, 비용, 개인정보 전송, 법적 증빙 오인 가능성은 `Effects`와 `Boundary`에 명시한다.
- 순수 계산기는 `Effects = None`으로 두고 순수성 경계를 적는다.
- 기능을 분리하거나 이름을 바꾸면 특성, reader 검증 테스트와 관련 아키텍처 문서를 함께 갱신한다.

## 탐색 방법

기능 키를 한 번 검색하면 관련 타입과 흐름 순서를 바로 확인할 수 있다.

```powershell
rg -n "HongdalCodeFeatureKeys.CommunityAuthoringImage" Hongdal.Contracts Hongdal.Ui.Common HongdalAdminApp Hongdal
```

특성이 적용된 기능 전체를 찾을 때는 다음 명령을 쓴다.

```powershell
rg -n "HongdalCodeMetadata\(" -g "*.cs" -g "*.razor"
```

런타임 또는 테스트에서는 `HongdalCodeMetadataReader.ReadFeature`에 관련 assembly를 전달하면 `FlowOrder` 순서의 descriptor를 얻는다. 현재 기준 구현인 `community-authoring-image`는 `View -> ViewModel -> client port -> 관리자 HTTP adapter -> API -> 문맥 planner -> 생성 orchestration -> Kie.ai adapter`로 이어진다.

## 경계

이 특성은 권한 검사, 트랜잭션, validation 또는 보안 통제를 대신하지 않는다. 코드가 실제로 수행하는 효과와 특성이 다르면 코드를 기준으로 즉시 특성을 고치고 테스트로 차이를 드러낸다. secret이나 실제 개인정보는 메타데이터 문자열에 기록하지 않는다.

## 제품 모듈 특성

`HongdalModuleAttribute`는 여러 타입이 어떤 제품 버전과 릴리즈 단계에 속하는지를 묶는다. 코드 계층과 부수효과를 설명하는 `HongdalCodeMetadataAttribute`보다 상위 분류다. 커뮤니티 0.0에는 파생 특성인 `[HongdalCommunityV0Module]`을 사용해 다음 값을 공통으로 고정한다.

- `ProductVersion`: `0.0`
- `FeatureFlag`: `CommunityTrustWorkflow`
- `WorkflowKey`: `CommunityTrust`
- `DefaultEnabled`: `true`

각 적용 지점은 `ModuleKey`, `Kind`, `ReleaseStage`, `Responsibility`, `Boundary`를 추가로 기록한다. `HongdalModuleMetadataReader.ReadVersion`으로 관련 assembly를 조회하면 UI 조립부터 API, Application, 원장 영속화와 background 처리까지 모듈별로 확인할 수 있다. API 모듈은 기존 `[HongdalApiVersion(V0_0)]`도 함께 유지하며 테스트에서 두 메타데이터의 일치를 검사한다.
