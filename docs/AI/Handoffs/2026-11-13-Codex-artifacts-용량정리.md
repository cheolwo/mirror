# artifacts 저장공간 정리 — 2026-11-13

사용자의 「artifacts 정리, 용량부족」 요청에 따라 재생성 가능한 빌드 캐시를 실제 삭제했다. 원본 자료·업무 DB·세이브·시안/영상·소스·최종 실행판과 배포물은 정리 대상에서 제외했다.

## 결과

- 초기 집계: 약 131.07 GiB, 936,502파일. 링크3곳과 읽기 실패46경로는 집계 상한/제외로 남겼다.
- 정리: **캐시 303곳, 당시 집계 파일 657,360개, 파일 논리 크기 약 23.10 GiB**.
- 정리 후 재조회한 C: 여유: **약 81.06 GiB**. 논리 파일 크기와 실제 디스크 여유를 별도로 기록하며 두 값이 같다고 보지 않는다.
- 빌드 소스 기준 파일 203개의 SHA256 일치, 전체 캐시 경로 소멸, 보호 경로·주요 실행판/시안 존재, tracked 파일 미포함을 재확인했다.

## 지운 것과 남긴 것

| 정리 | 보존 |
| --- | --- |
| 소스 프로젝트/출력 경로가 확인된 .NET `obj` 268곳 | 원본 csproj·코드·원문 사본·빌드/검증 로그 |
| `u`·`r74apk` 격리 출력의 `bin` 13곳 | 최종 APK·publish·설치물·player 출력 |
| 복제 Unity 프로젝트 `Library` 22곳 | `Assets`·`Packages`·`ProjectSettings`·구매 자산 원본 |
| 자체 대용량 감사 목록의 무손실 gzip 보관 | 감사 목록 내용/원문 SHA |

Unity `ShaderCache.db`는 확인된 Library 안의 생성 캐시로만 분류했다. 그 외 DB·세이브·키·APK가 발견된 폴더와 링크/미확인 출력은 제외했다. 에뮬레이터 userdata/snapshot, 구판 설치·검증 실행판, 공공 원본/수집 이력은 이번에 지우지 않았다.

131GiB 전체를 비우거나 파일을 다른 경로로 옮기는 작업은 아니다. 일부 과거 화면 비교용 bin도 당시 바이너리 재실행 근거를 남기기 위해 보존했다. 주기적 자동 삭제는 설정하지 않았다.

## 다음 빌드

.NET은 다음 빌드 때 restore/중간 출력을 다시 만들고, 복제 Unity 프로젝트는 Unity6000.5.6f1로 열 때 Library를 재수입한다. 그래서 첫 재빌드/열기는 더 오래 걸릴 수 있다. 캐시를 다시 채우는 제품 빌드·Runtime 검증은 이번에 실행하지 않았다. 실행 중인 앱이나 프로세스를 종료하지 않았다.

최신 Windows player `player-20261113-203619`, 첫 동 모듈 반환판 `player-20261113-200502`, 게임/관찰 시안2장과 개인게임 r123 설치물은 실제 존재를 확인했다. 수집 원본·음식점/메뉴·지역 자료와 DB/세이브를 보존했다.

## 근거

- [사전 목록(gzip)](../../../artifacts/local/artifact-cleanup-20261113/inventory-before.json.gz)
- [빌드 캐시 계획](../../../artifacts/local/artifact-cleanup-20261113/cleanup-plan.json) · [Unity 캐시 계획](../../../artifacts/local/artifact-cleanup-20261113/cleanup-plan-unity.json)
- [빌드 캐시 결과](../../../artifacts/local/artifact-cleanup-20261113/cleanup-result-parallel.json) · [Unity 캐시 결과](../../../artifacts/local/artifact-cleanup-20261113/cleanup-result-unity.json)
- [최종 재조회](../../../artifacts/local/artifact-cleanup-20261113/verification.json)

단일 순차 삭제를 파일 수 때문에 중단한 뒤, 남은 서로 다른 경로를 PowerShell 기본 삭제 명령으로 최대4개 병렬 처리했다. 이전 진행 기록도 같은 폴더에 보존했다. 최종 재조회는 전체303대상의 소멸을 확인하므로 중단 시점의 부분 처리를 완료 근거로 대신하지 않는다. 제품 코드·DB·수집 예약·게임 상태·commit/push는 변경하지 않았다.
