# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r39]

## r39 소수 지역 집중 정밀화

[소수 지역 집중 정밀화 r39](focused-area-refinement.decision.r39.md)는 사용자 요청에 따라 30개 일괄 확장을 보류하고, 면목제3·8동 사가정역 주변의 교차로·연결 골목 한 구간을 현재 작업 대상으로 좁힌다. 면목제7동·면목본동은 후속 후보로만 두며 동시에 시작하지 않는다. 기존 30개 자료와 증거는 보존하고, 구간별 건물·길 관계의 구체적 개선을 완료 기준으로 삼는다.

## r38 수평 등고선 흔적 Unity 검토 완료

[수평 등고선 흔적 r38](thirty-admin-dong-horizontal-contour-trace.implementation.r38.md)은 높이 속성을 읽지 않은 860개 후보·184,747개 선분을 기존 Mesh에 적용했다. 실제 Play Mode·Game View 31장과 Unity 집중 시험 44/44, Scene hash 불변을 확인했다. 이는 범위 축소 전 완료한 기준선이며 높이·지형 표면의 정밀화 완료는 아니다.

## r37 30개 행정동 차선표시 선형 비공개 Unity 검토

[차선표시 선형 비공개 후보·Unity 검토 r37](thirty-admin-dong-lane-marking-private-generation.implementation.r37.md)은 서울 열린데이터광장 `OA-15537`의 2021 차선 SHP를 2023 역사 경계 30개로 잘라 후보 23,360개·표시 선분 60,586개를 결속했다. 공통 ENU millimeter 좌표에서 원천 절단 선분 60,646개를 표시 60,586개·1mm 이하 2개·물리 중복 58개로 닫았고, 관리번호 원문 대신 hash stable ID만 전달했다.

Unity 실제 Play Mode·Game View에서 overview 30장과 면목제3·8동 detail 1장을 다시 검증했다. 차선 정점/인덱스 242,344/363,516을 기존 2×2 결합 Mesh에 추가했고, 모든 모듈은 Renderer 4·MeshFilter 4·Collider 0이다. 집중 시험 38/38, PNG 31개 hash 일치, Scene hash 불변을 확인했다. 이는 역사적 선형 윤곽이며 현재 차선·방향·통행·정지선·신호·보도 권위와 공개·Runtime·Traversal·Gameplay는 계속 `false`다.

## r36 지형 높이 공식 규정 근거 심화 감사

[지형 높이 공식 규정 근거 심화 감사 r36](thirty-admin-dong-terrain-official-standard-evidence.audit.r36.md)은 정확한 ZIP이 1:5,000 `N3L_F001/N3P_F002`임을 확인하고, `F001.CONT`·`F002.NUME`를 국가 속성목록·지형도 도식 규정·공간정보 법정 높이 기준과 연결했다. `CONT/NUME`는 공식 규정으로 해석한 metre 수치이고 국가 기준은 인천만 평균해수면·대한민국 수준원점이다.

그러나 ZIP XML에 수직 CRS·높이 단위·필드 변환 이력이 없고 `HEIGHT` 정의도 없다. 다음 세대는 `CONT/NUME`를 읽고 `HEIGHT`를 동등성 감사에만 써야 하며, export-level 메타데이터와 전용 Unity consumer가 없으므로 전체 상태는 `PrecisionTerrainBlockedBySourceMetadataAndConsumerContract`를 유지한다.

## r34 30개 행정동 Unity 검토 화면 가독성 개선

[Unity 검토 화면 가독성 개선 r34](thirty-admin-dong-unity-review-readability.implementation.r34.md)은 전체 동 overview의 큰 근거·집계 패널을 82px 핵심 패널로 줄이고, 면목제3·8동 detail에는 전체 근거와 선택 건물 정보를 유지했다. 같은 v5 입력·도형·Mesh를 실제 Unity Play Mode·Game View에서 다시 검증했고 evidence v6 overview 30장+detail 1장, PNG hash 불일치 0, 집중 시험 32/32, Scene hash 불변을 확인했다.

이는 검토 화면에서 도형을 더 넓게 보는 표현 개선이며 자료 권위 승격이 아니다. 역사 경계·현재성·주소·필지·통행·생태 결손과 공개·DB·Mongo·current·Runtime·Traversal·Gameplay 차단은 유지한다.

## r33 30개 행정동 정밀 지형 consumer 준비도 감사

[정밀 지형 consumer 준비도 감사 r33](thirty-admin-dong-terrain-consumer-readiness-audit.implementation.r33.md)은 r29의 최근접 원천 500m 초과 47개가 모두 물리 tile `x5/z6`의 단일 연결 성분이며, 신내1동 중앙 core와 30개 역사 행정동 polygon 밖 60m halo에만 있음을 확인했다. `core+30m`까지는 초과 0개지만 이 값만으로 halo를 줄이거나 자료를 삭제하지 않는다.

정확한 높이 필드의 물리 단위·수직 datum·기준면과 r29 전용 Unity consumer 계약이 없으므로 상태는 `PrecisionTerrainBlockedBySourceMetadataAndConsumerContract`다. Unity 높이 Mesh·Collider·건물/도로 접지·공개·Runtime·Traversal·Gameplay에는 적용하지 않았다.

## r32 30개 행정동 비오톱 외곽선 Unity 비공개 검토

[비오톱 비공개 후보·Unity 검토 번들 구현 기록 r32](thirty-admin-dong-biotope-private-generation.implementation.r32.md)은 2025 비오톱을 2023 역사 경계와 교차해 후보 3,181개·polygon part 3,408개·내부 ring 108개를 만들고, 외곽·내부 ring을 채우지 않은 선으로 기존 2x2 결합 Mesh에 중첩했다. Unity Mono의 JSON zero 내용 hash 규칙을 생성기와 맞춰 30개 bundle 전체를 다시 검산했다.

실제 Unity `6000.5.6f1` Play Mode·Game View에서 overview 30장과 면목제3·8동 detail 1장을 확인했다. 비오톱 정점/인덱스 408,348/612,522, 모듈별 Renderer 4·Collider 0, 집중 시험 32/32, PNG 31개 hash 일치, canonical Scene hash 불변을 기록했다. 현재 생태·종·법적·안전·통행 의미와 공개·DB·Mongo·current·Runtime·Traversal·Gameplay 권위는 성립하지 않는다.

## r31 30개 역사 행정동 방향표시 비공개 Unity 검토

[방향표시 비공개 후보·검토 번들 구현 기록 r31](thirty-admin-dong-road-direction-private-generation.implementation.r31.md)은 공식 방향표시 8,415개를 모두 의미 미해석 후보로 유지한 채 v4 검토 입력에 결속했다. Unity 실제 Play Mode·Game View에서 30개 동 overview와 면목제3·8동 detail 1장을 확인했고, 화살표 0개·방향표시 정점 33,660개·모듈별 Renderer 4·Collider 0, 집중 시험 26/26, canonical Scene hash 불변을 기록했다.

