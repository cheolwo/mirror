---
name: hongdal-synty-asset-workflow
description: "구매한 Synty 에셋을 기존 Unity 자산 대장에서 찾아 크기·피벗·재질·연결 지점을 확인하고 기존 게임 객체의 외형으로 적용하거나 점검할 때 사용한다. 팩별 실재 자산과 현재 소비 프로젝트를 대조한다. 일반 지도 자료 수집·카메라 입력·신규 게임 규칙은 각 작업의 절차를 사용한다."
---

# Synty 자산 검색과 외형 적용

보유한 자산을 현재 게임의 장소·행위자·소품에 맞게 고르고, 외형을 바꿔도 같은 선택·이동·업무 결과를 유지한다. 외부 스킬의 유용한 판단 방법을 프로젝트의 기존 대장·측정기·표현 연결에 맞춰 정리했다. 외부 프로그램의 설치나 구매 자산의 외부 전송을 전제하지 않는다.

## 현재 대상과 기존 대장

- [현재 작업](../../../docs/AI/CURRENT_WORK.md), 대상 checkout의 AGENTS와 허용 경로에서 실제 소비 프로젝트·판본·편집/실행 소유를 확인한다. 기본 Unity checkout과 현재 경영 게임의 별도 검토판을 같은 소스로 간주하지 않는다. 자료 확인 요청을 Scene 변경 요청으로 확대하지 않는다.
- [Unity 작업 영역](../../../eng/work-areas/unity.json)에서 Unity 소스를 찾는다. 아래 `Assets/` 경로는 그 Unity 프로젝트 루트 기준이다. 다른 PC에 경로가 없으면 현재 인계·공유 판본에서 해당 경로를 찾고 임의 새 대장을 만들지 않는다.
- 기존 기술 대장은 `Assets/Ssalddel/Presentation/World/Catalogs/SyntyPackAssetInventoryCatalog.asset`, 구조는 같은 상위 폴더의 `SyntyPackAssetInventoryCatalog.cs`다. 대장의 `Entries`를 `PackCode`, `NormalizedCategoryCode`, `AssetFamilyId`, `PrimaryUsageTrackCode`로 좁혀 후보를 찾는다. Editor가 없으면 직렬화된 대장과 관련 소스를 읽는다. 단순 검색 때문에 Editor를 열거나 대장을 재생성하지 않는다.
- 이 대장은 `synty-pack-inventory.v2`의 지정 5팩 범위이며 사용자가 보유한 모든 팩의 목록이 아니다. 실제 `Assets/Synty`와 대장 범위·소스 지문을 대조한다. 추가 팩·폴더를 구매나 사용권의 새 증거로 간주하지 않는다. 결과 없음과 미등재·미측정·판본 불일치를 구분한다.

## 목적에 맞는 자산 선택

1. 이번 대상 한 종류와 보존할 동작을 정한다. 예: 같은 매장 ID·출입구·선택 영역을 유지하며 간판과 외벽만 교체, 같은 기사 경로·시계·정산을 유지하며 외형만 교체.
2. [Synty 자산과 공간 관계](../../../docs/Architecture/Synty5팩자산-H1-H3연결지도.md)에서 장소·행위자·차량·소품의 역할을 찾은 뒤 실제 대장의 후보를 확인한다. 과거 문서의 수량·설치 상태를 오늘의 관측으로 쓰지 않는다.
3. 파일명만 보고 완성 건물·층 조각·그림·충돌 메시를 결정하지 않는다. 팩과 GUID/경로를 함께 확인해 다른 팩의 동명 자산을 구분한다. 대장의 `ClassificationStateCode`, `PresentationOnly`, `SourceFingerprintSha256`과 실제 Prefab 참조를 확인한다. 이 지문은 GUID와 Unity dependency hash를 결합한 값이므로 파일 SHA와 직접 같다고 비교하지 않는다. 검토 대기 후보를 자동 적용하지 않는다.
4. 선택한 소수 후보의 `LocalBounds`, 중심·피벗·축·단위·회전, Collider·Animator·FX·LOD와 재질 참조를 읽는다. 측정 근거가 없으면 미측정으로 남기고 추정 크기와 구분한다. 다른 팩의 그리드 간격·스케일을 일반 규칙으로 가져오지 않는다.

## 필요한 측정과 표현 연결

