# 서울 동북·동부 4개 구 행정동 표현 r27

사용자 요청: 기존 중랑구에 동대문구·광진구·노원구를 추가한다. r26의 로컬 검토 표현 범위 확장이며 새 Scene·운영 권위 변경은 없다.

## 자료와 변경

기존 서울 공식 OA-22160/OA-22161 ZIP의 **2023-10-31 참고 경계**를 재사용했다. 최신 행정동 수를 주장하지 않는다. 출처·공공누리1유형·좌표 변환·개별 도형 검증 및 한계는 [r26](seoul-hierarchy-preview.implementation.r26.md)을 따른다.

| 구 | 코드 | 원본에서 확인한 행정동 수 |
| --- | --- | --- |
| 중랑구 | 11260 | 16 |
| 동대문구 | 11230 | 14 |
| 광진구 | 11215 | 15 |
| 노원구 | 11350 | 19 |

- 총64동과 서울25구=89개. `parentCode`로 구/행정동 관계를 명시하고 v2 계약에서 부모·코드 접두사·구별 개수를 검증한다. 기존 v1 41개 사본 읽기는 유지한다.
- 변환 도구 `eng/neighborhood/build-seoul-intermediate-preview.py`가 별도 `seoul-four-district-preview.json`을 생성한다. 기존 r26 사본을 덮어쓰지 않는다.
- 출력 경로: `artifacts/local/public-data/seoul-intermediate-boundaries-20260923-r1/`. 964,192bytes, SHA256 `3a836c8c0969885a35f0a5e0d1e4ec4b2f44c4cba61e8f4e2d876f61fd2a336e`. 두 번 생성해 동일 결과 확인.
- Unity `서울중간공간View.cs`에 4구 선택과 구 경계 기반 카메라 중심/크기 조정 추가. 선택한 구의 행정동만 펼친다. 다른 구에서 확대했다는 이유로 사가정 디오라마에 진입하지 않는다.
- Domain reload 뒤 이전 검토 루트가 남는 실제 겹침을 발견하여, 이름과 DontSave 플래그가 일치하는 전용 임시 루트만 재로드 시 회수하고 새 루트를 소유 View 하위에 결속했다. 반복 실행 후 루트1개 확인.
- 기존 Editor 메뉴에서 새 파일을 수동으로 연다. DB 정규화 저장·배포 Resources·Scene 저장은 이번 범위 밖이다.

## 검증

- Unity EditMode `서울중간공간Tests` **7/7**. v1 호환, v2 64동, 부모 오류·누락·중복·비정상 좌표·비권위 경계 확인.
- 실제 Play에서 89객체 자료 로드, 4구 선택 및 캡처. 노원구 상태의 디오라마 진입 false. 마지막 수명 관리 수정 후 재컴파일·실제 Play 재확인.
- CLI로 View 함수를 호출했으며 물리 마우스 버튼·휠 입력과 Windows 빌드는 미검증. 기존 서버/Replay 오류 해결이나 Console0을 주장하지 않는다.
- 캡처는 별도 Unity 저장소 `Documentation/Changes/2026-09-23-four-districts/`의 `jungnang.png`, `dongdaemun.png`, `gwangjin.png`, `nowon.png`. 최종 디자인 승인 이미지가 아니라 실제 실행 증거다.
- 지명 밀집 구간의 겹침과 상단 UI 가림은 후속 표현 보완 대상이다. 64동의 건물·도로 상세 디오라마 완성을 뜻하지 않는다.

Play 종료. 관련 없는 기존 변경 보존. commit·push 없음. 역사 자료와 최신 공간 권위를 분리하는 기존 규칙을 지지하며 새 디오라마 규칙 후보 없음, E 자동 승격 없음.
