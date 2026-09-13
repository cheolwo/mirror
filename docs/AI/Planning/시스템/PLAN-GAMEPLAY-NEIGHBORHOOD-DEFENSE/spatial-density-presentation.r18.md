# [기획·구현 · 공간 밀도 표현 · PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE · r18]

- 판본: `sagajeong-spatial-density-presentation.r1`
- 상태: `Approved / LocalPrivateReviewImplemented / PublicPromotionBlocked / PlayModeGameViewVerified`
- 승인 근거: r17 목표 이미지 우선 원칙과 사용자의 후속 요청 “Implement the proposed plan.”
- 상위 기준: [목표 이미지 우선 표현 r17](concept-presentation.r17.md)

사가정역 1km 모형에서 자료 결손 구역의 표현 밀도, 건물 높이 차, 도로 위계와 생활 표면을 보완한다. 현실 건물 외관을 복제하거나 공터의 용도·통행 가능성을 추정하지 않고, 검증된 공간 자료를 읽기 전용 표현 사본으로 합성한다.

## 확정 범위

1. 기존 `SagajeongReference r3`의 좌표·해시·건물 602개·도로 2,397개는 바꾸지 않는다.
2. 국토교통부 GIS건물통합정보에서 1km 경계와 교차하는 건물도형 4,062개를 비공개 검토 overlay로 만든다. 원천 높이가 있는 1,427개는 그 값을 쓰고, 나머지 2,635개는 기존 승인 원칙대로 상징적 4m를 유지한다. 층수 추정은 자동 적용하지 않는다.
3. OSM에서 닫힌 생활 표면 108개만 표현 후보로 보존한다. 이 표면은 실제 소유, 개방, 통행 가능, 방어 배치 권위가 아니다.
4. 기존 r3 건물과의 결속은 공간 중첩 근거가 충분한 별칭 544개만 허용한다. 약한 후보 5개와 기타 모호 후보 90개는 자동 대체에서 제외한다.
5. Unity는 4×4 chunk, 건물 높이·용도별 massing, 절차적 창문/옥상, 도로 접합·연속 노면 표시, 실제 표면에 한정한 녹색, 가상 장식 나무, 제한된 접지 그림자를 표현한다. 업무·충돌·통행·Simulation 권위는 추가하지 않는다.
6. Synty 표현은 호환 선택지로 유지하되 이 비공개 overlay를 읽거나 적용했다고 보고하지 않는다. Synty 분기는 r3 판본과 해시를 명시한다.
7. 전체 보기 카메라는 사선 투영 뒤 가로·세로 범위를 모두 맞춘다. 확대 보기에서는 사가정역 주변 상세 묶음만 켠다.

## 자료 계보와 감사

- GIS 원천: [공공데이터포털 국토교통부 GIS건물통합정보](https://www.data.go.kr/data/15083092/fileData.do), 로컬 원본 SHA-256 `674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755`.
- OSM 원천 SHA-256: `3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3`.
- overlay revision: `sagajeong-spatial-presentation.private-review.r2`.
- overlay content hash: `138D1A47B286CE350CF339C1F69C6FFAD7778EBA7B6C95F3CF9F172F8248EBB1`.
- overlay 파일 SHA-256: `3EC6B95DC083F13F99042989E0B58957B3EE031E06C501F94022BE455DEC0A44`.
- 독립 감사: 100칸 중 `ConfirmedBuilt 88 / ConfirmedOpen 2 / IncompleteSurfaceEvidence 5 / MissingCoverage 5`. 누락 member가 있는 OSM multipolygon 1개는 표면으로 만들지 않고 7칸의 불완전 근거로 남긴다.
- 감사 상태: `PassedWithIncompleteSurfaceEvidence`; 감사 hash `9AA3874D0A6BEE58E4FDE2D466DD1A25FC7861F7D89ADAB51F3BC81A96E5322D`.

생성기와 독립 감사기는 SHP/DBF/OSM을 각각 다시 읽어 좌표와 개수를 대조한다. 동일 입력 두 번의 byte 결정성, 원본·산출물 재해시, 변조 거부, 승격 음성 시험을 포함한다. 결과는 `artifacts/local/`에만 두며 Unity `Resources`나 서버 API로 자동 복사하지 않는다.

## 공개 승격 관문

공공데이터포털 페이지의 이용허락 표시만으로 현재 로컬 합성 파일을 배포 승인하지 않는다. 이 판본은 `LocalPrivateReview`, `distributionApproved=false`, `RightsConflictUnresolved`이며 실제 공개 Resources 자산은 없다.

공개 후보는 모든 출처 영수증의 허용된 공개 상태·정확한 URL/라이선스/원본 길이와 해시, 표면 source 결속, 별도 검토된 revision→asset SHA 고정을 모두 만족해야 한다. 함께 배포되는 SHA sidecar는 전송 무결성일 뿐 승인 권위가 아니다. 한 조건이라도 맞지 않으면 r3 표현으로 복귀한다.

## Unity 구현 및 실행 검증

- 로컬 비공개 overlay는 명시적인 환경 변수 또는 Editor 설정 경로가 있을 때만 읽고, 원문과 절대 경로를 Scene·상태·오류에 남기지 않는다.
- 사가정 관찰은 전용 레이어 30, 기존 오프라인 생활 관찰은 레이어 29를 사용한다. 공용 계약에서 카메라 culling mask·조명·Volume mask·점유 판정을 함께 나눠 두 표현이 서로를 가리거나 점유한 것으로 오판하지 않는다.
- 실제 Play Mode 전체 보기에서 overlay 4,062개·표면 108개, 표현 renderer batch 36개, 장식 나무 97개, 역 주변 상세 건물 225개를 확인했다.
- 전체 보기와 사가정역 확대 Game View를 로컬 비공개 검증 폴더에 보존했다. 권리 검토 전 PNG도 문서/배포 자산으로 승격하지 않는다.
- Unity 컴파일 오류 0, 공간 overlay 집중 시험 54/54, 사가정 전체 시험 73/73, 관찰 레이어 계약 시험 2/2가 통과했다.
- PC Editor 최종 전체 보기 3,546프레임: 평균 289.28fps, 95백분위 4.78ms. Pipeline 측정값은 draw call 112, triangle 166,871, CPU frame 4.11ms, GPU frame 0.61ms였다. 해당 Editor 시점 값이며 배포 기기 성능 보장은 아니다.
- canonical `SimulationWorldShell`은 저장하지 않았고 실행 뒤 stopped/clean 상태를 확인했다.

## 검증 상한과 남은 일

- 이 결과는 자료 생성·독립 감사·Unity 컴파일/EditMode·Play Mode 관찰 카메라 렌더 증거다. 실제 마우스 입력, 주소/방어 폐루프 완주, 서버 연결, 운영 데이터, 실제 건물 외관, 보행·전투 NavMesh, WI/E 승격 증거가 아니다.
- 공식 원천 높이가 없는 2,635개는 4m 상징값이므로 높이 정확도는 아직 제한된다.
- `MissingCoverage` 5칸과 `IncompleteSurfaceEvidence` 5칸을 실제 공터로 해석하지 않는다.
- 다른 canonical Scene 모듈의 서버 세션 부재 오류는 별도 문제다. 사가정 렌더 자체의 성공과 전체 Console 정상 여부를 구분한다.
- 공개 승격은 정확한 권리 검토와 불변 asset 승인 영수증이 생긴 뒤 별도 요청으로 진행한다.
