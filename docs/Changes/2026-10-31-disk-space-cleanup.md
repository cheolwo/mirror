# 디스크 공간 정리 — 파일·다운로드 캐시와 폴더 차단 구분

2026-10-31. 사용자가 불필요한 파일을 삭제해 디스크 공간을 확보하도록 요청했다. 원본 코드·Assets·배달 영상·개인 저장·최종 설치판/바로 실행판은 보존하며, 이번 요청으로 확인된 과거 생성물과 다운로드 캐시만 정리했다.

## 실제 삭제·정리

| 대상 | 정리한 논리 크기 | 확인 |
| --- | ---: | --- |
| 과거 SagajeongGame 오류 덤프3개 | 238,710,121B | `CrashDumps/SagajeongGame.exe.42472.dmp`, `.43236.dmp`, `.30876.dmp` 삭제·부재 확인 |
| 실행 실패 release-01 Setup/ZIP과 구형 release-02 Setup | 322,178,380B | 기존 SHA 대조·정확한 파일3개만 삭제; 부모의 manifest/로그·소스는 유지 |
| LlamaLib-v2.0.5 다운로드 캐시 ZIP | 1,818,284,209B | Unity `artifacts/local/validation/local-npc-dialogue/backend/`의 ZIP1개만 삭제 |
| NuGet HTTP 다운로드 캐시 | 정리 전64,090,475B/274파일 | 공식 `dotnet nuget locals http-cache --clear` exit0, 이후 파일0; global-packages는 보존 |

앞선1차 정리에서 확인한 논리 크기는2,443,263,185B(약2.4GB)다. C 여유 공간은 시작6,320,807,936B에서 측정값으로 증가했으며, 현재 실행 중인 다른 프로그램의 쓰기 때문에 삭제한 논리 크기와 전체 볼륨 순증가는 다를 수 있다. 이후 Unity 중심 정리는 아래에 구분한다.

LlamaLib ZIP은 SHA `f3d85b2d6fbaa12839173f4438a7a2c7124bf6b25708d8be23348de2dec1156b`, 마지막 수정2026-08-30의 vendor 다운로드 캐시다. vendor 코드가 필요로 하는 runtime47파일/4,153,085,058B가 현재 `Assets/StreamingAssets/LlamaLib-v2.0.5`의 파일과 모두 SHA 일치하고, 실제 완료 표시 `setup/LlamaLib-v2.0.5.zip.complete`가 존재함을 확인했다. ZIP에는 설치하지 않는 SDK/헤더/라이선스/서버26항목도 있어 전체73항목이 전부 설치된 중복이라고 하지 않는다. 설치 라이브러리·모델·vendor 코드는 수정하지 않았다. 코드에 고정된 공급 경로는 `https://github.com/undreamai/LlamaLib/releases/download/v2.0.5/LlamaLib-v2.0.5.zip`이며 이번에 원격을 다시 조회하거나 다운로드한 결과는 아니다.

## 보존과 남은 삭제 범위

- 최종 release-03 Setup, 현재 안내가 가리키는 `verify-extract-02-retry1/Launcher.exe`, attempt16 원본 EXE의 SHA가 정리 전후 같다. 개인 저장2파일도 SHA가 같다. 게임 조작을 다시 시험한 결과와는 구분한다.
- 실패/구형 Player와 중복 검증 추출9폴더는 재분석 지점·경로·파일 목록을 검사했으나, 일괄 삭제와 단일 폴더 삭제가 각각 자동 승인 검토에서 `blocked by policy`로 거절됐다. **폴더 삭제0**이며 다른 API로 우회하지 않았다. 도구가 더 구체적인 이유를 반환하지는 않았다.
- 오래된 Visual Studio 임시 설치기25폴더는 동일 setup.exe SHA·파일969개씩·이전 최종 수정일을 확인했다. 최근6GB 설치 다운로드 폴더와 현재 갱신 중인 임시 파일은 제외했다. 앞의9개와 합쳐34폴더의 정확한 목록을 만들고 사용자에게 해당 범위의 명시적인 확인을 요청했다. 확인만으로 이후 도구 허용이 보장되는 것은 아니다.
- Premiere·Unity CLI와 Docker 컨테이너8개는 종료하지 않았다. Docker 빌드 캐시는0B이며 이미지/볼륨의 reclaimable 표시를 사용자 데이터 삭제 승인으로 해석하지 않았다. 공공데이터 원본 ZIP·배달 영상·최신 설치판·실제 DB/볼륨·사용 중 캐시는 보존했다.

