# 촬영 원본 푸시 차단

배달 촬영 원본·카메라 이미지·원음과 원본 폴더는 로컬에 보관한다. `.gitignore`가 일반 추가를 막고, 로컬 `pre-push` 검사기가 푸시되는 커밋 이력을 검사한다. 이미 커밋한 영상은 나중에 파일을 삭제해도 이력에 남으므로 차단된다. 검사 실패도 푸시를 차단한다. 원본 파일을 삭제하거나 외부에 업로드하지 않는다.

미러 모드는 영상·음원, 촬영 원본 폴더와 카메라 파일명의 이미지를 차단하며 기존 제품 아이콘·UI 검증 이미지는 허용한다. 사람이나 개인정보의 자동 시각 판별 기능은 아니며 메타데이터 내용 검수를 대신하지 않는다.

미러 모드에서 아래 **직접 생성한 앱 알림 자산의 검토된 판본**만 정확한 경로와 콘텐츠 SHA-256을 함께 확인해 허용한다. 기존 0.58초 기계음의 근거는 [r35 알림 구현 기록](../../docs/Changes/2026-10-08-agreed-rates-native-alerts-r35.md)의 `메시지와 소리 설정`이며 회원 앱은 같은 기사 알림음을 재사용한다.

2026-10-27 사용자가 합성 안내음 두 개의 차단 상태를 안내받은 뒤 푸시를 명시적으로 요청했다. [r19 승인 범위](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/generated-notification-assets-push.r19.md)에 따라 Windows 오프라인 합성 근거·단문·PCM·현재 바이트 SHA를 수동 대조해 ‘살뜰 배달!’와 ‘홍달배달’ 판본을 아래에 한정 추가했다. [정확한 생성 근거와 해시](../../docs/assets/changes/2026-10-27-driver-recommendation-signal-r18/audio-provenance.json)를 참조한다. 경로에서 SHA를 발견해 자동 승인한 것이 아니며, 이후 새 음원이나 변경 SHA는 계속 별도 검토한다. 녹음·임의 발화·촬영 원음의 허용 목록이 아니고 청취·배포약관 검증 완료를 뜻하지 않는다.

| 정확한 Git 경로 | 허용 SHA-256 |
| --- | --- |
| `FDriverApp/Platforms/Android/Resources/raw/ssalddel_delivery.wav` | 기존 beep `19846118acc73172933e20068874138c799fbf802701fc1027fedaa07d9f2e61` / 검토한 합성 음성 `d0ed818d6e9d6449d7224f849d5dc71ea88f3d2a4b788be1d5aa3828765d577b` |
| `RestaurantDeskApp/Platforms/Android/Resources/raw/ssalddel_order.wav` | `de4c10c92e28d1a0ba4a085713f358fcaea938cda96af105b9894aecdd65dd28` |
| `SsalddelApp/Platforms/Android/Resources/raw/ssalddel_member_delivery.wav` | `19846118acc73172933e20068874138c799fbf802701fc1027fedaa07d9f2e61` |
| `DriverApp/Platforms/Android/Resources/raw/hongdal_dispatch.wav` | 검토한 합성 음성 `6e0da9aaa1b36521b71a7ce0dff5412edd315cc6531d96c308e385a4c4cbbab2` |

검사기는 푸시 범위의 모든 커밋 트리에서 해당 경로의 blob을 읽는다. 각 이력 판본은 해당 경로의 정적 허용 SHA 집합에 속해야 한다. 따라서 기존 beep와 검토한 합성 음성이 함께 있는 정상 이력은 허용하되, 같은 경로의 검토되지 않은 내용, 다른 경로에 복사·이름 변경한 음원, 잠시 넣었다가 삭제하거나 정상 음원으로 되돌린 원음도 계속 차단한다. 경로는 NUL 구분으로 원문 그대로 읽고 줄바꿈 등 제어 문자가 있는 경로도 차단한다. index 검사는 working copy 대신 staged blob을 확인한다. 배달 모드에는 예외를 적용하지 않는다. 해시를 읽거나 검사를 끝내지 못해도 차단한다. 새 알림음이나 해시 변경을 자동 승인하지 않는다.

설치: 저장소 루트에서 `powershell -NoProfile -ExecutionPolicy Bypass -File eng/git/install-media-guard.ps1 -PythonPath <Python 실행 파일의 절대 경로> -Mode mirror`를 실행한다. Git과 Python 3.11 이상이 필요하다. 기존 사용자 hook은 덮어쓰지 않는다. hook은 로컬 설정이므로 새 복제·다른 PC에서는 재설치한다. `--no-verify` 등 hook 우회 실행은 보호 대상 밖이다.

전체 이력 확인: `python eng/git/raw_media_guard.py --mode mirror --audit-head`. 추가될 파일 확인: 같은 도구의 `--audit-index`. 합성 시험은 `python -m unittest discover -s eng/git -p test_raw_media_guard.py`다.
