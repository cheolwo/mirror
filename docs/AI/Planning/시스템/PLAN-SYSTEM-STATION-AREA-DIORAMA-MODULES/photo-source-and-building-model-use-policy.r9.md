[기획 · 월드·공간·배치 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r9]

# 무료 Steam 디오라마의 외관 사진·건물 모델 이용 제안

- 상태: `ProposalPrepared / ApprovalPending / FreeReleaseIntentRecorded / CommercialCompatibleAssetBaselineRecommended`
- 상위 기획: [역세권 디오라마 모듈 표준 r9](README.md)
- 선행 제안: [실제 외관 근거 기반 건물 표현 교체 r8](building-visual-replacement-proposal.r8.md)
- 기획 근거: 2026-09-13 사용자가 현재 게임을 유료 판매할 의도가 없고, 확보 가능한 외관 사진으로 Blender 건물을 만들어 Unity 디오라마에 배치하고 싶다는 방향과 직접 사진을 사용하는 것이 가능한지 질문함.

## 결론

무료로 배포할 예정이어도 사진을 발견했다는 이유만으로 게임에 넣을 수 있는 것은 아니다. 하지만 다음 자료를 쓰면 현실적인 제작이 가능하다.

1. **직접 촬영한 외관 사진**
2. 촬영자·건물 관계자가 게임용 모델 제작과 배포를 명시적으로 허락한 사진
3. 개별 자료에 상업적 이용과 변경 허용이 표시된 공공누리 제1유형·CC0·CC BY 계열 자료

현재 무료 출시 의도와 별개로, 첫 배포 자원은 가능한 한 `CommercialCompatible=true`까지 만족시키는 것을 추천한다. 추후 지역 광고·후원·기부·유료 부가 콘텐츠가 붙더라도 기존 건물 자원을 모두 교체하지 않아도 되기 때문이다.

이 문서는 법률 의견을 확정하지 않고, 프로젝트가 불필요한 위험을 피하도록 더 보수적인 제작·배포 관문을 제안한다.

## “사진을 쓴다”의 네 단계

| 사용 단계 | 예 | 기본 판정 |
| --- | --- | --- |
| 화면 비교 | 거리뷰·웹 사진을 보며 창문 수나 촬영 방향만 확인 | 로컬 참고 가능, 원본 저장·배포 금지 |
| 모델링 참고 | 사진을 보고 비례·차양·재질을 재해석 | 사진의 변경·2차 이용 조건 확인 필요 |
| 직접 자원 사용 | 사진을 벽 텍스처·간판·UI 카드에 포함 | 사진 파일의 복제·배포 허락 필수 |
| 홍보 사용 | Steam 상점 캡처·트레일러·광고 소재에 노출 | 게임 자원과 별도로 홍보 이용 범위까지 확인 |

따라서 외관 사진을 **직접 텍스처로 넣는 것**보다, 승인된 사진 여러 장에서 치수와 반복 특징만 읽어 저폴리 geometry와 새 재질로 재해석하는 방식을 기본값으로 둔다.

## 직접 촬영한 사진은 어떻게 쓸 수 있나

직접 찍은 사진의 사진저작권은 원칙적으로 촬영자 본인에게 있으므로, 타인이 찍은 사진보다 훨씬 안전한 출발점이다. 다만 사진 속 모든 표현까지 자동으로 자유 이용되는 것은 아니므로 다음을 지킨다.

- 공공도로·공개된 장소에서 외관만 촬영한다. 사유지·공동현관·실내는 허락 없이 들어가지 않는다.
- 인물 얼굴, 차량 번호판, 세대 호수, 연락처와 개인 물품은 원본 검토 단계에서 마스킹한다.
- 건물의 일반적인 창호·벽·지붕·차양은 모델링 참고로 삼되, 독창성이 강한 건축 외관은 별도 검토 대상으로 둔다.
- 벽화·조각·캐릭터·대형 그래픽은 건물 외관과 별개의 미술저작물일 수 있으므로 복제하지 않는다.
- 상호 문자는 장소 식별 참고로만 보존하고, 로고·브랜드 디자인은 기본 모델에서 일반화한다. 실제 광고 계약이 생기면 별도 후원 레이어에서 다시 허락받아 표시한다.
- 한 장의 원근·조명·색보정을 그대로 복제하지 않고, 여러 방향 사진과 공식 도형·높이를 함께 써 독자적인 저폴리 표현을 만든다.

