# 촬영 원본 푸시 차단

배달 촬영 원본·카메라 이미지·원음과 원본 폴더는 로컬에 보관한다. `.gitignore`가 일반 추가를 막고, 로컬 `pre-push` 검사기가 푸시되는 커밋 이력을 검사한다. 이미 커밋한 영상은 나중에 파일을 삭제해도 이력에 남으므로 차단된다. 검사 실패도 푸시를 차단한다. 원본 파일을 삭제하거나 외부에 업로드하지 않는다.

미러 모드는 영상·음원, 촬영 원본 폴더와 카메라 파일명의 이미지를 차단하며 기존 제품 아이콘·UI 검증 이미지는 허용한다. 사람이나 개인정보의 자동 시각 판별 기능은 아니며 메타데이터 내용 검수를 대신하지 않는다.

설치: 저장소 루트에서 `powershell -NoProfile -ExecutionPolicy Bypass -File eng/git/install-media-guard.ps1 -PythonPath <Python 실행 파일의 절대 경로> -Mode mirror`를 실행한다. Git과 Python 3.11 이상이 필요하다. 기존 사용자 hook은 덮어쓰지 않는다. hook은 로컬 설정이므로 새 복제·다른 PC에서는 재설치한다. `--no-verify` 등 hook 우회 실행은 보호 대상 밖이다.

전체 이력 확인: `python eng/git/raw_media_guard.py --mode mirror --audit-head`. 추가될 파일 확인: 같은 도구의 `--audit-index`. 합성 시험은 `python -m unittest discover -s eng/git -p test_raw_media_guard.py`다.