기존 canonical Scene 오류 8건은 남아 있으며 현행 경계·공개·DB·Mongo·current pointer·Runtime·Traversal·Gameplay·E 승격은 성립하지 않는다.

## r30 30개 행정동 방향표시·비오톱 공식 원본 감사

[방향표시·비오톱 공식 원본 감사 r30](thirty-admin-dong-direction-biotope-source-audit.implementation.r30.md)은 `OA-15536`의 2026-09-10 방향표시 158,373점과 `OA-21145`의 2025 비오톱 42,544 polygon을 공식 최신 첨부로 확보해 hash·CRS·필드·행 수를 고정했다. 역사 경계에서 방향표시 8,415점과 비오톱 polygon 교차 3,185건이 지정 30/30개 동에 있다. 원본·페이지·영수증은 Git 제외 로컬 폴더에 보존했다.

방향표시 각도와 종류는 노면 기호 표현 후보일 뿐 접근·차로·통행 권위가 아니며, 길이 단위는 미확인이다. 비오톱은 법적 효력이 없는 유형·평가 참고자료이며 생물종 현장 관찰·안전·이동 규칙이 아니다. r30 원본 감사 시점에는 private candidate generation과 Unity 검토가 미착수였고, 이후 r31·r32가 비공개 화면 검토까지만 진행했다. 현행 경계 재귀속, 공개·DB·Mongo·current·Runtime·Traversal·Gameplay는 계속 비승인 상태다.

## r29 30개 행정동 지형 공통 격자 비공개 검토 세대

[지형 공통 격자 비공개 검토 구현 기록 r29](thirty-admin-dong-terrain-private-generation.implementation.r29.md)은 `OA-22241`의 2025 등고선 ZIP만 사용해 30개 동·148개 물리 tile을 공통 ENU에서 한 번 보간했다. 500m tile, 60m halo, 10m 간격의 실제 union 391,619개 key를 만들고 공유 key 153,543개와 전역–tile 비교 195,793회의 bit 불일치 0을 확인했다. 사용하지 않는 사각 envelope 228,700개 key는 보간하거나 평지로 채우지 않았다.

Generation `D0AAE02B...B131F`은 450개 파일·8,609,465 bytes이며 self-test 7/7·build·verify·재실행 `changedFiles=0`·completion 449/449 hash 대조를 통과했다. 수직 datum·높이 단위가 확인되지 않았고 최근접 원천 500m 초과 표본 47개가 있어 구조 검토는 통과하되 `precisionTerrainReady=false`다. 공개·DB·Mongo·current·Unity·Mesh·Collider·Runtime·Traversal·Gameplay는 모두 차단했다.

## r28 30개 행정동 등고선·표고 원본 감사

[등고선·표고 원본 감사와 지형판 생성 계약 r28](thirty-admin-dong-terrain-contour-audit.implementation.r28.md)은 서울 열린데이터광장 `OA-22241`의 공식 HTML과 두 ZIP을 익명 POST로 확보해 길이·SHA-256·내부 SHP·`EPSG:5174`를 검증했다. 2025-03-20 `서울시 등고선.zip`과 2023-12-26 `서울시 경사도.zip`은 범위·레코드·hash가 다른 별도 판본이므로 섞지 않는다. 첫 지형 후보는 등고선 8,570건과 표고점 45,870건을 갖는 2025 ZIP 하나만 쓴다.

역사 경계와의 임시 대조에서 30/30개 동 모두 표고점과 등고선이 확인됐다. 다음 생성은 30개 동을 한 번에 보간한 scope-global 격자, 공통 수직 기준, 인접 tile의 `sharedEdgeBitEquality`를 사용해야 한다. 수직 datum과 높이 단위는 원천에 명시되지 않아 미확인으로 남겼고, 표면·Mesh, DB·Mongo, current pointer, 공개·배포, Unity 적용은 모두 `false`다.

## r27 30개 행정동 역사 보행망 Unity 비공개 검토

[역사 보행망 Unity 비공개 검토 구현 기록 r27](thirty-admin-dong-walk-network-unity-review.implementation.r27.md)은 r26 후보의 링크를 시작·끝점으로 줄이지 않고 54,574개 ordered path 점 전체로 결속했다. 최종 generation `29764F2B...F2972915`은 30개 동의 노드 17,267개·링크 조각 23,472개·노드와 path 점 합계 71,841개를 고정하고, 원천 길이 대비 전체 차이 0.585893m와 조각별 최대 차이 0.002409m를 검증했다. 정밀 ENU 위치가 들어가므로 산출물은 로컬 비공개로만 취급한다.

Unity의 v1 geometry·v2 observation 호환을 유지하며 v3 walk 계약과 기존 2x2 결합 Mesh 표현을 추가했다. 집중 EditMode 22/22와 실제 Play Mode·Game View를 통과했고 30개 overview와 면목제3·8동 detail을 남겼다. 모든 모듈은 Renderer 4·Collider 0이고 Scene hash는 실행 전후 같으며 저장하지 않았다. 기존 canonical Scene 오류 8건은 남아 있어 Console 0이나 서버 통합을 주장하지 않는다. 2020년 보행망·역사 경계·불완전 halo의 private review일 뿐 current pointer·공개·DB·Runtime·traversal·gameplay 권위는 계속 `false`다.

## r26 30개 행정동 역사 보행망 비공개 검토 세대

[역사 보행망 비공개 검토 구현 기록 r26](thirty-admin-dong-walk-network-private-review.implementation.r26.md)는 `OA-21208`의 2020년 기준 WGS84 자료를 2026-09-26 단일 영수증과 현재 CSV hash에 결속한다. 원천 59,724행에서 노드 17,267개와 링크 조각 23,472개, 합계 40,739개를 30개 역사 행정동 후보로 만들었고 generation `7A0887F6...A508FA6DD`의 5개 파일을 다시 계산해 `verify PASS`를 확인했다. 과거 r1 동결 scope를 복구하거나 서로 다른 내려받기 세대를 섞지 않았다.

결과는 Git 제외 로컬 비공개 후보이며 현재 통행 가능성이 아니다. 정확 좌표 공개, DB·Mongo 저장, current pointer, Unity 적용·Runtime, traversal·gameplay 권위는 모두 `false`다. 2020년 보행망·2023년 역사 경계의 판본 차이, 경계 halo, 보도 폭·연석·출입구·방향·정지선·신호 결손은 그대로 남는다.

## r25 30개 행정동 비공개 관찰 오버레이