[한국저작권위원회 FAQ](https://www.copyright.or.kr/customer-center/faq/list.do?categorycode1=&counselfaqno=47492&pageIndex=5&portalcode=&searchkeyword=)도 공개 장소의 건물을 직접 촬영해 비판매 목적으로 이용하는 경우와 타인이 촬영한 사진을 다른 유형의 저작물로 만드는 경우를 구분한다. 다만 게임 속 정밀 3D 재현은 구체적 표현·배포 형태에 따라 별도 판단이 필요하므로, 이 FAQ 하나만으로 모든 랜드마크를 승인하지 않는다.

## 무료 배포가 자동 허락이 아닌 이유

- 저작권법은 저작자에게 복제와 2차적저작물 작성 권리를 둔다. 타인의 사진을 3D 모델·텍스처로 바꾸는 행위는 단순 열람과 다르다. [저작권법 제22조](https://www.law.go.kr/lsLinkProc.do?chrClsCd=010202&joLnkStr=%EC%A0%9C22%EC%A1%B0&joNo=002200000&lsId=000798&lsNm=%EC%A0%80%EC%9E%91%EA%B6%8C%EB%B2%95&mode=2)
- 공개 장소에 상시 노출된 건축·미술저작물에는 별도 자유 이용 규정과 예외가 있지만, 판매 목적 복제 등 예외와 구체적 적용 범위가 있다. [저작권법 제35조](https://www.law.go.kr/lsLinkCommonInfo.do?chrClsCd=010202&lsJoLnkSeq=1033063887)
- 한국저작권위원회가 정리한 3D 골프 코스 사례처럼, 현실 공간을 가상공간에 거의 그대로 재현하는 경우에는 이용 목적·재현 비중·시장 영향 등이 쟁점이 될 수 있다. [가상공간 건축저작물 동향](https://www.copyright.or.kr/information-materials/trend/the-copyright/view.do?brdclasscode=02&brdctsno=50837&brdctsstatecode=&list.do%3FpageIndex=1&nationcode=&searchTarget=ALL&searchText=&servicecode=06)
- Steam은 무료 제품 설정을 지원하지만, 배포자는 포함한 콘텐츠를 배포할 권리를 보유해야 한다. [Steamworks 권리 안내](https://partner.steamgames.com/doc/sdk/uploading/distributing_opensource)

따라서 `Price=Free`는 출시 설정일 뿐 `RightsApproved=true`를 만들지 않는다.

## 자료 등급 제안

### 녹색 · 모델 제작·배포 후보

- `OwnCapture`: 사용자가 직접 촬영하고 개인정보·별도 미술저작물을 정리한 사진
- `ExplicitPermission`: 촬영자 또는 권리자가 게임 모델 제작·수정·Steam 배포를 허락한 자료
- `KOGLType1`: 개별 자료가 공공누리 제1유형이며 출처표시 조건을 충족한 자료
- `CC0` 또는 `CCBY`: 정확한 판본과 저작자·라이선스를 보존하고 변경·배포 조건을 충족한 자료

[공공누리 유형 안내](https://www.kogl.or.kr/static/html/opencode.html)에 따르면 제1유형은 출처표시 조건으로 상업적 이용과 변경 이용이 가능하다. 데이터 포털 페이지의 라이선스가 아니라 **사용하는 사진 한 장마다** 표시 유형을 확인한다.

### 노란색 · 로컬 시각 참고 전용

- 공공누리 제2·3·4유형처럼 비상업 또는 변경 제한이 붙은 자료
- S-MAP·브이월드·네이버·카카오·Google 등의 거리뷰·3D 지도 화면
- 언론 기사, 블로그, SNS, 부동산 매물, 업체 홈페이지 사진
- 촬영자를 모르거나 라이선스 문구가 없는 공개 이미지

현재 게임이 무료라도 이 자료들을 배포 자원 근거로 자동 승인하지 않는다. 서비스 약관·사진 저작권·미래 후원 가능성을 한꺼번에 해결하지 못하기 때문이다.

### 빨간색 · 수집·사용 제외

- 워터마크 제거, 접근 제한 우회, 원본 이미지·3D mesh 추출
- 사유지·실내 무단 촬영
- 얼굴·번호판·세대 정보가 남은 자료
- 변경·재배포 금지가 명확한 자료
- 출처와 원본을 재확인할 수 없는 파일

## 자산 승인 상태 제안

출처 종류와 승인·기술·배포 상태를 한 코드로 합치지 않는다.

| 상태 축 | 예시 값 |
| --- | --- |
| 취득 근거 | `OwnCapture / KOGLType0 / KOGLType1 / ExplicitPermission / ThirdPartyReference` |
| 사진 이용 판정 | `PendingHumanReview / ReferenceOnly / ApprovedForModelBrief / RightsBlocked / WithdrawnOrExpired` |
| 피사체 권리 | 건축물·벽화·상표·인물별 `Cleared / Generalized / Pending / Blocked` |
| Blender 기술 상태 | `BlockedByRights / WorkAuthorized / BlenderValidatedCopy / UnityImportCandidate / Rejected` |
| 배포 상태 | `LocalPrivateReview / ReleaseRightsReviewPending / DistributionApproved / Revoked` |

`BlenderValidatedCopy`는 원본 재열기·FBX 왕복·scale·hash 같은 기술 검증일 뿐이며 `DistributionApproved`를 뜻하지 않는다.

```text
Discovered
→ ReferenceOnly
→ RightsReviewed
→ ApprovedForModelBrief
→ ModelValidated
→ ApprovedForGameDistribution
```

각 사진·모델에는 다음을 보존한다.

- `SourceStableId`, 원본 URL 또는 직접 촬영 식별자
- 촬영자·권리자, 촬영/수집 시각, 촬영 방향
- 원본 SHA-256과 가공본 SHA-256
- `CommercialUseAllowed`, `ModificationAllowed`, `ModelDerivationAllowed`, `PhotoTextureUseAllowed`
- `SourceImageRedistributionAllowed`, `GameDistributionAllowed`, `StoreMediaAndPromotionAllowed`
- `SponsorshipContextAllowed`, `ThirdPartyPlatformDistributionAllowed`, `AttributionRequired`
- 출처표시 문구와 라이선스 사본 확인 시각
- 얼굴·번호판·상표·벽화 처리 결과
- 참조한 `BuildingStableId`, 모델·FBX·Prefab hash

원본 사진은 기본적으로 `artifacts/local/`의 비공개 검토 영역에 두고 Git·Unity 배포 자원에 바로 넣지 않는다. 승인된 파생 모델과 필요한 출처 영수증만 프로젝트 자원으로 승격한다.

권리 철회·기간 만료·출처 manifest 불일치가 생기면 해당 Prefab의 배포 상태를 `Revoked`로 내리고 r8의 `KeepProceduralMass` fallback으로 복귀한다.

## 사가정 첫 적용 제안

1. 현재 건물 polygon 가운데 일반적인 외관의 독립 건물 3개를 후보로 고른다.
2. 도로명주소·건물관리번호·건축물대장·항공사진으로 각 후보의 위치와 질량을 먼저 확정한다.
3. 공개도로에서 앞·양옆·사선·지붕선이 보이도록 직접 촬영 계획을 만든다.
4. 촬영 뒤 얼굴·번호판·상호·벽화를 검토하고 한 후보만 `ApprovedForModelBrief`로 승격한다.
5. Blender에서는 실제 비례와 큰 실루엣은 따르되, 재질·간판·확인하지 못한 면은 디오라마 문법으로 일반화한다.
6. r8의 안전 교체 방식대로 Prefab 검증 성공 뒤에만 해당 절차적 건물 매스를 숨긴다.

사가정시장은 계속 입구·골목 overlay로 다룬다. 처음부터 점포 간판과 여러 건물을 정밀 복원하지 않고, 직접 촬영한 반복 차양·골목 폭·입구 실루엣을 일반화한 시장 모듈로 만든다.

## 무료 출시에서 추천하는 현실적 기준

- 출시 가격은 무료로 유지할 수 있다.
- 자산 승인 기준은 상업적 배포 호환으로 잡는다.
- 일반 건물은 공식 geometry와 자체 촬영을 바탕으로 저폴리 일반화한다.
- 독창적 랜드마크는 소유자·설계자·촬영자 권리 검토 또는 명시적 허락을 우선한다.
- 실제 상호·광고는 기본 공간 모델과 분리한다.
- Steam 크레딧과 저장소의 `THIRD_PARTY_ASSETS` 자료는 같은 출처 대장에서 생성할 수 있게 한다.
- 전 세계 Steam 배포에서는 국가별 공개 건축물 이용 범위가 다를 수 있으므로, 독창적인 실제 랜드마크의 정밀 복원은 별도 배포권 검토 대상으로 둔다.

이 기준이면 지금은 무료로 편하게 공개하면서도, 나중에 광고·후원이나 다른 배포처가 생겼을 때 자산을 전면 교체할 가능성을 줄일 수 있다.

## 확정

- 사용자의 현재 의도는 Steam 기본 게임을 유료 판매하지 않는 것이다.
- 기존 절차적 디오라마를 보존하고 확보 가능한 외관 근거로 대표 건물을 점진적으로 제작한다.
- 사진 자체 사용, 모델링 참고, 게임 배포, 홍보 사용을 서로 다른 권리 단계로 관리한다.
- 직접 촬영 사진을 첫 외관 근거로 우선할 수 있다.

## 미정

- 첫 촬영·모델링 대상 건물
- 사용자가 직접 현장 촬영을 진행할 수 있는지
- 무료 출시 뒤 광고·후원·유료 부가 콘텐츠를 허용할지
- 독창성이 강한 랜드마크에 대해 개별 허락 또는 전문 법률 검토가 필요한 범위

## 다음 질문 하나

첫 표본은 **사가정역 주변의 일반적인 독립 건물 한 동을 사용자가 직접 촬영하고, 그 사진을 텍스처로 복제하지 않고 저폴리 모델링 참고 자료로 쓰는 방식**으로 진행할까?

추천은 그렇다. 타인의 사진 권리를 피하면서 현재 디오라마의 실제 좌표·높이 뼈대와 가장 쉽게 결합할 수 있다. 대가는 현장 촬영과 개인정보·간판 정리 작업이 필요하다는 점이다.

## 현재 검증 상한

현행 계획과 공식 저작권법·한국저작권위원회 안내·공공누리·Steamworks 배포 안내를 대조해 제작 정책 제안만 작성했다. 이는 개별 건물·사진에 대한 법률 의견이나 이용 허락이 아니다. 사진 다운로드·직접 촬영·권리자 문의·건물 후보 확정·Blender·FBX·Unity 자원·Scene·Play Mode·Game View는 수행하지 않았다.
