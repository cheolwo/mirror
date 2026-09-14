[기획 · 월드·공간·배치 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r24]

# 중랑구 전통시장 시각·모델링 자료 첫 수집

- 상태: `CollectedPrivateReview / IdentityPending / RightsPending / BlenderBlocked / UnityBlocked`
- 상위 기획: [역세권 디오라마 모듈 표준](README.md)
- 기계 판독 대장: [중랑구 전통시장 시각 자료 r1](../../../../../eng/world-seedbeds/station-landmarks/jungnang-traditional-market-visual.collection.r1.json)
- 기존 사가정시장 명세: [사가정시장 입구·대표 골목 수집 명세 r6](sagajeong-market-landmark-collection.r6.md)

## 목표와 실제 완료 범위

중랑구의 우림시장·동부시장·면목시장·동원시장·사가정시장을 역세권 디오라마의 랜드마크 후보로 발전시키기 위해, 시장별 시각 참고와 현행 공공 행을 같은 검토 패킷에 넣되 서로 다른 증거로 보존했다.

이번 판본에서 완료한 것은 비공개 자료 수집·시장별 화면 조각 생성·현재 공식 행 후보 연결·로컬 원장 저장·재조회다. Blender 모델, Unity 자원, Scene 배치, 현재 시장 정본 확정은 포함하지 않는다.

## 수집 결과

```text
중랑구 전통시장 시각 자료 r1
├─ 공식 시각 원본
│  └─ 중랑구소식지 2016년 9월호 PDF
├─ 시장별 비공개 검토 이미지 5장
│  ├─ 우림시장: 입구 아치·가로 접속
│  ├─ 동부시장: 도로변 다층 입구·수직 간판
│  ├─ 면목시장: 골목 입구·연속 아케이드 내부
│  ├─ 동원시장: 면목역 인접 가로 전면·혼합형 시장
│  └─ 사가정시장: 개방 골목 입구·저층 상가 벽
├─ 2025-11-10 현재 전통시장 공식 행 후보 7개
│  ├─ 단일 후보: 우림·동부·사가정
│  ├─ 면목 이름군: 면목골목시장·면목시장
│  └─ 동원 이름군: 동원시장·동원전통종합시장
└─ 권리·공간 관문
   ├─ 개별 사진 이용권 미확인
   ├─ 입구 좌표·시장 중심선·폭·차양 높이 미확인
   └─ Blender·Unity·배포 불허
```

비공개 원본과 화면 조각은 `artifacts/local/public-data/jungnang-traditional-market-visuals-20260914-r1/`에 있다. Git·Unity Resources·StreamingAssets·빌드에는 복사하지 않는다.

## 시장별 판정

| 2016년 소식지 표기 | 2025-11-10 공식 행 후보 | 현재 해석 | 모델링에 보충할 근거 |
| --- | --- | --- | --- |
| 우림시장 | 우림골목시장 | 주소가 같고 별칭 관계가 유력하지만 정본은 사람 검토 전 후보 | 전체 출입구, 중심선·폭, 차양 높이, 측후면, 현행 사진 |
| 동부시장 | 중랑동부시장(동부골목시장) | 이름 관계는 보이나 주소가 달라 변경 이력 확인 필요 | 주소 변경 이력, 내부 아케이드, 중심선·폭, 현행 사진 |
| 면목시장 | 면목골목시장, 면목시장 | 소식지 이름은 한 행, 주소는 다른 행과 맞아 자동 병합 금지 | 두 시장의 정체성, 출입구, 중심선·폭, 차양 높이 |
| 동원시장 | 동원시장, 동원전통종합시장 | 현재 행이 둘이고 소식지 주소도 달라 자동 병합 금지 | 정본·입구·아케이드 구간·중심선·폭·현행 사진 |
| 사가정시장 | 사가정시장 | 이름과 주소가 맞지만 현재 영업·외곽·입구 정본은 별도 확인 | 전체 출입구, 중심선·폭, 대표 골목 단면, 차양 높이 |

사진 한 장으로 실제 시장 전체를 복원하지 않는다. 지금 추출한 것은 입구 유형과 공간 문법을 비교하기 위한 참고이며, 치수와 현재 상태는 후속 자료가 소유한다.

## 권리 판정

소식지는 중랑구가 발행한 공식 자료지만, 이번 PDF와 개별 사진에 상업 이용·변형을 허용하는 항목별 공공누리 표기를 확인하지 못했다. 따라서 원본·시장별 화면 조각은 `PrivateReviewOnly`, `ItemLevelRightsUnverified`다.

이 상태에서는 사진을 텍스처로 쓰거나 사진에 의존해 배포용 Blender 형상을 만들지 않는다. 가장 빠른 재개 경로는 시장별로 직접 촬영하고 모델 파생·상업 배포에 사용할 수 있다는 동의와 촬영 방향·시각을 함께 남기는 것이다. 서면 이용 허락을 받는 경로도 가능하다.

## 원장과 재현 명령

수집기는 공식 PDF hash, 고정 crop, 시장 원본 hash와 권리 차단을 검사한다. 로컬 Docker MySQL `hongdal_dev`에는 시각 참고 5건, 정체성 검토 5건, 권리 경계 1건을 저장했다. 첫 적용은 신규 11건, 두 번째 적용은 신규 0·기존 11건, 독립 재조회는 11건이었다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-verify C:/Users/user/source/repos/Hongdal
```

## 디오라마 증거 체계 판정

- 기존 `원천·Simulation·Unity 표현 분리` 규칙을 지지한다. 시장 사진이 있어도 상태 사본이나 Unity 표현 권위가 되지 않는다.
- 기존 `외관 근거가 준비될 때까지 절차적 매스 유지` 규칙을 지지한다. 권리와 형상이 미완성인 시장 자산 때문에 현재 건물 매스를 제거하지 않는다.
- 새 `Candidate`로 **옛 랜드마크 명칭·주소와 현행 공식 행을 판본 후보로 연결하되 자동 병합하지 않는다**를 기록한다. 면목·동원·동부의 반례가 이 규칙의 필요성을 보여준다.
- 후보 기록은 공통 적용, Blender 시작, Unity 배치, E 단계 승격을 승인하지 않는다.

## 확정

- 다섯 시장의 공식 소식지 시각 참고와 당시 설명·주소를 비공개로 확보했다.
- 시장별 화면 조각 5장과 해시를 보존했다.
- 현행 공식 행 후보 7개를 이름·주소 충돌을 지우지 않은 채 연결했다.
- 원본과 정규화 11건의 로컬 저장·멱등 재적용·독립 재조회를 확인했다.

## 미정

- 개별 사진의 변형·상업 이용 허락
- 면목·동원·동부의 과거/현행 시장 정체성
- 모든 시장의 현행 출입구·외곽·중심선·골목 폭·차양 높이
- Blender 조립 단위의 실제 치수와 배포 승인

## 다음 질문 하나

첫 후속 현장 자료 패킷을 **사가정시장부터 직접 촬영 허용 자료로 채우고**, 같은 촬영표를 나머지 네 시장에 반복 적용할까?

추천은 그렇다. 사가정역 디오라마와 바로 비교할 수 있고, 이 한 시장에서 입구 정면·접근 연속·골목 축·골목 단면·분기·지붕선의 여섯 방향을 검증하면 다른 시장에 적용할 촬영 표준도 함께 다듬을 수 있다.