[비공개 관찰 오버레이 구현 기록 r25](thirty-admin-dong-private-observation-overlay.implementation.r25.md)는 공식 포털 POST 다운로드로 복구한 `OA-23081` 2026-08-24 XLSX와 `OA-15534` 2025-08-14 ZIP을 기존 역사 경계에 다시 결속한다. 횡단보도 1,533개(보행신호 설치 관측 933개)와 교차로 554개를 원천 관리번호·명칭·주소·업체 식별 없이 hash stable ID와 공통 ENU millimeter 중심의 `privateObservationOverlays v1`으로 만들었다. r2/r3 검토 generation `4D94046D...FE3971`은 30개 bundle과 index·completion, 첫 생성 32파일을 만들고 즉시 `verify PASS`를 확인했다. 공개 `DisplayOverlays`는 계속 0이며 current pointer·DB·Mongo·public·runtime·traversal·gameplay는 사용하거나 바꾸지 않았다.

과거 문서의 MySQL 저장 완료 기록과 달리 현재 `hongdal_dev`의 G2a~G4b 대상 행은 각각 0이고, Mongo는 인증 실패로 현재 상태를 재확인하지 못했다. Unity 비공개 관찰 overlay는 r25에서 실제 Play Mode·Game View까지 확인했으며, 이어진 G3c 역사 보행망의 생성·Unity 검토는 r26·r27에 별도 증거로 남겼다. 후속은 최신 `OA-15536` → `OA-22241` 지형·`OA-21145` 비오톱 → `OA-22784` 250m 생활인구 → 현행 JUSO 승인 순이다.

## r24 30개 행정동 공통 깊이 슬롯 첫 구현

[30개 행정동 공통 깊이 슬롯 구현 r24](thirty-admin-dong-common-depth-slots.implementation.r24.md)는 별도 Unity 저장소의 기존 행정동 상태 사본·검토 View에 역세권과 의미를 맞춘 행정동 전용 8슬롯 구조를 붙인다. 정확한 30개 scope 모두 같은 슬롯을 갖지만 현재 비공개 검토 가능 범위는 `DataEvidence / BaseSpatialPresentation / Interaction`뿐이고, 나머지는 근거에 따라 `Blocked / NotProvided`다. 역사 경계·건물·2026-08-12 NodeLink·행정동-법정동 원본을 기록 hash로 복구해 r2 30개 모듈을 다시 생성·검증했고, MySQL 관할 원장 25,739행을 멱등 적용·독립 재조회했다. Unity 검토 bundle 30개 export, 집중 EditMode 13/13, 실제 Play Mode Game View 31장과 Scene hash 불변을 확인했다. Mongo candidate는 보존 볼륨 인증 실패로 쓰기 전에 중단했고 current pointer는 바꾸지 않았다. JUSO 현행 경계·건물·건물군·출입구는 계속 신청·승인이 필요하며, 실제 마우스 입력과 나머지 결손 자료 보완은 남아 있다.

## r23 30개 행정동의 사가정 참조 깊이 확장

[30개 행정동 단계 계획 r23](thirty-admin-dong-sagajeong-depth-rollout.plan.r23.md)은 사용자 최신 방향에 따라 면목제3·8동만 깊게 만드는 우선순위를 30개 행정동의 단계적 깊이 확장으로 넓힌다. 30개 모두 역사 경계 기반 기초 모듈·G6 화면이 이미 있고 보존 PNG30장의 현재 hash도 일치했다. 현행 공간 정본은 r16의 경계→동일 세대 건물/건물군→출입구 순서로 30개 전체에 적용하고, 화면 Adapter의 첫 다른 동 검증은 면목본동에서 시작한다. 사가정 전용 값은 복사하지 않으며 현재 원본·출입구·통행 결손과 공개 차단을 유지한다.

## r22 진행 음식 관찰 연결 보완

[진행 음식 관찰 연결 보완 r22](active-food-observation-repair.implementation.r22.md)은 사용자가 수용한 r21의 첫 수리 범위다. 진행 사본 배치, TTL 재확인, 낮은 판본 삭제 보호와 음식 단계 표현을 수정했다. 공유 코어 28/28·서버 11/11 및 실제 Unity 소스의 순수 진단을 통과했다. Editor 재컴파일 시간 초과로 EditMode·Play Mode·Game View는 미검증이며 운영 의미 위치의 실제 결속은 후속으로 남는다.

## r21 정상 생명주기 API·Unity 연결 조사

[정상 생명주기·API·Unity 부족 연결 조사 r21](normal-lifecycle-api-unity-gap-audit.proposal.r21.md)은 기존 음식 업무 HTTP 실행과 Unity live 메모리 소비 기반을 확인하여 r20의 우선순위를 좁힌다. 정상 업무를 재구현하지 않고 진행 자료 배치·TTL 갱신·삭제 판본·단계 매핑을 먼저 보완한 뒤 실제 화면으로 검증하는 제안이다. 집중 자동 시험 50/50·54/54·18/18 통과와 별개로 독립 코어 진단에서 연결 결손 3개를 재현했다. 제품 코드 수정·실제 서버 실행·Unity Play Mode는 이번에 수행하지 않았다.

## r20 Unity 중심 참조 모듈·OS 생명주기 제안

[면목제3·8동 참조 모듈과 OS 생명주기 검증 제안 r20](unity-first-reference-and-os-lifecycle.proposal.r20.md)에 현재 코드·기존 실행 증거와 부족한 연결을 정리했다. 사용자 요청의 방향은 Unity 중심의 점진적 개발이며, 세부 작업 순서는 `Proposed`다. 현행 경계·권리 관문은 유지하면서 `참조 공간 연결 → 음식 업무 정상·회복 검증 → 다른 동 한 곳 재사용 → 창고·마트·화물의 독립 검증` 순서를 제안한다. r19는 이전 승인 기준선으로 보존하며 새 실행 승인·Scene 저장·E 승격을 뜻하지 않는다.

## 목표

서버가 출처·판본·경계 유효성을 검토한 행정동별 공간 사본을 Unity가 읽기 전용으로 받아, 하나의 `SimulationWorldShell`에서 행정동 단위 디오라마를 선택·관찰할 수 있게 한다.

첫 단일 대상이었던 면목제3·8동에서 동북서울 배달운영권역 후보 30개 행정동으로 자료 생성 범위를 넓힌다. 역 중심 1km 디오라마는 상세 관찰 창이고, 행정동 디오라마는 경계와 자료 귀속의 기본 공간 모듈이다. 어느 쪽도 배달권·통행·게임 상태의 권위를 자동으로 갖지 않는다.

## r19 면목제3·8동 아이소메트릭 디오라마 우선순위