## Unity 생성물 삭제의 명시 확인 후

사용자가 Unity 작업에서 생긴 중복 빌드 산출물은 삭제해도 된다고 확인했으며, 앞으로 영상 제작을 계속 고민할 수 있도록 공간을 확보하려는 의도를 밝혔다. 이에 따라 영상 제작에 사용할 원본 코드·Assets·ArtSource·Packages·영상/화면 자료와 최종 실행판은 보존했다. 오래된 VS 임시 폴더25개는 이 Unity 한정 재시도에서 제외했다.

이미 검토한 Unity 생성물9폴더 중 `attempt9/player`에 대해 절대 경로·범위·재분석 지점·파일71개를 다시 확인하는 삭제를 요청했으나 자동 승인 검토가 다시 `blocked by policy`로 거절했다. 새 사용자 승인 뒤에도 같은 차단이 반환됐으며 폴더 삭제0이다. **Unity 범위의 사용자 승인은 확인됐고, 현재 남은 차단은 삭제 도구의 거절이다.** 우회 API나 파일별 쪼개기 방식으로 이9폴더를 삭제하지 않았다.

추가로 `C:/Users/user/ssalddel/artifacts/local/validation/d337-editor-restart.log`를 확인했다. 마지막 쓰기2026-08-31, 논리11,384,976,929B·실제 점유1,425,457,152B인 오래된 생성 로그다. 읽기 전용 단독 열기와 활성 프로세스 경로 참조0을 확인했다. 원래 `d337-editor.log`와 `commit-review-d337`는 따로 남아 있다. 앞64KiB/뒤1MiB의 보호값을 가린 표본·전체 SHA를 보존하고 **이 단일 로그만 삭제**했다. 실제 C 여유 공간은8,343,224,320B→9,768,755,200B로 약1.4GB 늘었다. 압축 저장된 로그이므로11.4GB를 확보했다고 보고하지 않는다.

Unity `artifacts`의 별도 Player/UnityPlayer.dll은 발견되지 않았다. 가장 큰 나머지 묶음인 `area-set-authoring/isolated-project`는2.53GB의 Assets/Synty·Library 복사 프로젝트를 포함해 단순 빌드 출력으로 단정하지 않고 보존했다. `bin`16.87MB·`obj`약60KB·`LLMUnityBuild`481B는 효과가 작고, 원천 Natural Earth ZIP/TIF·검증 JSON/화면 자료는 이번에 삭제하지 않았다. Unity Library 전체를 지우거나 프로젝트를 재생성하지 않았다.

최종 Setup/Launcher/attempt16 EXE와 개인 저장2파일의 SHA도 후속 정리 뒤 그대로다. 근거는 `artifacts/local/disk-cleanup-unity-20261031-135245/`의 `large-log-review.private.json`, 보호값을 가린 표본, `large-log-deletion.private.json`, `cleanup-followup-result.private.json`이다. 새 게임 실행·영상 렌더·기능 검증을 한 결과는 아니다.

로컬 근거는 `artifacts/local/disk-cleanup-20261031-132019/`의 `cleanup-result.private.json`, 파일별 삭제 기록·지문, `vendor-cache-zip-review.private.json`, `nuget-http-cache-result.private.json`, `folder-candidates.private.json`, `삭제대상-검토.txt`다. 최초 검토 JSON의 OrderedDictionary 합계가 null이던 항목은 삭제 근거로 사용하지 않고, 별도 `folder-candidates.private.json`에서 수치를 다시 계산했다. 제품 소스·설정·Scene·운영 DB·commit/push는 변경하지 않았다.
