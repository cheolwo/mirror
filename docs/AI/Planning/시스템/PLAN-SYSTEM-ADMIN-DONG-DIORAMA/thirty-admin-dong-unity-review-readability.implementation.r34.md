# 30개 행정동 Unity 검토 화면 가독성 개선

[구현·검증 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r34]

- 상태: `CompactOverviewHudGameViewCaptured`
- 기준: [비오톱 비공개 후보·Unity 검토 r32](thirty-admin-dong-biotope-private-generation.implementation.r32.md), [정밀 지형 consumer 준비도 감사 r33](thirty-admin-dong-terrain-consumer-readiness-audit.implementation.r33.md)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 30개 역사 행정동 비공개 검토 화면

## 구현 결론

실제 v5 캡처를 다시 살펴보니 `765x457` overview에서 304px 상단 근거 패널과 116px 하단 집계 패널이 공간 도형의 상당 부분을 가렸다. 자료 층이 늘어도 전체 동의 도로·건물·보행망·방향표시·비오톱 배치가 한눈에 읽히지 않는 상태였다.

Unity 검토 View에 `CompactEvidenceHud` 모드를 추가하고, 자동 증거 캡처가 overview 30장에서는 이 모드를 사용하도록 했다. compact 패널은 높이 82px이며 행정동 이름, 비공개·배포 불가 상태, 건물·도로·보행 노드·방향 후보·비오톱 수와 역사 경계·권위 결손만 남긴다. 면목제3·8동 detail은 기존 전체 근거·깊이 슬롯·선택 건물·Mesh 집계를 그대로 표시한다.

동일한 650px 폭을 기준으로 overview의 패널 사각형 합은 `650×(304+116)=273,000px²`에서 `650×82=53,300px²`로 줄었다. 화면을 가리는 패널 면적은 80.48% 감소했고 하단 도형은 완전히 드러났다. 이는 도형의 정확도를 새로 만든 수치가 아니라, 이미 검증된 도형을 사람이 비교할 수 있는 화면 면적을 확보한 결과다.

이 변경은 입력 generation, geometry, 카메라 전체/선택 규칙, Mesh batch, Collider, Scene·Prefab을 바꾸지 않는다. 캡처 manifest는 두 HUD 모드를 명시하는 `administrative-dong-diorama-gameview-evidence.v6`으로 올렸다.

## 실제 Play Mode·Game View 증거

- 증거 폴더: `C:/Users/user/ssalddel/Documentation/Changes/2026-09-26-administrative-dong-private-biotope-r6-compact-hud/`
- 입력 generation: `5207BA41C67B6BA93182CFA2485F1CD89A0AF2964EC97E55D8D4A988E77C646E`
- index SHA-256: `951D8E361873ACFFE1EB162845F3E59F9428A6DF1F71FF69813F9947483CD7E6`
- manifest SHA-256: `609D723B171216B7D6F8F040DDDED0128AAC8287831B7AF8FEC8E694ECC21B3B`
- 실제 Unity: `6000.5.6f1`, Play Mode `true`, Game View `true`
- overview 30장 + 면목제3·8동 detail 1장, PNG hash 불일치 0
- overview HUD: `CompactSourceAndCount`
- detail HUD: `FullEvidenceAndSelection`
- 면목제3·8동 overview SHA-256: `249B781DB2817D5EA53540EBFC7B33E3BB6BFB3177AF545615D87A45860F46F8`
- 면목제3·8동 detail SHA-256: `99248B055E186273684AC633801404A0072873C371320F733CE4CB75FD4E2D7A`

도형과 성능 수치는 v5와 같다. 비오톱 후보 3,181개·정점 408,348개·인덱스 612,522개, 방향 후보/미해석 8,415/8,415개·화살표 0개, 전체 Mesh 정점/인덱스 2,692,752/4,246,746이다. 모든 모듈은 Renderer 4·MeshFilter 4·Collider 0이다.

집중 Unity EditMode 시험은 32/32 통과했고 XML SHA-256은 `B0B56DF340202D4AA1F9CB2CD7556A67F5A063CCC8E9F4ED892AA07EAE087B9A`다. canonical Scene hash는 실행 전후 같고 Scene 저장 `false`, 종료 뒤 dirty `false`다. 기존 Console 오류 8건과 첫 `SimulationConflictException: SimulationReplayHashMismatch`는 남아 있다.

## 권위 경계

compact HUD는 증거를 숨겨 권위를 높이는 기능이 아니다. 조망 화면에는 역사 경계와 현재성·주소·필지·통행·생태 권위 없음이 계속 보이고, 상세 화면과 manifest에 전체 근거·집계가 남는다. 공개·DB·Mongo·current pointer·Runtime·Traversal·Gameplay·E 승격은 모두 미적용이다. commit과 push도 수행하지 않았다. **새 디오라마 규칙 후보 없음**.