- 당분간 제품 우선순위는 모바일 운영 앱의 현실 적용 확대보다 `region:kr:hjd:1126057500` 면목제3·8동 디오라마의 공간·외관·거리 판독성을 정교화하는 데 둔다. 다른 29개 행정동은 같은 계약으로 확장할 수 있는 후보 상태를 유지하되, 면목제3·8동과 같은 깊이를 이미 갖췄다고 보고하지 않는다.
- Unity의 첫 표현은 하나의 사각 받침 위에서 내려다보는 아이소메트릭 행정동 모듈로 한다. 사각 받침은 카메라·배경·모듈 교체를 위한 표현 프레임일 뿐이며 실제 행정동 경계를 사각형으로 잘라 바꾸거나, 경계 밖 건물·도로를 면목제3·8동 소유로 편입하는 근거가 아니다. 실제 경계 밖은 중립 바탕 또는 인접 맥락으로만 표현한다.
- 기존 사가정역 1km×1km 디오라마는 폐기하지 않고 면목제3·8동 안의 고정밀 상세 관찰 창으로 재사용한다. 행정동 정본이 전체 귀속과 집계를 맡고, 역세권 창은 출구·교차로·대로변·시장·대표 건물의 세밀한 표현과 현장 비교를 맡는다.
- 정교화 순서는 `P0 현행 행정동 경계·건물 외곽·필지·도로명주소·출입구 근거 → P1 사각 받침·지형·도로·보도·건물 매스 → P2 현장 사진 근거의 외관·거리 시설 보정 → P3 검증된 대표 건물·출구·시장만 Blender 상세화 → P4 사람·차량·주문·배달·절기·오행·광고 overlay`로 고정한다. P4는 사실 기반 배경을 가리거나 앞 단계의 결손을 숨기지 않는다.
- 사용자가 배달 중 모은 현장 사진은 자동 위치 정본이나 실제 외관 완성 증거가 아니라 `FieldObservationCandidate`로 수집한다. 촬영 시각·대상·방향·공개 장소의 대략 위치·원본 hash·촬영자 권리·개인정보 검토·적용 대상을 기록하고, 공공자료의 건물·도로·주소 식별자와 검토 결속된 사진만 표현 보정에 사용한다.
- 촬영은 운행 중이 아니라 안전하게 정차한 뒤 공공장소에서만 한다. 주문 화면·고객 상세 주소·사유지 내부를 촬영하지 않고, 얼굴·차량번호·세대번호·공동현관 정보처럼 불필요한 개인정보는 수집 또는 배포하지 않는다. 안전하거나 권리·위치가 명확하지 않은 사진은 비공개 검토 후보로 남기며 디오라마에 반영하지 않는다.
- 네 역할 모바일 앱과 운영 화면은 현행 검증 결과를 보존하되 당분간 유지·회귀·현장 자료 확인에 필요한 최소 보완만 한다. 결제·배포·현실 운영 확대는 별도 재승인 전까지 디오라마 우선 작업을 앞서지 않는다.
- r19는 우선순위와 증거 순환을 확정한 기획 판본이다. 현행 경계 수집, 사진 수집, Blender 제작, Unity Scene·Prefab 저장, Play Mode·Game View, E 승격, commit·push를 수행하거나 승인한 판본이 아니다.

## 확정

- 대상 scope는 광진구 중곡동, 동대문구 전농·답십리·장안·휘경·이문동, 중랑구 면목·상봉·중화·묵·망우·신내동에 연결된 행정동 30개다. 정확 집합은 `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json`이 고정하고 행정안전부 코드 원장과 다시 대조한다.
- 법정동·행정동·역세권 관찰 창·관리자 배달운영권역·협력권역은 서로 다른 식별자와 판본을 유지한다. 하나를 다른 하나의 이름이나 경계로 바꾸지 않는다.
- 건물은 원본 도형의 `PointOnSurface`가 하나의 행정동 경계에 포함될 때만 귀속한다. 경계 밖·무효·복수 판정은 `Unresolved` 또는 quarantine으로 남기고 임의 귀속하지 않는다.
- 도로 중심선은 행정동 경계에서 잘라 여러 행정동에 걸칠 수 있다. 건물은 중복 소유시키지 않는다.
- 모든 행정동은 같은 사가정 원점 ENU와 500m 격자를 사용한다. 동마다 별도 원점을 잡아 인접 경계를 어긋나게 하지 않는다.
- MongoDB에는 hash별 불변 manifest·tile·overlay를 저장하고 현재 판본 포인터만 갱신한다. 다만 현행 경계 정본을 통과하지 못한 결과는 별도 candidate collection에만 저장하며 API current pointer를 바꾸지 않는다.
- 행정동별 디오라마는 `ObservationPresentationOnly=true`, `TraversalReady=false`, `GameplayReady=false`, `DistributionApproved=false`를 기본 상한으로 한다.
- 공개 사업장, 상인 Claim, 후원 Campaign은 별도 원장과 권한으로 검증한다. 이번 30개 기본 공간 batch에는 업체 표시·광고·상세 주소·개인정보를 넣지 않는다.
- Unity는 인증 GET과 명시적 `SemanticPlaceStableId`만 읽는다. Unity가 경계·주소·업체 위치를 추측하거나 운영 상태를 확정하지 않는다.

## 공식 경계 판본 정정

