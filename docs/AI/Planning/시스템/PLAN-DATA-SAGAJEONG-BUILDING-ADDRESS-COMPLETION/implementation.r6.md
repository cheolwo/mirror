# 사가정 화면 건물 결속·주소 증거 첫 절편 구현 r6

- 기준 기획: `PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION` 첫 절편 승인 r5, 구현 결과를 반영한 현행 r6
- 대상 역: `station:kr:kric:s1107:0722`
- 구현 상한: 비공개 로컬 자료 준비·RDB 저장·Development 관리자 조회
- 공개·Unity·배달·가격·업체·게임플레이 권위: 모두 `false`

## 구현 결과

사가정역 1km 표현 자료의 화면 건물 4,062개와 기존 기준 건물 602개를 한 원장으로 합치지 않고 다음 두 불변 자료로 분리했다.

| 자료 | 판본 | 내용 hash | 결과 |
| --- | --- | --- | --- |
| 화면–기준 건물 결속 | `sagajeong-presentation-building-binding.r1` | `7B47519C1FC9D4D89A04B734710F48FE64BFE8C0EEC9264C947D5DC6206D1411` | 화면 건물 4,062, 기준 건물 602, 유일 결속 544 |
| 화면 건물 주소 상태 | `sagajeong-presentation-building-address-assignment.r1` | `9908B700914DC9CB9AB22592E96A5A699FA3AE108F90063DB010CC9C8B870C33` | 4,062/4,062에 주소 판정 상태와 근거 기록 |

결속 결과는 `Bound 544 / BackdropOnly 3,423 / AmbiguousGlobal 88 / AmbiguousMultiple 2 / WeakCandidate 5`다. 기준 건물은 `Bound 544 / Unresolved 58`이다. 이 결속은 화면 도형과 기준 도형 사이의 자료 관계이며 실제 업체 입점·출입구·이동 가능성을 뜻하지 않는다.

주소 판정 결과는 다음과 같다.

| 상태 | 수량 |
| --- | ---: |
| `ParcelAddressCandidate` | 3,278 |
| `ReferenceBindingCandidate` | 516 |
| `MultipleAddressCandidates` | 47 |
| `CrossSourceConflict` | 18 |
| `Unresolved` | 203 |
| 합계 | 4,062 |

원천 단계의 주소 후보 분포 `단일 3,796 / 복수 60 / 없음 206`과 최종 판정의 `Unresolved 203`은 같은 수치가 아니다. 전자는 PNU·관련지번에서 나온 원천 후보 수이고, 후자는 기준 건물 결속까지 함께 판정한 최종 상태다. 공식 주소로 승격한 화면 건물은 0개이며 최근접 주소 복사도 0개다.

## RDB 저장과 독립 재조회

기존 `외부데이터정규화Record`를 재사용해 새 migration 없이 서로 다른 dataset으로 저장했다.

| dataset | 정규화 행 | 정규화 자료 hash |
| --- | ---: | --- |
| `sagajeong-presentation-building-binding-ledger` | 5,211 | `2426E8ABB083EFA3BA07B06675584768ECD1A30E97436E6CE1AED4A4ABA1CA47` |
| `sagajeong-presentation-building-address-ledger` | 7,582 | `07AF37D600F0B0DC080847F6E65FC1738AFFC827ED333E3ED173A6DC9758F20F` |

로컬 Docker MySQL `hongdal_dev`에 원본 snapshot 2개와 정규화 행 12,793개를 저장했다. 원본 snapshot ID는 111·112, 정규화 row ID 범위는 97,787–110,579다. 새 읽기 문맥에서 정확한 dataset·지역·key 집합, 원본 snapshot 연결, 자료 hash를 다시 읽고 같은 입력의 재적용·재생에서 신규 쓰기 0을 확인한다. 모든 행은 `PendingHumanReview`이며 공개·배달·가격·업체·Unity 사용 플래그는 false다.

정규화 자료는 기존 `TextValue` 2,000자 제한을 넘지 않도록 manifest·출처 영수증·건물 행·주소 후보 목록을 독립 행으로 나눴다. 최대 길이는 1,845자다.

## 비공개 읽기 계약

다음 station-scoped 읽기 경계를 추가했다.

- `GET api/v1/admin/world/stations/{transitStationStableId}/diorama-building-evidence/manifest`
- `GET api/v1/admin/world/stations/{transitStationStableId}/diorama-building-evidence/presentation-building-bindings`
- `GET api/v1/admin/world/stations/{transitStationStableId}/diorama-building-evidence/presentation-building-addresses`