- 크기·진입 방향·접촉면이 중요한 조립이면 `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/SyntyCompositionSourceMeasurement.cs`의 대상 표본과 측정 범위를 먼저 확인한다. `MeasureAndWriteReport()`는 파일을 쓰며, 이 측정기는 전 자산을 다루지 않는다. 대장 재생성 `SyntyPackAssetInventoryBuilder.Build()`도 `SaveAssets/Refresh`를 수행하는 쓰기 작업이다. 현재 작업에 필요한 범위와 실행 소유가 있을 때만 사용한다.
- 애니메이션이 필요하면 같은 폴더의 `SyntyAnimationSourceInventory.cs`와 현재 Prefab·FBX importer·Avatar·clip/controller를 대조한다. `importAnimation`·clip 설정·참조 파일의 존재와 실제 import 성공을 구분하며 `rigImportErrors`·`animationImportErrors`와 Avatar 계층 불일치가 있으면 적용 가능으로 확정하지 않는다. 저장된 오류와 현재 재현 결과도 구분한다. 기존 경로가 이동을 소유하면 RootMotion을 중복 적용하지 않는다. [애니메이션 재사용 설계](../../../docs/Architecture/UnitySyntyAnimationReuseAndRetargetDesign.md)의 과거 설치 수량을 현재 상태로 고정하지 않는다. 팩별 정적 메시와 리깅된 캐릭터를 구분한다.
- [City 도입 기준](../../../docs/Architecture/UnitySyntyCityPackAdoptionChecklist.md)처럼 원본 Prefab은 보존하고 프로젝트 wrapper의 `VisualRoot`나 기존 표현 연결만 바꾼다. 재질은 실제 Unity 버전·렌더링 방식·shader 상태를 먼저 확인하며 원본 전체의 일괄 변환을 기본 단계로 삼지 않는다. 색 변형은 atlas/재질과 실제 렌더를 대조한다.
- 사가정·법정동 표현은 `Assets/Ssalddel/Editor/사가정Synty표현CatalogBuilder.cs`, `Assets/Ssalddel/Presentation/World/사가정Synty표현Planner.cs`, `법정동Synty후단연결기.cs`와 `Assets/Ssalddel/Resources/SagajeongSyntyVisualCatalog.asset`의 현재 호출을 확인한다. 계획과 `VisualKey`가 먼저이고 Prefab은 표현 후보다. `VisualKeyMissing`, `PrefabMissing`, `LandCoverRejected`, `RegionRoleRejected`, `SlopeRejected`를 임의 대체 자산으로 성공 처리하지 않는다.
- 현재 2D 게임이면 승인된 그림·지도·입력 방식을 유지한다. 이 절차 때문에 3D Scene·새 카메라·DOTS 구조로 전환하지 않는다. Prefab 이름·위치·애니메이션 사건을 주문·수령·배차·보수의 권위로 쓰지 않는다. [배치 책임 분리](../../../docs/Architecture/지도구성과세계자산배치분리.md)를 따른다.

## 검증과 반환

- 후보 비교만 요청받았다면 후보·선정 이유·실재/미측정·현재 미검증을 반환한다. 코드나 Scene을 억지로 바꾸지 않는다.
- 외형 적용 작업이면 같은 합성 ID·입력·카메라에서 변경 전후를 확인한다. 바닥/벽 접촉·부유·겹침·방향·축척·재질과 가독성, Collider·출입·선택·이동·원래 결과를 함께 대조한다. 기존 생성기를 사용하는 경우 재실행 때 객체·재질·구독이 중복되지 않는지 확인한다.
- 현재 소비 프로젝트의 관련 시험과 빌드만 수행한다. 기본 Unity 대장·표현 연결을 바꿨을 때는 `Assets/Ssalddel/Tests/EditMode/`의 `SyntyPackAssetInventoryTests.cs`, `사가정Synty표현Tests.cs`, `법정동Synty후단연결Tests.cs` 중 영향 시험을 고른다. 측정·애니메이션은 해당 연구 폴더의 기존 시험을 확인한다. 파일 존재를 시험 통과로 쓰지 않는다.
- 화면 검증이 필요한 변경은 대상 AGENTS의 현재 Pipeline/CLI 순서와 같은 조건의 Game View·실제 입력을 따른다. 정적 대장 조회·Scene 구조·자동시험·실제 화면·운영 사용은 별도 결과로 남긴다.
- 결과는 대상 판본 → 목적/후보와 근거 → 적용 범위 → 보존된 동작 → 실제 검증 → 남은 한 가지로 기록한다. 후보 결과에는 팩·자산 참조·대장/소스 지문·측정 유무·선정/보류 이유를 현재 내부 기록에 연결하고 유료 원본·미리보기·전체 자산 목록을 공개 산출물에 포함하지 않는다.

## 외부 자료를 다시 사용할 때

[선별한 커뮤니티 자료](references/community-sources.md)는 출처·채택한 부분·도입하지 않은 부분을 확인할 때 읽는다. `synty-inventory`의 CLI나 AI 영상 분석 명령은 이번 스킬의 의존성이 아니다. 기존 대장에서 해결할 수 없는 구체적인 필요가 생겼을 때 도구 판본·범위·현재 검증부터 대조한다. 일반 지도 연결은 [지도 자료 연결](../hongdal-unity-map-data-binding/SKILL.md), UI 위 선택·줌 충돌은 [2D 객체 조작](../hongdal-unity-2d-interaction/SKILL.md)로 필요한 부분만 이어간다.
