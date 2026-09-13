[기획 · 월드·공간·배치 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r11]

# 사가정 공공 사진 첫 수집 결과

- 상태: `FirstPublicPhotoSliceStored / ExactMarketPhotoMissing / StationPhotosPrivateReview / CCBYSAProfilePending / BlenderAndUnityBlocked`
- 선행 방향: [공공 이용허락 사진 우선 수집 r10](public-licensed-photo-collection-direction.r10.md)
- 기계 대장: [사가정 공공 사진 수집 r2](../../../../../eng/world-seedbeds/station-landmarks/sagajeong-public-photo.collection.r2.json)
- 수집 보고: [사가정 공공 사진 첫 수집](../../../../Reports/사가정-공공사진-첫수집-2026-09-13.md)

## 확정

- Wikimedia Commons의 `Category:Sagajeong Station` 10건을 파일별 라이선스 메타데이터와 함께 조회하고, 그중 사가정역 정확 대응 사진 5건을 로컬 비공개 검토 영역에 수집했다.
- 공공영역 3건은 역명판·승강장 내부 사진이다. 사가정역 정체성 근거는 되지만 시장 입구나 독립 건물 외관을 복원하는 근거는 아니다.
- 출입구 1·2번 사진 2건은 사가정역 외부 구조를 정확히 보여주지만 `CC BY-SA 4.0`이다. 상업 이용과 변경 자체는 허용되나 출처·라이선스 링크·변경 고지·동일조건변경허락 의무가 있으므로, 현재 승인된 CC0/CC BY 우선 profile에 자동 포함하지 않는다.
- 출입구 사진에는 사람과 주변 상가 간판이 보인다. 형상 참고와 원본 텍스처 사용을 분리하고 별도 내용 검토 전에는 Blender·Unity로 넘기지 않는다.
- Commons에서 `사가정시장`, `Sagajeong Market`, `면목로44길 사가정시장`, `면목동 시장`을 조회한 결과는 0건이었다. 공식 중랑구 소식지는 사가정시장을 다루지만 개별 사진의 변경·배포 권리를 확인하지 못해 다운로드하지 않았다.
- 공공누리에서 확인한 중랑구 전경 2건과 옹기문화마당 1건은 모두 제4유형이었다. 상업 이용과 변경이 금지되므로 메타데이터만 기록하고 원본은 받지 않았다.
- 정확한 시장 외관 사진이 없다는 결손을 다른 시장·거리뷰·생성 이미지로 채우지 않는다. 사가정시장 절차적 매스와 기존 후보 위치는 그대로 유지한다.

## 저장과 재조회

- 비공개 원본 경로: `artifacts/local/public-data/sagajeong-public-photos-20260913-r2/`
- 수집 영수증 SHA-256: `f8480edafd0fa96ed0b5d603197f061cfcdb76fec644af89d681b440f58ce59a`
- 기존 로컬 Docker MySQL `hongdal_dev` 공공자료 원장을 재사용했다.
- 사진 후보 5건, 시장 검색 결손 1건, 공공누리 제4유형 제외 3건을 정규화 레코드 9건으로 저장했다.
- 첫 적용은 신규 9건, 같은 입력 재적용은 신규 0·기존 9건, 별도 연결 재조회는 9건이다.
- 원본 파일은 Git·Unity `Resources`·StreamingAssets·빌드에 넣지 않았고, DB에는 출처·판본·hash·권리·검토 상태만 남겼다.

## 미정

- `CC BY-SA 4.0` 자료를 Blender 파생 모델의 근거로 쓸 때 모델 파일과 출처 고지를 어떤 동일조건변경허락 단위로 공개할지
- 사가정시장 입구·대표 골목의 정확한 제0·1유형 또는 CC0/CC BY 사진을 다른 공식 기록관에서 확보할 수 있는지
- 2007년 내부 사진과 2018년 출입구 사진이 현재 외관을 어느 정도 반영하는지

## 다음 질문 하나

사가정역 출입구를 첫 모델링 후보로 쓰기 전에, `CC BY-SA` 파생 자산의 출처·라이선스·변경 고지와 자산 단위 공개 방식을 별도 정책으로 먼저 확정할까?

추천은 **먼저 정책을 확정하고 그동안은 비공개 참고 상태를 유지하는 것**이다. 사진은 정확하고 유용하지만, 게임 전체와 개별 모델 자산의 라이선스 경계를 정하지 않은 채 Blender 작업부터 열면 나중에 배포 판단이 어려워진다.

## 현재 검증 상한

공개 원천 검색, 파일별 이용조건 조회, 원본 5건 수집, SHA-256 보존, 시각 내용 1차 확인, 로컬 MySQL 저장·멱등 재적용·독립 재조회까지 완료했다. 사가정시장 정확 사진은 확보하지 못했다. 사진 내용 승인, CC BY-SA 프로젝트 정책, Blender 모델, Graph Map·배치맵 연결, Unity Prefab·Scene·Play Mode·Game View, 공개·배포·commit·push는 수행하지 않았다.
