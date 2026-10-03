# 페이지 문서의 소스·호스트 대조

기준 문서는 [화면별 문서 색인](../../docs/ProjectOverview/page-docs/README.md)이다. 제품 `@page` 목록과 평가된 MSBuild의 Content/RazorComponent를 연결한다. 실제 실행/권한/캡처/출시 상태를 검사하는 도구는 아니다.

```powershell
python eng/page-docs/catalog.py --write
python eng/page-docs/catalog.py
python eng/page-docs/verify.py
python eng/page-docs/roles.py --write
python eng/page-docs/roles.py
```

Python 3 표준 라이브러리와 현재 프로젝트를 평가할 수 있는 .NET SDK/MAUI workload가 필요하다. `--write`만 목록/ID 대장을 갱신하며 기본 실행은 차이가 있으면 실패한다. ID 대장의 기존 항목을 다시 번호화하지 않는다. 새 파일의 자동 ID는 경로에서 생성하여 대장에 고정한다. 파일 이동 때 기존 ID를 승계할지는 기존 페이지 책임/이력과 대조한 뒤 대장에서 명시적으로 처리한다.

별칭은 같은 소스의 route 목록으로 묶는다. linked source는 포함하는 모든 host를 연결하고 한 번만 센다. 제품 host가 포함하는 `eng/web-role-app` 진입 화면은 포함하며, test/preview/vendor/bin/obj/artifacts와 route 없는 컴포넌트는 제외한다. host 평가는 실패 시 차단하며 소스 추측값으로 채우지 않는다.

`verify.py`는 상세 네 문서·현재 생성 목록·관련 색인의 로컬 링크와 세 단계 제목을 확인한다. 로컬 파일 존재와 HEAD 포함/내용 변경을 구별해 `artifacts/local/page-docs-r1/document-verification.json`에 저장한다. 기존 문서의 변경되지 않은 깨진 링크는 별도 표시하며 새 오류는 실패한다. 외부 웹 링크 내용·실제 UI·앵커 의미·DB 동기화를 대신 검증하지 않는다.

현재 상세 검토 네 페이지는 사용자 승인 범위다. 다른 페이지는 `InventoryOnly`이며 기존 README가 있다는 사실을 최신 구현 검증으로 해석하지 않는다. 전체 캡처/원장 구현을 생성하거나 operating DB를 수정하지 않는다.

`roles.py`는 기존 소스 ID·공유 호스트·별칭을 보존하고 제품 MAUI `ContentPage`를 추가해 역할별 표/JSON/검색 HTML을 만든다. 역할 후보와 포함 호스트는 실제 로그인 권한 증거가 아니다. `role-reviews.json`에서 검토 이유와 근거를 명시하고 기본값은 `판단 보류`다. 검토한 소스의 해시가 바뀌면 현재 실행 검증으로 재사용하지 않는다. HTML은 개발용 문서 탐색이며 새 제품 앱이나 모바일 페이지의 대체물이 아니다.
