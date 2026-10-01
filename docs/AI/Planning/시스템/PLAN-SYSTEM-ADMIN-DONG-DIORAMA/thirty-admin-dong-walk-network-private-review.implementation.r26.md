# 30개 행정동 역사 보행망 비공개 검토 세대

[구현 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r26]

- 상태: `Implemented / CurrentSingleAcquisitionBound / LocalPrivateGenerationVerified / CurrentPublicationBlocked`
- 후속: [Unity 비공개 검토 r27](thirty-admin-dong-walk-network-unity-review.implementation.r27.md)에서 전체 path 보존 generation과 실제 Game View를 별도 revision으로 구현했다. 이 r26의 생성 당시 Unity 미적용 기록은 역사 스냅샷으로 유지한다.
- 기준: [30개 행정동 비공개 관찰 오버레이 r25](thirty-admin-dong-private-observation-overlay.implementation.r25.md)
- 과거 계약: [행정동 보행망 후보 G3c r1](administrative-dong-walk-network-candidate.implementation.r13.md)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 구현 결론

서울 열린데이터광장 [`OA-21208`](https://data.seoul.go.kr/dataList/OA-21208/A/1/datasetView.do)의 2020년 기준 WGS84 도보 네트워크를 2026-09-26에 다시 받은 단일 영수증에 결속해 별도 G3c r2 비공개 검토 세대로 만들었다. 공식 설명의 공간 기준은 2020년이며 포털 갱신일이나 내려받은 날짜를 현재 보행망 관측일로 바꾸어 해석하지 않는다.

새 생성기 `eng/neighborhood/administrative_dong_walk_network_private_review_r2.py`는 현재 영수증에 함께 기록된 광진구·동대문구·중랑구 CSV 3개만 소비한다. 과거 G3c r1의 동결 scope를 다시 쓰거나 서로 다른 내려받기 세대를 섞지 않았다. 기존 r1 파서의 WKT 검사·좌표 변환·역사 경계 귀속·선분 절단 함수를 재사용하지만 r1 파일과 hash 계약은 수정하지 않는다.

59,724개 원천 행에서 역사 경계 안 노드 후보 17,267개와 링크 조각 후보 23,472개, 합계 40,739개를 만들었고 30개 행정동 모두 후보를 갖는다. 결과는 현재 통행 가능성을 뜻하지 않는다. 2020년 원천과 2023-10-31 역사 행정동 경계를 결속한 로컬 조사 후보일 뿐이며 공개 지도, 서버·Unity Runtime, NavMesh, 배달 경로 또는 gameplay 권위가 아니다.

## 공식 원본과 단일 영수증 결속

- 공식 자료: `OA-21208`, 서울시 자치구별 도보 네트워크 공간정보
- 공식 설명 기준: 2020년
- 원천 좌표계: WGS84
- 이용허락: 공공누리 제1유형, 출처표시
- 영수증 획득 시각: `2026-09-26T02:47:41.887Z`
- 영수증의 포털 갱신 표기: `2026-09-26`
- 영수증 SHA-256: `6395471456E77DFCC643404CF08C074014C8921E94C3EA7C9079CA86629AE504`
- 자치구 필터 밖 행정동 경계 halo 완결: `false`

| 역할 | 행 수 | 길이 | SHA-256 |
| --- | ---: | ---: | --- |
| 광진구 CSV | 16,288 | 3,647,541 bytes | `BB485FDF1B31963F1109E4433E660DC6E4B2AF63A15B07D8356EE7F33F365915` |
| 동대문구 CSV | 21,988 | 5,066,359 bytes | `5DFBF2F0C237B680FFD2BF2B7CA8D6B43D35110C87398133F13B3F1C5B3B37C6` |
| 중랑구 CSV | 21,448 | 4,771,869 bytes | `55192969525242522C0335C56605B1286EC7D19C7365A65532291BBA5DEE4321` |
| 링크·노드 유형 코드북 | 해당 없음 | 12,510 bytes | `C718A21352D91B2A9BE50E8C52ED886D92A495ADB14070995B487B0CA5AB4E4F` |

세 CSV는 과거 r1 scope에 기록된 파일과 길이·행 수가 같더라도 SHA-256이 다르다. 따라서 과거 r1 source generation을 복구했다고 주장하지 않고, 현재 영수증과 현재 파일 hash 전체를 새 r2 후보 집합 hash에 포함했다. `oa21208SingleAcquisitionReceiptBound=true`, `oa21208SourceGenerationsMixed=false`, `legacyFrozenG3cR1ScopeLoaded=false`, `legacyFrozenG3cR1GenerationReconstructed=false`를 manifest와 audit에 보존한다.

역사 귀속 경계는 `OA-22160` 2023-10-31 파일 SHA-256 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`이며 현행 행정동 경계 정본이 아니다. 정확한 30개 대상 집합은 `northeast-seoul-rider.r2` SHA-256 `CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B`에 결속했다.

## 생성 세대와 파일

- candidate revision: `northeast-seoul-admin-dong-walk-network-private-review.g3c.r2`
- candidate schema: `administrative-dong-walk-network-private-review-candidate.v2`
- generation hash: `7A0887F625C19401A4F57B57C5C0F51C1FC63590637B2AE82B2FC71A508FA6DD`
- generation 경로: `artifacts/local/validation/admin-dong-walk-network-private-review/r2/generations/7a0887f625c19401a4f57b57c5c0f51c1fc63590637b2ae82b2fc71a508fa6dd/`
- 생성 파일: `manifest.json`, `audit.json`, `candidates.ndjson`, `complete.json`, `generator-source.py`
- `candidates.ndjson`: 40,739행, 107,989,198 bytes, SHA-256 `8E2CE178DE5091F7617C465FD6F01BEDE673F4FD9C8CEC92A1B9674C747065E5`

candidate set hash는 candidate schema·revision·생성기 hash·재사용 파서 hash·모든 입력 hash·권위 flag·후보 수를 길이 framing한 header와, 정렬된 전체 후보의 canonical JSON을 함께 SHA-256으로 계산한다. 원천 node/link ID는 출력하지 않고 SHA-256 digest로만 남긴다. 정확한 EPSG:5186 millimeter 점·선 좌표는 Git 제외 로컬 `candidates.ndjson`의 비공개 검토 자료에만 있으며 공개 요약·API·화면으로 내보내는 계약은 없다.

## 생성 수량

| 원천·후보 항목 | 수량 |
| --- | ---: |
| 원천 전체 행 | 59,724 |
| 원천 NODE 행 | 25,680 |
| 원천 LINK 행 | 34,044 |
| 대상 역사 경계 밖 행 | 19,770 |
| 노드 후보 | 17,267 |
| 링크 조각 후보 | 23,472 |
| 전체 후보 | 40,739 |
| 후보가 있는 행정동 | 30 / 30 |
| 원천 자치구와 공간 귀속 충돌 후보 | 48 |
| 여러 행정동에 걸친 원천 링크 | 728 |

동별 `노드 / 링크 조각 / 합계`는 다음과 같다.

| 행정동 | 노드 | 링크 조각 | 합계 |
| --- | ---: | ---: | ---: |
| 중곡제1동 | 476 | 627 | 1,103 |
| 중곡제2동 | 456 | 604 | 1,060 |
| 중곡제3동 | 468 | 648 | 1,116 |
| 중곡제4동 | 559 | 754 | 1,313 |
| 전농제1동 | 883 | 1,221 | 2,104 |
| 전농제2동 | 402 | 505 | 907 |
| 답십리제1동 | 693 | 947 | 1,640 |
| 답십리제2동 | 681 | 886 | 1,567 |
| 장안제1동 | 697 | 982 | 1,679 |
| 장안제2동 | 595 | 851 | 1,446 |
| 휘경제1동 | 464 | 621 | 1,085 |
| 휘경제2동 | 538 | 698 | 1,236 |
| 이문제1동 | 694 | 925 | 1,619 |
| 이문제2동 | 394 | 528 | 922 |
| 면목제2동 | 612 | 843 | 1,455 |
| 면목제4동 | 485 | 683 | 1,168 |
| 면목제5동 | 297 | 434 | 731 |
| 면목본동 | 1,108 | 1,437 | 2,545 |
| 면목제7동 | 605 | 820 | 1,425 |
| 면목제3·8동 | 771 | 1,000 | 1,771 |
| 상봉제1동 | 435 | 619 | 1,054 |
| 상봉제2동 | 556 | 795 | 1,351 |
| 중화제1동 | 336 | 478 | 814 |
| 중화제2동 | 810 | 1,096 | 1,906 |
| 묵제1동 | 517 | 741 | 1,258 |
| 묵제2동 | 459 | 631 | 1,090 |
| 망우본동 | 1,021 | 1,339 | 2,360 |
| 망우제3동 | 462 | 648 | 1,110 |
| 신내1동 | 624 | 855 | 1,479 |
| 신내2동 | 169 | 256 | 425 |

최소는 신내2동 425개, 최대는 면목본동 2,545개다. 수량 차이는 자료 분포의 검토 대상이며 행정동의 보행 품질·접근성·상권 가치 순위가 아니다.

## 검증 결과

- Python 문법 검사: `PASS`
- 입력·단일 영수증·30개 역사 경계·권위 flag self-test: `8 / 8 PASS`
- 첫 materialize: 5개 파일 생성 `PASS`
- 같은 입력으로 후보와 세대를 다시 계산한 `verify`: 동일 generation hash, 파일 집합·내용 `PASS`
- NDJSON 독립 전수 읽기: 40,739행, 노드 17,267개, 링크 조각 23,472개, 행정동 30개 `PASS`
- `manifest.json`, `audit.json`, `complete.json` content hash 재계산: `PASS`
- completion에 기록된 네 파일의 길이·SHA-256 재계산: `PASS`
- 누락·무효 WKT, 잘못된 좌표, 복수 경계 node: 각각 0
- 원천 링크의 대상 경계 교차 길이와 고유 조각 합집합 길이: 각각 `882,519.308089m`
- 최대 링크 길이 보존 오차: `0.0m`
- 기존 `administrative_dong_walk_network_candidate.py` 변경: 없음
- 생성 세대의 Git 제외 규칙 적용: 확인

## 권위와 적용 경계

| 항목 | 현재 값 | 의미 |
| --- | --- | --- |
| 비공개 검토 | `privateReviewOnly=true` | 로컬 후보를 조사·비교할 수 있다. |
| 정확 좌표 공개 | `exactCoordinatePublicationAllowed=false` | 좌표를 공개 API·문서·화면으로 게시하지 않는다. |
| 공개 표시·배포 | `publicDisplayAllowed=false`, `distributionApproved=false` | 공개 표시 세대를 만들지 않았다. |
| DB·Mongo | `databasePersistenceCompleted=false` | DB나 Mongo에 쓰거나 현재 상태를 바꾸지 않았다. |
| current pointer | 사용·생성·갱신 모두 `false` | 기존 current generation을 선택하거나 교체하지 않았다. |
| Unity | `unityApplyAllowed=false`, `runtimeAuthorized=false` | Scene·Prefab·Runtime에 적용하지 않았다. |
| 이동·게임 | `traversalReady=false`, `gameplayReady=false` | NavMesh·NPC·차량·오토바이·배달 경로에 소비하지 않는다. |
| 개인정보 | `personalDataIncluded=false` | 개인·고객·주문 정보는 후보에 포함하지 않는다. |

`privateReviewVisualizationAllowed=true`는 로컬 검토 도구가 후보를 읽을 수 있다는 상한일 뿐 실제 Unity 구현·Play Mode·Game View·Scene 저장 증거가 아니다. 이번 r26은 Hongdal의 비공개 자료 세대 생성과 파일 검증까지만 완료했다.

## 남은 한계와 후속

1. 원천은 2020년 기준이고 행정동 경계는 2023년 역사 후보다. 현행 도로 공사·보행로 변경·폐쇄·시간대 제한을 반영하지 않는다.
2. 세 자치구 CSV 필터는 대상 경계 바깥 halo를 완결하지 않는다. 경계 인접 링크의 바깥 연결성을 완전한 graph로 해석하지 않는다.
3. 원천 유형 코드는 출처의 관측 분류다. 보도 폭·포장·경사·계단·연석·출입구, 접근 방향·정지선·신호 현시와 실제 통행 권한을 확정하지 않는다.
4. 후속 Unity 표현을 만들더라도 링크 선을 현재 보행 가능 경로나 배달 오토바이 경로로 표시하지 않는다. 먼저 공통 ENU 변환·선 단순화·화면 밀도와 정확 좌표 비공개 경계를 별도 계약으로 검토한다.
5. 현행 JUSO 행정동 경계와 같은 세대의 건물·건물군·출입구를 승인·확보하면 새 revision에서 재귀속하며 이 r2 세대를 조용히 덮어쓰지 않는다.

이 결과는 r25에서 남긴 첫 후속 G3c 결속을 완료하지만 현재 통행 graph를 완성하지 않는다. **새 디오라마 규칙 후보 없음**.