- 서울시 `OA-22160` 페이지는 2026-09-11 갱신으로 표시되지만 실제 내려받기 파일은 페이지 수정일 2023-10-31, 내부 파일 시각 2023-10-20, 425행이다. SHA-256은 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`, 좌표계는 `EPSG:5181`이며 이번 대상 30개는 모두 존재한다.
- 위 ZIP은 `HistoricalOfficialBoundaryBootstrap`으로만 쓴다. 포털 갱신일을 경계 유효 기준일로 바꾸어 기록하지 않으며, 이 자료만 사용한 batch는 `WaitingForAdministrativeBoundary / CurrentAuthoritativeBoundaryUnavailable`이다.
- 현행 게시 정본은 행정안전부 주소정보누리집의 최신 월 전체분 `TL_SCCO_GEMD`다. 승인 취득한 실제 파일의 기준월·파일명·hash·공공누리 제1유형·`EPSG:5179`와 30개 코드 유일성·유효 도형·위상을 검증한다.
- 최신 정본이 없거나 코드 판본보다 낮거나 hash·CRS·대상 집합·위상 검사가 실패하면 `AdministrativeBoundaryVintageRejected`로 current 게시를 중단한다.
- SGIS 2025 행정구역 경계는 8자리 통계 코드와 공식 crosswalk를 확보한 경우에만 독립 비교에 쓴다. 서로 다른 원본 경계를 평균내거나 혼합하지 않는다.

## 공용 읽기 계약

- `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/diorama-manifest`
- `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/diorama-tiles/{tileStableId}`
- `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/display-overlays`

기존 계약은 manifest, 500m tile, 건물·도로, 출처·품질과 공개/후원 표시 overlay를 제공한다. 서버는 경계 판독, ENU 변환, 건물 귀속, 도로 절단, 결정적 hash, Mongo 불변 저장과 ETag를 담당한다. Unity Client·Decoder·Interpreter는 이를 읽기 전용 메모리 모델로만 유지한다.

## r4 구현 slice

- [구현 명세](administrative-dong-batch.implementation.r4.md)
- [역사 경계 후보 자료 구현 명세](administrative-dong-batch.data-implementation.v1.json)
- 자료 기반 scope catalog와 일괄 생성기는 `OA-22160`, 서울 `AL_D010`, 국가표준 NodeLink를 한 번씩 읽어 30개 모듈로 fan-out한다.
- 건물 계약 v1은 단일 외곽 ring만 지원하므로 hole이 있는 원본은 가장 넓은 외곽 ring만 표현 후보로 내보내고 생략 수를 감사자료에 기록한다. 원형 완전 보존으로 보고하지 않는다.
- NodeLink는 방향 링크 중심선이며 최대 100m 이하 두 점 선분으로 결정적으로 나눈다. 도로 폭·골목·보도·신호·통행 가능성은 별도 자료가 필요하다.
- 역사적 경계 결과는 로컬 `artifacts/local`과 Mongo candidate batch에만 저장한다. 기존 면목제3·8동 current pointer를 포함한 API current 자료는 변경하지 않는다.

## r4 구현·검증 결과

- `northeast-seoul-rider.r2`의 30개 행정동·12개 법정동을 모두 생성했다. 같은 입력을 다시 생성했을 때 변경 파일은 0개였다.
- 건물 61,897개를 하나의 행정동에 귀속했다. 미해결 27개는 경계 밖 13개, 원본 무효 형상 11개, 1mm 좌표 반올림 뒤 자기교차한 형상 3개이며 복수 귀속은 0개다. v1 외곽 ring과 원본 `PointOnSurface` 판정이 달라지는 2개는 원본 판정점을 명시적으로 보존했다.
- 방향 NodeLink 4,209개를 행정동 경계에서 절단해 행정동별 출현 4,620개, 100m 이하 2점 선분 11,769개로 만들었다. 397개 링크가 둘 이상의 행정동에 걸렸고, 게시한 1mm 경계 밖으로 1µm보다 많이 벗어난 선분은 0개다.
- C# 투영기는 Python 결과의 30개 경계를 독립 변환한 `OA-22160` 경계와 모든 꼭짓점에서 2mm 이내인지 확인한 뒤 건물 61,897개·도로 11,769개를 다시 조립해 500m tile 297개를 만들었다.
- 로컬 MongoDB candidate collection에 batch 1개·manifest 30개·tile 297개·overlay 30개, 합계 358개 문서를 저장하고 새 연결로 재조회했다. 같은 batch를 다시 적용했을 때 신규 0개·기존 일치 358개였으며 `administrative_dong_diorama_current` 사본 hash는 적용 전후 같았다.
- Python 실행 영수증은 Python 3.12.14, pyshp 2.3.1, pyproj 3.7.2, Shapely 2.1.2, GEOS 3.13.1, PROJ 9.5.1, EPSG DB v11.022를 고정한다. 도구나 의미 규칙이 달라지면 같은 자료라도 새 revision으로 만든다.
- 앞서 생성한 r1 로컬 산출물과 Mongo 후보는 삭제하거나 현행으로 승격하지 않고 `PreservedNotPromoted`로 보존한다. r4 정본 후보는 r2 산출물과 별도 candidate batch다.

세부 출처·행정동별 건물/도로/tile 수·hash·검증 상한은 [동북서울 30개 행정동 디오라마 역사 경계 후보 보고](../../../../Reports/동북서울-30개-행정동-디오라마-역사경계-후보-2026-09-14.md)에 기록한다.

## r5 기초 화면 기준선

- [깊이 기준·캡처 구현 명세](administrative-dong-depth-parity.implementation.r5.md)와 [관찰 표현 증거](administrative-dong-base-geometry-capture.presentation-evidence.v1.json)에 따라 역사 경계 후보 30개를 로컬 Unity 검토 묶음으로 결정적 내보냈다.
- canonical `SimulationWorldShell`을 저장하지 않고 한 번에 행정동 하나를 결합 Mesh 4개·Collider 0개로 조립했다.
- 실제 Play Mode Game View에서 전체 조망 30장과 면목제3·8동 건물 선택 근접 1장을 남겼다. 연락판에서 보이는 넓은 공백·분절은 생활상 완결이 아니라 `MissingCoverage`의 시각 증거다.
- 이 결과는 `G6 UnityBaseView / PrivateReviewOnly`만 충족한다. 현행 경계, 주소, 필지, 출입구, 이동 표면, 생활 자료와 NPC·운영 흐름은 검증하지 않았고 E 단계도 승격하지 않았다.

## r6 주소·필지 후보 첫 절편

- [주소·필지 후보 구현 명세](administrative-dong-address-parcel-candidate.implementation.r6.md)와 [자료 구현 명세](administrative-dong-address-parcel-candidate.data-implementation.v1.json)에 따라 61,897개 건물 전부의 PNU·필지 주소 후보 상태를 범용 공공자료 원장에 `PendingHumanReview`로 저장했다.
- 단일 후보 49,876개, 복수 후보 1,351개, 동결 판본 내 후보 없음 10,670개이며 후보 보유율은 82.76%다. 동일 입력 재적용 신규·갱신 0, 별도 연결의 exact set·분포·투영 hash 재조회가 통과했다.
- 이것은 `G2a`만 닫는다. 주소 문자열을 공개·Unity 계약에 내보내지 않으며 확정 건물 주소, 실제 필지 도형, 건물 출입구, 배달 목적지나 가격 권위를 만들지 않는다.

후속 r5·r6의 수치·화면·경고·권위 상한은 [30개 행정동 깊이 기준선 및 주소 후보 결과](../../../../Reports/동북서울-30개-행정동-디오라마-깊이-기준선-및-주소후보-2026-09-14.md)에 기록한다. r4 보고서는 역사 경계 후보 생성·Mongo 저장 당시 기록으로 보존한다.

## r8 횡단보도·보행등 설치 후보 G3a

- [정정 구현 명세 r8](administrative-dong-crosswalk-candidate.implementation.r8.md)과 [자료 구현 동반 명세](administrative-dong-crosswalk-candidate.data-implementation.v1.json)에 따라 서울시 `OA-23081` 2026-08-24 파일의 횡단보도 점을 30개 역사 경계 후보에 결속했다. 이 자료 후보는 `evidenceStageClaimed=null`이며 후속 Unity·통행·Simulation 소비에는 별도 등록된 `E1~E7 상호작용 수직 검증 명세`가 필요하다.
- 원본 21,776행 가운데 좌표 있음 21,775·결손 1·대상 30개 동 단일 귀속 1,533·범위 밖 20,242·복수 귀속 0이다. 30개 동 모두 후보가 있고 보행등 설치 관측은 있음 933·없음 600이다.
- 원본 자치구와 역사 경계의 귀속 자치구가 다른 21건은 정상 자료로 숨기지 않고 `SourceDistrictSpatialAssignmentConflict` 진단, 두 자치구와 경계 거리 `0.286~22.076m`를 후보 hash에 포함했다.
- 공식 사가정역 WGS84 기준점과 횡단보도 점의 `8.0946m` 대조가 EPSG:5186 해석을 지지하지만 원천 페이지·시트가 좌표계를 선언하지 않으므로 `EmpiricallyValidatedCandidateNotSourceDeclared`를 유지한다.
- 정정 후보 집합 SHA-256은 `78312E5F0DDB89BFFA9AD1881379CBF7E8037A6C6454D4F0ABDF2A3E4D22D30A`다. 경계 원본, 실제 scope, 설계, 공식 역 기준점, 좌표 검증과 생성기를 모두 hash 계보에 넣었고 Python 자체 시험 17/17·2회 결정성 생성·독립 재감사가 통과했다.
- 첫 r7 구현 결과였던 `g3a.r1`은 독립 감사 뒤 `RejectedAfterIndependentAudit / PreservedNotPromoted`로 보존한다. 정정판은 새 `g3a.r2` dataset/revision으로만 소비하며 과거 자료를 삭제·갱신·승격하지 않는다.
- 로컬 MySQL 범용 공공자료 원장에는 정정 후보 1,533행과 원천·계보 사본 13건을 별도 판본으로 저장했다. 반복 적용은 신규·갱신 0이고 별도 연결에서 exact set·30개 동 분포·충돌 21·후보 hash를 재조회했다.
- 이 절편은 점 위치와 보행등 설치 유무의 비공개 검토 원장일 뿐 횡단보도 면, 정지선, 보도 연결, 신호 현시·주기, 주행 차로·통행 graph 또는 Unity 표시를 만들지 않는다. 따라서 새 생활상 Game View는 촬영하지 않고 r5의 기초 화면을 현행 비교 기준으로 유지한다.

## r9·r12 지역 사업장 후보 G4a와 개인정보 경계 정정

- [첫 구현 명세 r9](administrative-dong-business-candidate.implementation.r9.md)의 `g4a.r1`은 소상공인시장진흥공단 2026-06-30 전국 상가 파일 16개·2,772,484행을 전수 확인하고, 서울 554,092행에서 정확한 30개 원천 행정동 코드의 사업장 29,721곳과 음식 대분류 `I2` 8,246곳을 별도 후보로 만들었다. 그러나 독립 개인정보 감사에서 선택된 원천 식별자 하나가 추적 생성기의 시험 fixture에 들어간 사실을 확인해 `RejectedAfterIndependentPrivacyAudit / PreservedNotPromoted`로 낮췄다. 그 값은 문서·보고에 반복하지 않으며 r1 산출물·DB 29,721행·사본 19건·실행 19건은 삭제하거나 갱신하지 않는다.
- [정정 명세 r12](administrative-dong-business-candidate.implementation.r12.md)와 [r2 자료 구현 명세](administrative-dong-business-candidate.r2.data-implementation.v1.json)는 실제 원천 집합에 없는 명시적 합성 fixture, 생성기·집계 산출물의 선택 식별자 교집합 0 관문, 새 dataset/revision·범위·세대·hash와 r1 불변 digest를 요구한다. [r1 거절 자료 구현 기록](administrative-dong-business-candidate.r1.data-implementation.v1.json)은 불변 이력이며 어느 문서도 Unity·운영·게임 소비를 승인하는 상호작용 수직 검증 명세가 아니다.
- 좌표·도로명주소·지번주소 결손은 0, 건물관리번호 결손은 67곳이며 고유 건물관리번호는 11,931개다. 역사 경계와 일치 29,686·다른 동 29·범위 밖 6·복수 경계 0을 재귀속이 아닌 진단으로 보존했다.
- 상가업소번호는 29,721개 모두 고유하다. 같은 원문 상호명·도로명주소를 가진 서로 다른 ID 236그룹·490행은 자동 병합하지 않는다. 기존 면목동 SEMAS 5,411행과 공장 126행은 그대로 보존하고 새 범위의 추가 ID 24,310개를 별도 Source/Dataset/Revision으로 저장했다.
- 정정 r2 후보 집합 SHA-256은 `5F9C61BD71112C69EE3988CFB3B8542AE22BF60AB95D542C702D2355B8A3774C`다. Python 자체 시험 25/25·두 번째 생성 변경 0·verify, C# 자체 시험 59,482/59,482를 통과했다. 로컬 MySQL 보호 원장에 r2 후보 29,721행과 원천·계보 사본 19건을 새로 저장했고 반복 적용은 신규·갱신·사본 0이다. 별도 연결에서 exact set·30개 동·음식 8,246·진단 35·결손 67·보호 필드 14개를 재조회했으며 r1 상태 digest와 기존 면목 5,411·공장 126행은 적용 전후 같았다. 독립 재감사는 tracked 10,757개·untracked 134개와 r2 집계 산출물을 선택 원천 식별자 29,721개와 교차 검사해 노출 0, High 0·Medium 0으로 판정했다.
- 정확 상호명·주소·좌표는 Git 무시 local-private 세대와 RDB 보호 필드에만 둔다. 현행 경계, 확정 건물·출입구, 현재 영업, 상인 Claim과 배포 승인이 없으므로 Unity 업체 아이콘·메뉴·주문 가능·광고·NPC 목적지는 만들지 않는다.

## r10·r11 교차로 점 후보 G3b

- [교차로 점 생성 명세 r10](administrative-dong-intersection-point-candidate.implementation.r10.md)과 [비-E7 자료 구현 명세](administrative-dong-intersection-point-candidate.data-implementation.v1.json)에 따라 서울시 `OA-15534`의 원천 점 도형 8,097건을 읽고 30개 역사 경계 후보에 단일 귀속한 교차로 554건을 별도 세대로 만들었다. 원천 자치구 일치 553·충돌 1·범위 밖 7,543·복수 귀속 0이며 30개 동 모두 후보가 있다.
- 교차로 자체 점으로 먼저 귀속한 뒤 G3a 횡단보도 ID는 진단 관계로만 연결했다. 교차로 524건은 횡단보도와 연결되고 30건은 연결되지 않으며, 관계 1,491개 중 같은 역사 행정동 1,271·다른 행정동 220개다. 횡단보도 귀속을 교차로에 복사하지 않는다.
- 후보 집합 SHA-256은 `DB83F1CC82EB65F864188DD790A003EDF570DBB459D56D4C07CE690988F2837B`다. Python 자체 시험 15/15·두 번째 생성 변경 0·verify·독립 hash 재계산을 통과했다.
- [교차로 보호 원장 명세 r11](administrative-dong-intersection-point-ledger.implementation.r11.md)과 [비-E7 동반 계약](administrative-dong-intersection-point-ledger.data-implementation.v1.json)에 따라 로컬 MySQL에 후보 554행과 계보 사본 19건을 저장했다. C# 자체 시험 16/16, 별도 연결의 exact set·30개 동·후보 hash 재조회, 반복 적용 신규·갱신·사본 0을 통과했다. 적용 전후 G3a r2 1,533행·사본 13건, G4a r1 29,721행·사본 19건, 기존 면목 5,411·공장 126행의 상태 digest는 같았다. 이후 별도 생성한 G4a r2는 r11 보존 계약에 포함하지 않으며 후속 G3b 판본에서 명시적으로 결속해야 한다.
- 교차로 점은 접근 방향, 도로 연결 위상, 정지선, 제어기, 차로 또는 신호 현시·주기를 증명하지 않는다. 따라서 G3a와 G3b를 가까운 점끼리 이어 실제 통행 graph로 만들거나 Unity에 신호등을 배치하지 않는다.

## r13 도보 네트워크 후보 G3c

- [구현 명세 r13](administrative-dong-walk-network-candidate.implementation.r13.md)과 [비-E7 자료 구현 계약](administrative-dong-walk-network-candidate.data-implementation.v1.json)에 따라 서울 열린데이터광장 `OA-21208`의 2020년 기준 WGS84 도보망을 광진구·동대문구·중랑구 공식 CSV 59,724행과 유형 코드북으로 동결했다.
- 모든 CSV 행의 자치구 코드·명과 공식 필터, 코드북 링크 16개·노드 4개 의미를 검사했다. 현재 원본에는 `RONUM`이 없으므로 파일 역할과 1부터 시작하는 CSV data-row 번호를 occurrence로 삼아 NODE와 LINK 모두 완전 동일행을 덮어쓰지 않게 했다.
- `OA-22160` 2023-10-31 역사 경계에 점을 귀속하고 선을 절단해 30개 동에 NODE 17,267개와 LINK fragment 23,472개, 합계 40,739개 후보를 만들었다. 범위 밖 원본은 19,770행이고 원천 자치구와 공간 귀속 충돌 후보는 48개다.
- 후보 집합 SHA-256은 `43248F7F563DCDFE059650F3EC000085CD0D7CFB65FC5300EFDFCBDD685990AA`다. 1mm 직렬화 좌표의 역사 경계 재포함, 동일 행정동 안 조각 중복 부재, 원선 대상 교차 길이 882,519.308089m 보존과 고정 field/framing hash를 검사했고 Python 자체 시험 17/17·재생성·verify가 통과했다.
- C# importer가 전체 98,781,674-byte 후보 파일을 읽어 framed 후보 hash를 독립 재계산하고 로컬 MySQL에 40,739행과 계보 사본 17건을 저장했다. build 경고 0·오류 0, 자체 시험 16/16, 새 연결의 30개 동 exact set·후보 hash 재조회가 통과했고 반복 적용은 신규·갱신·사본 0이다. G3a r2·G3b r1·G4a r2·사가정 r27 OA-21208을 합한 보호 상태 71,501행·사본 84건·실행 84건의 digest는 적용 전후 같았으며 최종 독립 정적 감사는 High·Medium·Low 0이다.
- 이 자료는 2020 역사 관측이고 세 자치구 밖 boundary halo가 없으며 보도 폭·연석·출입구·현재 통행·오토바이 허용·차로·방향·신호가 없다. Mongo/current/API/Unity/이동 graph·NPC 경로 권위로 승격하지 않는다.

## r15 사가정 깊이 격차 조사와 수집 대기열

- [사가정 깊이 격차·30개 행정동 수집 대기열 r15](sagajeong-depth-gap-and-collection-backlog.proposal.r15.md)은 사가정역 1km의 실제 검증 조각과 30개 동 공통 원장을 같은 관문으로 다시 대조한다.
- 30개 동은 이미 건물·기초 도로, 주소·PNU 후보, 횡단보도 점, 교차로 점, 2020 도보망, 비공개 사업장 후보와 Unity 기초 화면을 갖는다. 초기 r5의 `G3/G4 공통 자료 없음` 표는 당시 기준선이며 현행 공통 상태가 아니다.
- 사가정역에서만 더 확보한 차선·방향표시·신호 시설, 역 출구, 엘리베이터·공원·버스정류소·가로수와 화면 건물 주소 원장을 30개 동 공통 수집 후보로 분리했다.
- 양쪽 모두 부족한 현행 행정동 경계, 실제 필지 도형, 건물 출입구, 정확 보도면·연석·정지선·신호 현시와 현재 통행 graph는 추측하거나 복사하지 않는다.
- 다음 수집 순서는 `A 공간 정본 → B 이동 표면·신호 관계 → C 지역 생활 장소 → D 시간대별 생활 관측 → E API·Unity 적용`으로 고정한다. r15는 조사·기획 기록이며 새 원본 수집, DB 저장, API, Unity 실행·캡처 또는 E 승격을 수행하지 않았다.
- r14 생활인구 후보는 문서와 scope는 있으나 지정 생성기·완결 산출물·RDB 독립 재조회가 확인되지 않아 `PlannedNotGenerated`로 판정한다.

## r16 현행 경계·건물·출입구 첫 수집 관문 고정

- 사용자는 [현행 경계·건물·출입구 수집 관문 r16](current-boundary-building-entrance-collection-gate.decision.r16.md)의 순서를 첫 실제 자료 수집 절편으로 고정하고 디오라마 증거 체계에 포함하도록 승인했다.
- 관문은 정확 30개 scope에 `TL_SCCO_GEMD 현행 행정동 경계 → 주소 건물·건물군 → 건물·건물군 출입구` 순서를 요구한다. 세 자료가 한 파일이라는 뜻은 아니며 하나의 영수증 묶음 아래 원본별 판본·hash·CRS·이용조건을 보존한다.
- 증거 대장 `station-diorama-evidence-rules.r4`에 `AdministrativeAreaCollectionGate` 1개와 요구사항 3개를 추가했다. 모두 `NotCollected / Unassessed / Blocked`, `applicationAuthorized=false`, `currentPublicationAllowed=false`다.
- 이 고정은 수집 순서와 완료 검사의 승인일 뿐 실제 다운로드·DB 저장·current 게시·Unity 적용·통행·gameplay 또는 E 승격이 아니다. 건물군·출입구의 정확한 제품 식별자는 공식 제공 목록을 실제 확인할 때 확정한다.

## r17 디오라마 E1~E10·Codex 사용 적합성 감사

- [E1~E10·Codex 사용 적합성 감사 r17](diorama-e1-e10-codex-audit.proposal.r17.md)은 현행 E 정의·작업 명세 manager·E8 이후 캠페인·디오라마 보조 대장을 실제로 대조했다.
- E1~E10의 누적·상향식 의미 구조와 디오라마 보조 대장의 비승격 경계는 적절하다. 다만 현행 E7 공통 회귀의 오행 출력 v2/v3 불일치, E8 안정성 캠페인 1개 누락, 정본 E7 작업 명세 63개 중 12개 실패가 확인되어 저장소 전체의 기계 정합성은 미완료다.
- 행정동 디오라마의 과거 `.e7-work-order.json` 3개는 모두 `protocolRevision` 부재로 현행 manager가 거절한다. 기존 자료·화면 구현 증거는 보존하지만 E 작업 명세나 E7 승격 근거로 사용하지 않는다.
- Codex는 자료층·관찰 표현·플레이 상호작용을 먼저 구분하고, 현행 manager와 정본 등록을 통과한 `PlayableInteraction`에만 E단계를 계산해야 한다. 이번 r17은 감사·제안이며 검사기·원장·Unity를 수정하지 않았다.

## r18 E1~E10 결과물 계약·명칭 정비

- [결과물 계약·명칭 정비 구현 r18](evidence-stage-output-and-name-migration.implementation.r18.md)에 따라 사람이 읽는 `E7 작업 명세`를 `E1~E7 상호작용 수직 검증 명세`로 바꾸고 새 접미사를 `.interaction-e1-e7-validation.json`으로 정했다. 기존 식별자·script·`.e7-work-order.json`은 호환 경로로만 유지한다.
- E1~E10 각각에 필수 결과물 종류·최소 내용·그 결과만으로는 충분하지 않은 조건을 `evidence-stage-output-contract.r1`로 기계화했다. 모든 결과물은 주체·궤적·후보 revision·hash·증거·상태·차단·무효화 조건을 갖고 상위 단계는 같은 계보의 하위 결과를 소비한다.
- 행정동의 과거 자료·캡처 파일 5종은 자료 구현 기록 또는 관찰 표현 증거로 재분류하고 `evidenceStageClaimed=null`을 명시했다. 횡단보도 과거 경로 하나는 생성기 hash를 보존하는 읽기 호환 입력으로 원문 그대로 남기며 E 근거로 사용하지 않는다.
- 오행 출력 v3, 누락 E8 캠페인, 구성원·PlayableUnit 단계 검사 드리프트를 복구해 정본 63개 중 61개가 현행 manager를 통과한다. 남은 2개는 실제 WI·PlayableUnit 관계 또는 집계 단계가 맞지 않아 임의 수정하지 않고 차단으로 남겼다.
- r18은 증거 명칭·결과물 계약·검사기·원장 정합성 정비다. 자료 수집·DB 쓰기·Unity 실행·Game View·실제 입력·E 승격은 수행하지 않았다.

## 이전 r3 증거의 재해석

- 기존 면목제3·8동 current projection의 건물 250·도로 선분 722·타일 3은 사가정 1km 참고 지도 602개 건물을 면목동 6개 역사적 경계에 귀속한 좁은 자료다.
- 과거 문서가 `OA-22160` 포털 갱신일을 경계 판본으로 기록해 `ActualAdministrativeDongDataGate=Closed`라 한 부분은 실제 ZIP 내부 증거와 맞지 않는다. 현행 경계 관문은 다시 열어 `WaitingForAdministrativeBoundary`로 정정한다.
- 서울시 `OA-14991` 생활인구 4,464건은 2016 행정동 구역 기준의 2026년 7월 통계 관측이다. 현행 경계 주민 수나 NPC 생성 권위가 아니다.

## 미정·후속

- 최신 행정안전부 `TL_SCCO_GEMD` 서울 전체분 승인 취득과 30개 경계 재생성·독립 SGIS 대조.
- 필지 경계 `AL_D002`, 30개 행정동 건물의 확정 건물관리번호·출입구와 시설 자료 수집. PNU·필지 주소 후보 상태는 r6에서 전수 기록했고 사업장 29,721곳은 정정 r12의 비공개 G4a r2 보호 원장으로 저장했지만 건물·출입구 결속과 현재 영업 확인은 남아 있다.
- 건물 hole·multipart, 도로 폭·차로·보도·골목·신호를 보존할 v2 geometry·mobility 계약. 횡단보도 점 G3a·교차로 점 G3b·도보 네트워크 G3c의 비공개 후보 원장은 검증했지만, 접근 방향·정지선·제어기·신호 현시와 현행 통행을 같은 판본으로 결속한 graph는 없다.
- current 게시 승인, 행정동 선택 UI, 실제 Prefab/Scene 저장 결속. 역사 경계 후보의 runtime-only `SimulationWorldShell` 조립과 Game View는 r5에서 비공개 검토 범위로만 확인했다.
- current 승격 전 manifest·overlay의 시간·좌표 프레임·범위·권위 플래그까지 포함하는 전체 canonical hash와 중복 문서 exact-body 검증.
- Claim·후원 Campaign HTTP API, 결제·정산·실제 광고 판매. 현재 광고 표시는 0건이다.

## 검증 상한

r19의 현재 최대 구현 증거는 여전히 `HistoricalBoundaryCandidateBatchStoredAndVerified / G2aAddressParcelCandidateLedgerValidated / G3aCrosswalkPointCandidateLedgerValidated / G3bIntersectionPointCandidateLedgerStoredAndVerified / G3cWalkNetworkCandidateLedgerStoredAndVerified / G4aCorrectedPrivateBusinessCandidateLedgerStoredAndVerified / G6UnityBaseViewGameViewVerified / CurrentPublicationBlocked`다. r18의 `EvidenceTerminologyMigrated / StageOutputContractMachineValidated / LegacyPathsCompatibilityOnly / CommonEvidenceRegressionsMostlyRestored`를 보존했고 r19는 우선순위와 현장 관찰 증거 순환만 더했을 뿐 자료·Unity 구현 증거를 승격하지 않는다. 현행 행정동 경계, 확정 주소·필지·출입구, 현재 이동 graph·정지선·신호 현시, 공개 사업장 결속, 실제 통행, 운영 주문·배차와 생활상 Game View는 증명하지 않는다.

## 다음 질문 하나

첫 현장 촬영 묶음은 사가정역 1~4번 출구·주요 교차로·대로변·사가정시장 입구처럼 공개 장소의 방향 기준점을 먼저 모으고, 주거 골목·개별 건물 출입구는 개인정보·안전 기준을 적용한 두 번째 묶음으로 넘길 것인가? 추천은 `공개 장소 방향 기준점부터 진행`이다. 현행 `TL_SCCO_GEMD` 수집 관문은 이 촬영과 별개로 계속 열린 상태를 유지한다.