서버 관리자 정책과 Development 환경을 동시에 요구한다. 사가정 이외의 역, 결손·변조된 원장, 원본 snapshot·hash·시각·단위·제한·원장 전체 hash가 맞지 않는 자료는 응답하지 않는다. 응답은 비공개 cache 지시와 `ETag` 재검증을 사용하고, 자료 오류를 sample fallback으로 숨기지 않는다. 기존 공개 역세권·행정동 API와 Unity 계약은 변경하지 않았다.

## 디오라마 증거 필수 관문

역별 `StationEvidenceProfile`이 아래 세 항목을 정확히 한 번씩 갖도록 기계 대장과 검사를 확장했다.

| 증거 종류 | 사가정 현재 판정 | coverage | 적용 승인 |
| --- | --- | --- | --- |
| `RoadAddress` | `Collected` | 4,062/4,062 판정, 원천 후보 단일 3,796·복수 60·없음 206 | false |
| `ParcelIdentifier` | `Collected` | PNU 4,062/4,062, 고유 PNU 3,774 | false |
| `ParcelGeometry` | `NotCollected` | 고유 PNU 3,774건의 실제 경계 도형 0 | false |

`collectionState`, `coverageState`, 출처 영수증, 판본·hash와 `applicationAuthorized`를 각각 검증한다. 주소나 PNU를 가졌다는 이유로 필지 도형을 수집했다고 판정하지 않으며, 건물 도형을 필지 경계로 대체하지 않는다. 면목·용마산은 아직 세 항목 모두 `NotAssessed / Unassessed`다.

이 체크리스트는 이후 역세권 디오라마 자료 검토의 필수 정책으로 확정했지만, 기존 보편 규칙 12개는 계속 `Candidate`이고 `ProvisionalSharedRule`·`AcceptedSharedRule` 또는 E 증거로 승격하지 않았다.

## 검증

- 결정적 생성기: 두 번 생성한 파일의 byte·hash 일치, 변조 2종 거절, 임시 파일 잔류 0
- 생성·관리 검사: Build·Validate·Summary와 전용 회귀 검사 통과
- 반입기 자체 검사: 30건 통과. 부분 preflight, 예상 밖·중복 행 거절, 정확 key 집합·수와 3,514개 도로명주소 stable ID 공식 재계산을 포함
- 로컬 MySQL: 12,793행 저장·새 문맥 독립 재조회·재적용·재생의 신규 쓰기 0
- 서버 집중 시험: UseCase·Controller 15건 통과. 주소 본문뿐 아니라 정규화 행 수·단위·시각·원본 snapshot 연결, 원본 파일 hash·길이와 도로명주소 stable ID 공식 변조 거절을 포함
- 실제 DB 조회 probe: projection hash `6D4223B7239FAD52260A80DECD94E17F42D31B9DAD5DBD07F654854F882A4676`, 화면 건물 4,062, 결속 544, PNU 4,062·고유 3,774, 주소 후보 건물 3,856, 최종 미해결 203, 필지 도형 미수집, 모든 권위 false
- API 판본 회귀: 56건 통과
- 문서까지 포함한 최종 범위 지정 Fast: `artifacts/local/validation/20260914-172712`
- 범위 지정 Task: solution build는 통과했고 전체 시험은 5,250건 통과·이번 범위 밖 기존 작업 7건 실패. 기록은 `artifacts/local/validation/20260914-165846`
- 반입 상세 기록: `artifacts/local/validation/sagajeong-presentation-building-evidence-import-20260914-r1/README.md`

자동 시험과 로컬 DB 재조회까지가 이번 증거다. canonical `SimulationWorldShell`, Scene·Prefab, Play Mode·Game View, 실제 배달 경로, 가격·업체 연결과 공개 배포는 검증하거나 변경하지 않았다.

## 남은 관문

1. 신청형 도로명주소 건물군 도형과 집합건물 상세주소 동 도형을 확보해 현재 후보를 공식 건물 관계로 검토한다.
2. 연속지적도 등 실제 필지 경계 도형을 원본 CRS·판본·hash와 함께 수집하고 3,774개 PNU coverage를 감사한다.
3. 주소 충돌 18건, 복수 후보 47건, 미해결 203건의 사람 검토 우선순위를 정한다.
4. 공개·Unity 사용은 권리·개인정보·자료 품질 관문 뒤 별도 승인한다.
5. 역 Graph Map schema와 현행 인계 도구의 호환 차단을 별도 해결한다. 이번 자료 절편을 `Integrated` 또는 `ReadyForDevelopment`로 확대하지 않는다.

commit·push는 수행하지 않았다.
