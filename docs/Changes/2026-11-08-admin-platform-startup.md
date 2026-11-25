# 관리자 화면 시작 누락 보완과 기존 선택·메모 실제 웹 확인

실제 `SsalddelAdmin` Development 시작이 `IRoleWorkspaceLocationProvider`·`I업무사진촬영Adapter` 누락으로 실패했다. 공용 모듈이 등록한 참여·제안·수행 ViewModel 세 개를 조립할 수 없었다. DI 검증이나 환경을 바꿔 숨기지 않고 Admin에 미지원 위치와 기존 미지원 사진을 scoped 등록했다. 위치 두 요청은 취소를 보존하고 null을 반환하며 권한·좌표·촬영을 새로 제공하지 않는다. 기존 호스트 구현은 덮지 않는다.

| 기준 | 제품 변경 | 실제 검증 |
| --- | --- | --- |
| `dev/mirror-integration` · `e67cdbf0bbb086dc9ed547bdcd640074c9a18c86` · 로컬 미커밋 | Program1줄·Admin 플랫폼 등록·회귀3개. 공용 모듈/기존 O1/O2 페이지·서비스 불변 | Fast40·전체17,506통과/실패0/17미실행, 실제 Admin 시작·브라우저14확인/18원본 |

테스트는 실제 공용 모듈·Admin 세션과 세 ViewModel을 정상 `ValidateOnBuild/ValidateScopes`로 조립하고, HTTP/JS 호출0·사진 미지원·위치 null/취소·기존 등록 보존을 확인한다. 소비 Admin 빌드와 통합1.5 빌드도 통과했다. [통합 검증·소스 지문](../../artifacts/local/operator-driver-selection-browser-20261108-1420/verification.json)의 정확한 TRX·DLL/PDB를 따른다. 원래 dirty Program은 삽입한 한 줄을 바이트 단위로 제거하면 이전 SHA와 같았다. 다른 소스·설정·stage/commit/push는 정리하지 않았다.

## 실제 화면과 입력/응답 비교

실제 앱의 허용 alias `/admin/freight-delivery/dispatch-ai-review`·InteractiveServer·기존 DevelopmentBootstrap 가상 신원과 유한 loopback API를 사용했다. 메모리 fallback은 껐고 의뢰3건·제안3개·D1/D2의 명시 ID를 재사용했다. D1 선택 후 D2만 남는 재조회는 D1 버튼/핀 강조를 지우며, 미선택 저장은 기존 기사 안내와 POST0이다. 버튼을 새로 비활성화한 것이 아니다. D2 명시 선택 뒤 같은 묶음A·의뢰001/002·D2가 실제 격리 POST에 함께 전달됐다. 구성 펼치기의 두 의뢰·화물 내용·한국 시각08:30/09:10도 대조했다.

같은 페이지의 기존 O2는 응답을 유한 gate로 보류하고 실제 메모 입력 A→B를 확인했다. A 응답 뒤 B와 입력 초점은 유지되고 다음 명시 저장만 B를 전송·정리했다. 세 요청은 모두 같은 구성/D2이며 실제 DB/RAG/배정을 실행하지 않았다. 실제 키보드 Enter/Tab·클릭·503/재조회·주소 reload, 390/320/1280px의 줄바꿈·구성·넘침 없음을 확인했다. 물리 한글 조합, 실제 인증/JWT/DB·기기·운영 사용은 미실행이다.

[시작 전 오류와 현재 화면·지도 선택·메모 비교](../../artifacts/local/operator-driver-selection-browser-20261108-1420/comparison.html)에서 원본을 연다. **수정 전 앱은 시작되지 않아 화면 미확보**이고 오류 로그를 보존한다. 현재 O1/O2의 입력/응답 전후 사진을 옛 O1 소스의 브라우저 수정 전후로 표현하지 않는다. 예전 [정적 정상 구성 화면](2026-11-07-operator-bundle-members.md)과 O1/O2 실패 회귀·구판도 보존한다.

첫 회귀 도구의 using/촬영 인자 컴파일 오류, fixture의 chunked JSON 미지원400과 보완, SUMMARY locator 역할 차이와 정리 직후 포트 관측은 보조 원본에 남겼다. 최종 제품 오류/시험 수로 섞지 않는다. 소유 앱5414·fixture5415·브라우저/임시 폭을 반환했으며 원래 앱 설정과 USB·다른 예약은 변경하지 않았다.

다음은 `LoginHref`가 옛 `/dispatch/ai-review`를 반환하고 현재 정책은 canonical alias를 허용하는 정적 후속 후보다. 실제401→로그인 복귀를 먼저 재현해야 하며 이번에 실행/수정하지 않았다.
