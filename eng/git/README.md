# 촬영 원본 푸시 차단

배달 촬영 원본·카메라 이미지·원음과 원본 폴더는 로컬에 보관한다. `.gitignore`가 일반 추가를 막고, 로컬 `pre-push` 검사기가 푸시되는 커밋 이력을 검사한다. 이미 커밋한 영상은 나중에 파일을 삭제해도 이력에 남으므로 차단된다. 검사 실패도 푸시를 차단한다. 원본 파일을 삭제하거나 외부에 업로드하지 않는다.

미러 모드는 영상·음원, 촬영 원본 폴더와 카메라 파일명의 이미지를 차단하며 기존 제품 아이콘·UI 검증 이미지는 허용한다. 사람이나 개인정보의 자동 시각 판별 기능은 아니며 메타데이터 내용 검수를 대신하지 않는다.

미러 모드에서 아래 **직접 생성한 0.58초 앱 알림 기계음**만 정확한 경로와 콘텐츠 SHA-256을 함께 확인해 허용한다. 생성 근거는 [r35 알림 구현 기록](../../docs/Changes/2026-10-08-agreed-rates-native-alerts-r35.md)의 `메시지와 소리 설정`이며 회원 앱은 같은 기사 알림음을 재사용한다. 녹음·발화·촬영 원음의 허용 목록이 아니다.

| 정확한 Git 경로 | 허용 SHA-256 |
| --- | --- |
| `FDriverApp/Platforms/Android/Resources/raw/ssalddel_delivery.wav` | `19846118acc73172933e20068874138c799fbf802701fc1027fedaa07d9f2e61` |
| `RestaurantDeskApp/Platforms/Android/Resources/raw/ssalddel_order.wav` | `de4c10c92e28d1a0ba4a085713f358fcaea938cda96af105b9894aecdd65dd28` |
| `SsalddelApp/Platforms/Android/Resources/raw/ssalddel_member_delivery.wav` | `19846118acc73172933e20068874138c799fbf802701fc1027fedaa07d9f2e61` |

검사기는 푸시 범위의 모든 커밋 트리에서 해당 경로의 blob을 읽는다. 같은 경로의 다른 내용, 다른 경로에 복사·이름 변경한 음원, 잠시 넣었다가 삭제하거나 정상 음원으로 되돌린 원음도 계속 차단한다. 경로는 NUL 구분으로 원문 그대로 읽고 줄바꿈 등 제어 문자가 있는 경로도 차단한다. index 검사는 working copy 대신 staged blob을 확인한다. 배달 모드에는 예외를 적용하지 않는다. 해시를 읽거나 검사를 끝내지 못해도 차단한다. 새 알림음이나 해시 변경을 자동 승인하지 않는다.

설치: 저장소 루트에서 `powershell -NoProfile -ExecutionPolicy Bypass -File eng/git/install-media-guard.ps1 -PythonPath <Python 실행 파일의 절대 경로> -Mode mirror`를 실행한다. Git과 Python 3.11 이상이 필요하다. 기존 사용자 hook은 덮어쓰지 않는다. hook은 로컬 설정이므로 새 복제·다른 PC에서는 재설치한다. `--no-verify` 등 hook 우회 실행은 보호 대상 밖이다.

전체 이력 확인: `python eng/git/raw_media_guard.py --mode mirror --audit-head`. 추가될 파일 확인: 같은 도구의 `--audit-index`. 합성 시험은 `python -m unittest discover -s eng/git -p test_raw_media_guard.py`다.
