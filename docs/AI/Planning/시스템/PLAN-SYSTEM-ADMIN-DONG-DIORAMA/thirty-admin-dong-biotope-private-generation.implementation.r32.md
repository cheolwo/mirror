# 30개 행정동 비오톱 polygon 비공개 후보·Unity 검토 번들 생성

[구현 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r32]

- 상태: `Implemented / 2025BiotopeRevisionOnly / Historical30DongPrivateCandidatesGeneratedAndVerified / Historical30DongReviewBundleV5Generated / UnityAppliedAndCaptured / CurrentPublicationBlocked`
- 기준: [방향표시·비오톱 공식 원본 감사 r30](thirty-admin-dong-direction-biotope-source-audit.implementation.r30.md), [공통 깊이 슬롯 r24](thirty-admin-dong-common-depth-slots.implementation.r24.md)
- 공식 자료: [서울 열린데이터광장 OA-21145](https://data.seoul.go.kr/dataList/OA-21145/A/1/datasetView.do)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 구현 결론

`OA-21145`의 2025년 기준 비오톱 유형·평가도 polygon을 `OA-22160`의 2023년 역사 행정동 경계와 교차시켜, 30개 동별 비공개 관찰 후보 3,181개를 생성했다. 3,408개 polygon part의 외곽 ring과 내부 구멍 108개를 보존하고 공통 ENU millimeter ring으로 기록했다. 후보 stable ID는 원천 고유번호·원본 순번·동 코드·판본을 SHA-256으로 변환해 만들며, 원천 고유번호 자체는 출력하지 않는다. 분류는 공식 유형 코드·평가 등급·유형 범례의 최소 필드만 싣는다.

이는 2023 경계에 자른 2025 자료의 **로컬 비공개 관찰 후보**다. 현재 생태 상태나 현행 행정동 범위의 확정 자료가 아니다. 원본이 무효인 polygon을 임의로 수리하거나 주변 등급으로 빈 공간을 채우지 않았다.

## 원본·경계·판본 고정

| 입력 | 판본·경로 | SHA-256 |
| --- | --- | --- |
| 비오톱 ZIP | `artifacts/local/public-data/admin-dong-biotope-20260926-r1/raw/비오톱유형_평가도(2025년기준).zip` | `7FF3802116EC35BECABAAD8F5E8401C6AF8FC8C56FE1DFB526F39BF154657663` |
| 획득 receipt | `artifacts/local/public-data/admin-dong-biotope-20260926-r1/raw/receipt.json` | `CDF352945CF221B2850BC540D129396E32875250D8E1E8330DA78FED8E72AE0A` |
| 역사 경계 | `artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip` | `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68` |

비오톱 SHP·DBF는 각 42,544건, 좌표계 `EPSG:5174`, 인코딩 windows-949다. 경계는 `OA-22160`의 2023년 판본이며 현재 행정동 정본이 아니다. 공식 ZIP·receipt 해시와 `UPIS_BIOTOP_TYP_2025` 내부 구성, 좌표계를 검사해 2022년 `UPIS_BIOTOP_TYP.zip` 혼합을 차단한다. 공식 이용허락은 공공누리 제1유형이고, 원본은 법적 효력이 없는 참고자료다.

## 생성기와 로컬 세대

- 생성기: `eng/neighborhood/administrative_dong_biotope_private_review_r1.py`
- 생성기 SHA-256: `D070A131E9B4ED0AAA880B5726AA843EE9679908E0CDF5227078F288A02743B6`
- 산출 경로: `artifacts/local/validation/admin-dong-biotope-private-review/r1/`
- manifest SHA-256: `33347E9C4E8FBBF12A7C00E0BD09CE5898C68B4165F65244013EE3D3643609E2`
- 파일: 행정동 bundle 30개, `audit.json`, `manifest.json`, `complete.json`의 총 33개
- manifest 파일 기술서: 31개(행정동 bundle 30개와 audit 1개). `manifest.json`과 `complete.json`은 기술서에 자신을 넣지 않고 completion의 manifest hash로 결속한다.

출력은 Git 제외 `artifacts/local/`에만 있다. 기존 공개 overlay, DB, Mongo, current pointer 또는 Unity 자산에 원본 자료를 복사하지 않았다.

## v5 Unity 검토 입력 결속과 실제 화면

결속 생성기 `eng/neighborhood/administrative_dong_biotope_unity_review_r5.py`의 SHA-256은 `6D376489BFD03E0A5D32F561C54F905A341BD0D9492C76A2D132967CEB5F71E9`다. 기존 방향표시 v4 검토 세대에 비오톱 외곽·내부 ring outline만 중첩했다. 최종 불변 generation은 `5207BA41C67B6BA93182CFA2485F1CD89A0AF2964EC97E55D8D4A988E77C646E`, index SHA-256은 `951D8E361873ACFFE1EB162845F3E59F9428A6DF1F71FF69813F9947483CD7E6`이다. 생성 파일 34개와 completion descriptor 33개를 결속했고 기존 v1~v4 자료를 보존했다.

Python·일반 .NET과 Unity Mono가 JSON의 `0.0`을 내용 hash로 정규화하는 방식이 다름을 실제 실패로 발견했다. 생성기는 직렬화된 JSON을 Decimal로 다시 읽고 Unity Mono와 같은 zero 정규화를 적용하도록 수정했다. 자체 시험 53개와 build·verify·동일 입력 재생성 `changedFiles=0`을 통과했고, Unity Mono와 같은 실행 환경에서 30개 bundle·30개 비오톱 overlay·index·두 audit·completion의 내용 hash를 독립 재검산했다.

Unity `6000.5.6f1`의 실제 Play Mode·Game View에서 overview 30장과 면목제3·8동 detail 1장을 캡처했다. 증거 폴더는 `C:/Users/user/ssalddel/Documentation/Changes/2026-09-26-administrative-dong-private-biotope-r5-5207/`이다. `administrative-dong-diorama-gameview-evidence.v5` manifest와 PNG 31개 hash의 불일치는 0개다. 비오톱 후보 3,181개·part 3,408개·내부 ring 108개·ring 점 105,731개, 원본 선분 102,215개·1mm 이하 생략 128개·표시 102,087개가 그대로 결속됐다. 채운 polygon이나 새 후보별 GameObject 없이 기존 2x2 결합 Mesh에 정점 408,348개·인덱스 612,522개를 추가했다.

모든 모듈은 Renderer 4·MeshFilter 4·Collider 0이고, 전체 실제 Mesh 정점은 2,692,752개·인덱스는 4,246,746개다. 모듈 최대 정점은 176,695개, batch 최대 정점은 88,944개다. 집중 Unity EditMode 시험은 32/32 통과했고 XML SHA-256은 `77BC7AA14E37C4519AE284FCBCCE8D21754F1B1E4136B732FB7E8F2E920A18E8`이다. canonical Scene SHA-256은 실행 전후 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 같고 Scene은 저장되거나 dirty 상태로 남지 않았다.

실행 중 기존 canonical Scene의 Console 오류 8건을 다시 관찰했으며 첫 오류는 `SimulationConflictException: SimulationReplayHashMismatch`였다. 따라서 이 결과는 비오톱 외곽선 표현·Mesh 예산·Scene 불변의 실제 화면 증거이고 Console 0이나 서버·DB·Mongo 통합 증거가 아니다.

## 도형·격리·면적 감사

| 항목 | 결과 |
| --- | ---: |
| 원본 SHP·DBF 행 | 42,544 |
| 비어 있지 않은 도형 | 42,543 |
| 빈 도형 격리 | 1 |
| 범위 안 자체 교차 원본 도형 격리 | 3 |
| 역사 행정동 후보 coverage | 30 / 30 |
| 최종 polygon–동 후보 | 3,181 |
| polygon part / 내부 구멍 | 3,408 / 108 |
| 교차 직후 면적 합 | 32,047,562.146602㎡ |
| millimeter ring 재구성 면적 합 | 32,047,562.146600㎡ |
| 후보별 최대 면적 차이 | 0.000000453147㎡ |
| 경계 밖 수치 오차 합 | 0.000014963㎡ |
| 후보별 최대 경계 밖 수치 오차 | 0.000000214167㎡ |

원본 감사 r30의 polygon–동 교차 3,185건은 사전 coverage 수치다. 최종 생성기는 원본 ring의 자체 교차 3개를 격리했으며, 이 격리 때문에 최종 후보가 4건 적다. 무효 원본의 순번·오류 유형·영향 가능 동은 `audit.json`에 기록했다. `make_valid`나 임의 buffer로 수리하지 않았고 격리 영역의 면적·분류를 완성 후보로 주장하지 않는다. 따라서 r30의 사전 겹침 면적 `32,086,321.85㎡`를 이번 최종 면적으로 대체하거나 두 값을 같은 검증 조건의 결과로 취급하지 않는다.

좌표 변환, 경계 교차, millimeter ring 직렬화 후 면적과 경계 포함 여부를 다시 대조했다. 위 경계 밖 수치는 부동소수점 왕복에 따른 극소 오차이며, 후보별 상한 `0.0001㎡`보다 작다. polygon을 길찾기 면이나 안전 통행 영역으로 해석하지 않는다.

## 독립 검산과 적용 경계

- `self-test`, 첫 `build`, 독립 `verify`: 모두 `PASS`
- 동일 입력 재생성: `changedFiles=0`
- 실제 산출 파일 33개, manifest 기술서 31개, 기술서 SHA-256 불일치 0개
- `complete.json`의 manifest SHA-256과 실제 manifest SHA-256 일치
- 행정동 bundle 30개를 별도로 읽어 후보 stable ID 3,181개, 중복 0개, 허용되지 않은 후보 필드 0개를 확인
- 원천 고유번호·주소·관리번호 등의 원문 필드가 후보에 없고, 원본 ID는 hash stable ID로만 표현됨
- 기존 파일 수정, commit, push: 없음

| 권위 또는 적용 대상 | 현재 값 |
| --- | --- |
| 공개 표시·DB·Mongo·current | 모두 `false`, 미적용 |
| Unity 비공개 검토 표현 | 실제 Play Mode·Game View 캡처 검증 완료 |
| Runtime·Traversal·Gameplay | 모두 `false`, 권위 없음 |
| 법적 효력·종 현장 관찰·안전·통행 | 모두 `false`, 권위 없음 |

다음 관문은 현행 행정동 경계 확보·재귀속과 정밀 지형 consumer 계약이다. 현행 경계와 출처 판본이 준비되면 별도 revision으로 다시 생성·검증한다. 이번 비공개 화면 검토로 `EnvironmentPresentation`, 공개, 이동 또는 게임 권위를 올리지 않는다. **새 디오라마 규칙 후보 없음**.
